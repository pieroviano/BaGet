using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Identity.Pages.Account;

/// <summary>
/// The target of the links in confirmation emails: a new account's address, or, when
/// <c>email</c> is present, a change of address.
/// </summary>
public class ConfirmEmailModel : AccountPageModel
{
    protected override bool RequiresEmail => true;

    public bool Succeeded { get; private set; }

    public bool EmailChanged { get; private set; }

    /// <summary>Whether the account still waits for an administrator (<c>Accounts:RequireApproval</c>) after the confirmation.</summary>
    public bool PendingApproval { get; private set; }

    public async Task<IActionResult> OnGetAsync(string userId, string code, string email)
    {
        var user = string.IsNullOrEmpty(userId) ? null : await UserManager.FindByIdAsync(userId);
        var token = string.IsNullOrEmpty(code) ? null : DecodeToken(code);
        if (user == null || token == null) return Page();

        IdentityResult result;
        if (string.IsNullOrEmpty(email))
        {
            result = await UserManager.ConfirmEmailAsync(user, token);
        }
        else
        {
            result = await UserManager.ChangeEmailAsync(user, email, token);
            EmailChanged = result.Succeeded;
        }

        Succeeded = result.Succeeded;
        PendingApproval = Succeeded && user.PendingApproval;

        // The security stamp changed with the address; keep the session of the same user.
        if (Succeeded && User.Identity?.IsAuthenticated == true && UserManager.GetUserId(User) == user.Id)
        {
            await SignInManager.RefreshSignInAsync(user);
        }

        return Page();
    }
}
