using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.Mvc;
using BaGet.Identity.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BaGet.Identity.Workflows;

/// <summary>
/// The rules of the sign-in screen (password, external provider and its callback), run by <c>AccountController</c>. Messages, lockout handling, the pending-approval answer and
/// the local-only return URL are exactly those of the page.
/// </summary>
internal sealed class LoginWorkflow
{
    private const string PendingMessage = "Your account is waiting for an administrator to enable it. Try again once it has been approved.";
    private const string LockedMessage = "The account is locked after too many failed attempts. Try again later.";
    private const string UnconfirmedMessage = "Confirm your email address before signing in. Use the link we sent you, or reset your password.";

    private readonly AccountScreen _screen;

    public LoginWorkflow(AccountScreen screen)
    {
        _screen = screen ?? throw new ArgumentNullException(nameof(screen));
    }

    /// <summary>Fills what the screen shows from the configuration, and asks to render it.</summary>
    public ScreenOutcome Render(LoginViewModel model)
    {
        var accounts = _screen.Accounts;
        model.AllowRegistration = accounts.AllowRegistration;
        model.HasExternalProvider = accounts.OpenIdConnect.IsConfigured;
        model.ExternalProviderName = accounts.OpenIdConnect.DisplayName;
        model.EmailIsConfigured = _screen.EmailConfigured;

        return ScreenOutcome.Show;
    }

    /// <summary>The password form was posted.</summary>
    public async Task<ScreenOutcome> PostAsync(LoginViewModel model)
    {
        if (!_screen.ModelState.IsValid) return Render(model);

        #pragma warning disable CS8604
        var result = await _screen.SignInManager.PasswordSignInAsync(model.UserName, model.Password, model.RememberMe, lockoutOnFailure: true);
        #pragma warning restore CS8604
        if (result.Succeeded)
        {
            return ScreenOutcome.Local(model.ReturnUrl);
        }

        if (result.RequiresTwoFactor)
        {
            return ScreenOutcome.ToPage("/Account/LoginWith2fa", new { model.ReturnUrl, model.RememberMe });
        }

        if (result.IsNotAllowed && await FindPendingAsync(model) is not null)
        {
            // For an account awaiting approval the sign-in manager answers "not allowed" only to the right password
            // (a wrong one is a counted failure, a locked-out account answers "locked out"), so this message is
            // shown to the right password only and never reads like an unconfirmed email.
            model.Error = PendingMessage;

            return Render(model);
        }

        model.Error = result.IsLockedOut
            ? LockedMessage
            : result.IsNotAllowed
                ? UnconfirmedMessage
                : "The user name or password is incorrect.";

        return Render(model);
    }

    private async Task<BaGetUser?> FindPendingAsync(LoginViewModel model)
    {
        if (string.IsNullOrEmpty(model.UserName) || string.IsNullOrEmpty(model.Password)) return null;

        var user = await _screen.UserManager.FindByNameAsync(model.UserName);

        return user is { PendingApproval: true } ? user : null;
    }

    /// <summary>The "sign in with the provider" button was posted.</summary>
    public ScreenOutcome External(LoginViewModel model)
    {
        if (!_screen.Accounts.OpenIdConnect.IsConfigured) return ScreenOutcome.NotFound;

        var callback = _screen.Url.Page("/Account/Login", "Callback", new { model.ReturnUrl });
        var properties = _screen.SignInManager.ConfigureExternalAuthenticationProperties(
            AccountsServiceCollectionExtensions.OpenIdConnectScheme,
            callback);

        return ScreenOutcome.ChallengeWith(AccountsServiceCollectionExtensions.OpenIdConnectScheme, properties);
    }

    /// <summary>
    /// The provider has signed the user in. The first time, an account is created for
    /// them: the provider is the gate, so this does not depend on AllowRegistration.
    /// </summary>
    public async Task<ScreenOutcome> CallbackAsync(LoginViewModel model, CancellationToken cancellationToken)
    {
        var signIn = _screen.SignInManager;
        var users = _screen.UserManager;

        var info = await signIn.GetExternalLoginInfoAsync();
        if (info == null)
        {
            model.Error = "The external sign-in failed.";
            return Render(model);
        }

        var result = await signIn.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: false);
        if (result.Succeeded)
        {
            return ScreenOutcome.Local(model.ReturnUrl);
        }

        if (result.RequiresTwoFactor)
        {
            return ScreenOutcome.ToPage("/Account/LoginWith2fa", new { model.ReturnUrl });
        }

        if (result.IsLockedOut)
        {
            model.Error = LockedMessage;
            return Render(model);
        }

        if (result.IsNotAllowed)
        {
            // This provider login already belongs to an account, which cannot sign in yet
            // (its email is unconfirmed). Never make another account for it.
            var existing = await users.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
            if (existing is { PendingApproval: true })
            {
                model.Error = PendingMessage;

                return Render(model);
            }

            if (existing != null && await TrySendConfirmationAsync(existing))
            {
                model.Notice = "Confirm your email address before signing in. We sent you a new confirmation link.";
            }
            else
            {
                model.Error = UnconfirmedMessage;
            }

            return Render(model);
        }

        var email = info.Principal.FindFirstValue(ClaimTypes.Email);
        if (_screen.EmailConfigured && string.IsNullOrWhiteSpace(email))
        {
            model.Error = "The external account has no email address, which this server needs to confirm accounts.";
            return Render(model);
        }

        var name = await ChooseUserNameAsync(info.Principal, cancellationToken);
        if (name == null)
        {
            model.Error = "The external account has no usable name.";
            return Render(model);
        }

        var user = new BaGetUser
        {
            UserName = name,
            Email = email,

            // Only an address the provider vouches for is confirmed; any other must be
            // proven by following a link, like one typed into the registration form.
            EmailConfirmed = !string.IsNullOrWhiteSpace(email) && ProviderVerifiedEmail(info.Principal),
            Created = _screen.UtcNow,
            PendingApproval = _screen.Accounts.RequireApproval,
        };

        var created = await users.CreateAsync(user);
        if (created.Succeeded)
        {
            created = await users.AddLoginAsync(user, info);
        }

        if (!created.Succeeded)
        {
            // Do not leave an account behind that has no login to reach it.
            if (user.Id != null && await users.FindByIdAsync(user.Id) != null) await users.DeleteAsync(user);

            model.Error = string.Join(" ", created.Errors.Select(e => e.Description));
            return Render(model);
        }

        if (user.PendingApproval)
        {
            // Approval does not replace the address check: send the link now, so approving is the last step.
            if (_screen.EmailConfigured && !user.EmailConfirmed) await TrySendConfirmationAsync(user);

            model.Notice = "Your account was created. An administrator must enable it before you can sign in.";

            return Render(model);
        }

        if (_screen.EmailConfigured && !user.EmailConfirmed)
        {
            model.Notice = await TrySendConfirmationAsync(user)
                ? "Your account was created. Confirm your email address with the link we sent you, then sign in again."
                : "Your account was created, but we could not send the confirmation email. Use \"Forgot password\" to get a link later.";
            return Render(model);
        }

        await signIn.SignInAsync(user, isPersistent: false);

        return ScreenOutcome.Local(model.ReturnUrl);
    }

    private static bool ProviderVerifiedEmail(ClaimsPrincipal principal)
        => bool.TryParse(principal.FindFirstValue("email_verified"), out var verified) && verified;

    private async Task<bool> TrySendConfirmationAsync(BaGetUser user)
    {
        if (!_screen.EmailConfigured || string.IsNullOrEmpty(user.Email) || user.EmailConfirmed) return false;

        try
        {
            var token = await _screen.UserManager.GenerateEmailConfirmationTokenAsync(user);
            var link = _screen.PageLink("/Account/ConfirmEmail", new { userId = user.Id, code = AccountScreen.EncodeToken(token) });
            await _screen.EmailSender.SendConfirmationLinkAsync(user, user.Email, link);

            return true;
        }
        catch (Exception e)
        {
            _screen.Services.GetRequiredService<ILogger<LoginWorkflow>>()
                .LogError(e, "Could not send a confirmation link.");

            return false;
        }
    }

    /// <summary>
    /// The provider's preferred user name, else the email's local part, made unique
    /// across users and organizations by a numeric suffix.
    /// </summary>
    private async Task<string?> ChooseUserNameAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        var candidate = principal.FindFirstValue("preferred_username")
                        ?? principal.FindFirstValue(ClaimTypes.Email)?.Split('@')[0]
                        ?? principal.FindFirstValue(ClaimTypes.Name);

        if (string.IsNullOrWhiteSpace(candidate)) return null;

        var allowed = _screen.UserManager.Options.User.AllowedUserNameCharacters;
        candidate = new string(candidate.Where(c => allowed.Contains(c)).ToArray());
        if (candidate.Length == 0) return null;

        var name = candidate;
        for (var suffix = 2; await _screen.Ownership.AccountExistsAsync(name, cancellationToken); suffix++)
        {
            name = candidate + suffix;
        }

        return name;
    }
}
