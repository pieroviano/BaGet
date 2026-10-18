using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Identity.Pages.Account;

/// <summary>
/// The signed in user's organizations: create one, and manage the members of those
/// the user administers.
/// </summary>
public class OrganizationsModel : AccountPageModel
{
    protected override bool RequiresSignIn => true;

    public IReadOnlyList<OrganizationView> Organizations { get; private set; } = Array.Empty<OrganizationView>();

    public string? Message { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostCreateAsync(string name, CancellationToken cancellationToken)
    {
        var created = await Ownership.CreateOrganizationAsync(name, UserManager.GetUserId(User), cancellationToken);

        return await DoneAsync(created ? $"Created {name}." : "That name is taken.", cancellationToken);
    }

    public async Task<IActionResult> OnPostAddMemberAsync(string organization, string userName, bool isAdmin, CancellationToken cancellationToken)
    {
        if (!await Ownership.IsOrganizationAdminAsync(organization, UserManager.GetUserId(User), cancellationToken))
        {
            return StatusCode(403);
        }

        var user = string.IsNullOrWhiteSpace(userName) ? null : await UserManager.FindByNameAsync(userName.Trim());
        if (user == null)
        {
            return await DoneAsync("There is no user with that name.", cancellationToken);
        }

        var added = await Ownership.AddMemberAsync(organization, user.Id, isAdmin, cancellationToken);

        return await DoneAsync(added ? $"{user.UserName} joined {organization}." : $"{user.UserName} is already a member.", cancellationToken);
    }

    public async Task<IActionResult> OnPostRemoveMemberAsync(string organization, string? userName, CancellationToken cancellationToken)
    {
        var currentUserId = UserManager.GetUserId(User);
        var user = await UserManager.FindByNameAsync(userName ?? string.Empty);

        // An administrator may remove anyone; any member may leave.
        if (user == null
            || (user.Id != currentUserId && !await Ownership.IsOrganizationAdminAsync(organization, currentUserId, cancellationToken)))
        {
            return StatusCode(403);
        }

        var removed = await Ownership.RemoveMemberAsync(organization, user.Id, cancellationToken);

        return await DoneAsync(
            removed ? $"{user.UserName} left {organization}." : "An organization must keep at least one administrator.",
            cancellationToken);
    }

    private async Task<IActionResult> DoneAsync(string message, CancellationToken cancellationToken)
    {
        Message = message;
        await LoadAsync(cancellationToken);

        return Page();
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        var userId = UserManager.GetUserId(User);
        var views = new List<OrganizationView>();

        #pragma warning disable CS8602
        foreach (var organization in await Ownership.GetOrganizationsAsync(userId, cancellationToken))
        #pragma warning restore CS8602
        {
            var members = await Ownership.GetMembersAsync(organization.Name, cancellationToken);
            var names = new List<(string, bool)>();
            foreach (var member in members)
            {
                #pragma warning disable CS8604
                var user = await UserManager.FindByIdAsync(member.UserId);
                #pragma warning restore CS8604
                #pragma warning disable CS8620
                if (user != null) names.Add((user.UserName, member.IsAdmin));
                #pragma warning restore CS8620
            }

            views.Add(new OrganizationView
            {
                Name = organization.Name,
                IsAdmin = members.Any(m => m.UserId == userId && m.IsAdmin),
                Members = names.OrderBy(n => n.Item1, StringComparer.OrdinalIgnoreCase).ToList(),
            });
        }

        Organizations = views;
    }

    public class OrganizationView
    {
        public string? Name { get; set; }

        public bool IsAdmin { get; set; }

        public IReadOnlyList<(string UserName, bool IsAdmin)>? Members { get; set; }
    }
}