using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BaGet.Identity;

/// <summary>
/// Sends email off the request path, so that how long a page takes does not depend on
/// whether anything was sent: "forgot password" must not reveal which accounts exist.
/// </summary>
public class BackgroundEmailQueue
{
    private readonly Channel<Func<IServiceProvider, Task>> _channel = Channel.CreateBounded<Func<IServiceProvider, Task>>(
        new BoundedChannelOptions(1000) { FullMode = BoundedChannelFullMode.DropWrite, SingleReader = true });

    /// <summary>
    /// Queue work, run with a scope's services. False when the queue is full.
    /// </summary>
    public bool TryEnqueue(Func<IServiceProvider, Task> work) => _channel.Writer.TryWrite(work);

    internal System.Collections.Generic.IAsyncEnumerable<Func<IServiceProvider, Task>> ReadAllAsync(CancellationToken cancellationToken)
        => _channel.Reader.ReadAllAsync(cancellationToken);
}

internal sealed class BackgroundEmailService : BackgroundService
{
    private readonly BackgroundEmailQueue _queue;
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<BackgroundEmailService> _logger;

    public BackgroundEmailService(BackgroundEmailQueue queue, IServiceScopeFactory scopes, ILogger<BackgroundEmailService> logger)
    {
        _queue = queue;
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var work in _queue.ReadAllAsync(stoppingToken))
            {
                try
                {
                    using var scope = _scopes.CreateScope();
                    await work(scope.ServiceProvider);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Could not send a queued email.");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Shutting down.
        }
    }
}
