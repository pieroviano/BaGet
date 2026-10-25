using System.Threading;
using System.Threading.Tasks;
using BaGet.Identity.Mvc;
using BaGet.Identity.Workflows;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Identity.Controllers;

/// <summary>
/// The groups screens of the administrator: the list with the new-group form (<c>/admin/groups</c>) and one group
/// (<c>/admin/groups/{name}</c>: rename, members, delete). They replace the Razor Pages <c>/Admin/Groups</c> and
/// <c>/Admin/GroupDetail</c> (same URLs, handlers and <c>asp-page</c> names); the logic is in <see cref="GroupsWorkflow"/> and
/// <see cref="GroupDetailWorkflow"/>. Server administrators only; membership changes and deletion ask for the password again, and
/// every POST is refused with 503 while the read-only gate is closed (<see cref="AccessControllerBase"/>).
/// </summary>
public sealed class AdminGroupsController : AccessControllerBase
{
    /// <summary>Lists the groups.</summary>
    [Route("admin/groups"), HttpGet, HttpHead, PageHandler, LegacyPage("/Admin/Groups")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var workflow = StartList(null, null);
        await workflow.ListAsync(cancellationToken);

        return ShowList(workflow, null);
    }

    /// <summary>Creates a group and opens it.</summary>
    [Route("admin/groups"), HttpPost, PageHandler, LegacyPage("/Admin/Groups")]
    public async Task<IActionResult> Create(string? name, string? description, CancellationToken cancellationToken)
    {
        var workflow = StartList(name, description);

        return ShowList(workflow, await workflow.CreateAsync(cancellationToken));
    }

    /// <summary>Shows one group, with the member search for <paramref name="q"/>.</summary>
    [Route("admin/groups/{name}"), HttpGet, HttpHead, PageHandler, LegacyPage("/Admin/GroupDetail")]
    public async Task<IActionResult> Detail(string name, string? q, CancellationToken cancellationToken)
    {
        var workflow = StartDetail(name, q);

        return ShowDetail(workflow, await workflow.GetAsync(cancellationToken));
    }

    /// <summary>Renames the group.</summary>
    [Route("admin/groups/{name}"), HttpPost, PageHandler("Rename"), LegacyPage("/Admin/GroupDetail")]
    public async Task<IActionResult> Rename(string name, string? newName, CancellationToken cancellationToken)
    {
        var workflow = StartDetail(name, null);

        return ShowDetail(workflow, await workflow.RenameAsync(newName, cancellationToken));
    }

    /// <summary>Adds a user to the group.</summary>
    [Route("admin/groups/{name}"), HttpPost, PageHandler("AddMember"), LegacyPage("/Admin/GroupDetail")]
    public async Task<IActionResult> AddMember(string name, string? userId, string? password, CancellationToken cancellationToken)
    {
        var workflow = StartDetail(name, null);

        return ShowDetail(workflow, await workflow.AddMemberAsync(userId, password, cancellationToken));
    }

    /// <summary>Removes a user from the group.</summary>
    [Route("admin/groups/{name}"), HttpPost, PageHandler("RemoveMember"), LegacyPage("/Admin/GroupDetail")]
    public async Task<IActionResult> RemoveMember(string name, string? userId, string? password, CancellationToken cancellationToken)
    {
        var workflow = StartDetail(name, null);

        return ShowDetail(workflow, await workflow.RemoveMemberAsync(userId, password, cancellationToken));
    }

    /// <summary>Deletes the group with the grants and project roles that name it.</summary>
    [Route("admin/groups/{name}"), HttpPost, PageHandler("Delete"), LegacyPage("/Admin/GroupDetail")]
    public async Task<IActionResult> Delete(string name, string? password, CancellationToken cancellationToken)
    {
        var workflow = StartDetail(name, null);

        return ShowDetail(workflow, await workflow.DeleteAsync(password, cancellationToken));
    }

    /// <summary>A form posted to the group's own URL with no handler: the screen is rendered (the page had no default handler).</summary>
    [Route("admin/groups/{name}"), HttpPost, PageHandler, ImplicitPageHandler, LegacyPage("/Admin/GroupDetail")]
    public async Task<IActionResult> DetailPost(string name, CancellationToken cancellationToken)
    {
        var workflow = StartDetail(name, null);

        return ShowDetail(workflow, await workflow.GetAsync(cancellationToken));
    }

    private GroupsWorkflow StartList(string? name, string? description)
    {
        var workflow = new GroupsWorkflow(HttpContext, Url) { IsServerAdministrator = IsServerAdministrator, CurrentUserId = CurrentUserId };
        workflow.Model.Name = name;
        workflow.Model.Description = description;

        return workflow;
    }

    private GroupDetailWorkflow StartDetail(string? name, string? q)
    {
        var workflow = new GroupDetailWorkflow(HttpContext, Url) { IsServerAdministrator = IsServerAdministrator, CurrentUserId = CurrentUserId };
        workflow.Model.Name = name;
        workflow.Model.Q = q;

        return workflow;
    }

    private IActionResult ShowList(GroupsWorkflow workflow, IActionResult? result)
    {
        Flash(workflow.Model);

        return result ?? View("Index", workflow.Model);
    }

    private IActionResult ShowDetail(GroupDetailWorkflow workflow, IActionResult? result)
    {
        Flash(workflow.Model);

        return result ?? View("Detail", workflow.Model);
    }
}
