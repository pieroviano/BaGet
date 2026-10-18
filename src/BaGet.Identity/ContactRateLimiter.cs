using System;
using System.Collections.Concurrent;
using System.Linq;
using BaGet.Core;

namespace BaGet.Identity;

/// <summary>
/// Allows one contact-owners message per sender per package per <see cref="Window"/>.
/// </summary>
/// <remarks>
/// The state is in memory, so it is per process: it resets on restart and each instance of a
/// scaled-out deployment counts separately. That bounds spam well enough for a feed's
/// size and keeps the feature free of schema changes. Time comes from <see cref="SystemTime"/>.
/// </remarks>
public class ContactRateLimiter
{
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(10);

    private readonly SystemTime _time;
    private readonly ConcurrentDictionary<(string Sender, string PackageId), DateTime> _last = new();
    private readonly object _gate = new object();

    public ContactRateLimiter(SystemTime time) => _time = time ?? throw new ArgumentNullException(nameof(time));

    /// <summary>
    /// Takes the sender's allowance for the package, or returns false when it was used less
    /// than <see cref="Window"/> ago. Atomic, so two concurrent posts cannot both pass.
    /// </summary>
    public bool TryAcquire(string senderId, string packageId)
    {
        var now = _time.UtcNow;
        var key = (senderId, packageId.ToLowerInvariant());

        lock (_gate)
        {
            if (_last.TryGetValue(key, out var previous) && now - previous < Window)
            {
                return false;
            }

            _last[key] = now;
            if (_last.Count > 10_000) Prune(now);

            return true;
        }
    }

    /// <summary>
    /// Gives the allowance back, when the message could not be sent.
    /// </summary>
    public void Release(string senderId, string packageId)
    {
        lock (_gate) _last.TryRemove((senderId, packageId.ToLowerInvariant()), out _);
    }

    private void Prune(DateTime now)
    {
        foreach (var expired in _last.Where(p => now - p.Value >= Window).Select(p => p.Key).ToList())
        {
            _last.TryRemove(expired, out _);
        }
    }
}
