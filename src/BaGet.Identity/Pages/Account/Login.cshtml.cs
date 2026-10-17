using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace BaGet.Identity.Pages.Account
{
    public class LoginModel : AccountPageModel
    {
        [BindProperty]
        [Required]
        [Display(Name = "User name")]
        public string UserName { get; set; }

        [BindProperty]
        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        [BindProperty]
        [Display(Name = "Remember me")]
        public bool RememberMe { get; set; }

        [BindProperty(SupportsGet = true)]
        public string ReturnUrl { get; set; }

        public bool AllowRegistration => Accounts.AllowRegistration;

        public bool HasExternalProvider => Accounts.OpenIdConnect.IsConfigured;

        public string ExternalProviderName => Accounts.OpenIdConnect.DisplayName;

        public string Error { get; set; }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return Page();

            var result = await SignInManager.PasswordSignInAsync(UserName, Password, RememberMe, lockoutOnFailure: true);
            if (result.Succeeded)
            {
                return RedirectToLocal(ReturnUrl);
            }

            Error = result.IsLockedOut
                ? "The account is locked after too many failed attempts. Try again later."
                : "The user name or password is incorrect.";

            return Page();
        }

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

            var name = await ChooseUserNameAsync(info.Principal, cancellationToken);
            if (name == null)
            {
                Error = "The external account has no usable name.";
                return Page();
            }

            var user = new BaGetUser
            {
                UserName = name,
                Email = info.Principal.FindFirstValue(ClaimTypes.Email),
                Created = HttpContext.RequestServices.GetRequiredService<SystemTime>().UtcNow,
            };

            var created = await UserManager.CreateAsync(user);
            if (created.Succeeded)
            {
                created = await UserManager.AddLoginAsync(user, info);
            }

            if (!created.Succeeded)
            {
                Error = string.Join(" ", created.Errors.Select(e => e.Description));
                return Page();
            }

            await SignInManager.SignInAsync(user, isPersistent: false);

            return RedirectToLocal(ReturnUrl);
        }

        /// <summary>
        /// The provider's preferred user name, else the email's local part, made unique
        /// across users and organizations by a numeric suffix.
        /// </summary>
        private async Task<string> ChooseUserNameAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
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
}
