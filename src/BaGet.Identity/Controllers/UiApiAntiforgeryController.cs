using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace BaGet.Identity.Mvc;

/// <summary>The antiforgery token a client sends with every write of <c>/api/ui</c> made with a browser session.</summary>
/// <param name="HeaderName">The request header that carries <see cref="RequestToken"/>.</param>
/// <param name="RequestToken">The token; valid together with the antiforgery cookie this response sets.</param>
public sealed record AntiforgeryTokenResponse(string HeaderName, string RequestToken);

/// <summary>
/// <c>GET /api/ui/antiforgery</c>: hands a client the antiforgery token (and sets the antiforgery cookie it belongs to). A client using a
/// browser session or no credentials sends the token in the <see cref="AntiforgeryTokenResponse.HeaderName"/> header on every POST, PUT,
/// PATCH and DELETE to <c>/api/ui</c>; a client using an API key needs none. Anonymous, never cached.
/// </summary>
[Route("api/ui/antiforgery")]
public sealed class UiApiAntiforgeryController : ControllerBase
{
    private const string DefaultHeaderName = "RequestVerificationToken";

    private readonly IAntiforgery _antiforgery;
    private readonly IOptions<AntiforgeryOptions> _options;

    /// <summary>Creates the controller.</summary>
    public UiApiAntiforgeryController(IAntiforgery antiforgery, IOptions<AntiforgeryOptions> options)
    {
        _antiforgery = antiforgery;
        _options = options;
    }

    /// <summary>The token to send in the header.</summary>
    [HttpGet, Produces("application/json")]
    public ActionResult<AntiforgeryTokenResponse> Get()
    {
        var tokens = _antiforgery.GetAndStoreTokens(HttpContext);
        Response.Headers[HeaderNames.CacheControl] = "no-store";

        return new AntiforgeryTokenResponse(_options.Value.HeaderName ?? DefaultHeaderName, tokens.RequestToken!);
    }
}
