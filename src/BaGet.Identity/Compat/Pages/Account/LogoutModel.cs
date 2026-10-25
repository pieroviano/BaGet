using System;
using System.Threading.Tasks;
using BaGet.Identity.Workflows;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Identity.Pages.Account;

/// <summary>
/// The page model of the sign-out page when it was a Razor Page. Signing out is a POST, so a link elsewhere cannot sign the
/// user out.
/// </summary>
/// <remarks>
/// Kept as a shim (no breaking change of the public API); <c>AccountController</c> serves <c>/account/logout</c> and delegates to the
/// same <c>LogoutWorkflow</c>.
/// </remarks>
[Obsolete("The sign-out screen is served by AccountController; this page model has no page any more and will be removed in the next major version.")]
public class LogoutModel : AccountPageModel
{
    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync() => OutcomeResult(await new LogoutWorkflow(Screen).PostAsync());
}
