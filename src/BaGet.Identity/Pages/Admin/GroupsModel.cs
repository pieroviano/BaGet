using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.Workflows;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Identity.Pages.Admin;

/// <summary>
/// The page model of the groups page when it was a Razor Page: lists the groups (the built-in <c>Everyone</c> and
/// <c>Authenticated</c> first) and creates one. A group is a named set of users that permission targets and project roles can name.
/// Administrators only.
/// </summary>
/// <remarks>
/// Kept as a shim (no breaking change of the public API); <c>AdminGroupsController</c> serves <c>/admin/groups</c>. Both call
/// <c>GroupsWorkflow</c>.
/// </remarks>
[Obsolete("The groups screen is served by AdminGroupsController; this page model has no page any more and will be removed in the next major version.")]
public class GroupsModel : AccessPageModel
{
    private GroupsWorkflow? _workflow;

    private GroupsWorkflow Workflow
    {
        get
        {
            var workflow = _workflow ??= new GroupsWorkflow(HttpContext, Url);
            workflow.IsServerAdministrator = IsServerAdministrator;
            workflow.CurrentUserId = CurrentUserId;

            return workflow;
        }
    }

    /// <summary>The new group name.</summary>
    [BindProperty]
    public string? Name { get => Workflow.Model.Name; set => Workflow.Model.Name = value; }

    /// <summary>The new group description.</summary>
    [BindProperty]
    public string? Description { get => Workflow.Model.Description; set => Workflow.Model.Description = value; }

    /// <summary>The groups.</summary>
    public IReadOnlyList<GroupSummary> Groups => Workflow.Model.Groups;

    /// <summary>Shows the groups.</summary>
    public async Task OnGetAsync(CancellationToken cancellationToken) => await Workflow.ListAsync(cancellationToken);

    /// <summary>Creates a group and opens it.</summary>
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var result = await Workflow.CreateAsync(cancellationToken);
        AccessWorkflow.CopyOutcome(this, Workflow.Model);

        return result ?? Page();
    }
}
