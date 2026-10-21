using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Identity.Pages.Admin;

/// <summary>
/// Lists the groups (the built-in <c>Everyone</c> and <c>Authenticated</c> first) and creates one. A group is a named
/// set of users that permission targets and project roles can name. Administrators only.
/// </summary>
public class GroupsModel : AccessPageModel
{
    /// <summary>The new group's name.</summary>
    [BindProperty]
    public string? Name { get; set; }

    /// <summary>The new group's description.</summary>
    [BindProperty]
    public string? Description { get; set; }

    /// <summary>The groups.</summary>
    public IReadOnlyList<GroupSummary> Groups { get; private set; } = Array.Empty<GroupSummary>();

    /// <summary>Shows the groups.</summary>
    public async Task OnGetAsync(CancellationToken cancellationToken)
        => Groups = await AccessAdmin.ListGroupsAsync(cancellationToken);

    /// <summary>Creates a group and opens it.</summary>
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        try
        {
            var group = await AccessAdmin.CreateGroupAsync(Name ?? string.Empty, Description, Actor, cancellationToken);
            Message = $"Group {group.Name} was created.";

            return RedirectToPage("/Admin/GroupDetail", new { name = group.Name });
        }
        catch (AccessModelException e)
        {
            Error = e.Message;
            Groups = await AccessAdmin.ListGroupsAsync(cancellationToken);

            return Page();
        }
    }
}
