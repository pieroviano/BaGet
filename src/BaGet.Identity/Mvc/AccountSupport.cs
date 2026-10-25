using System;
using System.Text;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BaGet.Identity.Mvc;

/// <summary>
/// The rules of the account screens that do not depend on the programming model, shared by the Razor Page base
/// (<c>AccountPageModel</c>) and the controller base (<see cref="AccountControllerBase"/>) so the two cannot diverge while
/// pages are being migrated.
/// </summary>
internal static class AccountSupport
{
    /// <summary>How long a sign-in with the external provider counts as proof of presence for a sensitive action.</summary>
    public static readonly TimeSpan ReauthenticationWindow = TimeSpan.FromMinutes(5);

    /// <summary>The sign-in cookie property that holds the time (ticks, UTC) of the last re-authentication.</summary>
    public const string ReauthenticatedItem = "baget.reauthenticated";

    /// <summary>
    /// The check behind every sensitive action. An account with a password types it again; one without (external sign-in
    /// only) must have signed in again with its provider a moment ago.
    /// </summary>
    public static async Task<bool> IsConfirmedAsync(UserManager<BaGetUser> users, HttpContext httpContext, BaGetUser? user, string? password)
    {
        #pragma warning disable CS8604
        return await users.HasPasswordAsync(user)
            ? await users.CheckPasswordAsync(user, password ?? string.Empty)
            : await HasReauthenticatedRecentlyAsync(httpContext);
        #pragma warning restore CS8604
    }

    /// <summary>Whether the session re-authenticated with the external provider within <see cref="ReauthenticationWindow"/>.</summary>
    public static async Task<bool> HasReauthenticatedRecentlyAsync(HttpContext httpContext)
    {
        var authentication = await httpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        if (authentication?.Properties == null
            || !authentication.Properties.Items.TryGetValue(ReauthenticatedItem, out var value)
            || !long.TryParse(value, out var ticks)
            || ticks < DateTime.MinValue.Ticks || ticks > DateTime.MaxValue.Ticks)
        {
            return false;
        }

        var age = httpContext.RequestServices.GetRequiredService<SystemTime>().UtcNow - new DateTime(ticks, DateTimeKind.Utc);

        return age >= TimeSpan.Zero && age <= ReauthenticationWindow;
    }

    /// <summary>The URL to go to after a sign-in: <paramref name="returnUrl"/> when it is local, else the site root.</summary>
    public static string LocalTarget(IUrlHelper url, string? returnUrl)
        => !string.IsNullOrEmpty(returnUrl) && url.IsLocalUrl(returnUrl) ? returnUrl : url.Content("~/") ?? "/";

    /// <summary>
    /// An absolute link to an account page, for an email. Built only from <c>Site:PublicUrl</c>, never from the request's host
    /// or scheme: see <see cref="EmailLinkPolicy"/>.
    /// </summary>
    public static string PageLink(HttpContext httpContext, IUrlHelper url, string page, object values)
    {
        var publicUrl = httpContext.RequestServices.GetRequiredService<IOptions<SiteOptions>>().Value.PublicUrl;

        if (EmailLinkPolicy.TryParsePublicUrl(publicUrl, out var root))
        {
            // The path includes the request's path base, which the public URL replaces.
            var path = url.Page(page, pageHandler: null, values: values) ?? string.Empty;
            var pathBase = httpContext.Request.PathBase;
            if (pathBase.HasValue && path.StartsWith(pathBase.Value, StringComparison.OrdinalIgnoreCase))
            {
                path = path.Substring(pathBase.Value.Length);
            }

            return root?.GetLeftPart(UriPartial.Path).TrimEnd('/') + path;
        }

        throw new InvalidOperationException(EmailLinkPolicy.UnavailableWarning);
    }

    /// <summary>Decodes a token written by <see cref="PasswordResetLinkSender.EncodeToken"/>, or null.</summary>
    public static string? DecodeToken(string? code)
    {
        try
        {
            #pragma warning disable CS8604
            return Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
            #pragma warning restore CS8604
        }
        catch (FormatException)
        {
            return null;
        }
    }

    /// <summary>The messages of an identity failure as one sentence.</summary>
    public static string Describe(IdentityResult result)
        => string.Join(" ", System.Linq.Enumerable.Select(result.Errors, e => e.Description));
}
