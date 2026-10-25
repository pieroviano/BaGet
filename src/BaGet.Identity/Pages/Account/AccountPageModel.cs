using System;
using System.Text;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BaGet.Identity.Pages.Account;

/// <summary>
/// Shared by the account pages. The pages are always compiled in, but answer 404 while
/// accounts are disabled, so their services are resolved per request rather than
/// injected: they are only registered when accounts are enabled.
/// </summary>
public abstract class AccountPageModel : PageModel
{
    protected AccountsOptions Accounts
        => HttpContext.RequestServices.GetRequiredService<IOptions<AccountsOptions>>().Value;

    protected SignInManager<BaGetUser> SignInManager
        => HttpContext.RequestServices.GetRequiredService<SignInManager<BaGetUser>>();

    protected UserManager<BaGetUser> UserManager
        => HttpContext.RequestServices.GetRequiredService<UserManager<BaGetUser>>();

    protected PackageOwnershipService Ownership
        => HttpContext.RequestServices.GetRequiredService<PackageOwnershipService>();

    protected ApiKeyService ApiKeys
        => HttpContext.RequestServices.GetRequiredService<ApiKeyService>();

    protected bool EmailConfigured
        => HttpContext.RequestServices.GetRequiredService<IOptions<EmailOptions>>().Value.IsConfigured;

    protected IEmailSender<BaGetUser> EmailSender
        => HttpContext.RequestServices.GetRequiredService<IEmailSender<BaGetUser>>();

    /// <summary>
    /// Pages that only make sense when there is a way to send mail answer 404 otherwise.
    /// </summary>
    protected virtual bool RequiresEmail => false;

    /// <summary>
    /// Checked here rather than with [Authorize]: while accounts are disabled there is
    /// no scheme to challenge with, and the page must answer 404 instead.
    /// </summary>
    protected virtual bool RequiresSignIn => false;

    public override void OnPageHandlerExecuting(PageHandlerExecutingContext context)
    {
        if (!Accounts.Enabled)
        {
            context.Result = NotFound();
            return;
        }

        if (RequiresEmail && !EmailConfigured)
        {
            context.Result = NotFound();
            return;
        }

        if (RequiresSignIn && User.Identity?.IsAuthenticated != true)
        {
            context.Result = Challenge();
            return;
        }

        base.OnPageHandlerExecuting(context);
    }

    /// <summary>
    /// Only follow a local return URL, so a sign-in link cannot redirect elsewhere.
    /// </summary>
    protected IActionResult RedirectToLocal(string? returnUrl)
        => LocalRedirect(AccountSupport.LocalTarget(Url, returnUrl));

    /// <summary>
    /// Identity's tokens carry characters that do not survive a URL, so they travel encoded.
    /// </summary>
    protected static string EncodeToken(string token)
        => PasswordResetLinkSender.EncodeToken(token);

    protected static string? DecodeToken(string? code)
        => AccountSupport.DecodeToken(code);

    /// <summary>
    /// An absolute link to an account page, for an email. Built only from <c>Site:PublicUrl</c>,
    /// never from the request's host or scheme: see <see cref="EmailLinkPolicy"/>.
    /// </summary>
    protected string PageLink(string page, object values)
        => AccountSupport.PageLink(HttpContext, Url, page, values);

    /// <summary>
    /// How long a sign-in with the external provider counts as proof of presence for a
    /// sensitive action.
    /// </summary>
    public static readonly TimeSpan ReauthenticationWindow = AccountSupport.ReauthenticationWindow;

    internal const string ReauthenticatedItem = AccountSupport.ReauthenticatedItem;

    /// <summary>
    /// The check behind every sensitive action (change email, set a password, delete the
    /// account, turn two-factor off or reset it). An account with a password types it again.
    /// One without (external sign-in only) has nothing it can type that a stolen session
    /// would not know, so it must have signed in again with its provider a moment ago:
    /// see <see cref="ReauthenticateModel"/>.
    /// </summary>
    protected Task<bool> IsConfirmedAsync(BaGetUser? user, string? password)
        => AccountSupport.IsConfirmedAsync(UserManager, HttpContext, user, password);

    protected Task<bool> HasReauthenticatedRecentlyAsync()
        => AccountSupport.HasReauthenticatedRecentlyAsync(HttpContext);

    protected static string Describe(IdentityResult result)
        => AccountSupport.Describe(result);
}
