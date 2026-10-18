using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace BaGet.Identity.Pages.Account;

/// <summary>What the "sign in again" prompt shows.</summary>
public record ReauthenticateView(bool Done, bool Available, string ReturnUrl);

/// <summary>
/// Proof of presence for an account without a password: a fresh sign-in with its external
/// provider (the provider is asked to prompt for credentials again, whatever session it has).
/// The time is kept in the sign-in cookie's properties and checked by
/// <see cref="AccountPageModel.IsConfirmedAsync"/> for <see cref="AccountPageModel.ReauthenticationWindow"/>.
/// </summary>
public class ReauthenticateModel : AccountPageModel
{
    protected override bool RequiresSignIn => true;

    [BindProperty(SupportsGet = true)]
    public string ReturnUrl { get; set; }

    public string Error { get; private set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await UserManager.GetUserAsync(User);
        if (user == null) return Challenge();

        if (!Accounts.OpenIdConnect.IsConfigured)
        {
            Error = "There is no external provider to sign in with.";
            return Page();
        }

        var callback = Url.Page("/Account/Reauthenticate", "Callback", new { ReturnUrl });
        var properties = SignInManager.ConfigureExternalAuthenticationProperties(
            AccountsServiceCollectionExtensions.OpenIdConnectScheme,
            callback,
            user.Id);

        // Ask the provider to authenticate again whatever session it holds.
        properties.SetParameter(OpenIdConnectChallengeProperties.PromptKey, "login");
        properties.SetParameter(OpenIdConnectChallengeProperties.MaxAgeKey, System.TimeSpan.Zero);

        return Challenge(properties, AccountsServiceCollectionExtensions.OpenIdConnectScheme);
    }

    public async Task<IActionResult> OnGetCallbackAsync()
    {
        var user = await UserManager.GetUserAsync(User);
        if (user == null) return Challenge();

        // The anti-forgery value is the signed-in user's id, so an answer started by
        // someone else's session is not accepted.
        var info = await SignInManager.GetExternalLoginInfoAsync(user.Id);
        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

        // It must be this account's own login, not any account at the provider.
        var owner = info == null ? null : await UserManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
        if (owner == null || owner.Id != user.Id)
        {
            Error = "The sign-in did not match this account.";
            return Page();
        }

        var authentication = await HttpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        var properties = authentication.Properties ?? new AuthenticationProperties();
        properties.Items[ReauthenticatedItem] = HttpContext.RequestServices.GetRequiredService<SystemTime>().UtcNow.Ticks.ToString();

        // What RefreshSignInAsync keeps, plus the new property; the stamp is unchanged.
        var kept = new[] { ClaimTypes.AuthenticationMethod, "amr" }
            .Select(type => authentication.Principal?.FindFirst(type))
            .Where(claim => claim != null)
            .ToList();
        await SignInManager.SignInWithClaimsAsync(user, properties, kept);

        return RedirectToLocal(ReturnUrl);
    }
}
