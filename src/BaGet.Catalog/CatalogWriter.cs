using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Protocol.Models;
using Microsoft.Extensions.Logging;
using NuGet.Versioning;

namespace BaGet.Catalog
{
    /// <summary>
    /// Appends a catalog entry for every package change. Each entry's leaf document is
    /// written to storage first, so a leaf can always be served once its entry exists.
    /// </summary>
    public class CatalogWriter : IPackageChangeHandler
    {
        internal static readonly JsonSerializerOptions LeafSerializerOptions = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        // Commit timestamps must strictly increase, or a mirror reading the catalog
        // "after" its cursor could miss an entry. Commits in this process are serialized.
        // Several BaGet instances writing to one database are not coordinated.
        private static readonly SemaphoreSlim CommitLock = new SemaphoreSlim(1, 1);

        private readonly ICatalogDatabase _catalog;
        private readonly IPackageDatabase _packages;
        private readonly IPackageStorageService _packageStorage;
        private readonly IStorageService _storage;
        private readonly SystemTime _time;
        private readonly ILogger<CatalogWriter> _logger;

        public CatalogWriter(
            ICatalogDatabase catalog,
            IPackageDatabase packages,
            IPackageStorageService packageStorage,
            IStorageService storage,
            SystemTime time,
            ILogger<CatalogWriter> logger)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _packages = packages ?? throw new ArgumentNullException(nameof(packages));
            _packageStorage = packageStorage ?? throw new ArgumentNullException(nameof(packageStorage));
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _time = time ?? throw new ArgumentNullException(nameof(time));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        internal static string LeafPath(string commitId) => $"catalog/{commitId}.json";

        public async Task OnPackageChangedAsync(string id, NuGetVersion version, CancellationToken cancellationToken)
        {
            if (!_catalog.IsSupported) return;

            var package = await _packages.FindOrNullAsync(id, version, includeUnlisted: true, cancellationToken);
            if (package == null)
            {
                _logger.LogWarning(
                    "Package {PackageId} {PackageVersion} changed but no longer exists; no catalog entry written",
                    id,
                    version);
                return;
            }

            var (hash, size) = await HashPackageAsync(package, cancellationToken);

            await CommitAsync(
                CatalogEntry.PackageDetailsType,
                package.Id,
                package.Version,
                (commitId, timestamp) => BuildDetailsLeaf(package, hash, size, commitId, timestamp),
                cancellationToken);
        }

        public async Task OnPackageDeletedAsync(string id, NuGetVersion version, CancellationToken cancellationToken)
        {
            if (!_catalog.IsSupported) return;

            await CommitAsync(
                CatalogEntry.PackageDeleteType,
                id,
                version,
                (commitId, timestamp) => new PackageDeleteCatalogLeaf
                {
                    Type = new[] { CatalogEntry.PackageDeleteType, "catalog:Permalink" },
                    CommitId = commitId,
                    CommitTimestamp = timestamp,
                    PackageId = id,
                    PackageVersion = version.ToNormalizedString(),
                    Published = timestamp,
                },
                cancellationToken);
        }

        internal static PackageDetailsCatalogLeaf BuildDetailsLeaf(
            Package package,
            string hash,
            long size,
            string commitId,
            DateTimeOffset timestamp)
        {
            var published = new DateTimeOffset(DateTime.SpecifyKind(package.Published, DateTimeKind.Utc));

            return new PackageDetailsCatalogLeaf
            {
                Type = new[] { CatalogEntry.PackageDetailsType, "catalog:Permalink" },
                CommitId = commitId,
                CommitTimestamp = timestamp,
                PackageId = package.Id,
                PackageVersion = package.NormalizedVersionString,
                VerbatimVersion = package.OriginalVersionString ?? package.NormalizedVersionString,
                Published = published,
                Created = published,
                LastEdited = timestamp,
                Authors = string.Join(", ", package.Authors ?? Array.Empty<string>()),
                Description = package.Description,
                DependencyGroups = RegistrationBuilder.ToDependencyGroups(package).ToList(),
                IconUrl = NullIfEmpty(package.IconUrlString),
                IsPrerelease = package.IsPrerelease,
                Language = NullIfEmpty(package.Language),
                LicenseUrl = NullIfEmpty(package.LicenseUrlString),
                Listed = package.Listed,
                MinClientVersion = NullIfEmpty(package.MinClientVersion),
                PackageHash = hash,
                PackageHashAlgorithm = "SHA512",
                PackageSize = size,
                ProjectUrl = NullIfEmpty(package.ProjectUrlString),
                ReleaseNotes = NullIfEmpty(package.ReleaseNotes),
                RequireLicenseAcceptance = package.RequireLicenseAcceptance,
                Summary = NullIfEmpty(package.Summary),
                Tags = package.Tags?.ToList(),
                Title = NullIfEmpty(package.Title),
            };
        }

        /// <summary>
        /// The next commit timestamp: now, truncated to milliseconds so that every database
        /// provider stores it exactly, and always later than the previous commit.
        /// </summary>
        internal static DateTime NextTimestamp(DateTime now, DateTime? last)
        {
            now = now.ToUniversalTime();
            var truncated = new DateTime(now.Ticks - now.Ticks % TimeSpan.TicksPerMillisecond, DateTimeKind.Utc);

            if (last.HasValue && truncated <= last.Value)
            {
                return last.Value.AddMilliseconds(1);
            }

            return truncated;
        }

        private async Task CommitAsync(
            string type,
            string id,
            NuGetVersion version,
            Func<string, DateTimeOffset, CatalogLeaf> buildLeaf,
            CancellationToken cancellationToken)
        {
            await CommitLock.WaitAsync(cancellationToken);
            try
            {
                var last = await _catalog.GetLastCommitTimestampAsync(cancellationToken);
                var timestamp = NextTimestamp(_time.UtcNow, last);
                var commitId = Guid.NewGuid().ToString("D");

                var leaf = buildLeaf(commitId, new DateTimeOffset(timestamp));

                using (var content = new MemoryStream())
                {
                    await JsonSerializer.SerializeAsync(content, leaf, leaf.GetType(), LeafSerializerOptions, cancellationToken);
                    content.Position = 0;

                    var result = await _storage.PutAsync(LeafPath(commitId), content, "application/json", cancellationToken);
                    if (result == StoragePutResult.Conflict)
                    {
                        throw new InvalidOperationException($"Catalog leaf {commitId} already exists with different content");
                    }
                }

                await _catalog.AddAsync(
                    new CatalogEntry
                    {
                        CommitId = commitId,
                        CommitTimestamp = timestamp,
                        Type = type,
                        PackageId = id,
                        PackageVersion = version.ToNormalizedString(),
                    },
                    cancellationToken);

                _logger.LogInformation(
                    "Committed catalog {Type} entry {CommitId} for {PackageId} {PackageVersion}",
                    type,
                    commitId,
                    id,
                    version);
            }
            finally
            {
                CommitLock.Release();
            }
        }

        private async Task<(string Hash, long Size)> HashPackageAsync(Package package, CancellationToken cancellationToken)
        {
            using var stream = await _packageStorage.GetPackageStreamAsync(package.Id, package.Version, cancellationToken);
            using var counting = new CountingStream(stream);
            using var sha512 = SHA512.Create();

            var hash = await sha512.ComputeHashAsync(counting, cancellationToken);

            return (Convert.ToBase64String(hash), counting.BytesRead);
        }

        private static string NullIfEmpty(string value) => string.IsNullOrEmpty(value) ? null : value;

        /// <summary>
        /// Counts the bytes read through it, since a storage stream may not know its length.
        /// </summary>
        private sealed class CountingStream : Stream
        {
            private readonly Stream _source;

            public CountingStream(Stream source) => _source = source;

            public long BytesRead { get; private set; }

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();
            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

            public override int Read(byte[] buffer, int offset, int count)
            {
                var read = _source.Read(buffer, offset, count);
                BytesRead += read;
                return read;
            }

            public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            {
                var read = await _source.ReadAsync(buffer, cancellationToken);
                BytesRead += read;
                return read;
            }

            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
                => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

            public override void Flush() { }
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }
    }
}
