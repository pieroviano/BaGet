using System.Threading;
using System.Threading.Tasks;
using BaGet.Identity.Application;
using BaGet.Identity.Mvc;
using BaGet.Identity.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Identity.Controllers;

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

/// <summary>
/// The JSON twin of the signed-in user's organizations (<c>/api/ui/account/organizations</c>): <see cref="AccountOrganizationsController"/> calls it
/// in process. Cookie sessions only, signed in.
/// </summary>
[Route("api/ui/account/organizations")]
public sealed class AccountOrganizationsUiApiController : AccountUiApiControllerBase
{
    /// <inheritdoc/>
    protected override bool RequiresSignIn => true;

    /// <summary>The organizations of the user.</summary>
    [HttpGet, UiApi("/Account/Organizations")]
    public async Task<ActionResult<ScreenResult<OrganizationsViewModel>>> Organizations(CancellationToken cancellationToken)
        => Respond(await UseCase<AccountOverviewUseCase>().ShowOrganizationsAsync(User, cancellationToken));

    /// <summary>Create an organization.</summary>
    [HttpPost("create"), UiApi("/Account/Organizations", Handler = "Create")]
    public async Task<ActionResult<ScreenResult<OrganizationsViewModel>>> OrganizationsCreate([FromBody] CreateOrganizationForm? form, CancellationToken cancellationToken)
        => Respond(await UseCase<AccountOverviewUseCase>().CreateOrganizationAsync(User, form, cancellationToken));

    /// <summary>Add a member (organization administrators only; 403 otherwise).</summary>
    [HttpPost("add-member"), UiApi("/Account/Organizations", Handler = "AddMember")]
    public async Task<ActionResult<ScreenResult<OrganizationsViewModel>>> OrganizationsAddMember([FromBody] AddOrganizationMemberForm? form, CancellationToken cancellationToken)
        => Respond(await UseCase<AccountOverviewUseCase>().AddMemberAsync(User, form, cancellationToken));

    /// <summary>Remove a member (an administrator removes anyone; any member may leave; 403 otherwise).</summary>
    [HttpPost("remove-member"), UiApi("/Account/Organizations", Handler = "RemoveMember")]
    public async Task<ActionResult<ScreenResult<OrganizationsViewModel>>> OrganizationsRemoveMember([FromBody] RemoveOrganizationMemberForm? form, CancellationToken cancellationToken)
        => Respond(await UseCase<AccountOverviewUseCase>().RemoveMemberAsync(User, form, cancellationToken));
}

/// <summary>
/// The JSON twin of "My packages", "My access" and the "my profile" redirect (<c>/api/ui/account/packages</c>, <c>access</c>, <c>profile</c>):
/// <see cref="AccountPackagesController"/> calls it in process. Cookie sessions only, signed in.
/// </summary>
[Route("api/ui/account")]
public sealed class AccountPackagesUiApiController : AccountUiApiControllerBase
{
    /// <inheritdoc/>
    protected override bool RequiresSignIn => true;

    /// <summary>The package IDs the user owns, per repository.</summary>
    [HttpGet("packages"), UiApi("/Account/Packages")]
    public async Task<ActionResult<ScreenResult<AccountPackagesViewModel>>> Packages(CancellationToken cancellationToken)
        => Respond(await UseCase<AccountOverviewUseCase>().ShowPackagesAsync(User, cancellationToken));

    /// <summary>What the user may do in each repository (optionally for one package ID).</summary>
    [HttpGet("access"), UiApi("/Account/Access")]
    public async Task<ActionResult<ScreenResult<AccountAccessViewModel>>> Access(string? package, CancellationToken cancellationToken)
        => Respond(await UseCase<AccountOverviewUseCase>().ShowAccessAsync(User, package, cancellationToken));

    /// <summary>The user's own profile: continues at <c>next.localUrl</c> (<c>~/profiles/{name}</c>).</summary>
    [HttpGet("profile"), UiApi("/Account/Profile")]
    public ActionResult<ScreenResult<EmptyScreen>> MyProfile()
        => Respond(UseCase<AccountOverviewUseCase>().ShowMyProfile(User));
}

/// <summary>
/// The JSON twin of a user's or an organization's public page (<c>/api/ui/profiles/{name}</c>, and <c>/api/ui/r/{repo}/profiles/{name}</c>):
/// <see cref="ProfileController"/> calls it in process. The page is public data (names, owned package IDs with their downloads, an avatar address),
/// so unlike the other account screens an API key may read it too.
/// </summary>
[Route("api/ui/profiles")]
[RepositoryPage]
public sealed class ProfileUiApiController : AccountUiApiControllerBase
{
    /// <inheritdoc/>
    protected override bool CookieOnly => false;

    /// <summary>The profile named <paramref name="name"/>; 404 when neither a user nor an organization has it.</summary>
    [HttpGet("{name}"), UiApi("/Profiles/Profile")]
    public async Task<ActionResult<ScreenResult<ProfileViewModel>>> Profile(string? name, CancellationToken cancellationToken)
        => Respond(await UseCase<ProfileUseCase>().ShowAsync(User, name, cancellationToken));
}
