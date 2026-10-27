using System.Threading;
using System.Threading.Tasks;
using BaGet.Identity.Application.Screens;
using BaGet.Identity.Controllers.Base;
using Microsoft.AspNetCore.Mvc;
using BaGet.Identity.Application.Models.Account;
using BaGet.Identity.Application.Models.Settings;
using BaGet.Identity.Application.UseCases.Account;
using BaGet.Identity.Controllers.FrontEnd;
using BaGet.Identity.Mvc.UI;

namespace BaGet.Identity.Controllers.UI;

/// <summary>
/// The JSON twin of the signed-in user's own settings (<c>/api/ui/account/settings</c>, <c>twofactor</c>, <c>apikeys</c>):
/// <see cref="AccountSettingsController"/> calls it in process. <b>Screen only</b> (the browser session): over HTTP, where only API keys authenticate, every caller gets 403
/// "use the web screen"; an API key never changes credentials, reads the authenticator key or mints another key. A refused action answers
/// 400 with the screen's model (<c>error</c> says why); the password-confirmed actions re-ask the password like the page.
/// </summary>
[Route("api/ui/account")]
public sealed class AccountSettingsUiApiController : AccountUiApiControllerBase
{
    /// <inheritdoc/>
    protected override bool RequiresSignIn => true;

    // ----- settings

    /// <summary>Account settings.</summary>
    [HttpGet("settings"), UiApi("/Account/Settings")]
    public async Task<ActionResult<ScreenResult<SettingsViewModel>>> Settings()
        => Respond(await UseCase<AccountSettingsUseCase>().ShowAsync(User));

    /// <summary>Change the password.</summary>
    [HttpPost("settings/change-password"), UiApi("/Account/Settings", Handler = "ChangePassword")]
    public async Task<ActionResult<ScreenResult<SettingsViewModel>>> SettingsChangePassword([FromBody] ChangePasswordForm? form)
        => Respond(await UseCase<AccountSettingsUseCase>().ChangePasswordAsync(User, form));

    /// <summary>Change the email address (asks the password again).</summary>
    [HttpPost("settings/change-email"), UiApi("/Account/Settings", Handler = "ChangeEmail")]
    public async Task<ActionResult<ScreenResult<SettingsViewModel>>> SettingsChangeEmail([FromBody] ChangeEmailForm? form)
        => Respond(await UseCase<AccountSettingsUseCase>().ChangeEmailAsync(User, form));

    /// <summary>Delete the account (asks the password again). Success signs the user out.</summary>
    [HttpPost("settings/delete"), UiApi("/Account/Settings", Handler = "Delete")]
    public async Task<ActionResult<ScreenResult<SettingsViewModel>>> SettingsDelete([FromBody] PasswordConfirmationForm? form, CancellationToken cancellationToken)
        => Respond(await UseCase<AccountSettingsUseCase>().DeleteAsync(User, form, cancellationToken));

    // ----- two-factor authentication

    /// <summary>Two-factor authentication (the setup key and QR code while it is off).</summary>
    [HttpGet("twofactor"), UiApi("/Account/TwoFactor")]
    public async Task<ActionResult<ScreenResult<TwoFactorViewModel>>> TwoFactor()
        => Respond(await UseCase<TwoFactorUseCase>().ShowAsync(User));

    /// <summary>Turn two-factor authentication on with a code from the authenticator; the answer carries the recovery codes, once.</summary>
    [HttpPost("twofactor/enable"), UiApi("/Account/TwoFactor", Handler = "Enable")]
    public async Task<ActionResult<ScreenResult<TwoFactorViewModel>>> TwoFactorEnable([FromBody] TwoFactorCodeForm? form)
        => Respond(await UseCase<TwoFactorUseCase>().EnableAsync(User, form));

    /// <summary>Turn two-factor authentication off (asks the password again).</summary>
    [HttpPost("twofactor/disable"), UiApi("/Account/TwoFactor", Handler = "Disable")]
    public async Task<ActionResult<ScreenResult<TwoFactorViewModel>>> TwoFactorDisable([FromBody] PasswordConfirmationForm? form)
        => Respond(await UseCase<TwoFactorUseCase>().DisableAsync(User, form));

    /// <summary>Reset the authenticator (asks the password again).</summary>
    [HttpPost("twofactor/reset"), UiApi("/Account/TwoFactor", Handler = "Reset")]
    public async Task<ActionResult<ScreenResult<TwoFactorViewModel>>> TwoFactorReset([FromBody] PasswordConfirmationForm? form)
        => Respond(await UseCase<TwoFactorUseCase>().ResetAsync(User, form));

    // ----- API keys

    /// <summary>The user's API keys (never their values) and the new-key form.</summary>
    [HttpGet("apikeys"), UiApi("/Account/ApiKeys")]
    public async Task<ActionResult<ScreenResult<ApiKeysViewModel>>> ApiKeysIndex(CancellationToken cancellationToken)
        => Respond(await UseCase<ApiKeysUseCase>().ShowAsync(User, cancellationToken));

    /// <summary>Creates a key. The answer carries its value (<c>createdKey</c>) once.</summary>
    [HttpPost("apikeys/create"), UiApi("/Account/ApiKeys", Handler = "Create")]
    public async Task<ActionResult<ScreenResult<ApiKeysViewModel>>> ApiKeysCreate([FromBody] ApiKeyForm? form, CancellationToken cancellationToken)
        => Respond(await UseCase<ApiKeysUseCase>().CreateAsync(User, form, BindingErrors(), cancellationToken));

    /// <summary>Revokes a key.</summary>
    [HttpPost("apikeys/revoke/{key:int}"), UiApi("/Account/ApiKeys", Handler = "Revoke")]
    public async Task<ActionResult<ScreenResult<ApiKeysViewModel>>> ApiKeysRevoke(int key, CancellationToken cancellationToken)
        => Respond(await UseCase<ApiKeysUseCase>().RevokeAsync(User, key, cancellationToken));
}
