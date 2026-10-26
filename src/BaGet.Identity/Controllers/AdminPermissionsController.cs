using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.Application;
using BaGet.Identity.Mvc;
using BaGet.Identity.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Identity.Controllers;

/// <summary>
/// The permission target screens of the administrator: the list with the new-target form (<c>/admin/permissions</c>, server
/// administrators only) and one target (<c>/admin/permissions/{name}</c>: patterns, preview, grants, delete), which also admits the
/// administrators of the target's project for a project target. They replace the Razor Pages <c>/Admin/Permissions</c> and
/// <c>/Admin/PermissionDetail</c> (same URLs, handlers and <c>asp-page</c> names); the work is done by the JSON twin
/// (<see cref="AdminPermissionsUiApiController"/>) and this controller only renders its answers. Saving patterns, changing grants and deleting
/// ask for the password again, and every POST is refused with 503 while the read-only gate is closed (<see cref="AccessControllerBase"/>).
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
        => ShowList(await Api<AdminPermissionsUiApiController, PermissionsViewModel>(api => api.List(cancellationToken)));

    /// <summary>Creates a target and opens it.</summary>
    [Route("admin/permissions"), HttpPost, PageHandler, LegacyPage("/Admin/Permissions")]
    public async Task<IActionResult> Create(string? name, string? repositoryPatterns, string? includePatterns, string? excludePatterns, string? project, string? testPackage, CancellationToken cancellationToken)
        => ShowList(await Api<AdminPermissionsUiApiController, PermissionsViewModel>(
            api => api.Create(new PermissionTargetForm(name, repositoryPatterns, includePatterns, excludePatterns, project, testPackage), cancellationToken)));

    /// <summary>Previews the form patterns without saving anything.</summary>
    [Route("admin/permissions"), HttpPost, PageHandler("Preview"), LegacyPage("/Admin/Permissions")]
    public async Task<IActionResult> Preview(string? name, string? repositoryPatterns, string? includePatterns, string? excludePatterns, string? project, string? testPackage, CancellationToken cancellationToken)
        => ShowList(await Api<AdminPermissionsUiApiController, PermissionsViewModel>(
            api => api.Preview(new PermissionTargetForm(name, repositoryPatterns, includePatterns, excludePatterns, project, testPackage), cancellationToken)));

    /// <summary>Re-creates the built-in groups and targets that are missing (the upgrade defaults).</summary>
    [Route("admin/permissions"), HttpPost, PageHandler("RestoreBuiltIns"), LegacyPage("/Admin/Permissions")]
    public async Task<IActionResult> RestoreBuiltIns(string? password, CancellationToken cancellationToken)
        => ShowList(await Api<AdminPermissionsUiApiController, PermissionsViewModel>(api => api.RestoreBuiltIns(new PasswordForm(password), cancellationToken)));

    /// <summary>Shows one target. <paramref name="principalType"/>/<paramref name="principalName"/> pre-fill the grant form.</summary>
    [Route("admin/permissions/{name}"), HttpGet, HttpHead, PageHandler, LegacyPage(DetailPage)]
    public async Task<IActionResult> Detail(string name, string? testPackage, PrincipalType? principalType, string? principalName, CancellationToken cancellationToken)
        => ShowDetail(await Api<AdminPermissionsUiApiController, PermissionDetailViewModel>(api => api.Get(name, testPackage, principalType, principalName, cancellationToken)));

    /// <summary>Previews the form patterns (and the test package) without saving anything.</summary>
    [Route("admin/permissions/{name}"), HttpPost, PageHandler("Preview"), LegacyPage(DetailPage)]
    public async Task<IActionResult> DetailPreview(string name, string? repositoryPatterns, string? includePatterns, string? excludePatterns, string? testPackage, PrincipalType? principalType, string? principalName, CancellationToken cancellationToken)
        => ShowDetail(await Api<AdminPermissionsUiApiController, PermissionDetailViewModel>(
            api => api.PreviewDetail(name, new PermissionTargetForm(null, repositoryPatterns, includePatterns, excludePatterns, null, testPackage, principalType, principalName), cancellationToken)));

    /// <summary>Replaces the target patterns.</summary>
    [Route("admin/permissions/{name}"), HttpPost, PageHandler("Save"), LegacyPage(DetailPage)]
    public async Task<IActionResult> Save(string name, string? repositoryPatterns, string? includePatterns, string? excludePatterns, string? password, CancellationToken cancellationToken)
        => ShowDetail(await Api<AdminPermissionsUiApiController, PermissionDetailViewModel>(
            api => api.Save(name, new PermissionTargetForm(null, repositoryPatterns, includePatterns, excludePatterns, Password: password), cancellationToken)));

    /// <summary>Gives a principal the ticked actions through the target, replacing an earlier grant to it.</summary>
    [Route("admin/permissions/{name}"), HttpPost, PageHandler("SetGrant"), LegacyPage(DetailPage)]
    public async Task<IActionResult> SetGrant(string name, PrincipalType? principalType, string? principalName, string[]? actions, string? password, CancellationToken cancellationToken)
        => ShowDetail(await Api<AdminPermissionsUiApiController, PermissionDetailViewModel>(
            api => api.SetGrant(name, new GrantForm(principalType, principalName, actions, password), cancellationToken)));

    /// <summary>Removes a principal grant.</summary>
    [Route("admin/permissions/{name}"), HttpPost, PageHandler("RemoveGrant"), LegacyPage(DetailPage)]
    public async Task<IActionResult> RemoveGrant(string name, PrincipalType type, string? principalId, string? password, CancellationToken cancellationToken)
        => ShowDetail(await Api<AdminPermissionsUiApiController, PermissionDetailViewModel>(
            api => api.RemoveGrant(name, new PrincipalRemoval(type, principalId, password), cancellationToken)));

    /// <summary>Deletes the target and its grants.</summary>
    [Route("admin/permissions/{name}"), HttpPost, PageHandler("Delete"), LegacyPage(DetailPage)]
    public async Task<IActionResult> Delete(string name, string? password, CancellationToken cancellationToken)
        => ShowDetail(await Api<AdminPermissionsUiApiController, PermissionDetailViewModel>(api => api.Delete(name, new PasswordForm(password), cancellationToken)));

    /// <summary>A form posted to the target's own URL with no handler: the screen is rendered (the page had no default handler).</summary>
    [Route("admin/permissions/{name}"), HttpPost, PageHandler, ImplicitPageHandler, LegacyPage(DetailPage)]
    public async Task<IActionResult> DetailPost(string name, CancellationToken cancellationToken)
        => ShowDetail(await Api<AdminPermissionsUiApiController, PermissionDetailViewModel>(api => api.Get(name, null, null, null, cancellationToken)));

    private IActionResult ShowList(UiApiResponse<PermissionsViewModel> response) => RenderFlashed(response, "Index");

    private IActionResult ShowDetail(UiApiResponse<PermissionDetailViewModel> response) => RenderFlashed(response, "Detail");
}
