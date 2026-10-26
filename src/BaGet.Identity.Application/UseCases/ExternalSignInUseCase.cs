using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BaGet.Identity.Application;

/// <summary>Where a browser is sent to talk to the external provider.</summary>
/// <param name="Scheme">The authentication scheme to challenge.</param>
/// <param name="Properties">The challenge's properties (callback address, anti-forgery value, prompt).</param>
public sealed record ExternalChallenge(string Scheme, AuthenticationProperties Properties);

/// <summary>
/// Signing in, and proving presence again, through the external (OpenID Connect) provider. These steps are <b>browser-only</b>: the provider
/// redirects the browser and answers into a cookie, so they have no JSON twin; the view controller sends the challenge and handles the callback.
/// The rules are the same ones the pages applied: the provider is the gate for a new account (registration need not be open), only a provider-verified
/// address is confirmed, an unconfirmed or pending account never gets a second account made for it, and a re-authentication must be this account's own
/// login with a fresh <c>auth_time</c>.
/// </summary>
public sealed class ExternalSignInUseCase
{
    /// <summary>The name of the OpenID Connect scheme <c>AddBaGetAccounts</c> registers.</summary>
    public const string OpenIdConnectScheme = "oidc";

    /// <summary>The id token claim that tells when the user last authenticated at the provider.</summary>
    public const string AuthTimeClaim = "auth_time";

    /// <summary>Allowed difference between the provider's clock and ours.</summary>
    public static readonly TimeSpan ClockSkew = TimeSpan.FromMinutes(1);

    private readonly UserManager<BaGetUser> _users;
    private readonly SignInManager<BaGetUser> _signIn;
    private readonly PackageOwnershipService _ownership;
    private readonly IEmailSender<BaGetUser> _sender;
    private readonly IHttpContextAccessor _http;
    private readonly IOptions<AccountsOptions> _accounts;
    private readonly IOptions<EmailOptions> _email;
    private readonly IOptions<SiteOptions> _site;
    private readonly SystemTime _time;
    private readonly SignInUseCase _signInScreens;
    private readonly ILogger<ExternalSignInUseCase> _logger;

    /// <summary>Creates the use case.</summary>
    public ExternalSignInUseCase(
        UserManager<BaGetUser> users,
        SignInManager<BaGetUser> signIn,
        PackageOwnershipService ownership,
        IEmailSender<BaGetUser> sender,
        IHttpContextAccessor http,
        IOptions<AccountsOptions> accounts,
        IOptions<EmailOptions> email,
        IOptions<SiteOptions> site,
        SystemTime time,
        SignInUseCase signInScreens,
        ILogger<ExternalSignInUseCase> logger)
    {
        _users = users ?? throw new ArgumentNullException(nameof(users));
        _signIn = signIn ?? throw new ArgumentNullException(nameof(signIn));
        _ownership = ownership ?? throw new ArgumentNullException(nameof(ownership));
        _sender = sender ?? throw new ArgumentNullException(nameof(sender));
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _accounts = accounts ?? throw new ArgumentNullException(nameof(accounts));
        _email = email ?? throw new ArgumentNullException(nameof(email));
        _site = site ?? throw new ArgumentNullException(nameof(site));
        _time = time ?? throw new ArgumentNullException(nameof(time));
        _signInScreens = signInScreens ?? throw new ArgumentNullException(nameof(signInScreens));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    private bool EmailConfigured => _email.Value.IsConfigured;

    private HttpContext Http => _http.HttpContext ?? throw new InvalidOperationException("External sign-in needs a request.");

    // ----- sign in with the provider

    /// <summary>The "sign in with the provider" button: the challenge to send, or null when no provider is configured (404).</summary>
    /// <param name="callbackUrl">The address of the callback handler of the sign-in screen (built by the view with its URL helper).</param>
    public ExternalChallenge? StartSignIn(string callbackUrl)
    {
        if (!_accounts.Value.OpenIdConnect.IsConfigured) return null;

        var properties = _signIn.ConfigureExternalAuthenticationProperties(OpenIdConnectScheme, callbackUrl);

        return new ExternalChallenge(OpenIdConnectScheme, properties);
    }

    /// <summary>
    /// The provider has signed the user in. The first time, an account is created for them: the provider is the gate, so this does not
    /// depend on <c>AllowRegistration</c>.
    /// </summary>
    public async Task<ScreenResult<LoginViewModel>> CompleteSignInAsync(string? returnUrl, CancellationToken cancellationToken)
    {
        var model = _signInScreens.Describe(new LoginViewModel { ReturnUrl = returnUrl });

        var info = await _signIn.GetExternalLoginInfoAsync();
        if (info == null) return Refuse(model, "The external sign-in failed.");

        var result = await _signIn.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: false);
        if (result.Succeeded)
        {
            return ScreenResult<LoginViewModel>.GoTo(ScreenNext.Local(LocalUrls.Target(returnUrl)));
        }

        if (result.RequiresTwoFactor)
        {
            return ScreenResult<LoginViewModel>.GoTo(new ScreenNext("/Account/LoginWith2fa", new Dictionary<string, object?> { ["ReturnUrl"] = returnUrl }));
        }

        if (result.IsLockedOut) return Refuse(model, SignInUseCase.LockedMessage);

        if (result.IsNotAllowed)
        {
            // This provider login already belongs to an account, which cannot sign in yet
            // (its email is unconfirmed). Never make another account for it.
            var existing = await _users.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
            if (existing is { PendingApproval: true }) return Refuse(model, SignInUseCase.PendingMessage);

            if (existing != null && await TrySendConfirmationAsync(existing))
            {
                model.Notice = "Confirm your email address before signing in. We sent you a new confirmation link.";

                return ScreenResult<LoginViewModel>.Ok(model);
            }

            return Refuse(model, SignInUseCase.UnconfirmedMessage);
        }

        var email = info.Principal.FindFirstValue(ClaimTypes.Email);
        if (EmailConfigured && string.IsNullOrWhiteSpace(email))
        {
            return Refuse(model, "The external account has no email address, which this server needs to confirm accounts.");
        }

        var name = await ChooseUserNameAsync(info.Principal, cancellationToken);
        if (name == null) return Refuse(model, "The external account has no usable name.");

        var user = new BaGetUser
        {
            UserName = name,
            Email = email,

            // Only an address the provider vouches for is confirmed; any other must be
            // proven by following a link, like one typed into the registration form.
            EmailConfirmed = !string.IsNullOrWhiteSpace(email) && ProviderVerifiedEmail(info.Principal),
            Created = _time.UtcNow,
            PendingApproval = _accounts.Value.RequireApproval,
        };

        var created = await _users.CreateAsync(user);
        if (created.Succeeded)
        {
            created = await _users.AddLoginAsync(user, info);
        }

        if (!created.Succeeded)
        {
            // Do not leave an account behind that has no login to reach it.
            if (user.Id != null && await _users.FindByIdAsync(user.Id) != null) await _users.DeleteAsync(user);

            return Refuse(model, string.Join(" ", created.Errors.Select(e => e.Description)));
        }

        if (user.PendingApproval)
        {
            // Approval does not replace the address check: send the link now, so approving is the last step.
            if (EmailConfigured && !user.EmailConfirmed) await TrySendConfirmationAsync(user);

            model.Notice = "Your account was created. An administrator must enable it before you can sign in.";

            return ScreenResult<LoginViewModel>.Ok(model);
        }

        if (EmailConfigured && !user.EmailConfirmed)
        {
            model.Notice = await TrySendConfirmationAsync(user)
                ? "Your account was created. Confirm your email address with the link we sent you, then sign in again."
                : "Your account was created, but we could not send the confirmation email. Use \"Forgot password\" to get a link later.";

            return ScreenResult<LoginViewModel>.Ok(model);
        }

        await _signIn.SignInAsync(user, isPersistent: false);

        return ScreenResult<LoginViewModel>.GoTo(ScreenNext.Local(LocalUrls.Target(returnUrl)));
    }

    private static ScreenResult<LoginViewModel> Refuse(LoginViewModel model, string message)
    {
        model.Error = message;

        return ScreenResult<LoginViewModel>.Invalid(model, message);
    }

    private static bool ProviderVerifiedEmail(ClaimsPrincipal principal)
        => bool.TryParse(principal.FindFirstValue("email_verified"), out var verified) && verified;

    private async Task<bool> TrySendConfirmationAsync(BaGetUser user)
    {
        if (!EmailConfigured || string.IsNullOrEmpty(user.Email) || user.EmailConfirmed) return false;

        try
        {
            var token = await _users.GenerateEmailConfirmationTokenAsync(user);
            var link = AccountLinks.ConfirmEmail(_site, user.Id, token);
            await _sender.SendConfirmationLinkAsync(user, user.Email, link);

            return true;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Could not send a confirmation link.");

            return false;
        }
    }

    /// <summary>
    /// The provider's preferred user name, else the email's local part, made unique across users and organizations by a numeric suffix.
    /// </summary>
    private async Task<string?> ChooseUserNameAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        var candidate = principal.FindFirstValue("preferred_username")
                        ?? principal.FindFirstValue(ClaimTypes.Email)?.Split('@')[0]
                        ?? principal.FindFirstValue(ClaimTypes.Name);

        if (string.IsNullOrWhiteSpace(candidate)) return null;

        var allowed = _users.Options.User.AllowedUserNameCharacters;
        candidate = new string(candidate.Where(c => allowed.Contains(c)).ToArray());
        if (candidate.Length == 0) return null;

        var name = candidate;
        for (var suffix = 2; await _ownership.AccountExistsAsync(name, cancellationToken); suffix++)
        {
            name = candidate + suffix;
        }

        return name;
    }

    // ----- proof of presence for an account without a password

    /// <summary>
    /// The "sign in again" button: a fresh sign-in with the provider (it is asked to prompt for credentials again, whatever session it has). The
    /// time is kept in the sign-in cookie's properties and checked by <see cref="AccountConfirmation.IsConfirmedAsync"/> for
    /// <see cref="AccountConfirmation.ReauthenticationWindow"/>.
    /// </summary>
    /// <param name="caller">The signed-in user.</param>
    /// <param name="callbackUrl">The address of the callback handler of the screen.</param>
    /// <param name="returnUrl">Where to continue after the provider answered.</param>
    /// <returns>The challenge to send, or a refusal screen (sign in, or "there is no external provider").</returns>
    public async Task<(ExternalChallenge? Challenge, ScreenResult<ReauthenticateViewModel>? Screen)> StartReauthenticationAsync(ClaimsPrincipal caller, string callbackUrl, string? returnUrl)
    {
        var user = await _users.GetUserAsync(caller);
        if (user == null) return (null, ScreenResult<ReauthenticateViewModel>.SignIn());

        if (!_accounts.Value.OpenIdConnect.IsConfigured)
        {
            const string Message = "There is no external provider to sign in with.";

            return (null, ScreenResult<ReauthenticateViewModel>.Invalid(new ReauthenticateViewModel { ReturnUrl = returnUrl, Error = Message }, Message));
        }

        var properties = _signIn.ConfigureExternalAuthenticationProperties(OpenIdConnectScheme, callbackUrl, user.Id);

        // Ask the provider to authenticate again whatever session it holds.
        properties.SetParameter(OpenIdConnectChallengeProperties.PromptKey, "login");
        properties.SetParameter(OpenIdConnectChallengeProperties.MaxAgeKey, TimeSpan.Zero);

        return (new ExternalChallenge(OpenIdConnectScheme, properties), null);
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

        return age >= -ClockSkew && age <= AccountConfirmation.ReauthenticationWindow;
    }

    /// <summary>The provider answered.</summary>
    public async Task<ScreenResult<ReauthenticateViewModel>> CompleteReauthenticationAsync(ClaimsPrincipal caller, string? returnUrl)
    {
        var model = new ReauthenticateViewModel { ReturnUrl = returnUrl };
        var httpContext = Http;

        var user = await _users.GetUserAsync(caller);
        if (user == null) return ScreenResult<ReauthenticateViewModel>.SignIn();

        // The anti-forgery value is the signed-in user's id, so an answer started by
        // someone else's session is not accepted.
        var info = await _signIn.GetExternalLoginInfoAsync(user.Id);
        await httpContext.SignOutAsync(IdentityConstants.ExternalScheme);

        // It must be this account's own login, not any account at the provider.
        var owner = info == null ? null : await _users.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
        if (owner == null || owner.Id != user.Id)
        {
            return Refuse(model, "The sign-in did not match this account.");
        }

        // The provider may ignore prompt=login / max_age (the handler does not enforce max_age),
        // so the id_token's own auth_time (required by OIDC Core when max_age is sent) must show
        // a sign-in within the window.
        var now = _time.UtcNow;
        if (!IsFresh(info?.Principal?.FindFirst(AuthTimeClaim)?.Value, now))
        {
            return Refuse(model, "The provider did not confirm a fresh sign-in. Sign in again and enter your credentials when asked.");
        }

        var authentication = await httpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        var properties = authentication.Properties ?? new AuthenticationProperties();
        properties.Items[AccountConfirmation.ReauthenticatedItem] = now.Ticks.ToString();

        // What RefreshSignInAsync keeps, plus the new property; the stamp is unchanged.
        var kept = new[] { ClaimTypes.AuthenticationMethod, "amr" }
            .Select(type => authentication.Principal?.FindFirst(type))
            .Where(claim => claim != null)
            .ToList();
        #pragma warning disable CS8620
        await _signIn.SignInWithClaimsAsync(user, properties, kept);
        #pragma warning restore CS8620

        return ScreenResult<ReauthenticateViewModel>.GoTo(ScreenNext.Local(LocalUrls.Target(returnUrl)));
    }

    private static ScreenResult<ReauthenticateViewModel> Refuse(ReauthenticateViewModel model, string message)
    {
        model.Error = message;

        return ScreenResult<ReauthenticateViewModel>.Invalid(model, message);
    }

    /// <summary>The "confirm it is you" screen.</summary>
    public static ScreenResult<ReauthenticateViewModel> ShowReauthenticate(string? returnUrl)
        => ScreenResult<ReauthenticateViewModel>.Ok(new ReauthenticateViewModel { ReturnUrl = returnUrl });
}
