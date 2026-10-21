using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BaGet.Identity.Pages.Admin;

/// <summary>
/// One account as an administrator sees it: the details, its API keys (revocable), its organizations and a link to
/// its packages. The page is built from independent sections (<c>user-section-*</c>) so other features, such as
/// groups and effective permissions, can add theirs. Revoking asks for the administrator's password and is refused
/// with 503 while the <see cref="MaintenanceGate"/> is closed.
/// </summary>
public class UserDetailModel : AdminPageModel
{
    /// <summary>The user name (route).</summary>
    [BindProperty(SupportsGet = true)]
    public string? Name { get; set; }

    /// <summary>The account.</summary>
    public BaGetUser? Account { get; private set; }

    /// <summary>The account's API keys; the hash is never shown.</summary>
    public IReadOnlyList<ApiKey> Keys { get; private set; } = Array.Empty<ApiKey>();

    /// <summary>The organizations the account belongs to.</summary>
    public IReadOnlyList<Organization> Organizations { get; private set; } = Array.Empty<Organization>();

    /// <summary>How many package IDs the account owns, in every repository.</summary>
    public int OwnedPackageIds { get; private set; }

    /// <summary>Whether the signed in administrator has a password to confirm with.</summary>
    public bool HasPassword { get; private set; } = true;

    /// <summary>For an account without a password: it signed in again with its provider a moment ago.</summary>
    public bool Reauthenticated { get; private set; }

    /// <summary>Whether an external provider is available to sign in again with.</summary>
    public bool CanReauthenticate => Accounts.OpenIdConnect.IsConfigured;

    /// <summary>Outcome of the last action.</summary>
    [TempData]
    public string? Message { get; set; }

    /// <summary>Why the last action was refused.</summary>
    [TempData]
    public string? Error { get; set; }

    /// <summary>Shows the account.</summary>
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!await LoadAsync(cancellationToken)) return NotFound();

        return Page();
    }

    /// <summary>Revokes one of the account's API keys.</summary>
    public async Task<IActionResult> OnPostRevokeKeyAsync(int key, string? password, CancellationToken cancellationToken)
    {
        HttpContext.RequestServices.GetService<MaintenanceGate>()?.ThrowIfClosed();

        var user = string.IsNullOrEmpty(Name) ? null : await UserManager.FindByNameAsync(Name);
        if (user == null) return NotFound();

        var current = await UserManager.GetUserAsync(User);
        if (current == null) return Challenge();

        if (!await IsConfirmedAsync(current, password))
        {
            Error = "The confirmation is incorrect, so nothing was changed.";
        }
        else if (await ApiKeys.RevokeAsync(user.Id, key, cancellationToken))
        {
            HttpContext.RequestServices.GetRequiredService<ILogger<UserDetailModel>>()
                .LogWarning("{Admin} revoked API key {Key} of {User}", current.UserName, key, user.UserName);
            Message = "The API key was revoked.";
        }
        else
        {
            Error = $"{user.UserName} has no such API key.";
        }

        return RedirectToPage("/Admin/UserDetail", new { name = user.UserName });
    }

    private async Task<bool> LoadAsync(CancellationToken cancellationToken)
    {
        Account = string.IsNullOrEmpty(Name) ? null : await UserManager.FindByNameAsync(Name);
        if (Account == null) return false;

        Keys = await ApiKeys.ListAsync(Account.Id, cancellationToken);
        Organizations = await Ownership.GetOrganizationsAsync(Account.Id, cancellationToken) ?? (IReadOnlyList<Organization>)Array.Empty<Organization>();
        OwnedPackageIds = await Ownership.CountOwnedPackageIdsAsync(Account.UserName, cancellationToken);

        var current = await UserManager.GetUserAsync(User);
        HasPassword = current == null || await UserManager.HasPasswordAsync(current);
        Reauthenticated = !HasPassword && await HasReauthenticatedRecentlyAsync();

        return true;
    }
}
