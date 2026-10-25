using System.Threading;
using System.Threading.Tasks;
using BaGet.Identity.Mvc;
using BaGet.Identity.ViewModels;
using BaGet.Identity.Workflows;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace BaGet.Identity.Controllers;

/// <summary>
/// The sign-in side of the account screens: sign in (password and external provider), the two second-factor steps, sign out,
/// registration, email confirmation, the password reset pair and re-authentication. It replaces the Razor Pages of
/// <c>Pages/Account</c> of the same names: same URLs, same <c>?handler=</c> forms, same
/// <c>asp-page</c> names (<see cref="LegacyPageAttribute"/>). The rules live in the workflows of <c>BaGet.Identity.Workflows</c>.
/// </summary>
public sealed class AccountController : AccountControllerBase
{
    /// <summary>The registration screen answers 404 while <c>Accounts:AllowRegistration</c> is off, after the base checks.</summary>
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        base.OnActionExecuting(context);

        if (context.Result == null
            && context.ActionDescriptor is ControllerActionDescriptor { ActionName: nameof(Register) or nameof(RegisterPost) }
            && !Accounts.AllowRegistration)
        {
            context.Result = NotFound();
        }
    }

    // ----- /account/login

    /// <summary>The sign-in form.</summary>
    [Route("account/login"), HttpGet, HttpHead, PageHandler, LegacyPage("/Account/Login")]
    public IActionResult Login(string? returnUrl)
    {
        var model = new LoginViewModel { ReturnUrl = returnUrl };

        return Respond(new LoginWorkflow(Screen).Render(model), "Login", model);
    }

    /// <summary>The sign-in form was posted.</summary>
    [Route("account/login"), HttpPost, PageHandler, LegacyPage("/Account/Login")]
    public async Task<IActionResult> LoginPost(LoginViewModel model)
        => Respond(await new LoginWorkflow(Screen).PostAsync(model), "Login", model);

    /// <summary>"Sign in with the provider" was posted.</summary>
    [Route("account/login"), HttpPost, PageHandler("External"), LegacyPage("/Account/Login")]
    public IActionResult LoginExternal(LoginViewModel model)
        => Respond(new LoginWorkflow(Screen).External(model), "Login", model);

    /// <summary>The provider has signed the user in.</summary>
    [Route("account/login"), HttpGet, PageHandler("Callback"), LegacyPage("/Account/Login")]
    public async Task<IActionResult> LoginCallback(string? returnUrl, CancellationToken cancellationToken)
    {
        var model = new LoginViewModel { ReturnUrl = returnUrl };

        return Respond(await new LoginWorkflow(Screen).CallbackAsync(model, cancellationToken), "Login", model);
    }

    // ----- /account/loginwith2fa

    /// <summary>The authenticator code form.</summary>
    [Route("account/loginwith2fa"), HttpGet, HttpHead, PageHandler, LegacyPage("/Account/LoginWith2fa")]
    public async Task<IActionResult> LoginWith2fa(bool rememberMe, string? returnUrl)
    {
        var model = new TwoFactorLoginViewModel { RememberMe = rememberMe, ReturnUrl = returnUrl };

        return Respond(await new TwoFactorLoginWorkflow(Screen).ShowAsync(), "LoginWith2fa", model);
    }

    /// <summary>The authenticator code was posted.</summary>
    [Route("account/loginwith2fa"), HttpPost, PageHandler, LegacyPage("/Account/LoginWith2fa")]
    public async Task<IActionResult> LoginWith2faPost(TwoFactorLoginViewModel model)
        => Respond(await new TwoFactorLoginWorkflow(Screen).PostAsync(model), "LoginWith2fa", model);

    // ----- /account/loginwithrecoverycode

    /// <summary>The recovery code form.</summary>
    [Route("account/loginwithrecoverycode"), HttpGet, HttpHead, PageHandler, LegacyPage("/Account/LoginWithRecoveryCode")]
    public async Task<IActionResult> LoginWithRecoveryCode(string? returnUrl)
    {
        var model = new RecoveryCodeLoginViewModel { ReturnUrl = returnUrl };

        return Respond(await new RecoveryCodeLoginWorkflow(Screen).ShowAsync(), "LoginWithRecoveryCode", model);
    }

    /// <summary>The recovery code was posted.</summary>
    [Route("account/loginwithrecoverycode"), HttpPost, PageHandler, LegacyPage("/Account/LoginWithRecoveryCode")]
    public async Task<IActionResult> LoginWithRecoveryCodePost(RecoveryCodeLoginViewModel model)
        => Respond(await new RecoveryCodeLoginWorkflow(Screen).PostAsync(model), "LoginWithRecoveryCode", model);

    // ----- /account/logout

    /// <summary>The sign-out button (signing out is a POST, so a link elsewhere cannot do it).</summary>
    [Route("account/logout"), HttpGet, HttpHead, PageHandler, LegacyPage("/Account/Logout")]
    public IActionResult Logout() => View("Logout");

    /// <summary>Signs the user out.</summary>
    [Route("account/logout"), HttpPost, PageHandler, LegacyPage("/Account/Logout")]
    public async Task<IActionResult> LogoutPost()
        => Respond(await new LogoutWorkflow(Screen).PostAsync(), "Logout");

    // ----- /account/register

    /// <summary>The registration form.</summary>
    [Route("account/register"), HttpGet, HttpHead, PageHandler, LegacyPage("/Account/Register")]
    public IActionResult Register(string? returnUrl)
    {
        var model = new RegisterViewModel { ReturnUrl = returnUrl };

        return Respond(new RegisterWorkflow(Screen).Render(model), "Register", model);
    }

    /// <summary>The registration form was posted.</summary>
    [Route("account/register"), HttpPost, PageHandler, LegacyPage("/Account/Register")]
    public async Task<IActionResult> RegisterPost(RegisterViewModel model, CancellationToken cancellationToken)
        => Respond(await new RegisterWorkflow(Screen).PostAsync(model, cancellationToken), "Register", model);

    // ----- /account/confirmemail

    /// <summary>The target of the links in confirmation emails.</summary>
    [Route("account/confirmemail"), HttpGet, HttpHead, PageHandler, LegacyPage("/Account/ConfirmEmail"), RequiresEmail]
    public async Task<IActionResult> ConfirmEmail(string? userId, string? code, string? email)
    {
        var model = new ConfirmEmailViewModel();

        return Respond(await new ConfirmEmailWorkflow(Screen).ConfirmAsync(model, userId, code, email), "ConfirmEmail", model);
    }

    // ----- /account/forgotpassword

    /// <summary>The "forgot password" form.</summary>
    [Route("account/forgotpassword"), HttpGet, HttpHead, PageHandler, LegacyPage("/Account/ForgotPassword"), RequiresEmail]
    public IActionResult ForgotPassword() => View("ForgotPassword", new ForgotPasswordViewModel());

    /// <summary>The "forgot password" form was posted.</summary>
    [Route("account/forgotpassword"), HttpPost, PageHandler, LegacyPage("/Account/ForgotPassword"), RequiresEmail]
    public async Task<IActionResult> ForgotPasswordPost(ForgotPasswordViewModel model)
        => Respond(await new ForgotPasswordWorkflow(Screen).PostAsync(model), "ForgotPassword", model);

    // ----- /account/resetpassword

    /// <summary>The new-password form behind the link of a reset email.</summary>
    [Route("account/resetpassword"), HttpGet, HttpHead, PageHandler, LegacyPage("/Account/ResetPassword"), RequiresEmail]
    public IActionResult ResetPassword(string? userId, string? code)
    {
        var model = new ResetPasswordViewModel { UserId = userId, Code = code };

        return Respond(new ResetPasswordWorkflow(Screen).Show(model), "ResetPassword", model);
    }

    /// <summary>The new password was posted.</summary>
    [Route("account/resetpassword"), HttpPost, PageHandler, LegacyPage("/Account/ResetPassword"), RequiresEmail]
    public async Task<IActionResult> ResetPasswordPost(ResetPasswordViewModel model)
        => Respond(await new ResetPasswordWorkflow(Screen).PostAsync(model), "ResetPassword", model);

    // ----- /account/reauthenticate

    /// <summary>The "confirm it is you" screen (only the target of the sign-in-again button; the form is elsewhere).</summary>
    [Route("account/reauthenticate"), HttpGet, HttpHead, PageHandler, LegacyPage("/Account/Reauthenticate"), RequiresSignIn]
    public IActionResult Reauthenticate(string? returnUrl)
        => View("Reauthenticate", new ReauthenticateViewModel { ReturnUrl = returnUrl });

    /// <summary>"Sign in again" was posted: sends the user to the provider.</summary>
    [Route("account/reauthenticate"), HttpPost, PageHandler, LegacyPage("/Account/Reauthenticate"), RequiresSignIn]
    public async Task<IActionResult> ReauthenticatePost(ReauthenticateViewModel model)
        => Respond(await new ReauthenticateWorkflow(Screen).PostAsync(model), "Reauthenticate", model);

    /// <summary>The provider answered.</summary>
    [Route("account/reauthenticate"), HttpGet, PageHandler("Callback"), LegacyPage("/Account/Reauthenticate"), RequiresSignIn]
    public async Task<IActionResult> ReauthenticateCallback(string? returnUrl)
    {
        var model = new ReauthenticateViewModel { ReturnUrl = returnUrl };

        return Respond(await new ReauthenticateWorkflow(Screen).CallbackAsync(model), "Reauthenticate", model);
    }
}
