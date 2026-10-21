using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Identity.Pages.Admin;

/// <summary>
/// One group: rename it, add and remove members (found by a user-name search), see the grants that name it, delete it.
/// The built-in groups (<c>Everyone</c>, <c>Authenticated</c>) are shown but have implicit members and cannot be
/// renamed or deleted. Membership changes and deletion ask for the password again.
/// </summary>
public class GroupDetailModel : AccessPageModel
{
    /// <summary>How many users a search lists.</summary>
    public const int SearchSize = 20;

    /// <summary>The group's name (route).</summary>
    [BindProperty(SupportsGet = true)]
    public string? Name { get; set; }

    /// <summary>User-name search for the member picker (contains).</summary>
    [BindProperty(SupportsGet = true)]
    public string? Q { get; set; }

    /// <summary>The group.</summary>
    public GroupDetails? Group { get; private set; }

    /// <summary>The users found by <see cref="Q"/>, not yet members.</summary>
    public IReadOnlyList<GroupMemberInfo> Found { get; private set; } = Array.Empty<GroupMemberInfo>();

    /// <summary>Shows the group.</summary>
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!await LoadAsync(cancellationToken)) return NotFound();

        return Page();
    }

    /// <summary>Renames the group.</summary>
    public async Task<IActionResult> OnPostRenameAsync(string? newName, CancellationToken cancellationToken)
    {
        var name = Name;
        var result = await ActAsync(null, confirm: false, async () =>
        {
            await AccessAdmin.RenameGroupAsync(name ?? string.Empty, newName ?? string.Empty, Actor, cancellationToken);
            name = newName;
            return $"Group {Name} is now called {newName}.";
        }, () => RedirectToPage("/Admin/GroupDetail", new { name }));

        return result;
    }

    /// <summary>Adds a user to the group.</summary>
    public Task<IActionResult> OnPostAddMemberAsync(string? userId, string? password, CancellationToken cancellationToken)
        => ActAsync(password, confirm: true, async () =>
        {
            await AccessAdmin.AddGroupMemberAsync(Name ?? string.Empty, userId ?? string.Empty, Actor, cancellationToken);
            return "The member was added.";
        }, () => RedirectToPage("/Admin/GroupDetail", new { name = Name }));

    /// <summary>Removes a user from the group.</summary>
    public Task<IActionResult> OnPostRemoveMemberAsync(string? userId, string? password, CancellationToken cancellationToken)
        => ActAsync(password, confirm: true, async () =>
        {
            await AccessAdmin.RemoveGroupMemberAsync(Name ?? string.Empty, userId ?? string.Empty, Actor, cancellationToken);
            return "The member was removed.";
        }, () => RedirectToPage("/Admin/GroupDetail", new { name = Name }));

    /// <summary>Deletes the group with the grants and project roles that name it.</summary>
    public async Task<IActionResult> OnPostDeleteAsync(string? password, CancellationToken cancellationToken)
    {
        var deleted = false;
        var result = await ActAsync(password, confirm: true, async () =>
        {
            await AccessAdmin.DeleteGroupAsync(Name ?? string.Empty, Actor, cancellationToken);
            deleted = true;
            return $"Group {Name} was deleted.";
        }, () => RedirectToPage("/Admin/GroupDetail", new { name = Name }));

        return deleted ? RedirectToPage("/Admin/Groups") : result;
    }

    private async Task<bool> LoadAsync(CancellationToken cancellationToken)
    {
        Group = await AccessAdmin.GetGroupAsync(Name ?? string.Empty, cancellationToken);
        if (Group == null) return false;

        if (!Group.Group.IsBuiltIn && !string.IsNullOrWhiteSpace(Q))
        {
            var members = new HashSet<string>(StringComparer.Ordinal);
            foreach (var member in Group.Members) members.Add(member.UserId);

            var found = await AccessAdmin.SearchUsersAsync(Q, SearchSize, cancellationToken);
            var fresh = new List<GroupMemberInfo>();
            foreach (var user in found)
            {
                if (!members.Contains(user.UserId)) fresh.Add(user);
            }

            Found = fresh;
        }

        return true;
    }
}
