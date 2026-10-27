using System.Threading.Tasks;
using BaGet.Identity.Application.Screens;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using BaGet.Identity.Security;

namespace BaGet.Identity.Mvc.UI;

/// <summary>
/// Authenticates a request to <c>/api/ui</c> over HTTP before any action runs, like <c>/api/v1</c>: <b>API keys only</b>.
/// <list type="bullet">
/// <item><description><b>Bearer or <c>X-NuGet-ApiKey</c></b> (resolved by <see cref="RequestPrincipalResolver.ResolveForWriteAsync"/>): the key's owner acts within the key's
/// scopes and repository limits. A key that does not authenticate is refused with 401 (not read as "anonymous").</description></item>
/// <item><description><b>HTTP Basic</b> is refused with 401: browsers cache Basic credentials after a Basic challenge and re-send them on cross-site requests, and
/// there is no antiforgery here.</description></item>
/// <item><description><b>No credentials</b>: anonymous. The browser session cookie is <b>never</b> a credential on <c>/api/ui</c>: <c>User</c> is reset to an anonymous
/// principal and the request is marked so that no later code re-reads the cookie. With no ambient credential there is nothing for a cross-site page to
/// ride on, so no antiforgery token is needed (nor offered). An open server (accounts off, no <c>ApiKey</c>) accepts anonymous uploads, so a cross-site
/// form can post one, as it can to the protocol routes.</description></item>
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
                "HTTP Basic is not accepted on /api/ui: send the API key as 'Authorization: Bearer <key>' or in the 'X-NuGet-ApiKey' header."));
            return;
        }

        if (!UiApiCaller.HasKeyCredentials(http.Request))
        {
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
}
