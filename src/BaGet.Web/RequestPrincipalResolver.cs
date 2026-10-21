using System;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Http;
using IAuthenticationService = BaGet.Core.IAuthenticationService;

namespace BaGet.Web;

/// <summary>
/// Who is calling: the principal behind the request's credentials, for the read checks. Credentials are, in order, the
/// password of <c>Authorization: Basic</c> (an API key; the user name must be the key owner's when the key belongs to a
/// user), the <c>X-NuGet-ApiKey</c> header, then the browser session. Credentials that do not authenticate are the same
/// as none: a repository that is open to everyone stays readable, one that is not answers 401.
/// </summary>
public class RequestPrincipalResolver
{
    private const string ItemKey = "BaGet.RequestPrincipal";

    private readonly IAuthenticationService _authentication;
    private readonly ISessionPrincipalFactory? _sessions;

    public RequestPrincipalResolver(IAuthenticationService authentication, ISessionPrincipalFactory? sessions = null)
    {
        _authentication = authentication ?? throw new ArgumentNullException(nameof(authentication));
        _sessions = sessions;
    }

    /// <summary>The caller, or null when anonymous. Resolved once per request.</summary>
    public async Task<BaGetPrincipal?> ResolveAsync(HttpContext context, CancellationToken cancellationToken)
    {
        if (context.Items.TryGetValue(ItemKey, out var cached)) return cached as BaGetPrincipal;

        var principal = await ResolveCoreAsync(context, cancellationToken).ConfigureAwait(false);
        context.Items[ItemKey] = principal;
        return principal;
    }

    private async Task<BaGetPrincipal?> ResolveCoreAsync(HttpContext context, CancellationToken cancellationToken)
    {
        var basic = BasicCredentials.Read(context.Request);
        var key = basic?.Password;
        if (string.IsNullOrEmpty(key)) key = context.Request.GetApiKey();

        if (!string.IsNullOrEmpty(key))
        {
            var principal = await _authentication.AuthenticatePrincipalAsync(key, cancellationToken).ConfigureAwait(false);
            if (principal != null)
            {
                var userName = basic?.UserName;
                var mismatch = !string.IsNullOrEmpty(userName)
                               && principal.UserName != null
                               && !string.Equals(userName, principal.UserName, StringComparison.OrdinalIgnoreCase);

                if (!mismatch) return principal;
            }
        }

        if (_sessions != null && context.User.Identity?.IsAuthenticated == true)
        {
            return await _sessions.CreateAsync(context.User).ConfigureAwait(false);
        }

        return null;
    }
}
