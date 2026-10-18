using System;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BaGet.Identity.Pages.Admin;

/// <summary>
/// An administrator deleting another account: what goes, what happens to the organizations and
/// to the package IDs only that user owned (leave them without owners, or delete them with all
/// their versions), then a password confirmation. Never the administrator's own account, and
/// never the last administrator.
/// </summary>
public class DeleteUserModel : AdminPageModel
{
    /// <summary>The user to delete.</summary>
    [BindProperty(SupportsGet = true)]
    public string? Name { get; set; }

    /// <summary>"keep" (default) or "delete": what to do with the IDs only this user owns.</summary>
    [BindProperty]
    public string SoleOwned { get; set; } = "keep";

    /// <summary>What the deletion would remove or change.</summary>
    public AdminDeletionPreview? Preview { get; private set; }

    /// <summary>Whether the user is the server's only administrator.</summary>
    public bool LastAdministrator { get; private set; }

    /// <summary>Whether the signed in administrator has a password to confirm with.</summary>
    public bool HasPassword { get; private set; } = true;

    /// <summary>For an account without a password: it signed in again with its provider a moment ago.</summary>
    public bool Reauthenticated { get; private set; }

    /// <summary>Whether an external provider is available to sign in again with.</summary>
    public bool CanReauthenticate => Accounts.OpenIdConnect.IsConfigured;

    /// <summary>Read-only mode: the account may go, but package files are not deleted.</summary>
    public bool IsReadOnlyMode => HttpContext.RequestServices.GetRequiredService<IOptionsSnapshot<BaGetOptions>>().Value.IsReadOnlyMode;

    /// <summary>Shown when permanent package deletion is asked for in read-only mode.</summary>
    public const string ReadOnlyMessage = "The server is read-only, so packages cannot be deleted. Leave them without owners, or turn read-only mode off.";

    /// <summary>Why the deletion was refused.</summary>
    public string? Error { get; private set; }

    /// <summary>Shows what the deletion does.</summary>
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var (user, refusal) = await FindTargetAsync();
        if (refusal != null) return refusal;

        await LoadAsync(user, cancellationToken);

        return Page();
    }

    /// <summary>Deletes the user after the administrator's password, then returns to the users.</summary>
    public async Task<IActionResult> OnPostAsync(string? password, CancellationToken cancellationToken)
    {
        var (user, refusal) = await FindTargetAsync();
        if (refusal != null) return refusal;

        var current = await UserManager.GetUserAsync(User);
        var deletePackages = string.Equals(SoleOwned, "delete", StringComparison.OrdinalIgnoreCase);

        // The same refusal as the package page's hard delete: read-only mode freezes package removal.
        if (deletePackages && IsReadOnlyMode)
        {
            Error = ReadOnlyMessage;
            await LoadAsync(user, cancellationToken);

            return Page();
        }

        if (!await IsConfirmedAsync(current, password))
        {
            Error = "The confirmation is incorrect, so the account was not deleted.";
            await LoadAsync(user, cancellationToken);

            return Page();
        }

        var name = user.UserName;
        var outcome = await HttpContext.RequestServices.GetRequiredService<AccountDeletionService>()
            .DeleteByAdministratorAsync(user, deletePackages, current?.UserName, cancellationToken);

        if (!outcome.Result.Succeeded)
        {
            Error = Describe(outcome.Result);
            await LoadAsync(user, cancellationToken);

            return Page();
        }

        TempData["Message"] = Summarize(name, outcome);
        if (outcome.Failures.Count > 0)
        {
            TempData["Error"] = "Could not delete: " + string.Join(", ", outcome.Failures) + ". They have no owners and are listed under Packages without owners.";
        }

        return RedirectToPage("/Admin/Users");
    }

    private static string Summarize(string? name, AdminDeletionResult outcome)
    {
        var text = $"{name} was deleted.";
        if (outcome.DeletedPackageIds.Count > 0) text += $" Packages deleted: {string.Join(", ", outcome.DeletedPackageIds)}.";
        if (outcome.KeptWithoutOwners.Count > 0) text += $" Packages left without owners: {string.Join(", ", outcome.KeptWithoutOwners)}.";

        return text;
    }

    // The user, or what to answer instead: 404 for no such user, back to the list for oneself.
    private async Task<(BaGetUser User, IActionResult Refusal)> FindTargetAsync()
    {
        var current = await UserManager.GetUserAsync(User);
        #pragma warning disable CS8619
        if (current == null) return (null, Challenge());
        #pragma warning restore CS8619

        var user = string.IsNullOrEmpty(Name) ? null : await UserManager.FindByNameAsync(Name);
        #pragma warning disable CS8619
        if (user == null) return (null, NotFound());
        #pragma warning restore CS8619

        if (user.Id == current.Id)
        {
            TempData["Error"] = "Delete your own account from your account settings.";

            #pragma warning disable CS8619
            return (null, RedirectToPage("/Admin/Users"));
            #pragma warning restore CS8619
        }

        #pragma warning disable CS8619
        return (user, null);
        #pragma warning restore CS8619
    }

    private async Task LoadAsync(BaGetUser user, CancellationToken cancellationToken)
    {
        var deletion = HttpContext.RequestServices.GetRequiredService<AccountDeletionService>();
        Preview = await deletion.PreviewAdministratorDeletionAsync(user, cancellationToken);
        LastAdministrator = (await deletion.GetBlockersAsync(user, cancellationToken)).LastAdministrator;

        var current = await UserManager.GetUserAsync(User);
        HasPassword = current == null || await UserManager.HasPasswordAsync(current);
        Reauthenticated = !HasPassword && await HasReauthenticatedRecentlyAsync();
    }
}
