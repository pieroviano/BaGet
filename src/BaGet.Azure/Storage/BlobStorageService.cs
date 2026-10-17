using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Blob;
using Microsoft.Extensions.Options;

namespace BaGet.Azure
{
    // See: https://github.com/NuGet/NuGetGallery/blob/master/src/NuGetGallery.Core/Services/CloudBlobCoreFileStorageService.cs
    public class BlobStorageService : IStorageService
    {
        private readonly CloudBlobContainer _container;
        private readonly TimeSpan _downloadUriExpiry;

        public BlobStorageService(IOptionsSnapshot<AzureBlobStorageOptions> options, CloudBlobContainer container)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            _container = container ?? throw new ArgumentNullException(nameof(container));
            _downloadUriExpiry = options.Value.DownloadUriExpiry;

            if (_downloadUriExpiry <= TimeSpan.Zero)
            {
                throw new ArgumentException(
                    $"The {nameof(AzureBlobStorageOptions.DownloadUriExpiry)} option must be greater than zero",
                    nameof(options));
            }
        }

        public async Task<Stream> GetAsync(string path, CancellationToken cancellationToken)
        {
            return await _container
                .GetBlockBlobReference(path)
                .OpenReadAsync(cancellationToken);
        }

        public Task<Uri> GetDownloadUriAsync(string path, CancellationToken cancellationToken)
        {
            var blob = _container.GetBlockBlobReference(path);
            var accessPolicy = new SharedAccessBlobPolicy
            {
                SharedAccessExpiryTime = DateTimeOffset.UtcNow.Add(_downloadUriExpiry),
                Permissions = SharedAccessBlobPermissions.Read
            };

            var signature = blob.GetSharedAccessSignature(accessPolicy);
            var result = new Uri(blob.Uri, signature);

            return Task.FromResult(result);
        }

        public async Task<StoragePutResult> PutAsync(
            string path,
            Stream content,
            string contentType,
            CancellationToken cancellationToken)
        {
            var blob = _container.GetBlockBlobReference(path);
            var condition = AccessCondition.GenerateIfNotExistsCondition();

            blob.Properties.ContentType = contentType;

            try
            {
                await blob.UploadFromStreamAsync(
                    content,
                    condition,
                    options: null,
                    operationContext: null,
                    cancellationToken: cancellationToken);

                return StoragePutResult.Success;
            }
            catch (StorageException e) when (e.IsAlreadyExistsException())
            {
                using (var targetStream = await blob.OpenReadAsync(cancellationToken))
                {
                    content.Position = 0;
                    return content.Matches(targetStream)
                        ? StoragePutResult.AlreadyExists
                        : StoragePutResult.Conflict;
                }
            }
        }

        public async Task DeleteAsync(string path, CancellationToken cancellationToken)
        {
            await _container
                .GetBlockBlobReference(path)
                .DeleteIfExistsAsync(cancellationToken);
        }
    }
}
