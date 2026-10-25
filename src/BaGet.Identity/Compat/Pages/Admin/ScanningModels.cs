using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.Workflows;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Identity.Pages.Admin;

/// <summary>
/// The page model of the vulnerability scanning overview when it was a Razor Page: whether scanning is on, every repository with its
/// counts and the policy in force, and the projects whose policy can be edited.
/// </summary>
/// <remarks>
/// Kept as a shim (no breaking change of the public API); <c>AdminScanningController</c> serves <c>/admin/scanning</c>. Both call
/// <c>ScanningWorkflow</c>.
/// </remarks>
[Obsolete("The scanning screens are served by AdminScanningController; this page model has no page any more and will be removed in the next major version.")]
public class ScanningModel : AccessPageModel
{
    /// <summary>One repository.</summary>
    public sealed record RepositoryRow(RepositoryInfo Repository, ScanSummary Summary, EffectiveScanPolicy Policy);

    /// <summary>One project.</summary>
    public sealed record ProjectRow(ProjectSummary Project, bool HasPolicy);

    private ScanningWorkflow? _workflow;

    private ScanningWorkflow Workflow => _workflow ??= new ScanningWorkflow(HttpContext, Url);

    /// <summary>The scanning options in force.</summary>
    public ScanningOptions Options => Workflow.Overview.Options;

    /// <summary>Whether Trivy is configured (container images are scanned).</summary>
    public bool TrivyConfigured => Workflow.Overview.TrivyConfigured;

    /// <summary>The repositories that can hold artifacts (virtual ones hold none).</summary>
    public IReadOnlyList<RepositoryRow> Repositories
        => Workflow.Overview.Repositories.Select(r => new RepositoryRow(r.Repository, r.Summary, r.Policy)).ToList();

    /// <summary>The projects.</summary>
    public IReadOnlyList<ProjectRow> Projects
        => Workflow.Overview.Projects.Select(p => new ProjectRow(p.Project, p.HasPolicy)).ToList();

    /// <summary>Shows the overview.</summary>
    public Task OnGetAsync(CancellationToken cancellationToken) => Workflow.GetOverviewAsync(cancellationToken);
}

/// <summary>
/// The page model of the scanning dashboard of one repository when it was a Razor Page: counts, the versions with findings, the
/// policy that applies and its editor, and <em>Scan now</em>.
/// </summary>
/// <remarks>
/// Kept as a shim (no breaking change of the public API); <c>AdminScanningController</c> serves <c>/admin/scanning/{name}</c>. Both
/// call <c>ScanningWorkflow</c>.
/// </remarks>
[Obsolete("The scanning screens are served by AdminScanningController; this page model has no page any more and will be removed in the next major version.")]
public class ScanRepositoryModel : AccessPageModel
{
    /// <summary>Versions listed per page.</summary>
    public const int PageSize = 50;

    private ScanningWorkflow? _workflow;

    private ScanningWorkflow Workflow => _workflow ??= new ScanningWorkflow(HttpContext, Url);

    /// <summary>The repository's name (route).</summary>
    [BindProperty(SupportsGet = true)]
    public string? Name { get => Workflow.Repository.Name; set => Workflow.Repository.Name = value; }

    /// <summary>The lowest severity listed (0 low ... 3 critical).</summary>
    [BindProperty(Name = "min", SupportsGet = true)]
    public int MinSeverity { get => Workflow.Repository.MinSeverity; set => Workflow.Repository.MinSeverity = value; }

    /// <summary>The page of the list, from 1.</summary>
    [BindProperty(Name = "p", SupportsGet = true)]
    public int PageNumber { get => Workflow.Repository.PageNumber; set => Workflow.Repository.PageNumber = value; }

    /// <summary>The repository.</summary>
    public RepositoryInfo? Repository => Workflow.Repository.Repository;

    /// <summary>The counts.</summary>
    public ScanSummary? Summary => Workflow.Repository.Summary;

    /// <summary>The policy in force and where it comes from.</summary>
    public EffectiveScanPolicy Effective => Workflow.Repository.Effective;

    /// <summary>The policy stored for this repository itself, or null.</summary>
    public ScanPolicy? Own => Workflow.Repository.Own;

    /// <summary>The versions with findings on this page.</summary>
    public IReadOnlyList<ScanResult> Findings => Workflow.Repository.Findings;

    /// <summary>Whether there is another page.</summary>
    public bool HasMore => Workflow.Repository.HasMore;

    /// <summary>The last run of the repository scan, or null.</summary>
    public AdminJobStatus? Job => Workflow.Repository.Job;

    /// <summary>The fields of the policy editor.</summary>
    public ScanPolicyFormView PolicyForm => new(ScanPolicyFields.From(Own), Confirmation("scan-policy-password"), "scan-policy");

    /// <summary>The page of a version in its format (the repository's package page), for the findings list.</summary>
    public string? PackageUrl(ScanResult result) => Workflow.Repository.PackageUrl(result);

    /// <summary>Shows the dashboard.</summary>
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
        => Finish(await Workflow.GetRepositoryAsync(cancellationToken));

    /// <summary>Saves the repository policy; empty values remove it.</summary>
    public async Task<IActionResult> OnPostPolicyAsync(string? warn, string? blockDownload, string? blockDeploy, bool blockUnscanned, string? password, CancellationToken cancellationToken)
    {
        var fields = new ScanPolicyFields { Warn = warn, BlockDownload = blockDownload, BlockDeploy = blockDeploy, BlockUnscanned = blockUnscanned };

        return Finish(await Workflow.SaveRepositoryPolicyAsync(fields, password, cancellationToken));
    }

    /// <summary>Starts a scan of every artifact of the repository in the background.</summary>
    public async Task<IActionResult> OnPostScanAsync(CancellationToken cancellationToken)
        => Finish(await Workflow.ScanAsync(cancellationToken));

    private IActionResult Finish(IActionResult? result)
    {
        Message = Workflow.Repository.Message;
        Error = Workflow.Repository.Error;

        return result ?? Page();
    }
}

/// <summary>
/// The page model of the scanning policy of a project when it was a Razor Page: it applies to every repository of the project that
/// has no policy of its own.
/// </summary>
/// <remarks>
/// Kept as a shim (no breaking change of the public API); <c>AdminScanningController</c> serves
/// <c>/admin/scanning/projects/{name}</c>. Both call <c>ScanningWorkflow</c>.
/// </remarks>
[Obsolete("The scanning screens are served by AdminScanningController; this page model has no page any more and will be removed in the next major version.")]
public class ScanProjectModel : AccessPageModel
{
    private ScanningWorkflow? _workflow;

    private ScanningWorkflow Workflow => _workflow ??= new ScanningWorkflow(HttpContext, Url);

    /// <summary>The project's name (route).</summary>
    [BindProperty(SupportsGet = true)]
    public string? Name { get => Workflow.Project.Name; set => Workflow.Project.Name = value; }

    /// <summary>The project.</summary>
    public ProjectDetails? Project => Workflow.Project.Project;

    /// <summary>The policy stored for the project, or null.</summary>
    public ScanPolicy? Own => Workflow.Project.Own;

    /// <summary>The fields of the policy editor.</summary>
    public ScanPolicyFormView PolicyForm => new(ScanPolicyFields.From(Own), Confirmation("scan-project-password"), "scan-project");

    /// <summary>Shows the policy of the project.</summary>
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
        => Finish(await Workflow.GetProjectAsync(cancellationToken));

    /// <summary>Saves the project policy; empty values remove it.</summary>
    public async Task<IActionResult> OnPostPolicyAsync(string? warn, string? blockDownload, string? blockDeploy, bool blockUnscanned, string? password, CancellationToken cancellationToken)
    {
        var fields = new ScanPolicyFields { Warn = warn, BlockDownload = blockDownload, BlockDeploy = blockDeploy, BlockUnscanned = blockUnscanned };

        return Finish(await Workflow.SaveProjectPolicyAsync(fields, password, cancellationToken));
    }

    private IActionResult Finish(IActionResult? result)
    {
        Message = Workflow.Project.Message;
        Error = Workflow.Project.Error;

        return result ?? Page();
    }
}
