using System.Threading;
using System.Threading.Tasks;
using BaGet.Identity.Application;
using BaGet.Identity.Application.Models;
using BaGet.Identity.Application.UseCases;
using BaGet.Identity.Controllers.Base;
using BaGet.Identity.Controllers.UI;
using BaGet.Identity.Mvc;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Identity.Controllers;

/// <summary>
/// The signed-in user's own settings: account settings (email, password, delete the account), two-factor authentication and API
/// keys. It replaces the Razor Pages <c>Account/Settings</c>, <c>Account/TwoFactor</c> and <c>Account/ApiKeys</c>: same URLs, same <c>?handler=</c> forms, same <c>asp-page</c> names. Every action needs a signed-in
/// user (a visitor is challenged, 404 while accounts are off). The work is done by <see cref="AccountSettingsUiApiController"/>
/// (the use cases of <c>BaGet.Identity.Application</c>); this controller binds the form, calls it in process and renders the view.
/// </summary>
public sealed class AccountSettingsController : AccountControllerBase
{
    /// <inheritdoc/>
    protected override bool RequiresSignIn => true;

    // ----- /account/settings

    /// <summary>Account settings.</summary>
    [Route("account/settings"), HttpGet, HttpHead, PageHandler, LegacyPage("/Account/Settings")]
    public async Task<IActionResult> Settings()
        => Render(await Api<AccountSettingsUiApiController, SettingsViewModel>(api => api.Settings()), "Settings", modelShowsFormErrors: true);

    /// <summary>Change the password.</summary>
    [Route("account/settings"), HttpPost, PageHandler("ChangePassword"), LegacyPage("/Account/Settings")]
    public async Task<IActionResult> SettingsChangePassword(string? currentPassword, string newPassword, string confirmPassword)
        => Render(
            await Api<AccountSettingsUiApiController, SettingsViewModel>(api => api.SettingsChangePassword(
                new ChangePasswordForm { CurrentPassword = currentPassword, NewPassword = newPassword, ConfirmPassword = confirmPassword })),
            "Settings",
            modelShowsFormErrors: true);

    /// <summary>Change the email address (asks the password again).</summary>
    [Route("account/settings"), HttpPost, PageHandler("ChangeEmail"), LegacyPage("/Account/Settings")]
    public async Task<IActionResult> SettingsChangeEmail(string? newEmail, string? password)
        => Render(
            await Api<AccountSettingsUiApiController, SettingsViewModel>(api => api.SettingsChangeEmail(new ChangeEmailForm { NewEmail = newEmail, Password = password })),
            "Settings",
            modelShowsFormErrors: true);

    /// <summary>Delete the account (asks the password again).</summary>
    [Route("account/settings"), HttpPost, PageHandler("Delete"), LegacyPage("/Account/Settings")]
    public async Task<IActionResult> SettingsDelete(string? password, CancellationToken cancellationToken)
        => Render(
            await Api<AccountSettingsUiApiController, SettingsViewModel>(api => api.SettingsDelete(new PasswordConfirmationForm { Password = password }, cancellationToken)),
            "Settings",
            modelShowsFormErrors: true);

    // ----- /account/twofactor

    /// <summary>Two-factor authentication.</summary>
    [Route("account/twofactor"), HttpGet, HttpHead, PageHandler, LegacyPage("/Account/TwoFactor")]
    public async Task<IActionResult> TwoFactor()
        => Render(await Api<AccountSettingsUiApiController, TwoFactorViewModel>(api => api.TwoFactor()), "TwoFactor", modelShowsFormErrors: true);

    /// <summary>Turn two-factor authentication on with a code from the authenticator.</summary>
    [Route("account/twofactor"), HttpPost, PageHandler("Enable"), LegacyPage("/Account/TwoFactor")]
    public async Task<IActionResult> TwoFactorEnable(string? code)
        => Render(
            await Api<AccountSettingsUiApiController, TwoFactorViewModel>(api => api.TwoFactorEnable(new TwoFactorCodeForm { Code = code })),
            "TwoFactor",
            modelShowsFormErrors: true);

    /// <summary>Turn two-factor authentication off (asks the password again).</summary>
    [Route("account/twofactor"), HttpPost, PageHandler("Disable"), LegacyPage("/Account/TwoFactor")]
    public async Task<IActionResult> TwoFactorDisable(string? password)
        => Render(
            await Api<AccountSettingsUiApiController, TwoFactorViewModel>(api => api.TwoFactorDisable(new PasswordConfirmationForm { Password = password })),
            "TwoFactor",
            modelShowsFormErrors: true);

    /// <summary>Reset the authenticator (asks the password again).</summary>
    [Route("account/twofactor"), HttpPost, PageHandler("Reset"), LegacyPage("/Account/TwoFactor")]
    public async Task<IActionResult> TwoFactorReset(string? password)
        => Render(
            await Api<AccountSettingsUiApiController, TwoFactorViewModel>(api => api.TwoFactorReset(new PasswordConfirmationForm { Password = password })),
            "TwoFactor",
            modelShowsFormErrors: true);

    // ----- /account/apikeys

    /// <summary>The user's API keys and the new-key form.</summary>
    [Route("account/apikeys"), HttpGet, HttpHead, PageHandler, LegacyPage("/Account/ApiKeys")]
    public async Task<IActionResult> ApiKeysIndex(CancellationToken cancellationToken)
        => Render(await Api<AccountSettingsUiApiController, ApiKeysViewModel>(api => api.ApiKeysIndex(cancellationToken)), "ApiKeys");

    /// <summary>Create a key.</summary>
    [Route("account/apikeys"), HttpPost, PageHandler("Create"), LegacyPage("/Account/ApiKeys")]
    public async Task<IActionResult> ApiKeysCreate(ApiKeyForm form, CancellationToken cancellationToken)
        => Render(await Api<AccountSettingsUiApiController, ApiKeysViewModel>(api => api.ApiKeysCreate(form, cancellationToken)), "ApiKeys");

    /// <summary>Revoke a key.</summary>
    [Route("account/apikeys"), HttpPost, PageHandler("Revoke"), LegacyPage("/Account/ApiKeys")]
    public async Task<IActionResult> ApiKeysRevoke(int key, CancellationToken cancellationToken)
        => Render(await Api<AccountSettingsUiApiController, ApiKeysViewModel>(api => api.ApiKeysRevoke(key, cancellationToken)), "ApiKeys");
}
