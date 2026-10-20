using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.Extensions.Options;
using Serilog.Core;
using Serilog.Events;

namespace BaGet.Telemetry;

/// <summary>
/// The Serilog sink that exports logs over OTLP, rebuilt when <see cref="TelemetryOptions"/> change (same scheme as
/// <see cref="ReloadableExporter{T}"/>). It is a Serilog sink and deliberately not an <c>ILoggerProvider</c>:
/// <c>writeToProviders</c> would deliver every event to a provider twice. It receives exactly what the Serilog pipeline
/// lets through, so the minimum levels (the <c>Microsoft.AspNetCore.Hosting.Diagnostics</c> Warning floor that keeps
/// request URLs out of the log) apply to it too, plus <c>Telemetry:Logs:MinimumLevel</c>. While logs are off (no
/// <c>Logs:Endpoint</c>) <see cref="Emit"/> returns immediately.
/// </summary>
internal sealed class ReloadableOpenTelemetrySink : ILogEventSink, IDisposable
{
    private readonly IOptionsMonitor<TelemetryOptions> _options;
    private readonly TelemetryStatus.Signal _status;
    private readonly ITelemetryExporterFactory _factory;
    private readonly SwappableHolder<SinkHandle> _holder = new SwappableHolder<SinkHandle>();
    private readonly object _reloadLock = new object();
    private readonly List<Task> _retiring = new List<Task>();
    private readonly IDisposable? _subscription;
    private string? _key;
    private bool _initialized;
    private bool _disposed;
    private volatile int _minimumLevel = (int)LogEventLevel.Information;

    public ReloadableOpenTelemetrySink(IOptionsMonitor<TelemetryOptions> options, TelemetryStatus status, ITelemetryExporterFactory factory)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _status = (status ?? throw new ArgumentNullException(nameof(status))).Logs;
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));

        Reload();
        _subscription = options.OnChange(_ => Reload());
    }

    internal bool IsExporting => _holder.Current != null;

    internal void Reload()
    {
        lock (_reloadLock)
        {
            if (_disposed) return;

            var options = TelemetryEndpoints.Read(_options);
            var key = options == null ? null : TelemetryEndpoints.Key(options, TelemetrySignal.Logs);
            if (_initialized && key == _key) return;

            _initialized = true;
            _key = key;

            SinkHandle? handle = null;
            if (key != null)
            {
                try
                {
                    var minimum = Enum.TryParse<LogEventLevel>(options!.Logs?.MinimumLevel, ignoreCase: true, out var level)
                        ? level
                        : LogEventLevel.Information;
                    _minimumLevel = (int)minimum;
                    handle = new SinkHandle(_factory.CreateLogSink(options, TelemetryEndpoints.Resolve(options, TelemetrySignal.Logs)!, _status), Retire);
                }
                catch (Exception e)
                {
                    _status.Failure(TelemetryStatus.Describe(e, options!.Otlp?.Headers));
                }
            }

            _holder.Swap(handle);
            if (handle != null || key == null) _status.Configured(handle != null);
        }
    }

    public void Emit(LogEvent logEvent)
    {
        if ((int)logEvent.Level < _minimumLevel) return;

        using var lease = _holder.Acquire();
        if (lease == null) return;

        // The export's own HTTP clients log through the same pipeline: never export that (a feedback loop).
        if (logEvent.Properties.TryGetValue("SourceContext", out var context)
            && context is ScalarValue { Value: string name }
            && (name.StartsWith("Grpc.Net.Client", StringComparison.Ordinal) || name.StartsWith("System.Net.Http", StringComparison.Ordinal)))
        {
            return;
        }

        try
        {
            lease.Value.Sink.Emit(logEvent);
        }
        catch (Exception e)
        {
            _status.Failure(TelemetryStatus.Describe(e, TelemetryEndpoints.Read(_options)?.Otlp?.Headers));
        }
    }

    /// <summary>Stops the subscription and flushes and disposes the current sink. Idempotent.</summary>
    public void Dispose()
    {
        lock (_reloadLock) _disposed = true;
        _subscription?.Dispose();
        _holder.Close();

        // Shutdown is the one place that waits: sinks retired earlier are still draining in the background.
        Task[] pending;
        lock (_retiring) pending = _retiring.ToArray();
        try
        {
            Task.WaitAll(pending);
        }
        catch
        {
            // Best effort, as for the exporters.
        }
    }

    /// <summary>
    /// Disposing a Serilog batching sink waits for its run loop and a final drain, each send bounded only by
    /// <c>Otlp:Timeout</c>. A retired sink is therefore disposed on the thread pool: the thread that swaps it (inside a
    /// settings save, holding its lock) or that releases its last lease (possibly a request) must not wait for a collector.
    /// </summary>
    private void Retire(ILogEventSink sink)
    {
        if (sink is not IDisposable disposable) return;

        var task = Task.Run(() =>
        {
            try
            {
                disposable.Dispose();
            }
            catch
            {
                // Best effort: a failing sink must not take the host down.
            }
        });

        lock (_retiring)
        {
            _retiring.RemoveAll(t => t.IsCompleted);
            _retiring.Add(task);
        }
    }

    private sealed class SinkHandle : IDisposable
    {
        private readonly Action<ILogEventSink> _retire;

        public SinkHandle(ILogEventSink sink, Action<ILogEventSink> retire)
        {
            Sink = sink;
            _retire = retire;
        }

        public ILogEventSink Sink { get; }

        public void Dispose() => _retire(Sink);
    }
}
