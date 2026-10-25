using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace BaGet.Identity.Pages.Admin;

/// <summary>
/// What the policy form posts (<c>Form.*</c>): the rules of a <see cref="RetentionPolicy"/> in the units a person types
/// (the cache limit in MiB). <see cref="ToInput"/> turns it into the administration's input.
/// </summary>
public sealed class RetentionPolicyForm
{
    public string? Name { get; set; }

    /// <summary><c>repository</c> or <c>project</c>.</summary>
    public string? ScopeKind { get; set; } = "repository";

    /// <summary>The repository or project name.</summary>
    public string? ScopeName { get; set; }

    public bool Enabled { get; set; }

    public string? NamePatterns { get; set; }

    public int? KeepLast { get; set; }

    public int? OlderThanDays { get; set; }

    public int? UnusedDays { get; set; }

    public bool PrereleaseOnly { get; set; }

    /// <summary>The remote cache limit in MiB.</summary>
    public long? CacheLimitMb { get; set; }

    public int? UntaggedAfterDays { get; set; }

    public bool CollectGarbage { get; set; }

    public int? ScheduleHours { get; set; }

    /// <summary>The administration's input for policy <paramref name="key"/> (0 creates).</summary>
    public RetentionPolicyInput ToInput(int key)
    {
        var project = string.Equals(ScopeKind, "project", StringComparison.Ordinal);
        long? limit = CacheLimitMb is long mb ? (mb > long.MaxValue / (1024 * 1024) ? long.MaxValue : mb * 1024 * 1024) : null;

        return new RetentionPolicyInput(
            key, Name, project ? null : ScopeName, project ? ScopeName : null, Enabled, NamePatterns,
            KeepLast, OlderThanDays, UnusedDays, PrereleaseOnly, limit, UntaggedAfterDays, CollectGarbage, ScheduleHours);
    }

    /// <summary>The form filled in from a stored policy.</summary>
    public static RetentionPolicyForm From(RetentionPolicyView view)
    {
        var p = view.Policy;

        return new RetentionPolicyForm
        {
            Name = p.Name,
            ScopeKind = p.ProjectKey != null ? "project" : "repository",
            ScopeName = view.Repository ?? view.Project,
            Enabled = p.Enabled,
            NamePatterns = p.NamePatterns,
            KeepLast = p.KeepLast,
            OlderThanDays = p.OlderThanDays,
            UnusedDays = p.UnusedDays,
            PrereleaseOnly = p.PrereleaseOnly,
            CacheLimitMb = p.CacheLimitBytes is long bytes ? (bytes + 1024 * 1024 - 1) / (1024 * 1024) : null,
            UntaggedAfterDays = p.UntaggedAfterDays,
            CollectGarbage = p.CollectGarbage,
            ScheduleHours = p.ScheduleHours,
        };
    }
}

/// <summary>What the policy fields partial shows.</summary>
/// <param name="Form">The values.</param>
/// <param name="Repositories">The repositories to choose from.</param>
/// <param name="Projects">The projects to choose from.</param>
/// <param name="IdPrefix">Prefix of the element ids, so two forms on one page do not clash.</param>
public sealed record RetentionFieldsView(RetentionPolicyForm Form, IReadOnlyList<RepositoryInfo> Repositories, IReadOnlyList<string> Projects, string IdPrefix);

/// <summary>
/// Base of the retention pages (policies, a policy's report, legal holds): the access pages' rules (administrators only, 404 for
/// anyone else, redirect where there is no relational database, 503 on a POST while the server is read-only), with the retention
/// services and the lists the forms choose from. A <see cref="RetentionException"/> is an <see cref="AccessModelException"/>, so
/// <c>ActAsync</c> turns it into the page's <c>Error</c>.
/// </summary>
[Obsolete("The retention/replication screens are controllers now; use RetentionControllerBase. This page model base has no page left and will be removed in the next major version.")]
public abstract class RetentionPageModel : AccessPageModel
{
    /// <summary>Policies and legal holds.</summary>
    protected IRetentionAdministration Retention => HttpContext.RequestServices.GetRequiredService<IRetentionAdministration>();

    /// <summary>Runs policies in the background and keeps their last report.</summary>
    protected RetentionJobs Jobs => HttpContext.RequestServices.GetRequiredService<RetentionJobs>();

    /// <summary>The repositories a policy or hold can name (virtual ones store nothing), by name.</summary>
    public IReadOnlyList<RepositoryInfo> Repositories { get; private set; } = Array.Empty<RepositoryInfo>();

    /// <summary>The names of the projects a policy can name.</summary>
    public IReadOnlyList<string> ProjectNames { get; private set; } = Array.Empty<string>();

    /// <summary>Loads <see cref="Repositories"/> and <see cref="ProjectNames"/>.</summary>
    protected async Task LoadChoicesAsync(CancellationToken cancellationToken)
    {
        Repositories = await AccessSupport.RetentionRepositoriesAsync(HttpContext.RequestServices, cancellationToken);
        ProjectNames = await AccessSupport.ProjectNamesAsync(HttpContext.RequestServices, cancellationToken);
    }

    /// <summary>The rules of a policy in one line, for the lists.</summary>
    public static string Describe(RetentionPolicy p) => AccessSupport.DescribeRetention(p);
}
