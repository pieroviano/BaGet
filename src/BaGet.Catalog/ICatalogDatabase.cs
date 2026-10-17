using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;

namespace BaGet.Catalog
{
    /// <summary>
    /// Where the catalog log is kept.
    /// </summary>
    public interface ICatalogDatabase
    {
        /// <summary>
        /// False when the configured database cannot hold the catalog, such as Azure Table
        /// storage. The catalog is then neither written nor advertised.
        /// </summary>
        bool IsSupported { get; }

        /// <summary>
        /// The newest entry's commit timestamp, or null if the log is empty.
        /// </summary>
        Task<DateTime?> GetLastCommitTimestampAsync(CancellationToken cancellationToken);

        Task AddAsync(CatalogEntry entry, CancellationToken cancellationToken);

        /// <summary>
        /// Every entry's commit timestamp, in commit order. Used to lay out the pages.
        /// </summary>
        Task<IReadOnlyList<DateTime>> GetCommitTimestampsAsync(CancellationToken cancellationToken);

        /// <summary>
        /// The entries in commit order, skipping <paramref name="skip"/>.
        /// </summary>
        Task<IReadOnlyList<CatalogEntry>> GetEntriesAsync(int skip, int take, CancellationToken cancellationToken);

        Task<CatalogEntry> FindOrNullAsync(string commitId, CancellationToken cancellationToken);
    }
}
