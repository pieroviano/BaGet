using System;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.Application;
using BaGet.Web;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace BaGet.Identity.Mvc;

/// <summary>
/// Authenticates a request to <c>/api/ui</c> (decision D2) before any action runs:
/// <list type="bullet">
/// <item><description><b>API key</b> (Basic password, Bearer token or <c>X-NuGet-ApiKey</c>, resolved by <see cref="RequestPrincipalResolver.ResolveForWriteAsync"/>, which never
/// accepts the cookie): the key's owner acts within the key's scopes and repository limits; no antiforgery, because a web page cannot make a browser send such
/// a header. Credentials that do not authenticate are refused with 401 (they are not read as "anonymous").</description></item>
/// <item><description><b>Browser session or anonymous</b>: an unsafe method (anything but GET, HEAD, OPTIONS, TRACE) must carry the antiforgery token in the
/// <c>RequestVerificationToken</c> header (token from <c>GET /api/ui/antiforgery</c>), else 400 before anything else runs, as a form without its token is for a page.</description></item>
/// </list>
/// In-process calls of a view controller do not pass through here: the view action has its own antiforgery validation and its own cookie session.
/// </summary>
public sealed class UiApiSecurityFilter : IAsyncAuthorizationFilter
{
    /// <inheritdoc/>
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var http = context.HttpContext;

        if (UiApiCaller.HasKeyCredentials(http.Request))
        {
            var resolver = http.RequestServices.GetService<RequestPrincipalResolver>();
            var key = resolver == null ? null : await resolver.ResolveForWriteAsync(http, http.RequestAborted);
            if (key == null)
            {
                context.Result = new UiApiResult(ScreenResult<object>.SignIn());
                return;
            }

            UiApiCaller.SetKey(http, key);
            return;
        }

        if (HttpMethods.IsGet(http.Request.Method) || HttpMethods.IsHead(http.Request.Method)
            || HttpMethods.IsOptions(http.Request.Method) || HttpMethods.IsTrace(http.Request.Method))
        {
            return;
        }

        try
        {
            await http.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(http);
        }
        catch (AntiforgeryValidationException)
        {
            context.Result = new UiApiResult(ScreenResult<object>.Invalid(
                null!,
                "The antiforgery token is missing or invalid. Send the token of GET /api/ui/antiforgery in the RequestVerificationToken header, or authenticate with an API key."));
        }
    }
}
