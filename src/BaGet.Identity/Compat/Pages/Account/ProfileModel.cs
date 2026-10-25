using System;
using BaGet.Identity.Workflows;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Identity.Pages.Account;

/// <summary>
/// The page model of the signed in user's own profile when it was a Razor Page: a redirect to the public profile.
/// </summary>
/// <remarks>
/// Kept as a shim (no breaking change of the public API); <c>AccountPackagesController</c> serves <c>/account/profile</c> and delegates to
/// the same <c>ProfileWorkflow</c>.
/// </remarks>
[Obsolete("The own-profile redirect is served by AccountPackagesController; this page model has no page any more and will be removed in the next major version.")]
public class MyProfileModel : AccountPageModel
{
    protected override bool RequiresSignIn => true;

    public IActionResult OnGet() => OutcomeResult(new ProfileWorkflow(Screen).Mine());
}
