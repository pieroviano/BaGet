using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Frontend.Security;
using BaGet.Identity.Application.Screens;
using BaGet.Identity.Application.Security;
using BaGet.Identity.Application.Services.Sessions;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace BaGet.Frontend.Mvc.UI;

/// <summary>
/// Authenticates a request to <c>/api/ui</c> over HTTP before any action runs. The credentials, in order:
/// <list type="bullet">
/// <item><description><b>A session token</b> (<c>Authorization: Bearer bgs.xxx</c>, issued by <c>POST /api/ui/session</c>): the signed-in user, with the rights of their
/// cookie session (<see cref="SessionAuthenticator"/> re-checks the account and its security stamp). Only this API accepts it. An invalid or expired one is 401.</description></item>
/// <item><description><b>An API key</b> (<c>Authorization: Bearer</c> or <c>X-NuGet-ApiKey</c>, resolved by <see cref="RequestPrincipalResolver.ResolveForWriteAsync"/>): the key's owner
/// acts within the key's scopes and repository limits. A key that does not authenticate is 401 (not read as "anonymous").</description></item>
/// <item><description><b>HTTP Basic</b> is refused with 401: browsers cache Basic credentials after a Basic challenge and re-send them on cross-site requests.</description></item>
/// <item><description><b>The browser session cookie</b>, only for a same-origin client that also sends a valid antiforgery request token in the
/// <see cref="UiApiCaller.AntiforgeryHeader"/> header (<c>GET /api/ui/antiforgery</c>), for every method. A cross-site page can neither read the token nor set the header.
/// Without a valid token the cookie is ignored.</description></item>
/// <item><description><b>No credentials</b>: anonymous. <c>User</c> is reset to an anonymous principal and the request is marked so that no later code re-reads the cookie.
/// An open server (accounts off, no <c>ApiKey</c>) accepts anonymous uploads, so a cross-site form can post one, as it can to the protocol routes.</description></item>
/// </list>
/// In-process calls of a view controller do not pass through here: the view action has its own antiforgery validation and its own cookie session.
/// </summary>
public sealed class UiApiSecurityFilter : IAsyncAuthorizationFilter
{
    /// <inheritdoc/>
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var http = context.HttpContext;

        if (UiApiCaller.HasBasicCredentials(http.Request))
        {
            context.Result = new UiApiResult(ScreenResult<object>.SignIn(
                "HTTP Basic is not accepted on /api/ui: send the API key as 'Authorization: Bearer <key>' or in the 'X-NuGet-ApiKey' header, or a session token."));
            return;
        }

        var sessionToken = UiApiCaller.SessionTokenOf(http.Request);
        if (sessionToken != null)
        {
            var authenticator = http.RequestServices.GetService<SessionAuthenticator>();
            var session = authenticator == null ? null : await authenticator.AuthenticateAsync(SessionTokenKind.Access, sessionToken);
            if (session == null)
            {
                context.Result = new UiApiResult(ScreenResult<object>.SignIn("The session token has expired or is not valid: refresh it or sign in again."));
                return;
            }

            UiApiCaller.RememberCookieUser(http);
            UiApiCaller.SetInteractive(http, session.User, session.Principal, session.AuthenticatedAt);
            return;
        }

        if (!UiApiCaller.HasKeyCredentials(http.Request))
        {
            if (await TryCookieSessionAsync(http)) return;

            UiApiCaller.SetAnonymous(http);
            return;
        }

        var resolver = http.RequestServices.GetService<RequestPrincipalResolver>();
        var key = resolver == null ? null : await resolver.ResolveForWriteAsync(http, http.RequestAborted);
        if (key == null)
        {
            context.Result = new UiApiResult(ScreenResult<object>.SignIn("The API key is not valid."));
            return;
        }

        UiApiCaller.SetKey(http, key);
    }

    // The cookie session, accepted only together with a valid antiforgery token in the header (for every method).
    private static async Task<bool> TryCookieSessionAsync(Microsoft.AspNetCore.Http.HttpContext http)
    {
        if (http.User.Identity?.IsAuthenticated != true || string.IsNullOrEmpty(http.Request.Headers[UiApiCaller.AntiforgeryHeader].ToString())) return false;

        // ValidateRequestAsync, not IsRequestValidAsync: that one answers "valid" for GET, HEAD and OPTIONS, and the cookie needs the token for every method.
        var antiforgery = http.RequestServices.GetService<IAntiforgery>();
        if (antiforgery == null) return false;

        try
        {
            await antiforgery.ValidateRequestAsync(http);
        }
        catch (AntiforgeryValidationException)
        {
            return false;
        }

        var sessions = http.RequestServices.GetService<ISessionPrincipalFactory>();
        var principal = sessions == null ? null : await sessions.CreateAsync(http.User);
        if (principal == null) return false;

        UiApiCaller.SetInteractive(http, http.User, principal, authenticatedAt: null);

        return true;
    }
}
