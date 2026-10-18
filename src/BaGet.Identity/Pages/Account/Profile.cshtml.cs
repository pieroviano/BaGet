using Microsoft.AspNetCore.Mvc;

namespace BaGet.Identity.Pages.Account;

/// <summary>
/// The signed in user's own profile.
/// </summary>
public class MyProfileModel : AccountPageModel
{
    protected override bool RequiresSignIn => true;

    #pragma warning disable CS8604
    public IActionResult OnGet() => LocalRedirect(Url.Content($"~/profiles/{System.Uri.EscapeDataString(User.Identity?.Name)}"));
    #pragma warning restore CS8604
}