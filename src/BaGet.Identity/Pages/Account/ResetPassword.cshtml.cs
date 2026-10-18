using System.Linq;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Identity.Pages.Account;

public class ResetPasswordModel : AccountPageModel
{
    private const string InvalidLink = "This link is invalid, expired or already used.";

    protected override bool RequiresEmail => true;

    [BindProperty(SupportsGet = true)]
    public string UserId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string Code { get; set; }

    [BindProperty]
    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "New password")]
    public string Password { get; set; }

    [BindProperty]
    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "Confirm password")]
    [Compare(nameof(Password), ErrorMessage = "The passwords do not match.")]
    public string ConfirmPassword { get; set; }

    public string Error { get; private set; }

    public bool LinkIsUsable => !string.IsNullOrEmpty(UserId) && !string.IsNullOrEmpty(Code);

    public void OnGet()
    {
        if (!LinkIsUsable) Error = "This link is invalid.";
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var user = LinkIsUsable ? await UserManager.FindByIdAsync(UserId) : null;
        var token = LinkIsUsable ? DecodeToken(Code) : null;
        if (user == null || token == null)
        {
            Error = InvalidLink;
            return Page();
        }

        var result = await UserManager.ResetPasswordAsync(user, token, Password);
        if (!result.Succeeded)
        {
            Error = result.Errors.Any(e => e.Code == nameof(Microsoft.AspNetCore.Identity.IdentityErrorDescriber.InvalidToken))
                ? InvalidLink
                : Describe(result);
            return Page();
        }

        // The link went to this address and was followed, which confirms it.
        if (!user.EmailConfirmed && !string.IsNullOrEmpty(user.Email))
        {
            await UserManager.ConfirmEmailAsync(user, await UserManager.GenerateEmailConfirmationTokenAsync(user));
        }

        return RedirectToPage("/Account/Login");
    }
}
