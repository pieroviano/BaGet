using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.Mvc;
using BaGet.Identity.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using QRCoder;

namespace BaGet.Identity.Workflows;

/// <summary>
/// Change password, change email, delete the account. Each form posts to its own handler and takes its fields as parameters,
/// so one form's rules never block another's. Shared by <c>AccountSettingsController</c> and the obsolete <c>SettingsModel</c>.
/// </summary>
internal sealed class SettingsWorkflow
{
    private readonly AccountScreen _screen;

    public SettingsWorkflow(AccountScreen screen)
    {
        _screen = screen ?? throw new ArgumentNullException(nameof(screen));
    }

    /// <summary>The settings screen was requested.</summary>
    public async Task<ScreenOutcome> ShowAsync(SettingsViewModel model)
        => await LoadAsync(model) ? ScreenOutcome.Show : ScreenOutcome.Challenge;

    public async Task<ScreenOutcome> ChangePasswordAsync(SettingsViewModel model, string? currentPassword, string newPassword, string confirmPassword)
    {
        var users = _screen.UserManager;
        var user = await users.GetUserAsync(_screen.User);
        if (user == null) return ScreenOutcome.Challenge;

        if (string.IsNullOrEmpty(newPassword) || newPassword != confirmPassword)
        {
            model.Error = "The new passwords do not match.";
        }
        else
        {
            var hasPassword = await users.HasPasswordAsync(user);

            // Adding the first password to an external-only account would turn a stolen
            // session into a password for it, so it needs the same fresh proof.
            var result = hasPassword
                ? await users.ChangePasswordAsync(user, currentPassword ?? string.Empty, newPassword)
                : await _screen.HasReauthenticatedRecentlyAsync()
                    ? await users.AddPasswordAsync(user, newPassword)
                    : IdentityResult.Failed(new IdentityError { Description = "Sign in again with your provider first, then set the password." });

            if (result.Succeeded)
            {
                // The security stamp changed; keep this session signed in.
                await _screen.SignInManager.RefreshSignInAsync(user);
                model.Message = "Your password has been changed.";
            }
            else
            {
                model.Error = AccountScreen.Describe(result);
            }
        }

        await LoadAsync(model);

        return ScreenOutcome.Show;
    }

    /// <summary>
    /// The password is asked again, like for deleting the account: the address is where
    /// password resets go, so a stolen session must not be able to repoint it.
    /// </summary>
    public async Task<ScreenOutcome> ChangeEmailAsync(SettingsViewModel model, string? newEmail, string? password)
    {
        var users = _screen.UserManager;
        var user = await users.GetUserAsync(_screen.User);
        if (user == null) return ScreenOutcome.Challenge;

        newEmail = newEmail?.Trim();

        if (!await _screen.IsConfirmedAsync(user, password))
        {
            model.Error = "The confirmation is incorrect, so the email address was not changed.";
        }
        else if (string.IsNullOrEmpty(newEmail) || !new EmailAddressAttribute().IsValid(newEmail))
        {
            model.Error = "Enter a valid email address.";
        }
        else if (!_screen.EmailConfigured)
        {
            var result = await users.SetEmailAsync(user, newEmail);
            if (result.Succeeded)
            {
                await _screen.SignInManager.RefreshSignInAsync(user);
                model.Message = "Your email address has been changed.";
            }
            else model.Error = AccountScreen.Describe(result);
        }
        else
        {
            var token = await users.GenerateChangeEmailTokenAsync(user, newEmail);
            var link = _screen.PageLink("/Account/ConfirmEmail", new { userId = user.Id, email = newEmail, code = AccountScreen.EncodeToken(token) });

            try
            {
                await _screen.EmailSender.SendConfirmationLinkAsync(user, newEmail, link);
                model.Message = "We sent a confirmation link to the new address. Your address changes when you follow it.";
            }
            catch (Exception)
            {
                model.Error = "We could not send the confirmation email. Try again later.";
            }
        }

        await LoadAsync(model);

        return ScreenOutcome.Show;
    }

    /// <summary>
    /// The password is asked again, so a stolen session cannot delete the account. An
    /// account without a password (external sign-in only) signs in again with its provider.
    /// </summary>
    public async Task<ScreenOutcome> DeleteAsync(SettingsViewModel model, string? password, CancellationToken cancellationToken)
    {
        var user = await _screen.UserManager.GetUserAsync(_screen.User);
        if (user == null) return ScreenOutcome.Challenge;

        if (!await _screen.IsConfirmedAsync(user, password))
        {
            model.Error = "The confirmation is incorrect, so the account was not deleted.";
        }
        else
        {
            var deletion = _screen.Services.GetRequiredService<AccountDeletionService>();
            var (blockers, result) = await deletion.DeleteAsync(user, cancellationToken);

            if (blockers.Any)
            {
                model.Blockers = blockers;
                model.Error = "The account cannot be deleted yet.";
            }
            else if (!result.Succeeded)
            {
                model.Error = AccountScreen.Describe(result);
            }
            else
            {
                await _screen.SignInManager.SignOutAsync();

                return ScreenOutcome.Local(null);
            }
        }

        await LoadAsync(model);

        return ScreenOutcome.Show;
    }

    private async Task<bool> LoadAsync(SettingsViewModel model)
    {
        var users = _screen.UserManager;
        var user = await users.GetUserAsync(_screen.User);
        if (user == null) return false;

        model.Email = user.Email;
        model.EmailConfirmed = user.EmailConfirmed;
        model.HasPassword = await users.HasPasswordAsync(user);
        model.Reauthenticated = !model.HasPassword && await _screen.HasReauthenticatedRecentlyAsync();
        model.CanReauthenticate = _screen.Accounts.OpenIdConnect.IsConfigured;
        model.CanSendEmail = _screen.EmailConfigured;

        return true;
    }
}

/// <summary>
/// Authenticator-app (TOTP) two-factor authentication: set up with a QR code, recovery codes shown once, turn off, or start over
/// with a new key. Shared by <c>AccountSettingsController</c> and the obsolete <c>TwoFactorModel</c>.
/// </summary>
internal sealed class TwoFactorWorkflow
{
    private const int RecoveryCodeCount = 10;
    private const string Issuer = "BaGet Extension";

    private readonly AccountScreen _screen;

    public TwoFactorWorkflow(AccountScreen screen)
    {
        _screen = screen ?? throw new ArgumentNullException(nameof(screen));
    }

    /// <summary>The screen was requested.</summary>
    public async Task<ScreenOutcome> ShowAsync(TwoFactorViewModel model)
        => await LoadAsync(model) ? ScreenOutcome.Show : ScreenOutcome.Challenge;

    public async Task<ScreenOutcome> EnableAsync(TwoFactorViewModel model, string? code)
    {
        var users = _screen.UserManager;
        var user = await users.GetUserAsync(_screen.User);
        if (user == null) return ScreenOutcome.Challenge;

        var cleaned = (code ?? string.Empty).Replace(" ", string.Empty).Replace("-", string.Empty);
        var valid = cleaned.Length > 0 && await users.VerifyTwoFactorTokenAsync(
            user,
            users.Options.Tokens.AuthenticatorTokenProvider,
            cleaned);

        if (!valid)
        {
            model.Error = "The code is not valid. Check the authenticator app and try again.";
        }
        else
        {
            await users.SetTwoFactorEnabledAsync(user, true);
            await _screen.SignInManager.RefreshSignInAsync(user);
            #pragma warning disable CS8604
            model.RecoveryCodes = (await users.GenerateNewTwoFactorRecoveryCodesAsync(user, RecoveryCodeCount)).ToList();
            #pragma warning restore CS8604
            model.Message = "Two-factor authentication is on. Keep these recovery codes somewhere safe: each works once, and they are not shown again.";
        }

        await LoadAsync(model);

        return ScreenOutcome.Show;
    }

    public async Task<ScreenOutcome> DisableAsync(TwoFactorViewModel model, string? password)
    {
        var users = _screen.UserManager;
        var user = await users.GetUserAsync(_screen.User);
        if (user == null) return ScreenOutcome.Challenge;

        if (!await _screen.IsConfirmedAsync(user, password))
        {
            model.Error = "The confirmation is incorrect, so nothing was changed.";
        }
        else
        {
            await users.SetTwoFactorEnabledAsync(user, false);
            await _screen.SignInManager.RefreshSignInAsync(user);
            model.Message = "Two-factor authentication is off.";
        }

        await LoadAsync(model);

        return ScreenOutcome.Show;
    }

    /// <summary>
    /// A new key makes the old authenticator and every recovery code useless; it must be
    /// set up and enabled again.
    /// </summary>
    public async Task<ScreenOutcome> ResetAsync(TwoFactorViewModel model, string? password)
    {
        var users = _screen.UserManager;
        var user = await users.GetUserAsync(_screen.User);
        if (user == null) return ScreenOutcome.Challenge;

        if (!await _screen.IsConfirmedAsync(user, password))
        {
            model.Error = "The confirmation is incorrect, so nothing was changed.";
        }
        else
        {
            await users.SetTwoFactorEnabledAsync(user, false);
            await users.ResetAuthenticatorKeyAsync(user);
            await _screen.SignInManager.RefreshSignInAsync(user);
            model.Message = "The authenticator was reset. Scan the new code to turn two-factor authentication on again.";
        }

        await LoadAsync(model);

        return ScreenOutcome.Show;
    }

    private async Task<bool> LoadAsync(TwoFactorViewModel model)
    {
        var users = _screen.UserManager;
        var user = await users.GetUserAsync(_screen.User);
        if (user == null) return false;

        model.TwoFactorEnabled = user.TwoFactorEnabled;
        model.HasPassword = await users.HasPasswordAsync(user);
        model.Reauthenticated = !model.HasPassword && await _screen.HasReauthenticatedRecentlyAsync();
        model.CanReauthenticate = _screen.Accounts.OpenIdConnect.IsConfigured;
        model.RecoveryCodesLeft = await users.CountRecoveryCodesAsync(user);

        if (!model.TwoFactorEnabled)
        {
            var key = await users.GetAuthenticatorKeyAsync(user);
            if (string.IsNullOrEmpty(key))
            {
                await users.ResetAuthenticatorKeyAsync(user);

                // The security stamp changed with the key; keep this session signed in.
                await _screen.SignInManager.RefreshSignInAsync(user);
                key = await users.GetAuthenticatorKeyAsync(user);
            }

            model.SharedKey = Format(key);
            model.QrCodeSvg = RenderQrCode(SetupUri(user.UserName, key));
        }

        return true;
    }

    private static string Format(string? key)
    {
        var result = new StringBuilder();
        #pragma warning disable CS8602
        for (var i = 0; i < key.Length; i += 4)
        #pragma warning restore CS8602
        {
            if (i > 0) result.Append(' ');
            result.Append(key, i, Math.Min(4, key.Length - i));
        }

        return result.ToString();
    }

    #pragma warning disable CS8604
    internal static string SetupUri(string? userName, string? key)
        => $"otpauth://totp/{Uri.EscapeDataString(Issuer)}:{Uri.EscapeDataString(userName)}"
           + $"?secret={key}&issuer={Uri.EscapeDataString(Issuer)}&digits=6";
    #pragma warning restore CS8604

    internal static string RenderQrCode(string text)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.M);

        return new SvgQRCode(data).GetGraphic(4);
    }
}

/// <summary>
/// The user's API keys: list, create, revoke. Shared by <c>AccountSettingsController</c> and the obsolete <c>ApiKeysModel</c>.
/// </summary>
internal sealed class ApiKeysWorkflow
{
    private readonly AccountScreen _screen;

    public ApiKeysWorkflow(AccountScreen screen)
    {
        _screen = screen ?? throw new ArgumentNullException(nameof(screen));
    }

    /// <summary>The screen was requested.</summary>
    public async Task<ScreenOutcome> ShowAsync(ApiKeysViewModel model, CancellationToken cancellationToken)
    {
        await LoadAsync(model, cancellationToken);

        return ScreenOutcome.Show;
    }

    /// <summary>The new-key form was posted.</summary>
    public async Task<ScreenOutcome> CreateAsync(ApiKeysViewModel model, CancellationToken cancellationToken)
    {
        var modelState = _screen.ModelState;

        if (model.Scopes.Count == 0)
        {
            modelState.AddModelError(nameof(model.Scopes), "Choose at least one scope.");
        }

        var administrator = await IsAdministratorAsync(cancellationToken);
        model.AllScopes = ScopesFor(administrator);
        if (modelState.IsValid)
        {
            // Only a repository the user can read may be chosen: a key limited to one nobody can see is a probe.
            await LoadRepositoriesAsync(model, cancellationToken);
            if (model.RepositoryKeys.Except(model.Repositories.Select(r => r.Key)).Any())
            {
                modelState.AddModelError(nameof(model.RepositoryKeys), "Choose only repositories from the list.");
            }
        }

        if (modelState.IsValid)
        {
            var now = _screen.UtcNow;

            try
            {
                model.CreatedKey = await _screen.ApiKeys.CreateAsync(
                    _screen.UserManager.GetUserId(_screen.User),
                    model.Description,
                    model.Scopes,
                    model.PackageGlobPattern,
                    model.ExpiresInDays.HasValue ? now.AddDays(model.ExpiresInDays.Value) : (DateTime?)null,
                    model.RepositoryKeys,
                    cancellationToken);
            }
            catch (ArgumentException e)
            {
                modelState.AddModelError(nameof(model.Scopes), e.Message);
            }
        }

        await LoadAsync(model, cancellationToken);

        return ScreenOutcome.Show;
    }

    /// <summary>A key was revoked.</summary>
    public async Task<ScreenOutcome> RevokeAsync(int key, CancellationToken cancellationToken)
    {
        await _screen.ApiKeys.RevokeAsync(_screen.UserManager.GetUserId(_screen.User), key, cancellationToken);

        return ScreenOutcome.ToPage("/Account/ApiKeys");
    }

    private Task<bool> IsAdministratorAsync(CancellationToken cancellationToken)
        => _screen.Services.GetRequiredService<AdministratorService>().IsAdministratorAsync(_screen.User, cancellationToken);

    private static IReadOnlyList<string> ScopesFor(bool administrator) => administrator ? ApiKeyScopes.Assignable : ApiKeyScopes.All;

    private async Task LoadAsync(ApiKeysViewModel model, CancellationToken cancellationToken)
    {
        model.AllScopes = ScopesFor(await IsAdministratorAsync(cancellationToken));
        model.Keys = await _screen.ApiKeys.ListAsync(_screen.UserManager.GetUserId(_screen.User), cancellationToken);
        model.KeyRepositories = await _screen.ApiKeys.RepositoryNamesAsync(model.Keys, cancellationToken);
        await LoadRepositoriesAsync(model, cancellationToken);
    }

    private async Task LoadRepositoriesAsync(ApiKeysViewModel model, CancellationToken cancellationToken)
    {
        var access = _screen.Services.GetService<IRepositoryAccess>();
        model.Repositories = access == null || !access.SupportsMultipleRepositories
            ? Array.Empty<RepositoryInfo>()
            : await access.ReadableAsync(_screen.User, cancellationToken);
    }
}
