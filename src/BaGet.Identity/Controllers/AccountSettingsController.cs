using System.Threading;
using System.Threading.Tasks;
using BaGet.Identity.Mvc;
using BaGet.Identity.ViewModels;
using BaGet.Identity.Workflows;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Identity.Controllers;

/// <summary>
/// The signed-in user's own settings: account settings (email, password, delete the account), two-factor authentication and API
/// keys. It replaces the Razor Pages <c>Account/Settings</c>, <c>Account/TwoFactor</c> and <c>Account/ApiKeys</c>: same URLs, same <c>?handler=</c> forms, same <c>asp-page</c> names. Every action needs a signed-in
/// user (a visitor is challenged, 404 while accounts are off). The rules live in the workflows of <c>BaGet.Identity.Workflows</c>.
/// </summary>
public sealed class AccountSettingsController : AccountControllerBase
{
    /// <inheritdoc/>
    protected override bool RequiresSignIn => true;

    // ----- /account/settings

    /// <summary>Account settings.</summary>
    [Route("account/settings"), HttpGet, HttpHead, PageHandler, LegacyPage("/Account/Settings")]
    public async Task<IActionResult> Settings()
    {
        var model = new SettingsViewModel();

        return Respond(await new SettingsWorkflow(Screen).ShowAsync(model), "Settings", model);
    }

    /// <summary>Change the password.</summary>
    [Route("account/settings"), HttpPost, PageHandler("ChangePassword"), LegacyPage("/Account/Settings")]
    public async Task<IActionResult> SettingsChangePassword(string? currentPassword, string newPassword, string confirmPassword)
    {
        var model = new SettingsViewModel();

        return Respond(await new SettingsWorkflow(Screen).ChangePasswordAsync(model, currentPassword, newPassword, confirmPassword), "Settings", model);
    }

    /// <summary>Change the email address (asks the password again).</summary>
    [Route("account/settings"), HttpPost, PageHandler("ChangeEmail"), LegacyPage("/Account/Settings")]
    public async Task<IActionResult> SettingsChangeEmail(string? newEmail, string? password)
    {
        var model = new SettingsViewModel();

        return Respond(await new SettingsWorkflow(Screen).ChangeEmailAsync(model, newEmail, password), "Settings", model);
    }

    /// <summary>Delete the account (asks the password again).</summary>
    [Route("account/settings"), HttpPost, PageHandler("Delete"), LegacyPage("/Account/Settings")]
    public async Task<IActionResult> SettingsDelete(string? password, CancellationToken cancellationToken)
    {
        var model = new SettingsViewModel();

        return Respond(await new SettingsWorkflow(Screen).DeleteAsync(model, password, cancellationToken), "Settings", model);
    }

    // ----- /account/twofactor

    /// <summary>Two-factor authentication.</summary>
    [Route("account/twofactor"), HttpGet, HttpHead, PageHandler, LegacyPage("/Account/TwoFactor")]
    public async Task<IActionResult> TwoFactor()
    {
        var model = new TwoFactorViewModel();

        return Respond(await new TwoFactorWorkflow(Screen).ShowAsync(model), "TwoFactor", model);
    }

    /// <summary>Turn two-factor authentication on with a code from the authenticator.</summary>
    [Route("account/twofactor"), HttpPost, PageHandler("Enable"), LegacyPage("/Account/TwoFactor")]
    public async Task<IActionResult> TwoFactorEnable(string? code)
    {
        var model = new TwoFactorViewModel();

        return Respond(await new TwoFactorWorkflow(Screen).EnableAsync(model, code), "TwoFactor", model);
    }

    /// <summary>Turn two-factor authentication off (asks the password again).</summary>
    [Route("account/twofactor"), HttpPost, PageHandler("Disable"), LegacyPage("/Account/TwoFactor")]
    public async Task<IActionResult> TwoFactorDisable(string? password)
    {
        var model = new TwoFactorViewModel();

        return Respond(await new TwoFactorWorkflow(Screen).DisableAsync(model, password), "TwoFactor", model);
    }

    /// <summary>Reset the authenticator (asks the password again).</summary>
    [Route("account/twofactor"), HttpPost, PageHandler("Reset"), LegacyPage("/Account/TwoFactor")]
    public async Task<IActionResult> TwoFactorReset(string? password)
    {
        var model = new TwoFactorViewModel();

        return Respond(await new TwoFactorWorkflow(Screen).ResetAsync(model, password), "TwoFactor", model);
    }

    // ----- /account/apikeys

    /// <summary>The user's API keys and the new-key form.</summary>
    [Route("account/apikeys"), HttpGet, HttpHead, PageHandler, LegacyPage("/Account/ApiKeys")]
    public async Task<IActionResult> ApiKeysIndex(CancellationToken cancellationToken)
    {
        var model = new ApiKeysViewModel();

        return Respond(await new ApiKeysWorkflow(Screen).ShowAsync(model, cancellationToken), "ApiKeys", model);
    }

    /// <summary>Create a key.</summary>
    [Route("account/apikeys"), HttpPost, PageHandler("Create"), LegacyPage("/Account/ApiKeys")]
    public async Task<IActionResult> ApiKeysCreate(ApiKeysViewModel model, CancellationToken cancellationToken)
        => Respond(await new ApiKeysWorkflow(Screen).CreateAsync(model, cancellationToken), "ApiKeys", model);

    /// <summary>Revoke a key.</summary>
    [Route("account/apikeys"), HttpPost, PageHandler("Revoke"), LegacyPage("/Account/ApiKeys")]
    public async Task<IActionResult> ApiKeysRevoke(int key, CancellationToken cancellationToken)
        => Respond(await new ApiKeysWorkflow(Screen).RevokeAsync(key, cancellationToken), "ApiKeys");
}
