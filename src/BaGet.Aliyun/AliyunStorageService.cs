using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Aliyun.OSS;
using BaGet.Core;
using Microsoft.Extensions.Options;

namespace BaGet.Aliyun
{
    public class AliyunStorageService : IStorageService
    {
        private const string Separator = "/";

        // See: https://github.com/dotnet/corefx/blob/master/src/Common/src/CoreLib/System/IO/Stream.cs#L35
        private const int DefaultCopyBufferSize = 81920;

        private readonly string _bucket;
        private readonly string _prefix;
        private readonly OssClient _client;

        public AliyunStorageService(IOptionsSnapshot<AliyunStorageOptions> options, OssClient client)
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
            try
            {
                var ossObject = await Task.Factory.FromAsync(_client.BeginGetObject, _client.EndGetObject, _bucket, PrepareKey(path), null);

                return ossObject.ResponseStream;
            }
            catch (Exception)
            {
                // Unresolved: IStorageService.GetAsync is documented to return null when the
                // content is missing, but no implementation does - FileStorageService throws
                // FileNotFoundException and callers such as
                // SymbolStorageService.GetPortablePdbContentStreamOrNullAsync catch instead of
                // null-checking. Mapping OSS's NoSuchKey to null here alone would hide missing
                // content from PackageStorageService, so the mismatch belongs to the interface
                // and is left for a change that fixes every implementation at once.
                throw;
            }
        }

        public Task<Uri> GetDownloadUriAsync(string path, CancellationToken cancellationToken = default)
        {
            var uri = _client.GeneratePresignedUri(_bucket, PrepareKey(path));

            return Task.FromResult(uri);
        }

        public async Task<StoragePutResult> PutAsync(string path, Stream content, string contentType, CancellationToken cancellationToken = default)
        {
            if (content == null) throw new ArgumentNullException(nameof(content));

            var key = PrepareKey(path);

            using (var seekableContent = new MemoryStream())
            {
                await content.CopyToAsync(seekableContent, DefaultCopyBufferSize, cancellationToken);

                // Storing the same content twice is not a failure, storing different content
                // at a taken key is. OSS does signal the latter itself, through the
                // "x-oss-forbid-overwrite" request header and a 409 "FileAlreadyExists"
                // response, but this SDK exposes neither: ObjectMetadata has no such header
                // and OssException carries only ErrorCode, with no HTTP status code to match
                // on. So the check is made here instead, which leaves the window between the
                // check and the put open to a concurrent upload - unlike FileStorageService,
                // BlobStorageService and GoogleCloudStorageService, which all decide this
                // atomically in the store.
                // TODO: make this atomic once the SDK sends "x-oss-forbid-overwrite" and
                // surfaces the 409, or once it is confirmed against a live OSS account that
                // ObjectMetadata.AddHeader reaches the request unprefixed.
                if (_client.DoesObjectExist(_bucket, key))
                {
                    using (var targetStream = await GetAsync(path, cancellationToken))
                    {
                        seekableContent.Seek(0, SeekOrigin.Begin);

                        return seekableContent.Matches(targetStream)
                            ? StoragePutResult.AlreadyExists
                            : StoragePutResult.Conflict;
                    }
                }

                seekableContent.Seek(0, SeekOrigin.Begin);

                var metadata = new ObjectMetadata
                {
                    ContentType = contentType,
                };

                // PutObjectResult.HttpStatusCode is not inspected. The branches that used to
                // read it ("check sdk documents") mapped 409 to Conflict and 302 to
                // AlreadyExists, and neither is reachable: OSS reports a refused put by
                // throwing OssException, and PutObjectResult is not documented to carry any
                // status that means conflict.
                await Task<PutObjectResult>.Factory.FromAsync(
                    _client.BeginPutObject, _client.EndPutObject, _bucket, key, seekableContent, metadata);

                return StoragePutResult.Success;
            }
        }

        public Task DeleteAsync(string path, CancellationToken cancellationToken = default)
        {
            _client.DeleteObject(_bucket, PrepareKey(path));
            return Task.CompletedTask;
        }
    }
}
