using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using BaGet.Core;
using Microsoft.Extensions.Options;

namespace BaGet.Aws
{
    public class S3StorageService : IStorageService
    {
        private const string Separator = "/";

        /// <summary>
        /// The value S3 expects in the <c>If-None-Match</c> header to mean "only if the key
        /// is unused". See <see cref="PutObjectRequest.IfNoneMatch"/>.
        /// </summary>
        private const string IfKeyIsUnused = "*";

        // See: https://github.com/dotnet/corefx/blob/master/src/Common/src/CoreLib/System/IO/Stream.cs#L35
        private const int DefaultCopyBufferSize = 81920;

        private readonly string _bucket;
        private readonly string _prefix;
        private readonly AmazonS3Client _client;

        public S3StorageService(IOptionsSnapshot<S3StorageOptions> options, AmazonS3Client client)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            _bucket = options.Value.Bucket;
            _prefix = options.Value.Prefix;
            _client = client ?? throw new ArgumentNullException(nameof(client));

            if (!string.IsNullOrEmpty(_prefix) && !_prefix.EndsWith(Separator))
                _prefix += Separator;
        }

        private string PrepareKey(string path)
        {
            return _prefix + path.Replace("\\", Separator);
        }

        public async Task<Stream> GetAsync(string path, CancellationToken cancellationToken = default)
        {
            var stream = new MemoryStream();

            try
            {
                using (var request = await _client.GetObjectAsync(_bucket, PrepareKey(path), cancellationToken))
                {
                    await request.ResponseStream.CopyToAsync(stream);
                }

                stream.Seek(0, SeekOrigin.Begin);
            }
            catch (Exception)
            {
                stream.Dispose();

                // Unresolved: IStorageService.GetAsync is documented to return null when the
                // content is missing, but no implementation does - FileStorageService throws
                // FileNotFoundException and callers such as
                // SymbolStorageService.GetPortablePdbContentStreamOrNullAsync catch instead of
                // null-checking. Mapping S3's NoSuchKey to null here alone would hide missing
                // content from PackageStorageService, so the mismatch belongs to the interface
                // and is left for a change that fixes every implementation at once.
                throw;
            }

            return stream;
        }

        public Task<Uri> GetDownloadUriAsync(string path, CancellationToken cancellationToken = default)
        {
            var url = _client.GetPreSignedURL(new GetPreSignedUrlRequest
            {
                BucketName = _bucket,
                Key = PrepareKey(path)
            });

            return Task.FromResult(new Uri(url));
        }

        public async Task<StoragePutResult> PutAsync(string path, Stream content, string contentType, CancellationToken cancellationToken = default)
        {
            if (content == null) throw new ArgumentNullException(nameof(content));

            using (var seekableContent = new MemoryStream())
            {
                await content.CopyToAsync(seekableContent, DefaultCopyBufferSize, cancellationToken);

                seekableContent.Seek(0, SeekOrigin.Begin);

                try
                {
                    await _client.PutObjectAsync(new PutObjectRequest
                    {
                        BucketName = _bucket,
                        Key = PrepareKey(path),
                        InputStream = seekableContent,
                        ContentType = contentType,
                        // Make the upload idempotent: store the content only while the key is
                        // unused. S3 answers a taken key with "412 Precondition Failed".
                        IfNoneMatch = IfKeyIsUnused,
                        AutoResetStreamPosition = false,
                        AutoCloseStream = false
                    }, cancellationToken);

                    return StoragePutResult.Success;
                }
                catch (AmazonS3Exception e) when (e.StatusCode == HttpStatusCode.PreconditionFailed)
                {
                    // The key is taken. Storing the same content twice is not a failure, so
                    // this is only a conflict when the stored content differs.
                    using (var targetStream = await GetAsync(path, cancellationToken))
                    {
                        seekableContent.Seek(0, SeekOrigin.Begin);

                        return seekableContent.Matches(targetStream)
                            ? StoragePutResult.AlreadyExists
                            : StoragePutResult.Conflict;
                    }
                }
            }
        }

        public async Task DeleteAsync(string path, CancellationToken cancellationToken = default)
        {
            await _client.DeleteObjectAsync(_bucket, PrepareKey(path), cancellationToken);
        }
    }
}
