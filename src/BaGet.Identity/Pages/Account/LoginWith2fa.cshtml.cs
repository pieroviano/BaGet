using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Identity.Pages.Account;

/// <summary>
/// The second step of signing in with two-factor authentication: the password was right
/// and a half-signed-in cookie says who, but nothing is granted until the code is too.
/// </summary>
public class LoginWith2faModel : AccountPageModel
{
    [BindProperty]
    [Required]
    [Display(Name = "Authenticator code")]
    public string? Code { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool RememberMe { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public string? Error { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (await SignInManager.GetTwoFactorAuthenticationUserAsync() == null) return RedirectToPage("/Account/Login");

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (await SignInManager.GetTwoFactorAuthenticationUserAsync() == null) return RedirectToPage("/Account/Login");
        if (!ModelState.IsValid) return Page();

        var code = Code?.Replace(" ", string.Empty).Replace("-", string.Empty);
        #pragma warning disable CS8604
        var result = await SignInManager.TwoFactorAuthenticatorSignInAsync(code, RememberMe, rememberClient: false);
        #pragma warning restore CS8604

        if (result.Succeeded) return RedirectToLocal(ReturnUrl);

        Error = result.IsLockedOut
            ? "The account is locked after too many failed attempts. Try again later."
            : "The code is not valid.";

        return Page();
    }
}
