using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace BaGet.Core;

/// <summary>
/// A minimal storage implementation, used for advanced scenarios.
/// </summary>
public class NullStorageService : IStorageService, IListableStorageService
{
    public Task DeleteAsync(string path, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// A store that holds nothing lists nothing, so a sweep over it has nothing to do.
    /// </summary>
    public Task<IReadOnlyList<string>> ListAsync(string pathPrefix, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
    }

    public Task<Stream> GetAsync(string path, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<Stream>(null);
    }

    public Task<Uri> GetDownloadUriAsync(string path, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<Uri>(null);
    }

    public Task<StoragePutResult> PutAsync(
        string path,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(StoragePutResult.Success);
    }
}