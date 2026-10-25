using System.Security.Claims;
using BaGet.Core;
using BaGet.Web;
using Microsoft.AspNetCore.Http;

namespace BaGet.Identity.Mvc;

/// <summary>
/// Who calls <c>/api/ui</c>: a browser session (cookie, antiforgery header on writes) or an API key (Basic password, Bearer token or
/// <c>X-NuGet-ApiKey</c>; no antiforgery, because a page cannot make a browser send the header). <see cref="UiApiSecurityFilter"/> decides
/// and records it here; <c>User</c> is then the key's owner, so the gate and the services see one kind of caller.
/// </summary>
public static class UiApiCaller
{
    private const string KeyItem = "BaGet.UiApi.Key";

    /// <summary>The API key that authenticated the request, or null for a browser session or an anonymous visitor.</summary>
    public static BaGetPrincipal? Key(HttpContext context)
        => context.Items.TryGetValue(KeyItem, out var key) ? key as BaGetPrincipal : null;

    internal static void SetKey(HttpContext context, BaGetPrincipal key)
    {
        context.Items[KeyItem] = key;

        // The owner acts: User carries the key owner's id and name, as a session's does. The global key has no owner.
        var identity = new ClaimsIdentity("BaGetApiKey", ClaimTypes.Name, ClaimTypes.Role);
        if (key.UserId != null) identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, key.UserId));
        if (key.UserName != null) identity.AddClaim(new Claim(ClaimTypes.Name, key.UserName));
        context.User = new ClaimsPrincipal(identity);
    }

    /// <summary>Whether the request carries an API key (and so is judged as a key caller, not as a session).</summary>
    public static bool HasKeyCredentials(HttpRequest request)
        => BasicCredentials.Read(request) != null
           || BasicCredentials.ReadBearer(request) != null
           || !string.IsNullOrEmpty(request.Headers[RequestPrincipalResolver.ApiKeyHeader].ToString());
}
