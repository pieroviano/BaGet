using System;
using System.Text;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;

namespace BaGet.Identity.Application;

/// <summary>
/// The rules behind every sensitive account action (change email, set a password, delete the account, turn two-factor off or reset it), shared
/// by the account use cases and by the screens that have not moved yet (<c>AccountSupport</c> forwards here).
/// </summary>
public static class AccountConfirmation
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

    /// <summary>Identity's tokens carry characters that do not survive a URL, so they travel encoded.</summary>
    public static string EncodeToken(string token) => PasswordResetLinkSender.EncodeToken(token);

    /// <summary>Decodes a token written by <see cref="EncodeToken"/>, or null.</summary>
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
