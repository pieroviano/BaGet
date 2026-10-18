using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Mvc;
using QRCoder;

namespace BaGet.Identity.Pages.Account;

/// <summary>
/// Authenticator-app (TOTP) two-factor authentication: set up with a QR code, recovery
/// codes shown once, turn off, or start over with a new key.
/// </summary>
public class TwoFactorModel : AccountPageModel
{
    private const int RecoveryCodeCount = 10;
    private const string Issuer = "BaGet Extension";

    protected override bool RequiresSignIn => true;

    public bool TwoFactorEnabled { get; private set; }

    /// <summary>
    /// The key to type into an authenticator that cannot scan the QR code.
    /// </summary>
    public string? SharedKey { get; private set; }

    /// <summary>
    /// The QR code of the setup link, an SVG built here from the user's own key.
    /// </summary>
    public string? QrCodeSvg { get; private set; }

    public bool Reauthenticated { get; private set; }

    public bool CanReauthenticate => Accounts.OpenIdConnect.IsConfigured;

    public int RecoveryCodesLeft { get; private set; }

    public bool HasPassword { get; private set; } = true;

    /// <summary>
    /// The new recovery codes. Shown once, when two-factor authentication is turned on.
    /// </summary>
    public IReadOnlyList<string> RecoveryCodes { get; private set; } = Array.Empty<string>();

    public string? Error { get; private set; }

    public string? Message { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        return await LoadAsync() ? Page() : Challenge();
    }

    public async Task<IActionResult> OnPostEnableAsync(string? code)
    {
        var user = await UserManager.GetUserAsync(User);
        if (user == null) return Challenge();

        var cleaned = (code ?? string.Empty).Replace(" ", string.Empty).Replace("-", string.Empty);
        var valid = cleaned.Length > 0 && await UserManager.VerifyTwoFactorTokenAsync(
            user,
            UserManager.Options.Tokens.AuthenticatorTokenProvider,
            cleaned);

        if (!valid)
        {
            Error = "The code is not valid. Check the authenticator app and try again.";
        }
        else
        {
            await UserManager.SetTwoFactorEnabledAsync(user, true);
            await SignInManager.RefreshSignInAsync(user);
            #pragma warning disable CS8604
            RecoveryCodes = (await UserManager.GenerateNewTwoFactorRecoveryCodesAsync(user, RecoveryCodeCount)).ToList();
            #pragma warning restore CS8604
            Message = "Two-factor authentication is on. Keep these recovery codes somewhere safe: each works once, and they are not shown again.";
        }

        await LoadAsync();

        return Page();
    }

    public async Task<IActionResult> OnPostDisableAsync(string? password)
    {
        var user = await UserManager.GetUserAsync(User);
        if (user == null) return Challenge();

        if (!await IsConfirmedAsync(user, password))
        {
            Error = "The confirmation is incorrect, so nothing was changed.";
        }
        else
        {
            await UserManager.SetTwoFactorEnabledAsync(user, false);
            await SignInManager.RefreshSignInAsync(user);
            Message = "Two-factor authentication is off.";
        }

        await LoadAsync();

        return Page();
    }

    /// <summary>
    /// A new key makes the old authenticator and every recovery code useless; it must be
    /// set up and enabled again.
    /// </summary>
    public async Task<IActionResult> OnPostResetAsync(string? password)
    {
        var user = await UserManager.GetUserAsync(User);
        if (user == null) return Challenge();

        if (!await IsConfirmedAsync(user, password))
        {
            Error = "The confirmation is incorrect, so nothing was changed.";
        }
        else
        {
            await UserManager.SetTwoFactorEnabledAsync(user, false);
            await UserManager.ResetAuthenticatorKeyAsync(user);
            await SignInManager.RefreshSignInAsync(user);
            Message = "The authenticator was reset. Scan the new code to turn two-factor authentication on again.";
        }

        await LoadAsync();

        return Page();
    }

    private async Task<bool> LoadAsync()
    {
        var user = await UserManager.GetUserAsync(User);
        if (user == null) return false;

        TwoFactorEnabled = user.TwoFactorEnabled;
        HasPassword = await UserManager.HasPasswordAsync(user);
        Reauthenticated = !HasPassword && await HasReauthenticatedRecentlyAsync();
        RecoveryCodesLeft = await UserManager.CountRecoveryCodesAsync(user);

        if (!TwoFactorEnabled)
        {
            var key = await UserManager.GetAuthenticatorKeyAsync(user);
            if (string.IsNullOrEmpty(key))
            {
                await UserManager.ResetAuthenticatorKeyAsync(user);

                // The security stamp changed with the key; keep this session signed in.
                await SignInManager.RefreshSignInAsync(user);
                key = await UserManager.GetAuthenticatorKeyAsync(user);
            }

            SharedKey = Format(key);
            QrCodeSvg = RenderQrCode(SetupUri(user.UserName, key));
        }

        return true;
    }

    private static string Format(string? key)
    {
        var result = new StringBuilder();
        #pragma warning disable CS8602
        for (var i = 0; i < key.Length; i += 4)
        #pragma warning restore CS8602
        {
            if (i > 0) result.Append(' ');
            result.Append(key, i, Math.Min(4, key.Length - i));
        }

        return result.ToString();
    }

    #pragma warning disable CS8604
    internal static string SetupUri(string? userName, string? key)
        => $"otpauth://totp/{Uri.EscapeDataString(Issuer)}:{Uri.EscapeDataString(userName)}"
           + $"?secret={key}&issuer={Uri.EscapeDataString(Issuer)}&digits=6";
    #pragma warning restore CS8604

    internal static string RenderQrCode(string text)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.M);

        return new SvgQRCode(data).GetGraphic(4);
    }
}
