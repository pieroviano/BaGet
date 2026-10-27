using System;
using System.Linq;
using BaGet.Core;
using Microsoft.Extensions.Options;
using BaGet.Identity.Application.Email;
using BaGet.Identity.Application.Security;

namespace BaGet.Identity.Application.Links;

/// <summary>
/// The absolute links the account emails carry. Built only from <c>Site:PublicUrl</c>, never from the request's host or scheme (see
/// <see cref="EmailLinkPolicy"/>), so a use case that has no request URL helper still writes the same links the screens always wrote.
/// </summary>
public static class AccountLinks
{
    /// <summary>The address of the confirmation page (<c>/account/confirmemail</c>).</summary>
    public const string ConfirmEmailPath = "/account/confirmemail";

    /// <summary>The address of the new-password page (<c>/account/resetpassword</c>).</summary>
    public const string ResetPasswordPath = "/account/resetpassword";

    /// <summary>
    /// The link to <paramref name="path"/> (an application-relative path starting with <c>/</c>) with the query values in order. Throws
    /// when the server has no valid public URL: nothing is mailed then.
    /// </summary>
    public static string Build(IOptions<SiteOptions> site, string path, params (string Name, string? Value)[] query)
    {
        if (site == null) throw new ArgumentNullException(nameof(site));

        if (!EmailLinkPolicy.TryParsePublicUrl(site.Value.PublicUrl, out var root))
        {
            throw new InvalidOperationException(EmailLinkPolicy.UnavailableWarning);
        }

        var queryString = string.Join(
            "&",
            query.Where(q => q.Value != null).Select(q => Uri.EscapeDataString(q.Name) + "=" + Uri.EscapeDataString(q.Value!)));

        return root?.GetLeftPart(UriPartial.Path).TrimEnd('/') + path + (queryString.Length == 0 ? string.Empty : "?" + queryString);
    }

    /// <summary>The confirmation link of a new account's address, or (with <paramref name="email"/>) of a change of address.</summary>
    public static string ConfirmEmail(IOptions<SiteOptions> site, string userId, string token, string? email = null)
        => Build(site, ConfirmEmailPath, ("userId", userId), ("email", email), ("code", AccountConfirmation.EncodeToken(token)));

    /// <summary>The template of the password-reset link: the user id and the code are filled in later (<see cref="PasswordResetLinkSender"/>).</summary>
    public static string ResetPasswordTemplate(IOptions<SiteOptions> site)
        => Build(site, ResetPasswordPath, ("userId", PasswordResetLinkSender.UserIdPlaceholder), ("code", PasswordResetLinkSender.CodePlaceholder));
}
