using System;
using System.Threading.Tasks;
using BaGet.Identity.Mvc;
using BaGet.Identity.ViewModels;

namespace BaGet.Identity.Workflows;

/// <summary>
/// The second step of signing in with an authenticator code: the password was right and a half-signed-in cookie says who, but
/// nothing is granted until the code is too. Shared by <c>AccountController</c> and the obsolete <c>LoginWith2faModel</c>.
/// </summary>
internal sealed class TwoFactorLoginWorkflow
{
    private readonly AccountScreen _screen;

    public TwoFactorLoginWorkflow(AccountScreen screen)
    {
        _screen = screen ?? throw new ArgumentNullException(nameof(screen));
    }

    /// <summary>Without the half-signed-in cookie there is nothing to complete: back to the first step.</summary>
    public async Task<ScreenOutcome> ShowAsync()
    {
        if (await _screen.SignInManager.GetTwoFactorAuthenticationUserAsync() == null) return ScreenOutcome.ToPage("/Account/Login");

        return ScreenOutcome.Show;
    }

    /// <summary>The code was posted.</summary>
    public async Task<ScreenOutcome> PostAsync(TwoFactorLoginViewModel model)
    {
        if (await _screen.SignInManager.GetTwoFactorAuthenticationUserAsync() == null) return ScreenOutcome.ToPage("/Account/Login");
        if (!_screen.ModelState.IsValid) return ScreenOutcome.Show;

        var code = model.Code?.Replace(" ", string.Empty).Replace("-", string.Empty);
        #pragma warning disable CS8604
        var result = await _screen.SignInManager.TwoFactorAuthenticatorSignInAsync(code, model.RememberMe, rememberClient: false);
        #pragma warning restore CS8604

        if (result.Succeeded) return ScreenOutcome.Local(model.ReturnUrl);

        model.Error = result.IsLockedOut
            ? "The account is locked after too many failed attempts. Try again later."
            : "The code is not valid.";

        return ScreenOutcome.Show;
    }
}

/// <summary>
/// Second step of signing in with a recovery code instead of an authenticator code. Each code works once. Shared by
/// <c>AccountController</c> and the obsolete <c>LoginWithRecoveryCodeModel</c>.
/// </summary>
internal sealed class RecoveryCodeLoginWorkflow
{
    private readonly AccountScreen _screen;

    public RecoveryCodeLoginWorkflow(AccountScreen screen)
    {
        _screen = screen ?? throw new ArgumentNullException(nameof(screen));
    }

    /// <summary>Without the half-signed-in cookie there is nothing to complete: back to the first step.</summary>
    public async Task<ScreenOutcome> ShowAsync()
    {
        if (await _screen.SignInManager.GetTwoFactorAuthenticationUserAsync() == null) return ScreenOutcome.ToPage("/Account/Login");

        return ScreenOutcome.Show;
    }

    /// <summary>The code was posted.</summary>
    public async Task<ScreenOutcome> PostAsync(RecoveryCodeLoginViewModel model)
    {
        if (await _screen.SignInManager.GetTwoFactorAuthenticationUserAsync() == null) return ScreenOutcome.ToPage("/Account/Login");
        if (!_screen.ModelState.IsValid) return ScreenOutcome.Show;

        #pragma warning disable CS8604
        var result = await _screen.SignInManager.TwoFactorRecoveryCodeSignInAsync(model.RecoveryCode?.Replace(" ", string.Empty));
        #pragma warning restore CS8604

        if (result.Succeeded) return ScreenOutcome.Local(model.ReturnUrl);

        model.Error = result.IsLockedOut
            ? "The account is locked after too many failed attempts. Try again later."
            : "The recovery code is not valid, or was already used.";

        return ScreenOutcome.Show;
    }
}

/// <summary>Signing out is a POST, so a link elsewhere cannot sign the user out.</summary>
internal sealed class LogoutWorkflow
{
    private readonly AccountScreen _screen;

    public LogoutWorkflow(AccountScreen screen)
    {
        _screen = screen ?? throw new ArgumentNullException(nameof(screen));
    }

    /// <summary>Signs the user out and goes to the site root.</summary>
    public async Task<ScreenOutcome> PostAsync()
    {
        await _screen.SignInManager.SignOutAsync();

        return ScreenOutcome.Local(null);
    }
}
