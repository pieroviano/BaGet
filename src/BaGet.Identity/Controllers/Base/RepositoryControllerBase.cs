using System.Threading.Tasks;
using BaGet.Identity.Application.Models;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Identity.Controllers.Base;

/// <summary>
/// Base of the repository administration screens that change a remote repository's upstream or empty its cache. They ask for the administrator's password again (or a recent sign-in with the
/// external provider, for an account without one), like the access screens do for changes that hand out credentials or delete
/// data.
/// </summary>
public abstract class RepositoryControllerBase : AdminControllerBase
{
    /// <summary>The message shown when the confirmation fails; nothing was changed.</summary>
    protected const string NotConfirmed = "The confirmation is incorrect, so nothing was changed.";

    /// <summary>Why the last action was refused.</summary>
    public string? Error { get; protected set; }

    /// <summary>Whether the signed-in user has a password to confirm with.</summary>
    public bool HasPassword { get; private set; } = true;

    /// <summary>For an account without a password: it signed in again with its provider a moment ago.</summary>
    public bool Reauthenticated { get; private set; }

    /// <summary>Whether an external provider is available to sign in again with.</summary>
    public bool CanReauthenticate => Accounts.OpenIdConnect.IsConfigured;

    /// <summary>The model of the password confirmation partial, with <paramref name="id"/> for the input.</summary>
    [NonAction]
    public ConfirmPasswordView Confirmation(string id)
        => new ConfirmPasswordView(id, HasPassword, Reauthenticated, CanReauthenticate, Request.Path + Request.QueryString);

    /// <summary>Works out what the confirmation partials must show; call before rendering a screen that has them.</summary>
    protected async Task LoadConfirmationAsync()
    {
        var current = await UserManager.GetUserAsync(User);
        HasPassword = current == null || await UserManager.HasPasswordAsync(current);
        Reauthenticated = !HasPassword && await HasReauthenticatedRecentlyAsync();
    }

    /// <summary>Whether what the user typed (or a recent re-authentication) confirms the change.</summary>
    protected async Task<bool> ConfirmedAsync(string? password)
    {
        var current = await UserManager.GetUserAsync(User);

        return current != null && await IsConfirmedAsync(current, password);
    }
}
