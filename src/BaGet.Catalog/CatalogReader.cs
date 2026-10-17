using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Protocol.Models;

namespace BaGet.Catalog
{
    /// <summary>
    /// Builds the catalog documents. The index and pages are built from the database on
    /// read, so their URLs always match the host the client used; the leaves are read from
    /// storage, since a deleted package's details exist nowhere else.
    /// </summary>
    public class CatalogReader
    {
        /// <summary>
        /// nuget.org uses 550. Any size is valid; a client follows the index.
        /// </summary>
        internal const int PageSize = 500;

        private readonly ICatalogDatabase _catalog;
        private readonly IStorageService _storage;
        private readonly IUrlGenerator _url;

        public CatalogReader(ICatalogDatabase catalog, IStorageService storage, IUrlGenerator url)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _url = url ?? throw new ArgumentNullException(nameof(url));
        }

        public bool IsSupported => _catalog.IsSupported;

        public async Task<CatalogIndex> GetIndexAsync(CancellationToken cancellationToken)
        {
            var timestamps = await _catalog.GetCommitTimestampsAsync(cancellationToken);

            return new CatalogIndex
            {
                CommitTimestamp = timestamps.Count == 0 ? DateTimeOffset.UnixEpoch : Utc(timestamps[^1]),
                Count = PageCount(timestamps.Count),
                Items = Enumerable.Range(0, PageCount(timestamps.Count))
                    .Select(page =>
                    {
                        var count = Math.Min(PageSize, timestamps.Count - page * PageSize);

                        return new CatalogPageItem
                        {
                            CatalogPageUrl = _url.GetCatalogPageUrl(page),
                            CommitTimestamp = Utc(timestamps[page * PageSize + count - 1]),
                            Count = count,
                        };
                    })
                    .ToList(),
            };
        }

        /// <summary>
        /// One page, or null if the page does not exist.
        /// </summary>
        public async Task<CatalogPage> GetPageOrNullAsync(int pageIndex, CancellationToken cancellationToken)
        {
            if (pageIndex < 0) return null;

            var entries = await _catalog.GetEntriesAsync(pageIndex * PageSize, PageSize, cancellationToken);
            if (entries.Count == 0) return null;

            return new CatalogPage
            {
                CommitTimestamp = Utc(entries[^1].CommitTimestamp),
                Count = entries.Count,
                CatalogIndexUrl = _url.GetCatalogIndexUrl(),
                Items = entries
                    .Select(e => new CatalogLeafItem
                    {
                        CatalogLeafUrl = _url.GetCatalogLeafUrl(e.CommitId),
                        Type = "nuget:" + e.Type,
                        CommitTimestamp = Utc(e.CommitTimestamp),
                        PackageId = e.PackageId,
                        PackageVersion = e.PackageVersion,
                    })
                    .ToList(),
            };
        }

        /// <summary>
        /// One leaf document as JSON, or null if no such leaf exists.
        /// </summary>
        public async Task<string> GetLeafOrNullAsync(string commitId, CancellationToken cancellationToken)
        {
            if (!Guid.TryParseExact(commitId, "D", out _)) return null;

            var entry = await _catalog.FindOrNullAsync(commitId, cancellationToken);
            if (entry == null) return null;

            JsonNode leaf;
            using (var stream = await _storage.GetAsync(CatalogWriter.LeafPath(entry.CommitId), cancellationToken))
            {
                leaf = await JsonNode.ParseAsync(stream, cancellationToken: cancellationToken);
            }

            // The leaf's own URL is not stored, so it follows the host like every other URL.
            leaf["@id"] = _url.GetCatalogLeafUrl(entry.CommitId);

            return leaf.ToJsonString();
        }

        internal static int PageCount(int entries) => (entries + PageSize - 1) / PageSize;

        private static DateTimeOffset Utc(DateTime value)
            => new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc));
    }
}
