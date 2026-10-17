using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Packaging;

namespace BaGet.Validation;

/// <summary>
/// Runs one submission: every validator, then, if all passed, repository signing and
/// indexing. A package that fails stays staged, quarantined, for its owners to inspect.
/// </summary>
public class PackageValidationRunner
{
    private readonly IContext _context;
    private readonly IStorageService _storage;
    private readonly IPackageIndexingService _indexer;
    private readonly IPackageDatabase _packages;
    private readonly IEnumerable<IPackageValidator> _validators;
    private readonly RepositorySigner _signer;
    private readonly SystemTime _time;
    private readonly IOptionsSnapshot<ValidationOptions> _options;
    private readonly ILogger<PackageValidationRunner> _logger;

    public PackageValidationRunner(
        IContext context,
        IStorageService storage,
        IPackageIndexingService indexer,
        IPackageDatabase packages,
        IEnumerable<IPackageValidator> validators,
        RepositorySigner signer,
        SystemTime time,
        IOptionsSnapshot<ValidationOptions> options,
        ILogger<PackageValidationRunner> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        _indexer = indexer ?? throw new ArgumentNullException(nameof(indexer));
        _packages = packages ?? throw new ArgumentNullException(nameof(packages));
        _validators = validators ?? throw new ArgumentNullException(nameof(validators));
        _signer = signer ?? throw new ArgumentNullException(nameof(signer));
        _time = time ?? throw new ArgumentNullException(nameof(time));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<string>> GetPendingSubmissionIdsAsync(CancellationToken cancellationToken)
    {
        return await _context.PackageValidations
            .Where(v => v.State == PackageValidationState.Pending)
            .Select(v => v.SubmissionId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    public async Task RunAsync(string submissionId, CancellationToken cancellationToken)
    {
        var steps = await _context.PackageValidations
            .Where(v => v.SubmissionId == submissionId)
            .ToListAsync(cancellationToken);

        var index = steps.SingleOrDefault(s => s.Type == PackageValidation.IndexType);
        if (index == null || index.State != PackageValidationState.Pending)
        {
            return;
        }

        using var package = new MemoryStream();
        using (var staged = await _storage.GetAsync(ValidatingPackageSubmissionService.StagedPath(submissionId), cancellationToken))
        {
            await staged.CopyToAsync(package, cancellationToken);
        }

        package.Position = 0;

        using (var reader = new PackageArchiveReader(package, leaveStreamOpen: true))
        {
            var identity = await reader.GetIdentityAsync(cancellationToken);
            var context = new PackageValidationContext(identity.Id, identity.Version, package, reader);

            foreach (var validator in _validators)
            {
                var step = steps.FirstOrDefault(s => s.Type == validator.Name);
                if (step == null || step.State != PackageValidationState.Pending) continue;

                PackageValidatorResult result;
                try
                {
                    package.Position = 0;
                    result = await validator.ValidateAsync(context, cancellationToken);
                }
                catch (Exception e) when (!(e is OperationCanceledException))
                {
                    _logger.LogError(e, "Validator {Validator} failed on submission {SubmissionId}", validator.Name, submissionId);
                    result = PackageValidatorResult.Invalid($"The {validator.Name} validator failed: {e.Message}");
                }

                Complete(step, result.IsValid, result.Message);
            }
        }

        // A step whose validator is no longer registered cannot pass.
        foreach (var orphan in steps.Where(s => s.State == PackageValidationState.Pending && s != index))
        {
            Complete(orphan, succeeded: false, $"The {orphan.Type} validator is no longer registered.");
        }

        if (steps.Any(s => s.State == PackageValidationState.Failed))
        {
            Complete(index, succeeded: false, "Quarantined: the package failed validation.");
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogWarning("Submission {SubmissionId} failed validation and is quarantined", submissionId);
            return;
        }

        var indexError = await IndexAsync(package, _options.Value.RepositorySigningServiceIndexUrl, cancellationToken);
        Complete(index, indexError == null, indexError);

        if (indexError == null)
        {
            var version = NuGet.Versioning.NuGetVersion.Parse(index.PackageVersion);
            var indexed = await _packages.FindOrNullAsync(index.PackageId, version, includeUnlisted: true, cancellationToken);
            foreach (var step in steps)
            {
                step.PackageKey = indexed?.Key;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        if (indexError == null)
        {
            await _storage.DeleteAsync(ValidatingPackageSubmissionService.StagedPath(submissionId), cancellationToken);
        }
    }

    /// <returns>Null on success, otherwise why indexing failed.</returns>
    private async Task<string> IndexAsync(Stream package, string serviceIndexUrl, CancellationToken cancellationToken)
    {
        Stream toIndex = package;
        try
        {
            if (_signer.IsConfigured)
            {
                // NuGet only accepts a repository signature that names an https source.
                if (!Uri.TryCreate(serviceIndexUrl, UriKind.Absolute, out var url) || url.Scheme != Uri.UriSchemeHttps)
                {
                    return "Repository signing needs Validation:RepositorySigningServiceIndexUrl to be an absolute https URL.";
                }

                toIndex = await _signer.SignAsync(package, serviceIndexUrl, cancellationToken);
            }

            toIndex.Position = 0;
            var result = await _indexer.IndexAsync(toIndex, cancellationToken);

            return result == PackageIndexingResult.Success ? null : $"Indexing failed: {result}.";
        }
        catch (Exception e) when (!(e is OperationCanceledException))
        {
            _logger.LogError(e, "Failed to index a validated package");
            return $"Indexing failed: {e.Message}";
        }
        finally
        {
            if (!ReferenceEquals(toIndex, package))
            {
                toIndex.Dispose();
            }
        }
    }

    private void Complete(PackageValidation step, bool succeeded, string message)
    {
        step.State = succeeded ? PackageValidationState.Succeeded : PackageValidationState.Failed;
        step.Message = message?.Length > AbstractContext<DbContext>.DefaultMaxStringLength
            ? message.Substring(0, AbstractContext<DbContext>.DefaultMaxStringLength)
            : message;
        step.Completed = _time.UtcNow;
    }
}