using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Identity.Pages.Account;

/// <summary>
/// Second step of signing in with a recovery code instead of an authenticator code. Each
/// code works once.
/// </summary>
public class LoginWithRecoveryCodeModel : AccountPageModel
{
    [BindProperty]
    [Required]
    [Display(Name = "Recovery code")]
    public string? RecoveryCode { get; set; }

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

        #pragma warning disable CS8604
        var result = await SignInManager.TwoFactorRecoveryCodeSignInAsync(RecoveryCode?.Replace(" ", string.Empty));
        #pragma warning restore CS8604

        if (result.Succeeded) return RedirectToLocal(ReturnUrl);

        Error = result.IsLockedOut
            ? "The account is locked after too many failed attempts. Try again later."
            : "The recovery code is not valid, or was already used.";

        return Page();
    }
}
