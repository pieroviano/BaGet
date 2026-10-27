using System.Threading;
using System.Threading.Tasks;
using BaGet.Identity.Controllers.Base;
using BaGet.Identity.Controllers.UI;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using BaGet.Identity.Application.Models.Policies;
using BaGet.Identity.Mvc.Attributes;
using BaGet.Identity.Mvc.UI;

namespace BaGet.Identity.Controllers;

/// <summary>
/// The vulnerability scanning screens: the overview (<c>/admin/scanning</c>), the dashboard of a repository
/// (<c>/admin/scanning/{name}</c>) and the policy of a project (<c>/admin/scanning/projects/{name}</c>). They replace the Razor
/// Pages of the same names (same URLs, handlers and <c>asp-page</c> names); the work is done by the JSON twin
/// (<see cref="ScanningUiApiController"/>) and this controller only renders its answers. Administrators only (404 for anyone else), every POST
/// is refused with 503 while the read-only gate is closed.
/// </summary>
public sealed class AdminScanningController : AccessControllerBase
{
    // ---------------------------------------------------------------- overview

    /// <summary>Shows the overview.</summary>
    [Route("admin/scanning"), HttpGet, HttpHead, PageHandler, LegacyPage("/Admin/Scanning")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
        => Render(await Api<ScanningUiApiController, ScanningViewModel>(api => api.GetOverview(cancellationToken)), "Index");

    // ---------------------------------------------------------------- one repository

    /// <summary>Shows the dashboard of a repository.</summary>
    [Route("admin/scanning/{name}"), HttpGet, HttpHead, PageHandler, LegacyPage("/Admin/ScanRepository")]
    public async Task<IActionResult> Repository(
        string name, [FromQuery(Name = "min")] int minSeverity, [FromQuery(Name = "p")] int pageNumber, CancellationToken cancellationToken)
        => ShowRepository(await Api<ScanningUiApiController, ScanRepositoryViewModel>(api => api.GetRepository(name, minSeverity, pageNumber, cancellationToken)));

    /// <summary>Saves the repository policy; empty values remove it.</summary>
    [Route("admin/scanning/{name}"), HttpPost, PageHandler("Policy"), LegacyPage("/Admin/ScanRepository")]
    public async Task<IActionResult> RepositoryPolicy(
        string name, string? warn, string? blockDownload, string? blockDeploy, bool blockUnscanned, string? password, CancellationToken cancellationToken)
        => ShowRepository(await Api<ScanningUiApiController, ScanRepositoryViewModel>(
            api => api.SaveRepositoryPolicy(name, new ScanPolicyBody(warn, blockDownload, blockDeploy, blockUnscanned, password), cancellationToken)));

    /// <summary>Starts a scan of every artifact of the repository in the background.</summary>
    [Route("admin/scanning/{name}"), HttpPost, PageHandler("Scan"), LegacyPage("/Admin/ScanRepository")]
    public async Task<IActionResult> RepositoryScan(string name, CancellationToken cancellationToken)
        => ShowRepository(await Api<ScanningUiApiController, ScanRepositoryViewModel>(api => api.Scan(name, cancellationToken)));

    /// <summary>A form posted to the screen's own URL, with no handler: the screen is rendered (the page had no default handler).</summary>
    [Route("admin/scanning/{name}"), HttpPost, PageHandler, ImplicitPageHandler, LegacyPage("/Admin/ScanRepository")]
    public async Task<IActionResult> RepositoryPost(
        string name, [FromQuery(Name = "min")] int minSeverity, [FromQuery(Name = "p")] int pageNumber, CancellationToken cancellationToken)
        => ShowRepository(await Api<ScanningUiApiController, ScanRepositoryViewModel>(api => api.GetRepository(name, minSeverity, pageNumber, cancellationToken)));

    // ---------------------------------------------------------------- one project

    /// <summary>Shows the policy of a project.</summary>
    [Route("admin/scanning/projects/{name}"), HttpGet, HttpHead, PageHandler, LegacyPage("/Admin/ScanProject")]
    public async Task<IActionResult> Project(string name, CancellationToken cancellationToken)
        => ShowProject(await Api<ScanningUiApiController, ScanProjectViewModel>(api => api.GetProject(name, cancellationToken)));

    /// <summary>Saves the project policy; empty values remove it.</summary>
    [Route("admin/scanning/projects/{name}"), HttpPost, PageHandler("Policy"), LegacyPage("/Admin/ScanProject")]
    public async Task<IActionResult> ProjectPolicy(
        string name, string? warn, string? blockDownload, string? blockDeploy, bool blockUnscanned, string? password, CancellationToken cancellationToken)
        => ShowProject(await Api<ScanningUiApiController, ScanProjectViewModel>(
            api => api.SaveProjectPolicy(name, new ScanPolicyBody(warn, blockDownload, blockDeploy, blockUnscanned, password), cancellationToken)));

    /// <summary>A form posted to the screen's own URL, with no handler: the screen is rendered (the page had no default handler).</summary>
    [Route("admin/scanning/projects/{name}"), HttpPost, PageHandler, ImplicitPageHandler, LegacyPage("/Admin/ScanProject")]
    public async Task<IActionResult> ProjectPost(string name, CancellationToken cancellationToken)
        => ShowProject(await Api<ScanningUiApiController, ScanProjectViewModel>(api => api.GetProject(name, cancellationToken)));

    // ---------------------------------------------------------------- helpers

    // The use case names the package screen of each finding; the address is this controller's to build (it knows the routes).
    private IActionResult ShowRepository(UiApiResponse<ScanRepositoryViewModel> response)
        => Render(
            response,
            "Repository",
            model =>
            {
                Flash(model);
                model.LinkResolver = link => Url.Page(link.Screen, new RouteValueDictionary(link.RouteValues));
                return model;
            });

    private IActionResult ShowProject(UiApiResponse<ScanProjectViewModel> response) => RenderFlashed(response, "Project");
}
