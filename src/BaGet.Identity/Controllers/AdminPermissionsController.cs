using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.Mvc;
using BaGet.Identity.Workflows;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Identity.Controllers;

/// <summary>
/// The permission target screens of the administrator: the list with the new-target form (<c>/admin/permissions</c>, server
/// administrators only) and one target (<c>/admin/permissions/{name}</c>: patterns, preview, grants, delete), which also admits the
/// administrators of the target's project for a project target. They replace the Razor Pages <c>/Admin/Permissions</c> and
/// <c>/Admin/PermissionDetail</c> (same URLs, handlers and <c>asp-page</c> names); the logic is in <see cref="PermissionsWorkflow"/>
/// and <see cref="PermissionDetailWorkflow"/>. Saving patterns, changing grants and deleting ask for the password again, and every
/// POST is refused with 503 while the read-only gate is closed (<see cref="AccessControllerBase"/>).
/// </summary>
public sealed class AdminPermissionsController : AccessControllerBase
{
    private const string DetailPage = "/Admin/PermissionDetail";

    /// <inheritdoc />
    protected override async Task<bool> AuthorizeAsync(CancellationToken cancellationToken)
    {
        // Only the detail screen is widened to a project's administrators (the target is the name route value).
        var isDetail = ControllerContext.ActionDescriptor.EndpointMetadata.OfType<LegacyPageAttribute>().Any(a => a.PageName == DetailPage);

        return isDetail
            ? await AccessAuthorization.TargetAsync(AccessAdmin, IsServerAdministrator, CurrentUserId, RouteData.Values["name"]?.ToString(), cancellationToken)
            : IsServerAdministrator;
    }

    /// <summary>Lists the permission targets.</summary>
    [Route("admin/permissions"), HttpGet, HttpHead, PageHandler, LegacyPage("/Admin/Permissions")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var workflow = StartList(null, null, null, null, null, null);
        await workflow.ListAsync(cancellationToken);

        return ShowList(workflow, null);
    }

    /// <summary>Creates a target and opens it.</summary>
    [Route("admin/permissions"), HttpPost, PageHandler, LegacyPage("/Admin/Permissions")]
    public async Task<IActionResult> Create(string? name, string? repositoryPatterns, string? includePatterns, string? excludePatterns, string? project, string? testPackage, CancellationToken cancellationToken)
    {
        var workflow = StartList(name, repositoryPatterns, includePatterns, excludePatterns, project, testPackage);

        return ShowList(workflow, await workflow.CreateAsync(cancellationToken));
    }

    /// <summary>Previews the form patterns without saving anything.</summary>
    [Route("admin/permissions"), HttpPost, PageHandler("Preview"), LegacyPage("/Admin/Permissions")]
    public async Task<IActionResult> Preview(string? name, string? repositoryPatterns, string? includePatterns, string? excludePatterns, string? project, string? testPackage, CancellationToken cancellationToken)
    {
        var workflow = StartList(name, repositoryPatterns, includePatterns, excludePatterns, project, testPackage);
        await workflow.PreviewPatternsAsync(cancellationToken);

        return ShowList(workflow, null);
    }

    /// <summary>Re-creates the built-in groups and targets that are missing (the upgrade defaults).</summary>
    [Route("admin/permissions"), HttpPost, PageHandler("RestoreBuiltIns"), LegacyPage("/Admin/Permissions")]
    public async Task<IActionResult> RestoreBuiltIns(string? password, CancellationToken cancellationToken)
    {
        var workflow = StartList(null, null, null, null, null, null);

        return ShowList(workflow, await workflow.RestoreBuiltInsAsync(password, cancellationToken));
    }

    /// <summary>Shows one target. <paramref name="principalType"/>/<paramref name="principalName"/> pre-fill the grant form.</summary>
    [Route("admin/permissions/{name}"), HttpGet, HttpHead, PageHandler, LegacyPage(DetailPage)]
    public async Task<IActionResult> Detail(string name, string? testPackage, PrincipalType? principalType, string? principalName, CancellationToken cancellationToken)
    {
        var workflow = StartDetail(name, null, null, null, testPackage, principalType, principalName);

        return ShowDetail(workflow, await workflow.GetAsync(cancellationToken));
    }

    /// <summary>Previews the form patterns (and the test package) without saving anything.</summary>
    [Route("admin/permissions/{name}"), HttpPost, PageHandler("Preview"), LegacyPage(DetailPage)]
    public async Task<IActionResult> DetailPreview(string name, string? repositoryPatterns, string? includePatterns, string? excludePatterns, string? testPackage, PrincipalType? principalType, string? principalName, CancellationToken cancellationToken)
    {
        var workflow = StartDetail(name, repositoryPatterns, includePatterns, excludePatterns, testPackage, principalType, principalName);

        return ShowDetail(workflow, await workflow.PreviewPatternsAsync(cancellationToken));
    }

    /// <summary>Replaces the target patterns.</summary>
    [Route("admin/permissions/{name}"), HttpPost, PageHandler("Save"), LegacyPage(DetailPage)]
    public async Task<IActionResult> Save(string name, string? repositoryPatterns, string? includePatterns, string? excludePatterns, string? password, CancellationToken cancellationToken)
    {
        var workflow = StartDetail(name, repositoryPatterns, includePatterns, excludePatterns, null, null, null);

        return ShowDetail(workflow, await workflow.SaveAsync(password, cancellationToken));
    }

    /// <summary>Gives a principal the ticked actions through the target, replacing an earlier grant to it.</summary>
    [Route("admin/permissions/{name}"), HttpPost, PageHandler("SetGrant"), LegacyPage(DetailPage)]
    public async Task<IActionResult> SetGrant(string name, PrincipalType? principalType, string? principalName, string[]? actions, string? password, CancellationToken cancellationToken)
    {
        var workflow = StartDetail(name, null, null, null, null, principalType, principalName);

        return ShowDetail(workflow, await workflow.SetGrantAsync(actions, password, cancellationToken));
    }

    /// <summary>Removes a principal grant.</summary>
    [Route("admin/permissions/{name}"), HttpPost, PageHandler("RemoveGrant"), LegacyPage(DetailPage)]
    public async Task<IActionResult> RemoveGrant(string name, PrincipalType type, string? principalId, string? password, CancellationToken cancellationToken)
    {
        var workflow = StartDetail(name, null, null, null, null, null, null);

        return ShowDetail(workflow, await workflow.RemoveGrantAsync(type, principalId, password, cancellationToken));
    }

    /// <summary>Deletes the target and its grants.</summary>
    [Route("admin/permissions/{name}"), HttpPost, PageHandler("Delete"), LegacyPage(DetailPage)]
    public async Task<IActionResult> Delete(string name, string? password, CancellationToken cancellationToken)
    {
        var workflow = StartDetail(name, null, null, null, null, null, null);

        return ShowDetail(workflow, await workflow.DeleteAsync(password, cancellationToken));
    }

    /// <summary>A form posted to the target's own URL with no handler: the screen is rendered (the page had no default handler).</summary>
    [Route("admin/permissions/{name}"), HttpPost, PageHandler, ImplicitPageHandler, LegacyPage(DetailPage)]
    public async Task<IActionResult> DetailPost(string name, CancellationToken cancellationToken)
    {
        var workflow = StartDetail(name, null, null, null, null, null, null);

        return ShowDetail(workflow, await workflow.GetAsync(cancellationToken));
    }

    private PermissionsWorkflow StartList(string? name, string? repositoryPatterns, string? includePatterns, string? excludePatterns, string? project, string? testPackage)
    {
        var workflow = new PermissionsWorkflow(HttpContext, Url) { IsServerAdministrator = IsServerAdministrator, CurrentUserId = CurrentUserId };
        var model = workflow.Model;
        model.Name = name;
        model.RepositoryPatterns = repositoryPatterns;
        model.IncludePatterns = includePatterns;
        model.ExcludePatterns = excludePatterns;
        model.Project = project;
        model.TestPackage = testPackage;

        return workflow;
    }

    private PermissionDetailWorkflow StartDetail(string? name, string? repositoryPatterns, string? includePatterns, string? excludePatterns, string? testPackage, PrincipalType? principalType, string? principalName)
    {
        var workflow = new PermissionDetailWorkflow(HttpContext, Url) { IsServerAdministrator = IsServerAdministrator, CurrentUserId = CurrentUserId };
        var model = workflow.Model;
        model.Name = name;
        model.RepositoryPatterns = repositoryPatterns;
        model.IncludePatterns = includePatterns;
        model.ExcludePatterns = excludePatterns;
        model.TestPackage = testPackage;
        model.PrincipalType = principalType ?? PrincipalType.Group;
        model.PrincipalName = principalName;

        return workflow;
    }

    private IActionResult ShowList(PermissionsWorkflow workflow, IActionResult? result)
    {
        Flash(workflow.Model);

        return result ?? View("Index", workflow.Model);
    }

    private IActionResult ShowDetail(PermissionDetailWorkflow workflow, IActionResult? result)
    {
        Flash(workflow.Model);

        return result ?? View("Detail", workflow.Model);
    }
}
