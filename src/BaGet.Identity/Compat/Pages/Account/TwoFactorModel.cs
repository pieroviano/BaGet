using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BaGet.Identity.ViewModels;
using BaGet.Identity.Workflows;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Identity.Pages.Account;

/// <summary>
/// The page model of the two-factor authentication page when it was a Razor Page: authenticator-app (TOTP) set up with a QR
/// code, recovery codes shown once, turn off, or start over with a new key.
/// </summary>
/// <remarks>
/// Kept as a shim (no breaking change of the public API); <c>AccountSettingsController</c> serves <c>/account/twofactor</c> and delegates
/// to the same <c>TwoFactorWorkflow</c>.
/// </remarks>
[Obsolete("The two-factor screen is served by AccountSettingsController; this page model has no page any more and will be removed in the next major version.")]
public class TwoFactorModel : AccountPageModel
{
    private readonly TwoFactorViewModel _state = new TwoFactorViewModel();

    protected override bool RequiresSignIn => true;

    public bool TwoFactorEnabled => _state.TwoFactorEnabled;

    /// <summary>
    /// The key to type into an authenticator that cannot scan the QR code.
    /// </summary>
    public string? SharedKey => _state.SharedKey;

    /// <summary>
    /// The QR code of the setup link, an SVG built from the user's own key.
    /// </summary>
    public string? QrCodeSvg => _state.QrCodeSvg;

    public bool Reauthenticated => _state.Reauthenticated;

    public bool CanReauthenticate => Accounts.OpenIdConnect.IsConfigured;

    public int RecoveryCodesLeft => _state.RecoveryCodesLeft;

    public bool HasPassword => _state.HasPassword;

    /// <summary>
    /// The new recovery codes. Shown once, when two-factor authentication is turned on.
    /// </summary>
    public IReadOnlyList<string> RecoveryCodes => _state.RecoveryCodes;

    public string? Error => _state.Error;

    public string? Message => _state.Message;

    public async Task<IActionResult> OnGetAsync() => OutcomeResult(await new TwoFactorWorkflow(Screen).ShowAsync(_state));

    public async Task<IActionResult> OnPostEnableAsync(string? code) => OutcomeResult(await new TwoFactorWorkflow(Screen).EnableAsync(_state, code));

    public async Task<IActionResult> OnPostDisableAsync(string? password) => OutcomeResult(await new TwoFactorWorkflow(Screen).DisableAsync(_state, password));

    /// <summary>
    /// A new key makes the old authenticator and every recovery code useless; it must be
    /// set up and enabled again.
    /// </summary>
    public async Task<IActionResult> OnPostResetAsync(string? password) => OutcomeResult(await new TwoFactorWorkflow(Screen).ResetAsync(_state, password));
}
