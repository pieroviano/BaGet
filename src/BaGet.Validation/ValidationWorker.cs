using System;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BaGet.Validation;

/// <summary>
/// Runs queued submissions one at a time. On startup it re-queues every submission still
/// pending, so a restart delays validation but loses nothing.
/// </summary>
public class ValidationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IValidationQueue _queue;
    private readonly ILogger<ValidationWorker> _logger;

    public ValidationWorker(IServiceScopeFactory scopes, IValidationQueue queue, ILogger<ValidationWorker> logger)
    {
        _scopes = scopes ?? throw new ArgumentNullException(nameof(scopes));
        _queue = queue ?? throw new ArgumentNullException(nameof(queue));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Yield so that host startup does not wait on the database.
        await Task.Yield();

        await RequeuePendingAsync(stoppingToken);

        await foreach (var submissionId in _queue.DequeueAllAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var runner = scope.ServiceProvider.GetRequiredService<PackageValidationRunner>();

                await runner.RunAsync(submissionId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception e)
            {
                // Left pending, so the next start retries it.
                _logger.LogError(e, "Validation of submission {SubmissionId} failed", submissionId);
            }
        }
    }

    private async Task RequeuePendingAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopes.CreateScope();
            if (!scope.ServiceProvider.GetRequiredService<IOptions<ValidationOptions>>().Value.Enabled) return;
            if (scope.ServiceProvider.GetRequiredService<IContext>() is NullContext) return;

            var runner = scope.ServiceProvider.GetRequiredService<PackageValidationRunner>();
            foreach (var submissionId in await runner.GetPendingSubmissionIdsAsync(cancellationToken))
            {
                await _queue.EnqueueAsync(submissionId, cancellationToken);
            }
        }
        catch (Exception e) when (!(e is OperationCanceledException))
        {
            // The database may not be migrated yet on a first start.
            _logger.LogWarning(e, "Could not re-queue pending validations");
        }
    }
}