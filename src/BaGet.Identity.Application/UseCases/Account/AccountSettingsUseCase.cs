using System;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.Application.Screens;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using BaGet.Identity.Application.Links;
using BaGet.Identity.Application.Models.Settings;
using BaGet.Identity.Application.Security;
using BaGet.Identity.Application.Services.Accounts;

namespace BaGet.Identity.Application.UseCases.Account;

/// <summary>
/// The signed-in user's account settings: change the password, change the email address, delete the account. Each form has its own method and
/// takes its fields as parameters, so one form's rules never block another's. A sensitive action asks the password again (an account without one
/// signs in again with its provider: <see cref="AccountConfirmation"/>). Cookie sessions only: an API key never changes credentials.
/// </summary>
public sealed class AccountSettingsUseCase
{
    private readonly UserManager<BaGetUser> _users;
    private readonly SignInManager<BaGetUser> _signIn;
    private readonly AccountDeletionService _deletion;
    private readonly IHttpContextAccessor _http;
    private readonly IOptions<AccountsOptions> _accounts;
    private readonly IOptions<EmailOptions> _email;
    private readonly IOptions<SiteOptions> _site;
    private readonly IEmailSender<BaGetUser> _sender;

    /// <summary>Creates the use case.</summary>
    public AccountSettingsUseCase(
        UserManager<BaGetUser> users,
        SignInManager<BaGetUser> signIn,
        AccountDeletionService deletion,
        IHttpContextAccessor http,
        IOptions<AccountsOptions> accounts,
        IOptions<EmailOptions> email,
        IOptions<SiteOptions> site,
        IEmailSender<BaGetUser> sender)
    {
        _users = users ?? throw new ArgumentNullException(nameof(users));
        _signIn = signIn ?? throw new ArgumentNullException(nameof(signIn));
        _deletion = deletion ?? throw new ArgumentNullException(nameof(deletion));
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _accounts = accounts ?? throw new ArgumentNullException(nameof(accounts));
        _email = email ?? throw new ArgumentNullException(nameof(email));
        _site = site ?? throw new ArgumentNullException(nameof(site));
        _sender = sender ?? throw new ArgumentNullException(nameof(sender));
    }

    private bool EmailConfigured => _email.Value.IsConfigured;

    private HttpContext Http => _http.HttpContext ?? throw new InvalidOperationException("The account settings need a request.");

    /// <summary>The settings screen.</summary>
    public async Task<ScreenResult<SettingsViewModel>> ShowAsync(ClaimsPrincipal caller)
    {
        var model = new SettingsViewModel();

        return await LoadAsync(caller, model) ? ScreenResult<SettingsViewModel>.Ok(model) : ScreenResult<SettingsViewModel>.SignIn();
    }

    /// <summary>Change the password.</summary>
    public async Task<ScreenResult<SettingsViewModel>> ChangePasswordAsync(ClaimsPrincipal caller, ChangePasswordForm? form)
    {
        var user = await _users.GetUserAsync(caller);
        if (user == null) return ScreenResult<SettingsViewModel>.SignIn();

        var model = new SettingsViewModel();
        var newPassword = form?.NewPassword;

        if (string.IsNullOrEmpty(newPassword) || newPassword != form?.ConfirmPassword)
        {
            model.Error = "The new passwords do not match.";
        }
        else
        {
            var hasPassword = await _users.HasPasswordAsync(user);

            // Adding the first password to an external-only account would turn a stolen
            // session into a password for it, so it needs the same fresh proof.
            var result = hasPassword
                ? await _users.ChangePasswordAsync(user, form!.CurrentPassword ?? string.Empty, newPassword)
                : await AccountConfirmation.HasReauthenticatedRecentlyAsync(Http)
                    ? await _users.AddPasswordAsync(user, newPassword)
                    : IdentityResult.Failed(new IdentityError { Description = "Sign in again with your provider first, then set the password." });

            if (result.Succeeded)
            {
                // The security stamp changed; keep this session signed in.
                await _signIn.RefreshSignInAsync(user);
                model.Message = "Your password has been changed.";
            }
            else
            {
                model.Error = AccountConfirmation.Describe(result);
            }
        }

        return await DoneAsync(caller, model);
    }

    /// <summary>
    /// Change the email address. The password is asked again, like for deleting the account: the address is where password resets go, so a
    /// stolen session must not be able to repoint it.
    /// </summary>
    public async Task<ScreenResult<SettingsViewModel>> ChangeEmailAsync(ClaimsPrincipal caller, ChangeEmailForm? form)
    {
        var user = await _users.GetUserAsync(caller);
        if (user == null) return ScreenResult<SettingsViewModel>.SignIn();

        var model = new SettingsViewModel();
        var newEmail = form?.NewEmail?.Trim();

        if (!await AccountConfirmation.IsConfirmedAsync(_users, Http, user, form?.Password))
        {
            model.Error = "The confirmation is incorrect, so the email address was not changed.";
        }
        else if (string.IsNullOrEmpty(newEmail) || !new EmailAddressAttribute().IsValid(newEmail))
        {
            model.Error = "Enter a valid email address.";
        }
        else if (!EmailConfigured)
        {
            var result = await _users.SetEmailAsync(user, newEmail);
            if (result.Succeeded)
            {
                await _signIn.RefreshSignInAsync(user);
                model.Message = "Your email address has been changed.";
            }
            else model.Error = AccountConfirmation.Describe(result);
        }
        else
        {
            var token = await _users.GenerateChangeEmailTokenAsync(user, newEmail);
            var link = AccountLinks.ConfirmEmail(_site, user.Id, token, newEmail);

            try
            {
                await _sender.SendConfirmationLinkAsync(user, newEmail, link);
                model.Message = "We sent a confirmation link to the new address. Your address changes when you follow it.";
            }
            catch (Exception)
            {
                model.Error = "We could not send the confirmation email. Try again later.";
            }
        }

        return await DoneAsync(caller, model);
    }

    /// <summary>
    /// Delete the account. The password is asked again, so a stolen session cannot delete it. An account without a password (external sign-in
    /// only) signs in again with its provider. Success signs the user out and continues at the site root.
    /// </summary>
    public async Task<ScreenResult<SettingsViewModel>> DeleteAsync(ClaimsPrincipal caller, PasswordConfirmationForm? form, CancellationToken cancellationToken)
    {
        var user = await _users.GetUserAsync(caller);
        if (user == null) return ScreenResult<SettingsViewModel>.SignIn();

        var model = new SettingsViewModel();

        if (!await AccountConfirmation.IsConfirmedAsync(_users, Http, user, form?.Password))
        {
            model.Error = "The confirmation is incorrect, so the account was not deleted.";
        }
        else
        {
            var (blockers, result) = await _deletion.DeleteAsync(user, cancellationToken);

            if (blockers.Any)
            {
                model.Blockers = blockers;
                model.Error = "The account cannot be deleted yet.";
            }
            else if (!result.Succeeded)
            {
                model.Error = AccountConfirmation.Describe(result);
            }
            else
            {
                await _signIn.SignOutAsync();

                return ScreenResult<SettingsViewModel>.GoTo(ScreenNext.Local(LocalUrls.Root));
            }
        }

        return await DoneAsync(caller, model);
    }

    // The screen again with what the action said: a refusal is Invalid (the model carries the message the page prints), anything else Ok.
    private async Task<ScreenResult<SettingsViewModel>> DoneAsync(ClaimsPrincipal caller, SettingsViewModel model)
    {
        await LoadAsync(caller, model);

        return model.Error == null ? ScreenResult<SettingsViewModel>.Ok(model) : ScreenResult<SettingsViewModel>.Invalid(model, model.Error);
    }

    private async Task<bool> LoadAsync(ClaimsPrincipal caller, SettingsViewModel model)
    {
        var user = await _users.GetUserAsync(caller);
        if (user == null) return false;

        model.Email = user.Email;
        model.EmailConfirmed = user.EmailConfirmed;
        model.HasPassword = await _users.HasPasswordAsync(user);
        model.Reauthenticated = !model.HasPassword && await AccountConfirmation.HasReauthenticatedRecentlyAsync(Http);
        model.CanReauthenticate = _accounts.Value.OpenIdConnect.IsConfigured;
        model.CanSendEmail = EmailConfigured;

        return true;
    }
}
