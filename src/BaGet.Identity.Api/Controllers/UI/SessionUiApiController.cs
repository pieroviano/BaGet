using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Frontend.Controllers.Base;
using BaGet.Frontend.Mvc.UI;
using BaGet.Identity.Application.Models.Session;
using BaGet.Identity.Application.Screens;
using BaGet.Identity.Application.Security;
using BaGet.Identity.Application.UseCases.Account;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace BaGet.Identity.Controllers.UI;

/// <summary>
/// Interactive sessions for a single-page application (<c>/api/ui/session</c>, <c>/api/ui/antiforgery</c>): sign in with the password (and the second
/// factor), refresh, sign out, the external provider, and the antiforgery token for a same-origin client that uses the cookie. These are not the twin of a
/// screen (the sign-in page sets a cookie; here the answer is a pair of tokens), so they carry no <c>[UiApi]</c> and are not part of the parity rules. They
/// answer 404 while accounts are off. Tokens are only ever in a body, a header, or (the external sign-in) the URL fragment of the address the application
/// returned to, never in a query string or a log; every answer carrying them is <c>no-store</c>.
/// </summary>
[Route("api/ui")]
public sealed class SessionUiApiController : UiApiControllerBase
{
    private readonly IOptions<CorsSettings> _cors;

    /// <summary>Creates the controller.</summary>
    public SessionUiApiController(IOptions<CorsSettings> cors)
    {
        _cors = cors ?? throw new ArgumentNullException(nameof(cors));
    }

    // ----- password

    /// <summary>
    /// Signs in with the user name and password. Answers the tokens, or <c>twoFactorRequired</c> with a short-lived <c>twoFactorTicket</c> for
    /// <c>/session/two-factor</c> or <c>/session/recovery-code</c>. 401 for wrong credentials, 403 for a locked, unapproved or unconfirmed account.
    /// </summary>
    [HttpPost("session")]
    public async Task<ActionResult<ScreenResult<SessionModel>>> SignIn([FromBody] SessionSignInForm? form)
        => Tokens(Disabled() ?? await UseCase<SessionUseCase>().SignInAsync(form));

    /// <summary>Completes a sign-in with a code of the authenticator app.</summary>
    [HttpPost("session/two-factor")]
    public async Task<ActionResult<ScreenResult<SessionModel>>> TwoFactor([FromBody] SessionTwoFactorForm? form)
        => Tokens(Disabled() ?? await UseCase<SessionUseCase>().TwoFactorAsync(form));

    /// <summary>Completes a sign-in with a recovery code (each works once).</summary>
    [HttpPost("session/recovery-code")]
    public async Task<ActionResult<ScreenResult<SessionModel>>> RecoveryCode([FromBody] SessionRecoveryCodeForm? form)
        => Tokens(Disabled() ?? await UseCase<SessionUseCase>().RecoveryCodeAsync(form));

    /// <summary>Exchanges the refresh token for a new pair of tokens (the authentication time is kept).</summary>
    [HttpPost("session/refresh")]
    public async Task<ActionResult<ScreenResult<SessionModel>>> Refresh([FromBody] SessionRefreshForm? form)
        => Tokens(Disabled() ?? await UseCase<SessionUseCase>().RefreshAsync(form));

    /// <summary>
    /// Ends the session: the client forgets its tokens. With <c>everywhere</c> the account's security stamp changes and every token and cookie
    /// session of the account stops working. The caller is the access token, or the refresh token in the body.
    /// </summary>
    [HttpPost("session/sign-out")]
    public async Task<ActionResult<ScreenResult<EmptyScreen>>> SignOutSession([FromBody] SessionSignOutForm? form)
    {
        if (!Gate.AccountsEnabled) return Respond(ScreenResult<EmptyScreen>.NotFound());

        NoStore();

        return Respond(await UseCase<SessionUseCase>().SignOutAsync(InteractiveCaller != null ? User : null, form));
    }

    // ----- antiforgery for the cookie

    /// <summary>
    /// The antiforgery request token for a same-origin client that uses the cookie session: send it in the <c>X-XSRF-TOKEN</c> header (every method). It
    /// is bound to the signed-in user, so fetch it again after signing in or out. Also sets the antiforgery cookie.
    /// </summary>
    [HttpGet("antiforgery")]
    public ActionResult<ScreenResult<AntiforgeryTokenModel>> AntiforgeryToken([FromServices] IAntiforgery? antiforgery)
    {
        if (!Gate.AccountsEnabled || antiforgery == null) return Respond(ScreenResult<AntiforgeryTokenModel>.NotFound());

        // The token is bound to the user the cookie authenticates, which the security filter reset for this (credential-less) request.
        var current = HttpContext.User;
        HttpContext.User = UiApiCaller.CookieUser(HttpContext) ?? current;
        try
        {
            var tokens = antiforgery.GetAndStoreTokens(HttpContext);
            NoStore();

            return Respond(ScreenResult<AntiforgeryTokenModel>.Ok(new AntiforgeryTokenModel { HeaderName = UiApiCaller.AntiforgeryHeader, RequestToken = tokens.RequestToken }));
        }
        finally
        {
            HttpContext.User = current;
        }
    }

    // ----- the external provider

    /// <summary>
    /// Starts the sign-in with the external provider (a browser navigation, not a fetch). The tokens come back in the URL fragment of
    /// <paramref name="returnUrl"/>, which must be a path of this server or an origin listed in <c>Cors:AllowedOrigins</c>.
    /// </summary>
    [HttpGet("session/external/{provider}")]
    public IActionResult External(string provider, string? returnUrl)
    {
        if (!Gate.AccountsEnabled || !string.Equals(provider, ExternalSignInUseCase.OpenIdConnectScheme, StringComparison.OrdinalIgnoreCase)) return NotFound();
        if (!ReturnUrlAllowed(returnUrl)) return BadRequest(new { error = "returnUrl must be a path of this server or an origin listed in Cors:AllowedOrigins." });

        var callback = Url.Action(nameof(ExternalCallback), "SessionUiApi", new { returnUrl }, Request.Scheme);
        var challenge = UseCase<ExternalSessionUseCase>().Start(callback!);
        if (challenge == null) return NotFound();

        return Challenge(challenge.Properties, challenge.Scheme);
    }

    /// <summary>
    /// The provider's answer: redirects to <paramref name="returnUrl"/> with, in the URL fragment, <c>access_token</c>, <c>expires_at</c>, <c>refresh_token</c>,
    /// <c>refresh_expires_at</c>, <c>user_name</c> — or <c>two_factor_ticket</c> (+ <c>two_factor_expires_at</c>, <c>user_name</c>), <c>notice</c> or <c>error</c>.
    /// </summary>
    [HttpGet("session/external-callback")]
    public async Task<IActionResult> ExternalCallback(string? returnUrl, CancellationToken cancellationToken)
    {
        if (!Gate.AccountsEnabled) return NotFound();
        if (!ReturnUrlAllowed(returnUrl)) return BadRequest(new { error = "returnUrl must be a path of this server or an origin listed in Cors:AllowedOrigins." });

        var result = await UseCase<ExternalSessionUseCase>().CompleteAsync(cancellationToken);
        var fragment = new Dictionary<string, string?>();
        var model = result.Model;

        if (result.Status == ScreenStatus.Ok && model?.Notice != null) fragment["notice"] = model.Notice;
        else if (model?.TwoFactorRequired == true)
        {
            fragment["two_factor_ticket"] = model.TwoFactorTicket;
            fragment["two_factor_expires_at"] = Iso(model.TwoFactorTicketExpiresAt);
            fragment["user_name"] = model.UserName;
        }
        else if (result.Status == ScreenStatus.Ok && model?.AccessToken != null)
        {
            fragment["access_token"] = model.AccessToken;
            fragment["expires_at"] = Iso(model.ExpiresAt);
            fragment["refresh_token"] = model.RefreshToken;
            fragment["refresh_expires_at"] = Iso(model.RefreshExpiresAt);
            fragment["user_name"] = model.UserName;
        }
        else
        {
            fragment["error"] = result.Errors.TryGetValue(string.Empty, out var errors) && errors.Length > 0 ? errors[0] : "The external sign-in failed.";
        }

        NoStore();
        Response.Headers["Referrer-Policy"] = "no-referrer";

        return Redirect(returnUrl + "#" + string.Join("&", System.Linq.Enumerable.Select(
            System.Linq.Enumerable.Where(fragment, p => p.Value != null),
            p => Uri.EscapeDataString(p.Key) + "=" + Uri.EscapeDataString(p.Value!))));
    }

    // ----- helpers

    private bool ReturnUrlAllowed(string? returnUrl)
        => SessionReturnUrls.IsAllowed(returnUrl, Request.Scheme + "://" + Request.Host, SessionReturnUrls.Origins(_cors.Value.AllowedOrigins));

    private static string? Iso(DateTime? value) => value?.ToString("O", CultureInfo.InvariantCulture);

    private ScreenResult<SessionModel>? Disabled()
        => Gate.AccountsEnabled ? null : ScreenResult<SessionModel>.NotFound();

    private ActionResult<ScreenResult<SessionModel>> Tokens(ScreenResult<SessionModel> result)
    {
        NoStore();

        return Respond(result);
    }

    private void NoStore()
    {
        Response.Headers[HeaderNames.CacheControl] = "no-store";
        Response.Headers[HeaderNames.Pragma] = "no-cache";
    }
}
