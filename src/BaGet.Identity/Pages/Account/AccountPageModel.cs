using BaGet.Core;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BaGet.Identity.Pages.Account
{
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
        protected IActionResult RedirectToLocal(string returnUrl)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }

            return LocalRedirect(Url.Content("~/"));
        }
    }
}
