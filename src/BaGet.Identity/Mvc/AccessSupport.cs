using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.Application;
using BaGet.Identity.Pages.Admin;
using Microsoft.Extensions.DependencyInjection;

namespace BaGet.Identity.Mvc;

/// <summary>
/// The parts of the retention and replication screens (and the thin forwards of the access screens' pattern helpers, which live in <see cref="AccessPatternSupport"/>), used by the controller bases
/// (<see cref="AccessControllerBase"/>, <see cref="RetentionControllerBase"/>, <see cref="ReplicationControllerBase"/>).
/// </summary>
internal static class AccessSupport
{
    /// <summary>The single actions, in display order.</summary>
    public static readonly AccessAction[] AllActions = AccessPatternSupport.AllActions;

    /// <summary>Parses the ticked action boxes (names of <see cref="AccessAction"/> values).</summary>
    public static AccessAction ParseActions(IEnumerable<string>? names) => AccessPatternSupport.ParseActions(names);

    /// <summary>The actions in <paramref name="actions"/> as words, or "none".</summary>
    public static string Words(AccessAction actions) => AccessPatternSupport.Words(actions);

    /// <summary>Patterns one per line, the way the editors show them.</summary>
    public static string Lines(IEnumerable<string> patterns) => AccessPatternSupport.Lines(patterns);

    /// <summary>The package filter and repository patterns as one line a person can read back.</summary>
    public static string Join(IEnumerable<string> patterns) => AccessPatternSupport.Join(patterns);

    /// <summary>What a set of patterns selects: the matching repository names and, for a typed package ID, whether it applies.</summary>
    public static Task<PatternPreview> PreviewAsync(
        IServiceProvider services,
        string[] repositoryPatterns,
        string[] includePatterns,
        string[] excludePatterns,
        string? testPackage,
        CancellationToken cancellationToken)
        => AccessPatternSupport.PreviewAsync(services.GetRequiredService<IRepositoryService>(), repositoryPatterns, includePatterns, excludePatterns, testPackage, cancellationToken);

    /// <summary>The repositories a retention policy or hold can name (virtual ones store nothing), by name.</summary>
    public static async Task<IReadOnlyList<RepositoryInfo>> RetentionRepositoriesAsync(IServiceProvider services, CancellationToken cancellationToken)
        => (await services.GetRequiredService<IRepositoryService>().ListAsync(cancellationToken))
            .Where(r => r.Kind != RepositoryKind.Virtual).OrderBy(r => r.Name, StringComparer.Ordinal).ToList();

    /// <summary>The names of the projects a policy can name.</summary>
    public static async Task<IReadOnlyList<string>> ProjectNamesAsync(IServiceProvider services, CancellationToken cancellationToken)
        => (await services.GetRequiredService<IAccessAdministration>().ListProjectsAsync(cancellationToken)).Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToList();

    /// <summary>The local repositories a replication can name (remote and virtual ones are not replicated), by name.</summary>
    public static async Task<IReadOnlyList<RepositoryInfo>> ReplicationRepositoriesAsync(IServiceProvider services, CancellationToken cancellationToken)
        => (await services.GetRequiredService<IRepositoryService>().ListAsync(cancellationToken))
            .Where(r => r.Kind == RepositoryKind.Local).OrderBy(r => r.Name, StringComparer.Ordinal).ToList();

    /// <summary>The schedule and triggers of a configuration in a few words.</summary>
    public static string DescribeReplication(ReplicationConfig c)
    {
        var parts = new List<string> { c.Direction == ReplicationDirection.Push ? "push" : "pull" };
        if (c.OnDeploy) parts.Add("on deploy");
        parts.Add(c.ScheduleMinutes is int minutes ? "every " + minutes + " min" : "manual");
        if (c.PropagateDeletes) parts.Add("deletes follow");
        parts.Add(c.Conflict == ReplicationConflict.SourceWins ? "source wins" : "target wins");

        return string.Join(", ", parts);
    }

    /// <summary>The rules of a policy in one line, for the lists.</summary>
    public static string DescribeRetention(RetentionPolicy p)
    {
        var rules = new List<string>();
        if (p.KeepLast is int keep) rules.Add($"keep the newest {keep}");
        if (p.OlderThanDays is int age) rules.Add($"older than {age} d");
        if (p.UnusedDays is int unused) rules.Add($"unused for {unused} d");
        if (p.CacheLimitBytes is long cache) rules.Add($"cache over {RetentionReport.FormatBytes(cache)}");
        if (p.UntaggedAfterDays is int untagged) rules.Add($"untagged for {untagged} d");
        if (p.CollectGarbage) rules.Add("garbage collection");
        var text = rules.Count == 0 ? "no rule" : string.Join(", ", rules);
        if (p.PrereleaseOnly) text += " (prerelease only)";

        return text;
    }
}
