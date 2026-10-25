using System;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Identity.ViewModels;
using BaGet.Identity.Workflows;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Identity.Pages.Account;

/// <summary>
/// The page model of the account settings page when it was a Razor Page: change password, change email, delete the account.
/// </summary>
/// <remarks>
/// Kept as a shim (no breaking change of the public API); <c>AccountSettingsController</c> serves <c>/account/settings</c> and delegates
/// to the same <c>SettingsWorkflow</c>.
/// </remarks>
[Obsolete("The account settings screen is served by AccountSettingsController; this page model has no page any more and will be removed in the next major version.")]
public class SettingsModel : AccountPageModel
{
    private readonly SettingsViewModel _state = new SettingsViewModel();

    protected override bool RequiresSignIn => true;

    public string? Email => _state.Email;

    public bool EmailConfirmed => _state.EmailConfirmed;

    public bool HasPassword => _state.HasPassword;

    /// <summary>
    /// For an account without a password: it signed in again with its provider a moment ago.
    /// </summary>
    public bool Reauthenticated => _state.Reauthenticated;

    public bool CanReauthenticate => Accounts.OpenIdConnect.IsConfigured;

    public bool CanSendEmail => EmailConfigured;

    public string? Message => _state.Message;

    public string? Error => _state.Error;

    /// <summary>
    /// Why the account cannot be deleted yet, when it cannot.
    /// </summary>
    public DeletionBlockers? Blockers => _state.Blockers;

    public async Task<IActionResult> OnGetAsync() => OutcomeResult(await new SettingsWorkflow(Screen).ShowAsync(_state));

    public async Task<IActionResult> OnPostChangePasswordAsync(string? currentPassword, string newPassword, string confirmPassword)
        => OutcomeResult(await new SettingsWorkflow(Screen).ChangePasswordAsync(_state, currentPassword, newPassword, confirmPassword));

    /// <summary>
    /// The password is asked again, like for deleting the account: the address is where
    /// password resets go, so a stolen session must not be able to repoint it.
    /// </summary>
    public async Task<IActionResult> OnPostChangeEmailAsync(string? newEmail, string? password)
        => OutcomeResult(await new SettingsWorkflow(Screen).ChangeEmailAsync(_state, newEmail, password));

    /// <summary>
    /// The password is asked again, so a stolen session cannot delete the account. An
    /// account without a password (external sign-in only) signs in again with its provider.
    /// </summary>
    public async Task<IActionResult> OnPostDeleteAsync(string? password, CancellationToken cancellationToken)
        => OutcomeResult(await new SettingsWorkflow(Screen).DeleteAsync(_state, password, cancellationToken));
}
