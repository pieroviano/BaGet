using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace BaGet.Identity.Pages.Account;

/// <summary>
/// Change password, change email, delete the account. Each form posts to its own handler
/// and takes its fields as handler parameters, so one form's rules never block another's.
/// </summary>
public class SettingsModel : AccountPageModel
{
    protected override bool RequiresSignIn => true;

    public string Email { get; private set; }

    public bool EmailConfirmed { get; private set; }

    public bool HasPassword { get; private set; } = true;

    public bool CanSendEmail => EmailConfigured;

    public string Message { get; private set; }

    public string Error { get; private set; }

    /// <summary>
    /// Why the account cannot be deleted yet, when it cannot.
    /// </summary>
    public DeletionBlockers Blockers { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        return await LoadAsync() ? Page() : Challenge();
    }

    public async Task<IActionResult> OnPostChangePasswordAsync(string currentPassword, string newPassword, string confirmPassword)
    {
        var user = await UserManager.GetUserAsync(User);
        if (user == null) return Challenge();

        if (string.IsNullOrEmpty(newPassword) || newPassword != confirmPassword)
        {
            Error = "The new passwords do not match.";
        }
        else
        {
            var result = await UserManager.HasPasswordAsync(user)
                ? await UserManager.ChangePasswordAsync(user, currentPassword ?? string.Empty, newPassword)
                : await UserManager.AddPasswordAsync(user, newPassword);

            if (result.Succeeded)
            {
                // The security stamp changed; keep this session signed in.
                await SignInManager.RefreshSignInAsync(user);
                Message = "Your password has been changed.";
            }
            else
            {
                Error = Describe(result);
            }
        }

        await LoadAsync();

        return Page();
    }

    public async Task<IActionResult> OnPostChangeEmailAsync(string newEmail)
    {
        var user = await UserManager.GetUserAsync(User);
        if (user == null) return Challenge();

        newEmail = newEmail?.Trim();

        if (string.IsNullOrEmpty(newEmail) || !new EmailAddressAttribute().IsValid(newEmail))
        {
            Error = "Enter a valid email address.";
        }
        else if (!EmailConfigured)
        {
            var result = await UserManager.SetEmailAsync(user, newEmail);
            if (result.Succeeded) Message = "Your email address has been changed.";
            else Error = Describe(result);
        }
        else
        {
            var token = await UserManager.GenerateChangeEmailTokenAsync(user, newEmail);
            var link = PageLink("/Account/ConfirmEmail", new { userId = user.Id, email = newEmail, code = EncodeToken(token) });

            try
            {
                await EmailSender.SendConfirmationLinkAsync(user, newEmail, link);
                Message = "We sent a confirmation link to the new address. Your address changes when you follow it.";
            }
            catch (System.Exception)
            {
                Error = "We could not send the confirmation email. Try again later.";
            }
        }

        await LoadAsync();

        return Page();
    }

    /// <summary>
    /// The password is asked again, so a stolen session cannot delete the account. An
    /// account without a password (external sign-in only) confirms by typing its user name.
    /// </summary>
    public async Task<IActionResult> OnPostDeleteAsync(string password, CancellationToken cancellationToken)
    {
        var user = await UserManager.GetUserAsync(User);
        if (user == null) return Challenge();

        var confirmed = await UserManager.HasPasswordAsync(user)
            ? await UserManager.CheckPasswordAsync(user, password ?? string.Empty)
            : string.Equals(password, user.UserName, System.StringComparison.Ordinal);

        if (!confirmed)
        {
            Error = "The confirmation is incorrect, so the account was not deleted.";
        }
        else
        {
            var deletion = HttpContext.RequestServices.GetRequiredService<AccountDeletionService>();
            var (blockers, result) = await deletion.DeleteAsync(user, cancellationToken);

            if (blockers.Any)
            {
                Blockers = blockers;
                Error = "The account cannot be deleted yet.";
            }
            else if (!result.Succeeded)
            {
                Error = Describe(result);
            }
            else
            {
                await SignInManager.SignOutAsync();

                return RedirectToLocal(null);
            }
        }

        await LoadAsync();

        return Page();
    }

    private async Task<bool> LoadAsync()
    {
        var user = await UserManager.GetUserAsync(User);
        if (user == null) return false;

        Email = user.Email;
        EmailConfirmed = user.EmailConfirmed;
        HasPassword = await UserManager.HasPasswordAsync(user);

        return true;
    }
}
