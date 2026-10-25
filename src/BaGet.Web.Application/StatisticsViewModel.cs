using BaGet.Core;

namespace BaGet.Web;

/// <summary>What the statistics page shows.</summary>
/// <param name="Statistics">The feed's totals and lists; null when the database cannot report them.</param>
public sealed record StatisticsViewModel(FeedStatistics? Statistics);
