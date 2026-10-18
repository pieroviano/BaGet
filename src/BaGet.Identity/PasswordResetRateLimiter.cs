using System;
using System.Collections.Generic;
using System.Linq;
using BaGet.Core;

namespace BaGet.Identity;

/// <summary>
/// Limits "forgot password" requests: one reset email per target per <see cref="Window"/>,
/// and at most <see cref="MaxPerClient"/> requests per client per <see cref="Window"/>.
/// </summary>
/// <remarks>
/// Like <see cref="ContactRateLimiter"/> the state is in memory: per process, reset on
/// restart. The caller answers identically whether or not a request was allowed.
/// </remarks>
public class PasswordResetRateLimiter
{
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(10);

    public const int MaxPerClient = 10;

    private readonly SystemTime _time;
    private readonly Dictionary<string, DateTime> _targets = new();
    private readonly Dictionary<string, List<DateTime>> _clients = new();
    private readonly object _gate = new object();

    public PasswordResetRateLimiter(SystemTime time) => _time = time ?? throw new ArgumentNullException(nameof(time));

    /// <summary>
    /// Takes the allowance, or returns false when the target was mailed less than
    /// <see cref="Window"/> ago or the client has used up its requests. Atomic.
    /// </summary>
    public bool TryAcquire(string client, string target)
    {
        var now = _time.UtcNow;

        lock (_gate)
        {
            if (_targets.Count > 10_000 || _clients.Count > 10_000) Prune(now);

            if (_targets.TryGetValue(target, out var previous) && now - previous < Window) return false;

            if (!_clients.TryGetValue(client, out var hits)) _clients[client] = hits = new List<DateTime>();
            hits.RemoveAll(hit => now - hit >= Window);
            if (hits.Count >= MaxPerClient) return false;

            hits.Add(now);
            _targets[target] = now;

            return true;
        }
    }

    private void Prune(DateTime now)
    {
        foreach (var key in _targets.Where(p => now - p.Value >= Window).Select(p => p.Key).ToList()) _targets.Remove(key);
        foreach (var key in _clients.Where(p => p.Value.All(hit => now - hit >= Window)).Select(p => p.Key).ToList()) _clients.Remove(key);
    }
}
