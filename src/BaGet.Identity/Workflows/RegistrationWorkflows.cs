using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.Mvc;
using BaGet.Identity.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BaGet.Identity.Workflows;

/// <summary>
/// Creating an account. Shared by <c>AccountController</c> and the obsolete <c>RegisterModel</c>. The screen itself answers 404
/// while <c>Accounts:AllowRegistration</c> is off: that check is made by the host before this runs.
/// </summary>
internal sealed class RegisterWorkflow
{
    private readonly AccountScreen _screen;

    public RegisterWorkflow(AccountScreen screen)
    {
        _screen = screen ?? throw new ArgumentNullException(nameof(screen));
    }

    /// <summary>Fills what the screen shows from the configuration, and asks to render it.</summary>
    public ScreenOutcome Render(RegisterViewModel model)
    {
        model.EmailRequired = _screen.EmailConfigured;

        return ScreenOutcome.Show;
    }

    /// <summary>The form was posted.</summary>
    public async Task<ScreenOutcome> PostAsync(RegisterViewModel model, CancellationToken cancellationToken)
    {
        var modelState = _screen.ModelState;

        if (_screen.EmailConfigured && string.IsNullOrWhiteSpace(model.Email))
        {
            modelState.AddModelError(nameof(model.Email), "An email address is required to confirm the account.");
        }

        if (!modelState.IsValid) return Render(model);

        // User and organization names share a namespace: either can own a package.
        if (await _screen.Ownership.AccountExistsAsync(model.UserName, cancellationToken))
        {
            modelState.AddModelError(nameof(model.UserName), "That name is taken.");
            return Render(model);
        }

        var user = new BaGetUser
        {
            UserName = model.UserName,
            Email = model.Email,
            Created = _screen.UtcNow,
            PendingApproval = _screen.Accounts.RequireApproval,
        };

        #pragma warning disable CS8604
        var result = await _screen.UserManager.CreateAsync(user, model.Password);
        #pragma warning restore CS8604
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                modelState.AddModelError(string.Empty, error.Description);
            }

            return Render(model);
        }

        if (user.PendingApproval)
        {
            _screen.Services.GetRequiredService<ILogger<RegisterWorkflow>>()
                .LogWarning("{User} registered and awaits approval", user.UserName);
        }

        if (_screen.EmailConfigured)
        {
            // Signing in needs a confirmed address, so the user is not signed in yet.
            var token = await _screen.UserManager.GenerateEmailConfirmationTokenAsync(user);
            var link = _screen.PageLink("/Account/ConfirmEmail", new { userId = user.Id, code = AccountScreen.EncodeToken(token) });

            try
            {
                #pragma warning disable CS8604
                await _screen.EmailSender.SendConfirmationLinkAsync(user, user.Email, link);
                #pragma warning restore CS8604
                model.ConfirmationSent = true;
                model.AwaitingApproval = user.PendingApproval;
            }
            catch (Exception)
            {
                // The account exists; "Forgot password" mails a link that also confirms the address.
                modelState.AddModelError(string.Empty, "Your account was created, but we could not send the confirmation email. Use \"Forgot password\" to get a link later."
                    + (user.PendingApproval ? " An administrator must also enable your account before you can sign in." : string.Empty));
            }

            return Render(model);
        }

        if (user.PendingApproval)
        {
            // Not signed in: an administrator enables the account first.
            model.AwaitingApproval = true;

            return Render(model);
        }

        await _screen.SignInManager.SignInAsync(user, isPersistent: false);

        return ScreenOutcome.Local(model.ReturnUrl);
    }
}

/// <summary>
/// Asking for a password reset link. Shared by <c>AccountController</c> and the obsolete <c>ForgotPasswordModel</c>.
/// </summary>
internal sealed class ForgotPasswordWorkflow
{
    private readonly AccountScreen _screen;

    public ForgotPasswordWorkflow(AccountScreen screen)
    {
        _screen = screen ?? throw new ArgumentNullException(nameof(screen));
    }

    /// <summary>
    /// Answers the same, in the same time, whether or not an account matches, whether or
    /// not the mail could be sent and whether or not the request was rate limited, so the
    /// page cannot be used to find out who has an account. The mail itself is sent off the
    /// request path.
    /// </summary>
    public async Task<ScreenOutcome> PostAsync(ForgotPasswordViewModel model)
    {
        if (!_screen.ModelState.IsValid) return ScreenOutcome.Show;

        var identifier = model.Identifier?.Trim();
        var users = _screen.UserManager;

        // Both lookups always run, so the work done does not depend on which one matches.
        #pragma warning disable CS8604
        var byName = await users.FindByNameAsync(identifier);
        #pragma warning restore CS8604
        BaGetUser? byEmail = null;
        try
        {
            byEmail = await users.FindByEmailAsync(identifier);
        }
        catch (InvalidOperationException)
        {
            // Several accounts share the address: none is singled out.
        }

        var user = byName ?? byEmail;

        // One link template either way; the user and token are filled in later.
        var template = _screen.PageLink("/Account/ResetPassword", PasswordResetLinkSender.Template);

        // Only an account that would be mailed uses up an allowance, so requests for names
        // that do not exist can neither fill the limiter nor crowd real accounts out of it.
        // The per-account limit is the protection; the client and site limits are backstops.
        var client = _screen.HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
        if (user != null
            && !string.IsNullOrEmpty(user.Email)
            && _screen.Services.GetRequiredService<PasswordResetRateLimiter>().TryAcquire(client, user.Id))
        {
            PasswordResetLinkSender.TryEnqueue(_screen.Services, user.Id, template);
        }

        model.Sent = true;

        return ScreenOutcome.Show;
    }
}

/// <summary>
/// Setting a new password from the link of a reset email. Shared by <c>AccountController</c> and the obsolete
/// <c>ResetPasswordModel</c>.
/// </summary>
internal sealed class ResetPasswordWorkflow
{
    private const string InvalidLink = "This link is invalid, expired or already used.";

    private readonly AccountScreen _screen;

    public ResetPasswordWorkflow(AccountScreen screen)
    {
        _screen = screen ?? throw new ArgumentNullException(nameof(screen));
    }

    /// <summary>The link was followed.</summary>
    public ScreenOutcome Show(ResetPasswordViewModel model)
    {
        if (!model.LinkIsUsable) model.Error = "This link is invalid.";

        return ScreenOutcome.Show;
    }

    /// <summary>The new password was posted.</summary>
    public async Task<ScreenOutcome> PostAsync(ResetPasswordViewModel model)
    {
        if (!_screen.ModelState.IsValid) return ScreenOutcome.Show;

        var users = _screen.UserManager;

        #pragma warning disable CS8604
        var user = model.LinkIsUsable ? await users.FindByIdAsync(model.UserId) : null;
        #pragma warning restore CS8604
        var token = model.LinkIsUsable ? AccountScreen.DecodeToken(model.Code) : null;
        if (user == null || token == null)
        {
            model.Error = InvalidLink;
            return ScreenOutcome.Show;
        }

        #pragma warning disable CS8604
        var result = await users.ResetPasswordAsync(user, token, model.Password);
        #pragma warning restore CS8604
        if (!result.Succeeded)
        {
            model.Error = result.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.InvalidToken))
                ? InvalidLink
                : AccountScreen.Describe(result);
            return ScreenOutcome.Show;
        }

        // The link went to this address and was followed, which confirms it.
        if (!user.EmailConfirmed && !string.IsNullOrEmpty(user.Email))
        {
            await users.ConfirmEmailAsync(user, await users.GenerateEmailConfirmationTokenAsync(user));
        }

        return ScreenOutcome.ToPage("/Account/Login");
    }
}

/// <summary>
/// The target of the links in confirmation emails: a new account's address, or, when <c>email</c> is present, a change of
/// address. Shared by <c>AccountController</c> and the obsolete <c>ConfirmEmailModel</c>.
/// </summary>
internal sealed class ConfirmEmailWorkflow
{
    private readonly AccountScreen _screen;

    public ConfirmEmailWorkflow(AccountScreen screen)
    {
        _screen = screen ?? throw new ArgumentNullException(nameof(screen));
    }

    /// <summary>The link was followed.</summary>
    public async Task<ScreenOutcome> ConfirmAsync(ConfirmEmailViewModel model, string? userId, string? code, string? email)
    {
        var users = _screen.UserManager;

        var user = string.IsNullOrEmpty(userId) ? null : await users.FindByIdAsync(userId);
        var token = string.IsNullOrEmpty(code) ? null : AccountScreen.DecodeToken(code);
        if (user == null || token == null) return ScreenOutcome.Show;

        IdentityResult result;
        if (string.IsNullOrEmpty(email))
        {
            result = await users.ConfirmEmailAsync(user, token);
        }
        else
        {
            result = await users.ChangeEmailAsync(user, email, token);
            model.EmailChanged = result.Succeeded;
        }

        model.Succeeded = result.Succeeded;
        model.PendingApproval = model.Succeeded && user.PendingApproval;

        // The security stamp changed with the address; keep the session of the same user.
        if (model.Succeeded && _screen.User.Identity?.IsAuthenticated == true && users.GetUserId(_screen.User) == user.Id)
        {
            await _screen.SignInManager.RefreshSignInAsync(user);
        }

        return ScreenOutcome.Show;
    }
}

/// <summary>
/// Proof of presence for an account without a password: a fresh sign-in with its external provider (the provider is asked to
/// prompt for credentials again, whatever session it has). The time is kept in the sign-in cookie's properties and checked by
/// <see cref="AccountSupport.IsConfirmedAsync"/> for <see cref="AccountSupport.ReauthenticationWindow"/>. Shared by
/// <c>AccountController</c> and the obsolete <c>ReauthenticateModel</c>.
/// </summary>
internal sealed class ReauthenticateWorkflow
{
    internal const string AuthTimeClaim = "auth_time";

    /// <summary>Allowed difference between the provider's clock and ours.</summary>
    internal static readonly TimeSpan ClockSkew = TimeSpan.FromMinutes(1);

    private readonly AccountScreen _screen;

    public ReauthenticateWorkflow(AccountScreen screen)
    {
        _screen = screen ?? throw new ArgumentNullException(nameof(screen));
    }

    /// <summary>The "sign in again" button was posted.</summary>
    public async Task<ScreenOutcome> PostAsync(ReauthenticateViewModel model)
    {
        var user = await _screen.UserManager.GetUserAsync(_screen.User);
        if (user == null) return ScreenOutcome.Challenge;

        if (!_screen.Accounts.OpenIdConnect.IsConfigured)
        {
            model.Error = "There is no external provider to sign in with.";
            return ScreenOutcome.Show;
        }

        var callback = _screen.Url.Page("/Account/Reauthenticate", "Callback", new { model.ReturnUrl });
        var properties = _screen.SignInManager.ConfigureExternalAuthenticationProperties(
            AccountsServiceCollectionExtensions.OpenIdConnectScheme,
            callback,
            user.Id);

        // Ask the provider to authenticate again whatever session it holds.
        properties.SetParameter(OpenIdConnectChallengeProperties.PromptKey, "login");
        properties.SetParameter(OpenIdConnectChallengeProperties.MaxAgeKey, TimeSpan.Zero);

        return ScreenOutcome.ChallengeWith(AccountsServiceCollectionExtensions.OpenIdConnectScheme, properties);
    }

    /// <summary>True when <paramref name="authTime"/> (seconds since the epoch) is present and within the window.</summary>
    internal static bool IsFresh(string? authTime, DateTime now)
    {
        if (!long.TryParse(authTime, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var seconds)
            || seconds < 0 || seconds > 253402300799L)
        {
            return false;
        }

        var age = now - DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime;

        return age >= -ClockSkew && age <= AccountSupport.ReauthenticationWindow;
    }

    /// <summary>The provider answered.</summary>
    public async Task<ScreenOutcome> CallbackAsync(ReauthenticateViewModel model)
    {
        var users = _screen.UserManager;
        var signIn = _screen.SignInManager;
        var httpContext = _screen.HttpContext;

        var user = await users.GetUserAsync(_screen.User);
        if (user == null) return ScreenOutcome.Challenge;

        // The anti-forgery value is the signed-in user's id, so an answer started by
        // someone else's session is not accepted.
        var info = await signIn.GetExternalLoginInfoAsync(user.Id);
        await httpContext.SignOutAsync(IdentityConstants.ExternalScheme);

        // It must be this account's own login, not any account at the provider.
        var owner = info == null ? null : await users.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
        if (owner == null || owner.Id != user.Id)
        {
            model.Error = "The sign-in did not match this account.";
            return ScreenOutcome.Show;
        }

        // The provider may ignore prompt=login / max_age (the handler does not enforce max_age),
        // so the id_token's own auth_time (required by OIDC Core when max_age is sent) must show
        // a sign-in within the window.
        var now = _screen.UtcNow;
        if (!IsFresh(info?.Principal?.FindFirst(AuthTimeClaim)?.Value, now))
        {
            model.Error = "The provider did not confirm a fresh sign-in. Sign in again and enter your credentials when asked.";
            return ScreenOutcome.Show;
        }

        var authentication = await httpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        var properties = authentication.Properties ?? new AuthenticationProperties();
        properties.Items[AccountSupport.ReauthenticatedItem] = now.Ticks.ToString();

        // What RefreshSignInAsync keeps, plus the new property; the stamp is unchanged.
        var kept = new[] { ClaimTypes.AuthenticationMethod, "amr" }
            .Select(type => authentication.Principal?.FindFirst(type))
            .Where(claim => claim != null)
            .ToList();
        #pragma warning disable CS8620
        await signIn.SignInWithClaimsAsync(user, properties, kept);
        #pragma warning restore CS8620

        return ScreenOutcome.Local(model.ReturnUrl);
    }
}
