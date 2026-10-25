using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.Workflows;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Identity.Pages.Admin;

/// <summary>
/// The page model of the group page when it was a Razor Page: rename it, add and remove members (found by a user-name search), see
/// the grants that name it, delete it. The built-in groups have implicit members and cannot be renamed or deleted. Membership
/// changes and deletion ask for the password again.
/// </summary>
/// <remarks>
/// Kept as a shim (no breaking change of the public API); <c>AdminGroupsController</c> serves <c>/admin/groups/{name}</c>. Both
/// call <c>GroupDetailWorkflow</c>.
/// </remarks>
[Obsolete("The group screen is served by AdminGroupsController; this page model has no page any more and will be removed in the next major version.")]
public class GroupDetailModel : AccessPageModel
{
    /// <summary>How many users a search lists.</summary>
    public const int SearchSize = GroupDetailWorkflow.SearchSize;

    private GroupDetailWorkflow? _workflow;

    private GroupDetailWorkflow Workflow
    {
        get
        {
            var workflow = _workflow ??= new GroupDetailWorkflow(HttpContext, Url);
            workflow.IsServerAdministrator = IsServerAdministrator;
            workflow.CurrentUserId = CurrentUserId;

            return workflow;
        }
    }

    /// <summary>The group name (route).</summary>
    [BindProperty(SupportsGet = true)]
    public string? Name { get => Workflow.Model.Name; set => Workflow.Model.Name = value; }

    /// <summary>User-name search for the member picker (contains).</summary>
    [BindProperty(SupportsGet = true)]
    public string? Q { get => Workflow.Model.Q; set => Workflow.Model.Q = value; }

    /// <summary>The group.</summary>
    public GroupDetails? Group => Workflow.Model.Group;

    /// <summary>The users found by <see cref="Q"/>, not yet members.</summary>
    public IReadOnlyList<GroupMemberInfo> Found => Workflow.Model.Found;

    /// <summary>Shows the group.</summary>
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
        => await Workflow.GetAsync(cancellationToken) ?? Page();

    /// <summary>Renames the group.</summary>
    public async Task<IActionResult> OnPostRenameAsync(string? newName, CancellationToken cancellationToken)
        => Outcome(await Workflow.RenameAsync(newName, cancellationToken));

    /// <summary>Adds a user to the group.</summary>
    public async Task<IActionResult> OnPostAddMemberAsync(string? userId, string? password, CancellationToken cancellationToken)
        => Outcome(await Workflow.AddMemberAsync(userId, password, cancellationToken));

    /// <summary>Removes a user from the group.</summary>
    public async Task<IActionResult> OnPostRemoveMemberAsync(string? userId, string? password, CancellationToken cancellationToken)
        => Outcome(await Workflow.RemoveMemberAsync(userId, password, cancellationToken));

    /// <summary>Deletes the group with the grants and project roles that name it.</summary>
    public async Task<IActionResult> OnPostDeleteAsync(string? password, CancellationToken cancellationToken)
        => Outcome(await Workflow.DeleteAsync(password, cancellationToken));

    private IActionResult Outcome(IActionResult result)
    {
        AccessWorkflow.CopyOutcome(this, Workflow.Model);

        return result;
    }
}
