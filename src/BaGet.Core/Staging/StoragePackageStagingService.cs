using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace BaGet.Core
{
    /// <summary>
    /// Stages uploads in <see cref="IStorageService"/> under a prefix of their own, so that
    /// the two requests of a browser upload may land on different instances and so that
    /// every storage provider works, exactly as the validation pipeline stages pushed
    /// packages under "validation/".
    /// </summary>
    /// <remarks>
    /// Nothing serves the staging prefix: no route turns a request into a storage path, so a
    /// staged upload is reachable only through <see cref="OpenAsync"/>, which is where the
    /// owner is checked.
    /// </remarks>
    public class StoragePackageStagingService : IPackageStagingService
    {
        /// <summary>
        /// The prefix every staged upload lives under. A token never becomes anything else.
        /// </summary>
        internal const string StagingPathPrefix = "staging";

        /// <summary>
        /// 128 bits from a cryptographic generator, hex encoded. The token is the only thing
        /// naming a staged upload, so guessing one must not be feasible.
        /// </summary>
        private const int TokenBytes = 16;

        private const int TokenLength = TokenBytes * 2;

        private readonly IStorageService _storage;
        private readonly SystemTime _time;
        private readonly ILogger<StoragePackageStagingService> _logger;

        public StoragePackageStagingService(
            IStorageService storage,
            SystemTime time,
            ILogger<StoragePackageStagingService> logger)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _time = time ?? throw new ArgumentNullException(nameof(time));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        internal static string StagedPath(string token) => $"{StagingPathPrefix}/{token}.nupkg";

        internal static string MetadataPath(string token) => $"{StagingPathPrefix}/{token}.json";

        public async Task<string> StageAsync(Stream package, string ownerId, CancellationToken cancellationToken = default)
        {
            if (package == null) throw new ArgumentNullException(nameof(package));

            var token = CreateToken();
            var metadata = new StagedUpload
            {
                OwnerId = Owner(ownerId),
                StagedAtUtc = _time.UtcNow,
            };

            // The metadata is written before the package because the sweep finds abandoned
            // uploads through the metadata: a package whose metadata never arrived would be
            // collected by nothing.
            using (var json = new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(metadata)))
            {
                await StoreAsync(MetadataPath(token), json, "application/json", cancellationToken);
            }

            try
            {
                await StoreAsync(StagedPath(token), package, "application/octet-stream", cancellationToken);
            }
            catch
            {
                // Half a staged upload can never be confirmed, so it is removed now rather
                // than left for the sweep.
                await RemoveAsync(token, CancellationToken.None);
                throw;
            }

            _logger.LogInformation("Staged an upload as {Token}", token);

            return token;
        }

        public async Task<Stream> OpenAsync(string token, string ownerId, CancellationToken cancellationToken = default)
        {
            if (!IsToken(token)) return null;

            var metadata = await ReadMetadataOrNullAsync(token, cancellationToken);
            if (metadata == null) return null;

            // The real authorization: the prefix being unreachable over HTTP is a property of
            // the route table, not an access check, and two users share one store.
            if (!string.Equals(metadata.OwnerId, Owner(ownerId), StringComparison.Ordinal))
            {
                _logger.LogWarning("Refusing staged upload {Token}: it was staged by a different user", token);
                return null;
            }

            return await OpenOrNullAsync(StagedPath(token), cancellationToken);
        }

        public async Task RemoveAsync(string token, CancellationToken cancellationToken = default)
        {
            if (!IsToken(token)) return;

            await DeleteAsync(StagedPath(token), cancellationToken);
            await DeleteAsync(MetadataPath(token), cancellationToken);
        }

        public async Task<int> SweepAsync(DateTime stagedBefore, CancellationToken cancellationToken = default)
        {
            if (!(_storage is IListableStorageService listable))
            {
                // Said out loud: silence here would mean staged uploads accumulating
                // unnoticed on a store that cannot be swept.
                _logger.LogWarning(
                    "Cannot sweep abandoned staged uploads: {Storage} cannot list what it holds",
                    _storage.GetType().Name);

                return 0;
            }

            var tokens = new HashSet<string>(StringComparer.Ordinal);
            foreach (var path in await listable.ListAsync(StagingPathPrefix, cancellationToken))
            {
                var token = TokenOrNull(path);
                if (token != null) tokens.Add(token);
            }

            var swept = 0;
            foreach (var token in tokens)
            {
                var metadata = await ReadMetadataOrNullAsync(token, cancellationToken);

                // An upload without readable metadata cannot be opened by anyone, so it is
                // collected whatever its age.
                if (metadata != null && metadata.StagedAtUtc >= stagedBefore) continue;

                await RemoveAsync(token, cancellationToken);
                swept++;
            }

            if (swept > 0)
            {
                _logger.LogInformation("Swept {Count} staged uploads abandoned before {Cutoff}", swept, stagedBefore);
            }

            return swept;
        }

        private async Task StoreAsync(string path, Stream content, string contentType, CancellationToken cancellationToken)
        {
            var result = await _storage.PutAsync(path, content, contentType, cancellationToken);

            if (result == StoragePutResult.Conflict)
            {
                throw new InvalidOperationException($"Could not stage an upload: '{path}' is already in use");
            }
        }

        private async Task<StagedUpload> ReadMetadataOrNullAsync(string token, CancellationToken cancellationToken)
        {
            using var stream = await OpenOrNullAsync(MetadataPath(token), cancellationToken);
            if (stream == null) return null;

            try
            {
                return await JsonSerializer.DeserializeAsync<StagedUpload>(stream, cancellationToken: cancellationToken);
            }
            catch (JsonException e)
            {
                _logger.LogWarning(e, "Staged upload {Token} has unreadable metadata", token);
                return null;
            }
        }

        private async Task<Stream> OpenOrNullAsync(string path, CancellationToken cancellationToken)
        {
            try
            {
                // IStorageService is documented to answer a missing path with null, but no
                // implementation does: the file store throws, each cloud store throws its
                // own type, and the null store really does return null. An upload that
                // cannot be read back is an unknown token either way.
                return await _storage.GetAsync(path, cancellationToken);
            }
            catch (Exception e) when (!(e is OperationCanceledException))
            {
                _logger.LogDebug(e, "Nothing staged at {Path}", path);
                return null;
            }
        }

        private async Task DeleteAsync(string path, CancellationToken cancellationToken)
        {
            try
            {
                await _storage.DeleteAsync(path, cancellationToken);
            }
            catch (Exception e) when (!(e is OperationCanceledException))
            {
                // Left for the next sweep rather than failing a confirmed upload.
                _logger.LogWarning(e, "Could not remove staged content at {Path}", path);
            }
        }

        private static string CreateToken()
            => Convert.ToHexString(RandomNumberGenerator.GetBytes(TokenBytes)).ToLowerInvariant();

        /// <summary>
        /// A token becomes a storage path, so anything that is not one is refused before it
        /// gets there - a caller-supplied "../packages/..." must never be opened or deleted.
        /// </summary>
        private static bool IsToken(string token)
        {
            if (token == null || token.Length != TokenLength) return false;

            foreach (var character in token)
            {
                var isHex = (character >= '0' && character <= '9') || (character >= 'a' && character <= 'f');
                if (!isHex) return false;
            }

            return true;
        }

        /// <summary>
        /// The token a staged path belongs to, or null if the path is not one of ours.
        /// </summary>
        private static string TokenOrNull(string path)
        {
            if (path == null) return null;
            if (!path.StartsWith(StagingPathPrefix + "/", StringComparison.Ordinal)) return null;

            var extension = Path.GetExtension(path);
            if (extension != ".json" && extension != ".nupkg") return null;

            var token = Path.GetFileNameWithoutExtension(path);

            return IsToken(token) ? token : null;
        }

        /// <summary>
        /// With accounts disabled there is no user to own an upload, so every upload shares
        /// the one empty owner - the same identity the API key path has there.
        /// </summary>
        private static string Owner(string ownerId) => ownerId ?? string.Empty;

        internal class StagedUpload
        {
            public string OwnerId { get; set; }

            public DateTime StagedAtUtc { get; set; }
        }
    }
}
