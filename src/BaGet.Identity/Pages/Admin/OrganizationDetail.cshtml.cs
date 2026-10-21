using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BaGet.Identity.Pages.Admin;

/// <summary>What the password confirmation partial needs.</summary>
/// <param name="Id">The input's id.</param>
/// <param name="HasPassword">Whether the administrator has a password to type.</param>
/// <param name="Reauthenticated">For an account without a password: it signed in again a moment ago.</param>
/// <param name="CanReauthenticate">Whether a provider is available to sign in again with.</param>
/// <param name="ReturnPage">The page to come back to after signing in again.</param>
public record ConfirmPasswordView(string Id, bool HasPassword, bool Reauthenticated, bool CanReauthenticate, string ReturnPage);

/// <summary>A member as the organization page lists them.</summary>
/// <param name="UserName">The user name.</param>
/// <param name="IsAdmin">Whether the member administers the organization.</param>
public record OrganizationMemberRow(string? UserName, bool IsAdmin);

/// <summary>
/// One organization: rename it (package ownership follows in every repository, atomically), add, promote and remove
/// members (an organization keeps at least one administrator) and delete it (refused while it owns packages). Every
/// action asks for the administrator's password again and is refused with 503 while the
/// <see cref="MaintenanceGate"/> is closed.
/// </summary>
public class OrganizationDetailModel : AdminPageModel
{
    /// <summary>The organization's name (route).</summary>
    [BindProperty(SupportsGet = true)]
    public string? Name { get; set; }

    /// <summary>The organization.</summary>
    public Organization? Organization { get; private set; }

    /// <summary>Its members, by name.</summary>
    public IReadOnlyList<OrganizationMemberRow> Members { get; private set; } = Array.Empty<OrganizationMemberRow>();

    /// <summary>How many package IDs it owns, in every repository.</summary>
    public int OwnedPackageIds { get; private set; }

    /// <summary>Whether the signed in administrator has a password to confirm with.</summary>
    public bool HasPassword { get; private set; } = true;

    /// <summary>For an account without a password: it signed in again with its provider a moment ago.</summary>
    public bool Reauthenticated { get; private set; }

    /// <summary>Whether an external provider is available to sign in again with.</summary>
    public bool CanReauthenticate => Accounts.OpenIdConnect.IsConfigured;

    /// <summary>The model of the password confirmation partial, with <paramref name="id"/> for the input.</summary>
    public ConfirmPasswordView Confirmation(string id)
        => new ConfirmPasswordView(id, HasPassword, Reauthenticated, CanReauthenticate, "/Admin/OrganizationDetail");

    /// <summary>Outcome of the last action.</summary>
    [TempData]
    public string? Message { get; set; }

    /// <summary>Why the last action was refused.</summary>
    [TempData]
    public string? Error { get; set; }

    /// <summary>Shows the organization.</summary>
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!await LoadAsync(cancellationToken)) return NotFound();

        return Page();
    }

    /// <summary>Renames the organization.</summary>
    public Task<IActionResult> OnPostRenameAsync(string? newName, string? password, CancellationToken cancellationToken)
        => ActAsync(password, async current =>
        {
            var result = await Ownership.RenameOrganizationAsync(Name, newName, cancellationToken);
            if (result == OrganizationChange.Success)
            {
                Log(current, "renamed organization {Old} to {New}", Name, newName?.Trim());
                Name = newName!.Trim();
                Message = $"The organization is now called {Name}.";
            }
            else
            {
                Error = Describe(result);
            }
        });

    /// <summary>Deletes the organization.</summary>
    public async Task<IActionResult> OnPostDeleteAsync(string? password, CancellationToken cancellationToken)
    {
        var deleted = false;
        var result = await ActAsync(password, async current =>
        {
            var change = await Ownership.DeleteOrganizationAsync(Name, cancellationToken, current.UserName);
            if (change == OrganizationChange.Success)
            {
                Log(current, "deleted organization {Old}", Name, null);
                TempData["Message"] = $"{Name} was deleted.";
                deleted = true;
            }
            else
            {
                Error = Describe(change);
            }
        });

        return deleted ? RedirectToPage("/Admin/Organizations") : result;
    }

    /// <summary>Adds a user to the organization.</summary>
    public Task<IActionResult> OnPostAddMemberAsync(string? userName, bool isAdmin, string? password, CancellationToken cancellationToken)
        => ActAsync(password, async current =>
        {
            var user = string.IsNullOrWhiteSpace(userName) ? null : await UserManager.FindByNameAsync(userName.Trim());
            if (user == null)
            {
                Error = "There is no user with that name.";
            }
            else if (await Ownership.AddMemberAsync(Name!, user.Id, isAdmin, cancellationToken))
            {
                Log(current, "added {New} to organization {Old}", Name, user.UserName);
                Message = $"{user.UserName} joined {Name}.";
            }
            else
            {
                Error = $"{user.UserName} is already a member.";
            }
        });

    /// <summary>Makes a member an administrator of the organization, or not.</summary>
    public Task<IActionResult> OnPostSetAdminAsync(string? userName, bool isAdmin, string? password, CancellationToken cancellationToken)
        => ActAsync(password, async current =>
        {
            var user = string.IsNullOrWhiteSpace(userName) ? null : await UserManager.FindByNameAsync(userName.Trim());
            var result = user == null
                ? OrganizationChange.NotFound
                : await Ownership.SetMemberAdminAsync(Name!, user.Id, isAdmin, cancellationToken);
            if (result == OrganizationChange.Success)
            {
                Log(current, "set administrator={New} for a member of organization {Old}", Name, isAdmin.ToString());
                Message = isAdmin ? $"{user!.UserName} now administers {Name}." : $"{user!.UserName} no longer administers {Name}.";
            }
            else
            {
                Error = Describe(result);
            }
        });

    /// <summary>Removes a member from the organization.</summary>
    public Task<IActionResult> OnPostRemoveMemberAsync(string? userName, string? password, CancellationToken cancellationToken)
        => ActAsync(password, async current =>
        {
            var user = string.IsNullOrWhiteSpace(userName) ? null : await UserManager.FindByNameAsync(userName.Trim());
            if (user == null)
            {
                Error = "There is no user with that name.";
            }
            else if (await Ownership.RemoveMemberAsync(Name!, user.Id, cancellationToken))
            {
                Log(current, "removed {New} from organization {Old}", Name, user.UserName);
                Message = $"{user.UserName} left {Name}.";
            }
            else
            {
                Error = "An organization must keep at least one administrator, and the user must be a member.";
            }
        });

    private static string Describe(OrganizationChange change) => change switch
    {
        OrganizationChange.NameTaken => "That name is taken.",
        OrganizationChange.InvalidName => "That is not a valid name: it must not be blank or contain / \\ ? # %.",
        OrganizationChange.OwnsPackages => "The organization owns packages, so it cannot be deleted. Remove it as an owner of those packages first.",
        OrganizationChange.LastAdministrator => "An organization must keep at least one administrator.",
        _ => "There is no such organization or member.",
    };

    private void Log(BaGetUser admin, string template, string? oldValue, string? newValue)
        => HttpContext.RequestServices.GetRequiredService<ILogger<OrganizationDetailModel>>()
            .LogWarning("{Admin} " + template, admin.UserName, oldValue, newValue);

    // Refuse during maintenance, confirm the password, run the change, then show the (possibly renamed) page.
    private async Task<IActionResult> ActAsync(string? password, Func<BaGetUser, Task> change)
    {
        HttpContext.RequestServices.GetService<MaintenanceGate>()?.ThrowIfClosed();

        var current = await UserManager.GetUserAsync(User);
        if (current == null) return Challenge();

        if (await Ownership.FindOrganizationAsync(Name, HttpContext.RequestAborted) == null) return NotFound();

        if (!await IsConfirmedAsync(current, password))
        {
            Error = "The confirmation is incorrect, so nothing was changed.";
        }
        else
        {
            await change(current);
        }

        return RedirectToPage("/Admin/OrganizationDetail", new { name = Name });
    }

    private async Task<bool> LoadAsync(CancellationToken cancellationToken)
    {
        Organization = await Ownership.FindOrganizationAsync(Name, cancellationToken);
        if (Organization == null) return false;

        var rows = new List<OrganizationMemberRow>();
        foreach (var member in await Ownership.GetMembersAsync(Organization.Name, cancellationToken))
        {
            #pragma warning disable CS8604
            var user = await UserManager.FindByIdAsync(member.UserId);
            #pragma warning restore CS8604
            if (user != null) rows.Add(new OrganizationMemberRow(user.UserName, member.IsAdmin));
        }

        Members = rows.OrderBy(r => r.UserName, StringComparer.OrdinalIgnoreCase).ToList();
        OwnedPackageIds = await Ownership.CountOwnedPackageIdsAsync(Organization.Name, cancellationToken);

        var current = await UserManager.GetUserAsync(User);
        HasPassword = current == null || await UserManager.HasPasswordAsync(current);
        Reauthenticated = !HasPassword && await HasReauthenticatedRecentlyAsync();

        return true;
    }
}
