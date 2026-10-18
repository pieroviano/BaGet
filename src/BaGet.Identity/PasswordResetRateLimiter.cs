using System;
using System.Collections.Generic;
using BaGet.Core;

namespace BaGet.Identity;

/// <summary>
/// Limits "forgot password" mail: one per account per <see cref="Window"/> (the real
/// protection), plus two generous backstops, <see cref="MaxPerClient"/> per client address and
/// <see cref="MaxTotal"/> for the whole site, both per <see cref="Window"/>.
/// </summary>
/// <remarks>
/// <para>
/// The client address is only as good as the forwarded headers the server trusts (they are
/// trusted from anyone, see <c>ConfigureBaGetOptions</c>), so it can be spoofed, and behind a
/// proxy that sets no forwarded header every request shares one address. That is why the
/// client limit is high and is not the only gate.
/// </para>
/// <para>
/// Only allowed mail is recorded, so the state never holds more than <see cref="MaxTotal"/>
/// entries, and entries leave in the order they came in: expiry is O(1) amortised, with no
/// scan. The caller asks only for accounts that exist, so requests for unknown names cost the
/// limiter nothing. Like <see cref="ContactRateLimiter"/> the state is in memory: per process,
/// reset on restart. The caller answers identically whether or not a request was allowed.
/// </para>
/// </remarks>
public class PasswordResetRateLimiter
{
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(10);

    public const int MaxPerClient = 60;

    public const int MaxTotal = 1000;

    private readonly SystemTime _time;
    private readonly Queue<(DateTime At, string Client, string Target)> _log = new();
    private readonly Dictionary<string, DateTime> _targets = new();
    private readonly Dictionary<string, int> _clients = new();
    private readonly object _gate = new object();

    /// <summary>What is being remembered; never more than <see cref="MaxTotal"/>.</summary>
    internal int TrackedCount
    {
        get
        {
            lock (_gate) return _log.Count;
        }
    }

    public PasswordResetRateLimiter(SystemTime time) => _time = time ?? throw new ArgumentNullException(nameof(time));

    /// <summary>
    /// Takes the allowance, or returns false when the target was mailed less than
    /// <see cref="Window"/> ago, or the client or the site has used up its requests. Atomic.
    /// </summary>
    public bool TryAcquire(string client, string target)
    {
        var now = _time.UtcNow;

        lock (_gate)
        {
            Expire(now);

            if (_targets.ContainsKey(target)) return false;
            if (_log.Count >= MaxTotal) return false;
            if (_clients.TryGetValue(client, out var used) && used >= MaxPerClient) return false;

            _log.Enqueue((now, client, target));
            _targets[target] = now;
            _clients[client] = used + 1;

            return true;
        }
    }

    private void Expire(DateTime now)
    {
        while (_log.Count > 0 && now - _log.Peek().At >= Window)
        {
            var (_, client, target) = _log.Dequeue();
            _targets.Remove(target);

            if (_clients[client] <= 1) _clients.Remove(client);
            else _clients[client]--;
        }
    }
}
