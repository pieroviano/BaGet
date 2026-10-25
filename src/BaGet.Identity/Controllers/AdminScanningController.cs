using System.Threading;
using System.Threading.Tasks;
using BaGet.Identity.Mvc;
using BaGet.Identity.Pages.Admin;
using BaGet.Identity.Workflows;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Identity.Controllers;

/// <summary>
/// The vulnerability scanning screens: the overview (<c>/admin/scanning</c>), the dashboard of a repository
/// (<c>/admin/scanning/{name}</c>) and the policy of a project (<c>/admin/scanning/projects/{name}</c>). They replace the Razor
/// Pages of the same names (same URLs, handlers and <c>asp-page</c> names); the logic is in <see cref="ScanningWorkflow"/>.
/// Administrators only (404 for anyone else), every POST is refused with 503 while the read-only gate is closed.
/// </summary>
public sealed class AdminScanningController : AccessControllerBase
{
    // ---------------------------------------------------------------- overview

    /// <summary>Shows the overview.</summary>
    [Route("admin/scanning"), HttpGet, HttpHead, PageHandler, LegacyPage("/Admin/Scanning")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var workflow = new ScanningWorkflow(HttpContext, Url);
        await workflow.GetOverviewAsync(cancellationToken);

        return View("Index", workflow.Overview);
    }

    // ---------------------------------------------------------------- one repository

    /// <summary>Shows the dashboard of a repository.</summary>
    [Route("admin/scanning/{name}"), HttpGet, HttpHead, PageHandler, LegacyPage("/Admin/ScanRepository")]
    public async Task<IActionResult> Repository(
        string name, [FromQuery(Name = "min")] int minSeverity, [FromQuery(Name = "p")] int pageNumber, CancellationToken cancellationToken)
    {
        var workflow = StartRepository(name);
        workflow.Repository.MinSeverity = minSeverity;
        workflow.Repository.PageNumber = pageNumber == 0 ? 1 : pageNumber;

        return ShowRepository(workflow, await workflow.GetRepositoryAsync(cancellationToken));
    }

    /// <summary>Saves the repository policy; empty values remove it.</summary>
    [Route("admin/scanning/{name}"), HttpPost, PageHandler("Policy"), LegacyPage("/Admin/ScanRepository")]
    public async Task<IActionResult> RepositoryPolicy(
        string name, string? warn, string? blockDownload, string? blockDeploy, bool blockUnscanned, string? password, CancellationToken cancellationToken)
    {
        var workflow = StartRepository(name);
        var fields = new ScanPolicyFields { Warn = warn, BlockDownload = blockDownload, BlockDeploy = blockDeploy, BlockUnscanned = blockUnscanned };

        return ShowRepository(workflow, await workflow.SaveRepositoryPolicyAsync(fields, password, cancellationToken));
    }

    /// <summary>Starts a scan of every artifact of the repository in the background.</summary>
    [Route("admin/scanning/{name}"), HttpPost, PageHandler("Scan"), LegacyPage("/Admin/ScanRepository")]
    public async Task<IActionResult> RepositoryScan(string name, CancellationToken cancellationToken)
    {
        var workflow = StartRepository(name);

        return ShowRepository(workflow, await workflow.ScanAsync(cancellationToken));
    }

    /// <summary>A form posted to the screen's own URL, with no handler: the screen is rendered (the page had no default handler).</summary>
    [Route("admin/scanning/{name}"), HttpPost, PageHandler, ImplicitPageHandler, LegacyPage("/Admin/ScanRepository")]
    public async Task<IActionResult> RepositoryPost(
        string name, [FromQuery(Name = "min")] int minSeverity, [FromQuery(Name = "p")] int pageNumber, CancellationToken cancellationToken)
    {
        var workflow = StartRepository(name);
        workflow.Repository.MinSeverity = minSeverity;
        workflow.Repository.PageNumber = pageNumber == 0 ? 1 : pageNumber;

        return ShowRepository(workflow, await workflow.GetRepositoryAsync(cancellationToken));
    }

    // ---------------------------------------------------------------- one project

    /// <summary>Shows the policy of a project.</summary>
    [Route("admin/scanning/projects/{name}"), HttpGet, HttpHead, PageHandler, LegacyPage("/Admin/ScanProject")]
    public async Task<IActionResult> Project(string name, CancellationToken cancellationToken)
    {
        var workflow = StartProject(name);

        return ShowProject(workflow, await workflow.GetProjectAsync(cancellationToken));
    }

    /// <summary>Saves the project policy; empty values remove it.</summary>
    [Route("admin/scanning/projects/{name}"), HttpPost, PageHandler("Policy"), LegacyPage("/Admin/ScanProject")]
    public async Task<IActionResult> ProjectPolicy(
        string name, string? warn, string? blockDownload, string? blockDeploy, bool blockUnscanned, string? password, CancellationToken cancellationToken)
    {
        var workflow = StartProject(name);
        var fields = new ScanPolicyFields { Warn = warn, BlockDownload = blockDownload, BlockDeploy = blockDeploy, BlockUnscanned = blockUnscanned };

        return ShowProject(workflow, await workflow.SaveProjectPolicyAsync(fields, password, cancellationToken));
    }

    /// <summary>A form posted to the screen's own URL, with no handler: the screen is rendered (the page had no default handler).</summary>
    [Route("admin/scanning/projects/{name}"), HttpPost, PageHandler, ImplicitPageHandler, LegacyPage("/Admin/ScanProject")]
    public async Task<IActionResult> ProjectPost(string name, CancellationToken cancellationToken)
    {
        var workflow = StartProject(name);

        return ShowProject(workflow, await workflow.GetProjectAsync(cancellationToken));
    }

    // ---------------------------------------------------------------- helpers

    private ScanningWorkflow StartRepository(string? name)
    {
        var workflow = new ScanningWorkflow(HttpContext, Url);
        workflow.Repository.Name = name;

        return workflow;
    }

    private ScanningWorkflow StartProject(string? name)
    {
        var workflow = new ScanningWorkflow(HttpContext, Url);
        workflow.Project.Name = name;

        return workflow;
    }

    private IActionResult ShowRepository(ScanningWorkflow workflow, IActionResult? result)
    {
        Flash(workflow.Repository);

        return result ?? View("Repository", workflow.Repository);
    }

    private IActionResult ShowProject(ScanningWorkflow workflow, IActionResult? result)
    {
        Flash(workflow.Project);

        return result ?? View("Project", workflow.Project);
    }
}
