using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace BaGet.Core;

public class FeedStatistics
{
    public int PackageIds { get; set; }

    public int PackageVersions { get; set; }

    public long Downloads { get; set; }

    /// <summary>
    /// The most downloaded package IDs, all versions together.
    /// </summary>
    public IReadOnlyList<PackageDownloads> TopPackages { get; set; }

    /// <summary>
    /// The most recently published versions.
    /// </summary>
    public IReadOnlyList<PackageDownloads> RecentVersions { get; set; }
}

public class PackageDownloads
{
    public string Id { get; set; }

    /// <summary>
    /// Null when the downloads are a whole package ID's.
    /// </summary>
    public string Version { get; set; }

    public long Downloads { get; set; }

    public DateTime? Published { get; set; }
}

/// <summary>
/// Feed-wide download statistics, from the per-version counts the database keeps.
/// There is no per-day history: a count only ever goes up.
/// </summary>
public class StatisticsService
{
    private const int ListSize = 10;

    private readonly IContext _context;

    public StatisticsService(IContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// False when the database cannot be queried for aggregates, such as Azure Table storage.
    /// </summary>
    public bool IsSupported => !(_context is NullContext);

    public async Task<FeedStatistics> GetAsync(CancellationToken cancellationToken)
    {
        var listed = _context.Packages.AsNoTracking().Where(p => p.Listed);

        var top = await listed
            .GroupBy(p => p.Id)
            .Select(g => new PackageDownloads { Id = g.Key, Downloads = g.Sum(p => p.Downloads) })
            .OrderByDescending(p => p.Downloads)
            .ThenBy(p => p.Id)
            .Take(ListSize)
            .ToListAsync(cancellationToken);

        var recent = await listed
            .OrderByDescending(p => p.Published)
            .ThenByDescending(p => p.Key)
            .Take(ListSize)
            .Select(p => new PackageDownloads
            {
                Id = p.Id,
                Version = p.OriginalVersionString ?? p.NormalizedVersionString,
                Downloads = p.Downloads,
                Published = p.Published,
            })
            .ToListAsync(cancellationToken);

        return new FeedStatistics
        {
            PackageIds = await listed.Select(p => p.Id).Distinct().CountAsync(cancellationToken),
            PackageVersions = await listed.CountAsync(cancellationToken),
            Downloads = await listed.SumAsync(p => p.Downloads, cancellationToken),
            TopPackages = top,
            RecentVersions = recent,
        };
    }
}