using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Identity.Workflows;

/// <summary>
/// The groups list: the built-in <c>Everyone</c> and <c>Authenticated</c> first, and the new-group form. A group is a named set of
/// users that permission targets and project roles can name. Shared by <c>AdminGroupsController</c> and the obsolete
/// <c>GroupsModel</c>.
/// </summary>
internal sealed class GroupsWorkflow : AccessWorkflow
{
    public GroupsWorkflow(HttpContext httpContext, IUrlHelper url) : base(httpContext, url)
    {
    }

    /// <summary>The screen; the caller sets <c>Name</c> and <c>Description</c> from the form first.</summary>
    public GroupsViewModel Model { get; } = new();

    /// <summary>Lists the groups.</summary>
    public async Task ListAsync(CancellationToken cancellationToken)
    {
        Model.IsServerAdministrator = IsServerAdministrator;
        Model.Groups = await AccessAdmin.ListGroupsAsync(cancellationToken);
    }

    /// <summary>Creates a group and opens it; a refusal renders the list with the reason.</summary>
    public async Task<IActionResult?> CreateAsync(CancellationToken cancellationToken)
    {
        try
        {
            var group = await AccessAdmin.CreateGroupAsync(Model.Name ?? string.Empty, Model.Description, Actor, cancellationToken);
            Model.Message = $"Group {group.Name} was created.";

            return RedirectToScreen("/Admin/GroupDetail", new { name = group.Name });
        }
        catch (AccessModelException e)
        {
            Model.Error = e.Message;
            await ListAsync(cancellationToken);

            return null;
        }
    }
}

/// <summary>
/// One group: rename it, add and remove members (found by a user-name search), see the grants that name it, delete it. The built-in
/// groups are shown but have implicit members and cannot be renamed or deleted. Membership changes and deletion ask for the password
/// again. Shared by <c>AdminGroupsController</c> and the obsolete <c>GroupDetailModel</c>.
/// </summary>
internal sealed class GroupDetailWorkflow : AccessWorkflow
{
    /// <summary>How many users a search lists.</summary>
    public const int SearchSize = 20;

    public GroupDetailWorkflow(HttpContext httpContext, IUrlHelper url) : base(httpContext, url)
    {
    }

    /// <summary>The screen; the caller sets <c>Name</c> (route) and <c>Q</c> (query) first.</summary>
    public GroupDetailViewModel Model { get; } = new();

    /// <summary>Shows the group; 404 when there is none.</summary>
    public async Task<IActionResult?> GetAsync(CancellationToken cancellationToken)
        => await LoadAsync(cancellationToken) ? null : new NotFoundResult();

    /// <summary>Renames the group.</summary>
    public Task<IActionResult> RenameAsync(string? newName, CancellationToken cancellationToken)
    {
        var name = Model.Name;

        return ActAsync(Model, null, confirm: false, async () =>
        {
            await AccessAdmin.RenameGroupAsync(name ?? string.Empty, newName ?? string.Empty, Actor, cancellationToken);
            name = newName;
            return $"Group {Model.Name} is now called {newName}.";
        }, () => RedirectToScreen("/Admin/GroupDetail", new { name }));
    }

    /// <summary>Adds a user to the group.</summary>
    public Task<IActionResult> AddMemberAsync(string? userId, string? password, CancellationToken cancellationToken)
        => ActAsync(Model, password, confirm: true, async () =>
        {
            await AccessAdmin.AddGroupMemberAsync(Model.Name ?? string.Empty, userId ?? string.Empty, Actor, cancellationToken);
            return "The member was added.";
        }, Back);

    /// <summary>Removes a user from the group.</summary>
    public Task<IActionResult> RemoveMemberAsync(string? userId, string? password, CancellationToken cancellationToken)
        => ActAsync(Model, password, confirm: true, async () =>
        {
            await AccessAdmin.RemoveGroupMemberAsync(Model.Name ?? string.Empty, userId ?? string.Empty, Actor, cancellationToken);
            return "The member was removed.";
        }, Back);

    /// <summary>Deletes the group with the grants and project roles that name it.</summary>
    public async Task<IActionResult> DeleteAsync(string? password, CancellationToken cancellationToken)
    {
        var deleted = false;
        var result = await ActAsync(Model, password, confirm: true, async () =>
        {
            await AccessAdmin.DeleteGroupAsync(Model.Name ?? string.Empty, Actor, cancellationToken);
            deleted = true;
            return $"Group {Model.Name} was deleted.";
        }, Back);

        return deleted ? RedirectToScreen("/Admin/Groups") : result;
    }

    private IActionResult Back() => RedirectToScreen("/Admin/GroupDetail", new { name = Model.Name });

    private async Task<bool> LoadAsync(CancellationToken cancellationToken)
    {
        Model.IsServerAdministrator = IsServerAdministrator;
        await LoadConfirmationAsync(Model);

        Model.Group = await AccessAdmin.GetGroupAsync(Model.Name ?? string.Empty, cancellationToken);
        if (Model.Group == null) return false;

        if (!Model.Group.Group.IsBuiltIn && !string.IsNullOrWhiteSpace(Model.Q))
        {
            var members = new HashSet<string>(StringComparer.Ordinal);
            foreach (var member in Model.Group.Members) members.Add(member.UserId);

            var found = await AccessAdmin.SearchUsersAsync(Model.Q, SearchSize, cancellationToken);
            var fresh = new List<GroupMemberInfo>();
            foreach (var user in found)
            {
                if (!members.Contains(user.UserId)) fresh.Add(user);
            }

            Model.Found = fresh;
        }

        return true;
    }
}
