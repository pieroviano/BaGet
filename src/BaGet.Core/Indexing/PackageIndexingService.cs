using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Packaging;

namespace BaGet.Core;

public class PackageIndexingService : IPackageIndexingService
{
    private readonly IPackageDatabase _packages;
    private readonly IPackageStorageService _storage;
    private readonly ISearchIndexer _search;
    private readonly SystemTime _time;
    private readonly IOptionsSnapshot<BaGetOptions> _options;
    private readonly ILogger<PackageIndexingService> _logger;
    private readonly PackageChangeNotifier _notifier;
    private readonly MaintenanceGate? _gate;

    public PackageIndexingService(
        IPackageDatabase packages,
        IPackageStorageService storage,
        ISearchIndexer search,
        SystemTime time,
        IOptionsSnapshot<BaGetOptions> options,
        ILogger<PackageIndexingService> logger)
        : this(packages, storage, search, time, options, logger, PackageChangeNotifier.None)
    {
    }

    public PackageIndexingService(
        IPackageDatabase packages,
        IPackageStorageService storage,
        ISearchIndexer search,
        SystemTime time,
        IOptionsSnapshot<BaGetOptions> options,
        ILogger<PackageIndexingService> logger,
        PackageChangeNotifier notifier)
        : this(packages, storage, search, time, options, logger, notifier, gate: null)
    {
    }

    /// <param name="packages">The package database.</param>
    /// <param name="storage">Where package content is stored.</param>
    /// <param name="search">Indexes the package for search.</param>
    /// <param name="time">The current time.</param>
    /// <param name="options">The server's options.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="notifier">Told when a package changes, once it is indexed.</param>
    /// <param name="gate">
    /// When closed, indexing is refused up front with <see cref="MaintenanceException"/>, before any
    /// content reaches storage, so a refused push leaves no orphan files behind.
    /// </param>
    public PackageIndexingService(
        IPackageDatabase packages,
        IPackageStorageService storage,
        ISearchIndexer search,
        SystemTime time,
        IOptionsSnapshot<BaGetOptions> options,
        ILogger<PackageIndexingService> logger,
        PackageChangeNotifier notifier,
        MaintenanceGate? gate)
    {
        _gate = gate;
        _notifier = notifier ?? throw new ArgumentNullException(nameof(notifier));
        _packages = packages ?? throw new ArgumentNullException(nameof(packages));
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        _search = search ?? throw new ArgumentNullException(nameof(search));
        _time = time ?? throw new ArgumentNullException(nameof(time));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<PackageIndexingResult> IndexAsync(Stream? packageStream, CancellationToken cancellationToken)
    {
        // Costs nothing without a listener: StartActivity returns null.
        using var activity = BaGetDiagnostics.Source.StartActivity(BaGetDiagnostics.Spans.PackageIndex);

        try
        {
            var result = await IndexCoreAsync(packageStream, activity, cancellationToken);

            activity?.SetTag(BaGetDiagnostics.Tags.Result, result.ToString());
            BaGetDiagnostics.PackagesPushed.Add(1, new KeyValuePair<string, object?>(BaGetDiagnostics.Tags.Result, result.ToString()));

            return result;
        }
        catch (Exception e)
        {
            BaGetDiagnostics.Fail(activity, e);
            BaGetDiagnostics.PackagesPushed.Add(1, new KeyValuePair<string, object?>(BaGetDiagnostics.Tags.Result, "error"));

            throw;
        }
    }

    private async Task<PackageIndexingResult> IndexCoreAsync(
        Stream? packageStream,
        Activity? activity,
        CancellationToken cancellationToken)
    {
        _gate?.ThrowIfClosed();

        // Try to extract all the necessary information from the package.
        Package package;
        Stream nuspecStream;
        Stream? readmeStream;
        Stream? iconStream;

        try
        {
            using (var packageReader = new PackageArchiveReader(packageStream, leaveStreamOpen: true))
            {
                // Reject packages whose entries cannot be extracted safely, such as an
                // entry whose path escapes the extraction directory.
                await packageReader.ValidatePackageEntriesAsync(cancellationToken);

                package = packageReader.GetPackageMetadata();
                package.Published = _time.UtcNow;

                nuspecStream = await packageReader.GetNuspecAsync(cancellationToken);
                nuspecStream = await nuspecStream.AsTemporaryFileStreamAsync(cancellationToken);

                if (package.HasReadme)
                {
                    readmeStream = await packageReader.GetReadmeAsync(cancellationToken);
                    readmeStream = await readmeStream.AsTemporaryFileStreamAsync(cancellationToken);
                }
                else
                {
                    readmeStream = null;
                }

                if (package.HasEmbeddedIcon)
                {
                    iconStream = await packageReader.GetIconAsync(cancellationToken);
                    iconStream = await iconStream.AsTemporaryFileStreamAsync(cancellationToken);
                }
                else
                {
                    iconStream = null;
                }
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Uploaded package is invalid");

            return PackageIndexingResult.InvalidPackage;
        }

        activity?.SetTag(BaGetDiagnostics.Tags.PackageId, package.Id);
        activity?.SetTag(BaGetDiagnostics.Tags.PackageVersion, package.NormalizedVersionString);

        // The package is well-formed. Ensure this is a new package.
        if (await _packages.ExistsAsync(package.Id, package.Version, cancellationToken))
        {
            if (!_options.Value.AllowPackageOverwrites)
            {
                return PackageIndexingResult.PackageAlreadyExists;
            }

            await _packages.HardDeletePackageAsync(package.Id, package.Version, cancellationToken);
            await _storage.DeleteAsync(package.Id, package.Version, cancellationToken);
        }

        // TODO: Add more package validations
        _logger.LogInformation(
            "Validated package {PackageId} {PackageVersion}, persisting content to storage...",
            package.Id,
            package.NormalizedVersionString);

        PackageSaveResult storageResult;

        try
        {
            packageStream?.Position = 0;

            storageResult = await _storage.SavePackageContentAsync(
                package,
                packageStream,
                nuspecStream,
                readmeStream,
                iconStream,
                cancellationToken);
        }
        catch (Exception e)
        {
            _logger.LogError(
                e,
                "Failed to persist package {PackageId} {PackageVersion} content to storage",
                package.Id,
                package.NormalizedVersionString);

            throw;
        }

        if (storageResult == PackageSaveResult.Conflict)
        {
            // Content for this id/version is already stored and differs from what was pushed.
            // This may happen due to concurrent pushes.
            _logger.LogWarning(
                "Package {PackageId} {PackageVersion} content conflicts with the content already in storage",
                package.Id,
                package.NormalizedVersionString);

            return PackageIndexingResult.PackageAlreadyExists;
        }

        if (storageResult != PackageSaveResult.Success)
        {
            _logger.LogError($"Unknown {nameof(PackageSaveResult)} value: {{PackageSaveResult}}", storageResult);

            throw new InvalidOperationException($"Unknown {nameof(PackageSaveResult)} value: {storageResult}");
        }

        _logger.LogInformation(
            "Persisted package {Id} {Version} content to storage, saving metadata to database...",
            package.Id,
            package.NormalizedVersionString);

        var result = await _packages.AddAsync(package, cancellationToken);
        if (result == PackageAddResult.PackageAlreadyExists)
        {
            _logger.LogWarning(
                "Package {Id} {Version} metadata already exists in database",
                package.Id,
                package.NormalizedVersionString);

            return PackageIndexingResult.PackageAlreadyExists;
        }

        if (result != PackageAddResult.Success)
        {
            _logger.LogError($"Unknown {nameof(PackageAddResult)} value: {{PackageAddResult}}", result);

            throw new InvalidOperationException($"Unknown {nameof(PackageAddResult)} value: {result}");
        }

        _logger.LogInformation(
            "Successfully persisted package {Id} {Version} metadata to database. Indexing in search...",
            package.Id,
            package.NormalizedVersionString);

        await _search.IndexAsync(package, cancellationToken);

        _logger.LogInformation(
            "Successfully indexed package {Id} {Version} in search",
            package.Id,
            package.NormalizedVersionString);

        await _notifier.PackageChangedAsync(package.Id, package.Version, cancellationToken);

        return PackageIndexingResult.Success;
    }
}