using System.Security.Claims;
using BaGet.Core;
using BaGet.Identity.Application;
using BaGet.Web;
using Microsoft.AspNetCore.Http;

namespace BaGet.Identity.Mvc.UI;

/// <summary>
/// Who calls <c>/api/ui</c> over HTTP: an API key sent as <c>Authorization: Bearer</c> or <c>X-NuGet-ApiKey</c> (headers a web page cannot make a browser
/// send), or nobody. The browser session is never a credential here and neither is HTTP Basic (browsers cache Basic credentials and re-send them on
/// cross-site requests). <see cref="UiApiSecurityFilter"/> decides and records it here; <c>User</c> is then the key's owner (or anonymous), so the gate
/// and the services see one kind of caller. The in-process call of a view controller does not pass through the filter: it has its own cookie session.
/// </summary>
public static class UiApiCaller
{
    private const string KeyItem = "BaGet.UiApi.Key";

    /// <summary>The API key that authenticated the request, or null for an anonymous caller.</summary>
    public static BaGetPrincipal? Key(HttpContext context)
        => context.Items.TryGetValue(KeyItem, out var key) ? key as BaGetPrincipal : null;

    internal static void SetKey(HttpContext context, BaGetPrincipal key)
    {
        context.Items[KeyItem] = key;
        MarkSessionIgnored(context);

        // The owner acts: User carries the key owner's id and name, as a session's does. The global key has no owner.
        var identity = new ClaimsIdentity("BaGetApiKey", ClaimTypes.Name, ClaimTypes.Role);
        if (key.UserId != null) identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, key.UserId));
        if (key.UserName != null) identity.AddClaim(new Claim(ClaimTypes.Name, key.UserName));
        context.User = new ClaimsPrincipal(identity);
    }

    /// <summary>
    /// An anonymous caller: whatever the cookie authentication put in <c>User</c> is dropped, the request's resolved principal is forgotten and the
    /// request is marked so that nothing re-reads the session cookie (<see cref="AccountConfirmation.IgnoreSessionItem"/>).
    /// </summary>
    internal static void SetAnonymous(HttpContext context)
    {
        MarkSessionIgnored(context);
        context.User = new ClaimsPrincipal(new ClaimsIdentity());
    }

    private static void MarkSessionIgnored(HttpContext context)
    {
        context.Items[AccountConfirmation.IgnoreSessionItem] = true;
        RequestPrincipalResolver.Forget(context);
    }

    /// <summary>Whether the request carries an HTTP Basic <c>Authorization</c> header (not accepted on <c>/api/ui</c>).</summary>
    public static bool HasBasicCredentials(HttpRequest request)
        => request.Headers.Authorization.ToString().StartsWith("Basic", System.StringComparison.OrdinalIgnoreCase);

    /// <summary>Whether the request carries an API key in an accepted header (Bearer or <c>X-NuGet-ApiKey</c>).</summary>
    public static bool HasKeyCredentials(HttpRequest request)
        => BasicCredentials.ReadBearer(request) != null
           || !string.IsNullOrEmpty(request.Headers[RequestPrincipalResolver.ApiKeyHeader].ToString());
}
