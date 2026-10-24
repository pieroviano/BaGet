using System.Collections.Concurrent;

namespace BaGet.Core;

/// <summary>
/// The container images being scanned right now, process-wide: the scanner pulls the image through this registry, and that pull must
/// not be refused as "unscanned" (see <see cref="IScanGate.IsScannerPullAsync"/>).
/// In memory per process: a multi-node deployment must set <see cref="ScanningOptions.TrivyRegistryUser"/> instead.
/// </summary>
internal static class ScanInFlight
{
    private static readonly ConcurrentDictionary<(int, string, string), int> Running = new();

    public static bool Contains(int repositoryKey, string name, string version) => Running.ContainsKey((repositoryKey, name, version));

    /// <summary>Marks the image as being scanned until the returned handle is disposed.</summary>
    public static System.IDisposable Begin(int repositoryKey, string name, string version)
    {
        var key = (repositoryKey, name, version);
        Running.AddOrUpdate(key, 1, (_, n) => n + 1);

        return new Handle(key);
    }

    private sealed class Handle : System.IDisposable
    {
        private readonly (int, string, string) _key;
        private int _done;

        public Handle((int, string, string) key) => _key = key;

        public void Dispose()
        {
            if (System.Threading.Interlocked.Exchange(ref _done, 1) != 0) return;

            while (Running.TryGetValue(_key, out var n))
            {
                if (n <= 1 ? Running.TryRemove(new System.Collections.Generic.KeyValuePair<(int, string, string), int>(_key, n)) : Running.TryUpdate(_key, n - 1, n)) break;
            }
        }
    }
}
