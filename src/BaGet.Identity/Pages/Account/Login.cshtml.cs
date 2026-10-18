using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BaGet.Identity.Pages.Account;

public class LoginModel : AccountPageModel
{
    [BindProperty]
    [Required]
    [Display(Name = "User name")]
    public string? UserName { get; set; }

    [BindProperty]
    [Required]
    [DataType(DataType.Password)]
    public string? Password { get; set; }

    [BindProperty]
    [Display(Name = "Remember me")]
    public bool RememberMe { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public bool AllowRegistration => Accounts.AllowRegistration;

    public bool HasExternalProvider => Accounts.OpenIdConnect.IsConfigured;

    public string ExternalProviderName => Accounts.OpenIdConnect.DisplayName;

    public string? Error { get; set; }

    public string? Notice { get; set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        #pragma warning disable CS8604
        var result = await SignInManager.PasswordSignInAsync(UserName, Password, RememberMe, lockoutOnFailure: true);
        #pragma warning restore CS8604
        if (result.Succeeded)
        {
            return RedirectToLocal(ReturnUrl);
        }

        if (result.RequiresTwoFactor)
        {
            return RedirectToPage("/Account/LoginWith2fa", new { ReturnUrl, RememberMe });
        }

        Error = result.IsLockedOut
            ? "The account is locked after too many failed attempts. Try again later."
            : result.IsNotAllowed
                ? "Confirm your email address before signing in. Use the link we sent you, or reset your password."
                : "The user name or password is incorrect.";

        return Page();
    }

    public bool EmailIsConfigured => EmailConfigured;

    public IActionResult OnPostExternal()
    {
        if (!HasExternalProvider) return NotFound();

        var callback = Url.Page("/Account/Login", "Callback", new { ReturnUrl });
        var properties = SignInManager.ConfigureExternalAuthenticationProperties(
            AccountsServiceCollectionExtensions.OpenIdConnectScheme,
            callback);

        return Challenge(properties, AccountsServiceCollectionExtensions.OpenIdConnectScheme);
    }

    /// <summary>
    /// The provider has signed the user in. The first time, an account is created for
    /// them: the provider is the gate, so this does not depend on AllowRegistration.
    /// </summary>
    public async Task<IActionResult> OnGetCallbackAsync(CancellationToken cancellationToken)
    {
        var info = await SignInManager.GetExternalLoginInfoAsync();
        if (info == null)
        {
            Error = "The external sign-in failed.";
            return Page();
        }

        var result = await SignInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: false);
        if (result.Succeeded)
        {
            return RedirectToLocal(ReturnUrl);
        }

        if (result.RequiresTwoFactor)
        {
            return RedirectToPage("/Account/LoginWith2fa", new { ReturnUrl });
        }

        if (result.IsLockedOut)
        {
            Error = "The account is locked after too many failed attempts. Try again later.";
            return Page();
        }

        if (result.IsNotAllowed)
        {
            // This provider login already belongs to an account, which cannot sign in yet
            // (its email is unconfirmed). Never make another account for it.
            var existing = await UserManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
            if (existing != null && await TrySendConfirmationAsync(existing))
            {
                Notice = "Confirm your email address before signing in. We sent you a new confirmation link.";
            }
            else
            {
                Error = "Confirm your email address before signing in. Use the link we sent you, or reset your password.";
            }

            return Page();
        }

        var email = info.Principal.FindFirstValue(ClaimTypes.Email);
        if (EmailConfigured && string.IsNullOrWhiteSpace(email))
        {
            Error = "The external account has no email address, which this server needs to confirm accounts.";
            return Page();
        }

        var name = await ChooseUserNameAsync(info.Principal, cancellationToken);
        if (name == null)
        {
            Error = "The external account has no usable name.";
            return Page();
        }

        var user = new BaGetUser
        {
            UserName = name,
            Email = email,

            // Only an address the provider vouches for is confirmed; any other must be
            // proven by following a link, like one typed into the registration form.
            EmailConfirmed = !string.IsNullOrWhiteSpace(email) && ProviderVerifiedEmail(info.Principal),
            Created = HttpContext.RequestServices.GetRequiredService<SystemTime>().UtcNow,
        };

        var created = await UserManager.CreateAsync(user);
        if (created.Succeeded)
        {
            created = await UserManager.AddLoginAsync(user, info);
        }

        if (!created.Succeeded)
        {
            // Do not leave an account behind that has no login to reach it.
            if (user.Id != null && await UserManager.FindByIdAsync(user.Id) != null) await UserManager.DeleteAsync(user);

            Error = string.Join(" ", created.Errors.Select(e => e.Description));
            return Page();
        }

        if (EmailConfigured && !user.EmailConfirmed)
        {
            Notice = await TrySendConfirmationAsync(user)
                ? "Your account was created. Confirm your email address with the link we sent you, then sign in again."
                : "Your account was created, but we could not send the confirmation email. Use \"Forgot password\" to get a link later.";
            return Page();
        }

        await SignInManager.SignInAsync(user, isPersistent: false);

        return RedirectToLocal(ReturnUrl);
    }

    private static bool ProviderVerifiedEmail(ClaimsPrincipal principal)
        => bool.TryParse(principal.FindFirstValue("email_verified"), out var verified) && verified;

    private async Task<bool> TrySendConfirmationAsync(BaGetUser user)
    {
        if (!EmailConfigured || string.IsNullOrEmpty(user.Email) || user.EmailConfirmed) return false;

        try
        {
            var token = await UserManager.GenerateEmailConfirmationTokenAsync(user);
            var link = PageLink("/Account/ConfirmEmail", new { userId = user.Id, code = EncodeToken(token) });
            await EmailSender.SendConfirmationLinkAsync(user, user.Email, link);

            return true;
        }
        catch (Exception e)
        {
            HttpContext.RequestServices.GetRequiredService<Microsoft.Extensions.Logging.ILogger<LoginModel>>()
                .LogError(e, "Could not send a confirmation link.");

            return false;
        }
    }

    /// <summary>
    /// The provider's preferred user name, else the email's local part, made unique
    /// across users and organizations by a numeric suffix.
    /// </summary>
    private async Task<string?> ChooseUserNameAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        var candidate = principal.FindFirstValue("preferred_username")
                        ?? principal.FindFirstValue(ClaimTypes.Email)?.Split('@')[0]
                        ?? principal.FindFirstValue(ClaimTypes.Name);

        if (string.IsNullOrWhiteSpace(candidate)) return null;

        var allowed = UserManager.Options.User.AllowedUserNameCharacters;
        candidate = new string(candidate.Where(c => allowed.Contains(c)).ToArray());
        if (candidate.Length == 0) return null;

        var name = candidate;
        for (var suffix = 2; await Ownership.AccountExistsAsync(name, cancellationToken); suffix++)
        {
            name = candidate + suffix;
        }

        return name;
    }
}