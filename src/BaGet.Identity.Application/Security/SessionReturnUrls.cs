using System;
using System.Collections.Generic;
using System.Linq;

namespace BaGet.Identity.Application.Security;

/// <summary>
/// Where the external sign-in of a single-page application may send the tokens it issued (in the URL fragment). A wrong rule would hand a user's
/// tokens to a hostile page, so the answer is strict: a path of this server, or an absolute address whose origin is this server's or one the operator listed
/// (<c>Cors:AllowedOrigins</c>). An empty list does <b>not</b> mean "any origin" here, unlike for CORS.
/// </summary>
public static class SessionReturnUrls
{
    /// <summary>Whether tokens may be returned to <paramref name="returnUrl"/>.</summary>
    /// <param name="returnUrl">The address the application asked to return to.</param>
    /// <param name="requestOrigin">This server's origin as the request reached it (<c>https://baget.example.com</c>).</param>
    /// <param name="allowedOrigins">The operator's list of origins (see <see cref="Origins"/>).</param>
    public static bool IsAllowed(string? returnUrl, string requestOrigin, IEnumerable<string> allowedOrigins)
    {
        if (string.IsNullOrWhiteSpace(returnUrl) || returnUrl.Length > 2048) return false;
        if (returnUrl.Any(c => char.IsControl(c) || c == '\\' || c == '#')) return false;

        // A path of this server ("/app/"), but not "//host" or "/\host", which a browser reads as another host.
        if (returnUrl[0] == '/') return returnUrl.Length == 1 || (returnUrl[1] != '/' && returnUrl[1] != '\\');

        if (!Uri.TryCreate(returnUrl, UriKind.Absolute, out var uri)) return false;
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return false;
        if (!string.IsNullOrEmpty(uri.UserInfo)) return false;

        var origin = Normalize(uri.GetLeftPart(UriPartial.Authority));

        return string.Equals(origin, Normalize(requestOrigin), StringComparison.OrdinalIgnoreCase)
               || allowedOrigins.Any(allowed => string.Equals(origin, Normalize(allowed), StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The origins of a setting such as <c>Cors:AllowedOrigins</c>: separated by semicolons (or commas), blanks dropped.</summary>
    public static IReadOnlyList<string> Origins(string? setting)
        => (setting ?? string.Empty)
            .Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Normalize)
            .ToList();

    private static string Normalize(string origin) => origin.Trim().TrimEnd('/');
}
