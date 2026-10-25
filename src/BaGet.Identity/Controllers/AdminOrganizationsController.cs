using System.Threading;
using System.Threading.Tasks;
using BaGet.Identity.Mvc;
using BaGet.Identity.Workflows;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Identity.Controllers;

/// <summary>
/// The administrator's organization screens: the list (<c>/admin/organizations</c>) and one organization
/// (<c>/admin/organizations/{name}</c>: rename, members, delete). They replace the Razor Pages <c>/Admin/Organizations</c> and
/// <c>/Admin/OrganizationDetail</c> (same URLs, handlers and <c>asp-page</c> names); the logic is in
/// <see cref="OrganizationsWorkflow"/>. Every change asks the administrator's password again and is refused with 503 while the
/// read-only gate is closed.
/// </summary>
public sealed class AdminOrganizationsController : AdminControllerBase
{
    /// <summary>Lists the organizations.</summary>
    [Route("admin/organizations"), HttpGet, HttpHead, PageHandler, LegacyPage("/Admin/Organizations")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var workflow = new OrganizationsWorkflow(HttpContext, Url);
        await workflow.ListAsync(cancellationToken);

        return View("Index", workflow.List);
    }

    /// <summary>Shows one organization.</summary>
    [Route("admin/organizations/{name}"), HttpGet, HttpHead, PageHandler, LegacyPage("/Admin/OrganizationDetail")]
    public async Task<IActionResult> Detail(string name, CancellationToken cancellationToken)
    {
        var workflow = Start(name);

        return Show(workflow, await workflow.GetAsync(cancellationToken));
    }

    /// <summary>Renames the organization.</summary>
    [Route("admin/organizations/{name}"), HttpPost, PageHandler("Rename"), LegacyPage("/Admin/OrganizationDetail")]
    public async Task<IActionResult> Rename(string name, string? newName, string? password, CancellationToken cancellationToken)
    {
        var workflow = Start(name);

        return Show(workflow, await workflow.RenameAsync(newName, password, cancellationToken));
    }

    /// <summary>Deletes the organization.</summary>
    [Route("admin/organizations/{name}"), HttpPost, PageHandler("Delete"), LegacyPage("/Admin/OrganizationDetail")]
    public async Task<IActionResult> Delete(string name, string? password, CancellationToken cancellationToken)
    {
        var workflow = Start(name);

        return Show(workflow, await workflow.DeleteAsync(password, cancellationToken));
    }

    /// <summary>Adds a user to the organization.</summary>
    [Route("admin/organizations/{name}"), HttpPost, PageHandler("AddMember"), LegacyPage("/Admin/OrganizationDetail")]
    public async Task<IActionResult> AddMember(string name, string? userName, bool isAdmin, string? password, CancellationToken cancellationToken)
    {
        var workflow = Start(name);

        return Show(workflow, await workflow.AddMemberAsync(userName, isAdmin, password, cancellationToken));
    }

    /// <summary>Makes a member an administrator of the organization, or not.</summary>
    [Route("admin/organizations/{name}"), HttpPost, PageHandler("SetAdmin"), LegacyPage("/Admin/OrganizationDetail")]
    public async Task<IActionResult> SetAdmin(string name, string? userName, bool isAdmin, string? password, CancellationToken cancellationToken)
    {
        var workflow = Start(name);

        return Show(workflow, await workflow.SetAdminAsync(userName, isAdmin, password, cancellationToken));
    }

    /// <summary>Removes a member from the organization.</summary>
    [Route("admin/organizations/{name}"), HttpPost, PageHandler("RemoveMember"), LegacyPage("/Admin/OrganizationDetail")]
    public async Task<IActionResult> RemoveMember(string name, string? userName, string? password, CancellationToken cancellationToken)
    {
        var workflow = Start(name);

        return Show(workflow, await workflow.RemoveMemberAsync(userName, password, cancellationToken));
    }

    /// <summary>A form posted to the screen's own URL, with no handler: the screen is rendered (the page had no default handler).</summary>
    [Route("admin/organizations/{name}"), HttpPost, PageHandler, ImplicitPageHandler, LegacyPage("/Admin/OrganizationDetail")]
    public async Task<IActionResult> DetailPost(string name, CancellationToken cancellationToken)
    {
        var workflow = Start(name);

        return Show(workflow, await workflow.GetAsync(cancellationToken));
    }

    private OrganizationsWorkflow Start(string? name)
    {
        var workflow = new OrganizationsWorkflow(HttpContext, Url);
        workflow.Model.Name = name;

        return workflow;
    }

    private IActionResult Show(OrganizationsWorkflow workflow, IActionResult? result)
    {
        Flash(workflow.Model);

        return result ?? View("Detail", workflow.Model);
    }
}
