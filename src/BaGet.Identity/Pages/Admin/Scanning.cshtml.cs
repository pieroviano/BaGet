using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BaGet.Identity.Pages.Admin;

/// <summary>
/// Vulnerability scanning at a glance: whether it is on and with what (OSV online or an offline copy, Trivy for container
/// images), every repository with its counts and the policy in force, and the projects whose policy can be edited. Each row leads to
/// the repository's dashboard (<see cref="ScanRepositoryModel"/>) or the project's policy (<see cref="ScanProjectModel"/>).
/// Administrators only; reads only.
/// </summary>
public class ScanningModel : AccessPageModel
{
    /// <summary>One repository.</summary>
    public sealed record RepositoryRow(RepositoryInfo Repository, ScanSummary Summary, EffectiveScanPolicy Policy);

    /// <summary>One project.</summary>
    public sealed record ProjectRow(ProjectSummary Project, bool HasPolicy);

    /// <summary>The scanning options in force.</summary>
    public ScanningOptions Options { get; private set; } = new();

    /// <summary>Whether Trivy is configured (container images are scanned).</summary>
    public bool TrivyConfigured { get; private set; }

    /// <summary>The repositories that can hold artifacts (virtual ones hold none).</summary>
    public IReadOnlyList<RepositoryRow> Repositories { get; private set; } = Array.Empty<RepositoryRow>();

    /// <summary>The projects.</summary>
    public IReadOnlyList<ProjectRow> Projects { get; private set; } = Array.Empty<ProjectRow>();

    /// <summary>Shows the overview.</summary>
    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var services = HttpContext.RequestServices;
        Options = services.GetRequiredService<IOptionsSnapshot<ScanningOptions>>().Value;
        TrivyConfigured = services.GetRequiredService<ITrivyRunner>().IsConfigured(Options);

        var scopes = services.GetRequiredService<IServiceScopeFactory>();
        var policies = services.GetRequiredService<IScanPolicyService>();
        var rows = new List<RepositoryRow>();
        foreach (var repository in (await services.GetRequiredService<IRepositoryService>().ListAsync(cancellationToken)).Where(r => r.Kind != RepositoryKind.Virtual))
        {
            using var scope = scopes.CreateRepositoryScope(repository);
            var summary = await scope.ServiceProvider.GetRequiredService<IScanService>().SummarizeAsync(cancellationToken);
            rows.Add(new RepositoryRow(repository, summary, await policies.GetEffectiveAsync(repository.Key, cancellationToken)));
        }

        Repositories = rows;

        var projects = new List<ProjectRow>();
        foreach (var project in await AccessAdmin.ListProjectsAsync(cancellationToken))
        {
            projects.Add(new ProjectRow(project, await policies.GetAsync(ScanPolicyScope.Project, project.Key, cancellationToken) != null));
        }

        Projects = projects;
    }
}
