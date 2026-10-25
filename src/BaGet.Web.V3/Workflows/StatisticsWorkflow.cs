using System;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;

namespace BaGet.Web;

/// <summary>The logic of the statistics page, used by <see cref="StatisticsController"/>.</summary>
public sealed class StatisticsWorkflow
{
    private readonly StatisticsService _statistics;

    /// <summary>Creates the workflow.</summary>
    public StatisticsWorkflow(StatisticsService statistics)
    {
        _statistics = statistics ?? throw new ArgumentNullException(nameof(statistics));
    }

    /// <summary>The feed's statistics, or null when the database cannot report them.</summary>
    public async Task<FeedStatistics?> GetAsync(CancellationToken cancellationToken)
        => _statistics.IsSupported ? await _statistics.GetAsync(cancellationToken) : null;
}
