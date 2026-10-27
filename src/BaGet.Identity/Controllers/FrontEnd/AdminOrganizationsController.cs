using System.Threading;
using System.Threading.Tasks;
using BaGet.Identity.Controllers.Base;
using BaGet.Identity.Controllers.UI;
using Microsoft.AspNetCore.Mvc;
using BaGet.Identity.Application.Models.Organizations;
using BaGet.Identity.Application.Models.Shared;
using BaGet.Identity.Mvc.Attributes;
using BaGet.Identity.Mvc.UI;

namespace BaGet.Identity.Controllers.FrontEnd;

/// <summary>
/// The administrator's organization screens: the list (<c>/admin/organizations</c>) and one organization
/// (<c>/admin/organizations/{name}</c>: rename, members, delete). They replace the Razor Pages <c>/Admin/Organizations</c> and
/// <c>/Admin/OrganizationDetail</c> (same URLs, handlers and <c>asp-page</c> names); the work is done by the JSON twin
/// (<see cref="AdminOrganizationsUiApiController"/>) and this controller only renders its answers. Every change asks the administrator's
/// password again and is refused with 503 while the read-only gate is closed.
/// </summary>
public sealed class AdminOrganizationsController : AdminControllerBase
{
    /// <summary>Lists the organizations.</summary>
    [Route("admin/organizations"), HttpGet, HttpHead, PageHandler, LegacyPage("/Admin/Organizations")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
        => Render(await Api<AdminOrganizationsUiApiController, AdminOrganizationsViewModel>(api => api.List(cancellationToken)), "Index");

    /// <summary>Shows one organization.</summary>
    [Route("admin/organizations/{name}"), HttpGet, HttpHead, PageHandler, LegacyPage("/Admin/OrganizationDetail")]
    public async Task<IActionResult> Detail(string name, CancellationToken cancellationToken)
        => Show(await Api<AdminOrganizationsUiApiController, OrganizationDetailViewModel>(api => api.Get(name, cancellationToken)));

    /// <summary>Renames the organization.</summary>
    [Route("admin/organizations/{name}"), HttpPost, PageHandler("Rename"), LegacyPage("/Admin/OrganizationDetail")]
    public async Task<IActionResult> Rename(string name, string? newName, string? password, CancellationToken cancellationToken)
        => Show(await Api<AdminOrganizationsUiApiController, OrganizationDetailViewModel>(api => api.Rename(name, new OrganizationRenameForm(newName, password), cancellationToken)));

    /// <summary>Deletes the organization.</summary>
    [Route("admin/organizations/{name}"), HttpPost, PageHandler("Delete"), LegacyPage("/Admin/OrganizationDetail")]
    public async Task<IActionResult> Delete(string name, string? password, CancellationToken cancellationToken)
        => Show(await Api<AdminOrganizationsUiApiController, OrganizationDetailViewModel>(api => api.Delete(name, new PasswordForm(password), cancellationToken)));

    /// <summary>Adds a user to the organization.</summary>
    [Route("admin/organizations/{name}"), HttpPost, PageHandler("AddMember"), LegacyPage("/Admin/OrganizationDetail")]
    public async Task<IActionResult> AddMember(string name, string? userName, bool isAdmin, string? password, CancellationToken cancellationToken)
        => Show(await Api<AdminOrganizationsUiApiController, OrganizationDetailViewModel>(api => api.AddMember(name, new OrganizationMemberForm(userName, isAdmin, password), cancellationToken)));

    /// <summary>Makes a member an administrator of the organization, or not.</summary>
    [Route("admin/organizations/{name}"), HttpPost, PageHandler("SetAdmin"), LegacyPage("/Admin/OrganizationDetail")]
    public async Task<IActionResult> SetAdmin(string name, string? userName, bool isAdmin, string? password, CancellationToken cancellationToken)
        => Show(await Api<AdminOrganizationsUiApiController, OrganizationDetailViewModel>(api => api.SetAdmin(name, new OrganizationMemberForm(userName, isAdmin, password), cancellationToken)));

    /// <summary>Removes a member from the organization.</summary>
    [Route("admin/organizations/{name}"), HttpPost, PageHandler("RemoveMember"), LegacyPage("/Admin/OrganizationDetail")]
    public async Task<IActionResult> RemoveMember(string name, string? userName, string? password, CancellationToken cancellationToken)
        => Show(await Api<AdminOrganizationsUiApiController, OrganizationDetailViewModel>(api => api.RemoveMember(name, new OrganizationMemberForm(userName, false, password), cancellationToken)));

    /// <summary>A form posted to the screen's own URL, with no handler: the screen is rendered (the page had no default handler).</summary>
    [Route("admin/organizations/{name}"), HttpPost, PageHandler, ImplicitPageHandler, LegacyPage("/Admin/OrganizationDetail")]
    public async Task<IActionResult> DetailPost(string name, CancellationToken cancellationToken)
        => Show(await Api<AdminOrganizationsUiApiController, OrganizationDetailViewModel>(api => api.Get(name, cancellationToken)));

    private IActionResult Show(UiApiResponse<OrganizationDetailViewModel> response) => RenderFlashed(response, "Detail");
}
