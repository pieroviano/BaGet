using System;
using System.Threading.Tasks;
using BaGet.Identity.ViewModels;
using BaGet.Identity.Workflows;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Identity.Pages.Account;

/// <summary>
/// The page model of the second step of signing in with two-factor authentication when it was a Razor Page: the password was
/// right and a half-signed-in cookie says who, but nothing is granted until the code is too.
/// </summary>
/// <remarks>
/// Kept as a shim (no breaking change of the public API); <c>AccountController</c> serves <c>/account/loginwith2fa</c> and delegates to
/// the same <c>TwoFactorLoginWorkflow</c>.
/// </remarks>
[Obsolete("The second sign-in step is served by AccountController; this page model has no page any more and will be removed in the next major version.")]
public class LoginWith2faModel : AccountPageModel
{
    private readonly TwoFactorLoginViewModel _state = new TwoFactorLoginViewModel();

    [BindProperty]
    public string? Code { get => _state.Code; set => _state.Code = value; }

    [BindProperty(SupportsGet = true)]
    public bool RememberMe { get => _state.RememberMe; set => _state.RememberMe = value; }

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get => _state.ReturnUrl; set => _state.ReturnUrl = value; }

    public string? Error => _state.Error;

    public async Task<IActionResult> OnGetAsync() => OutcomeResult(await new TwoFactorLoginWorkflow(Screen).ShowAsync());

    public async Task<IActionResult> OnPostAsync() => OutcomeResult(await new TwoFactorLoginWorkflow(Screen).PostAsync(_state));
}
