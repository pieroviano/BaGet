using System;
using System.Threading.Tasks;
using BaGet.Identity.ViewModels;
using BaGet.Identity.Workflows;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Identity.Pages.Account;

/// <summary>
/// The page model of the second step of signing in with a recovery code when it was a Razor Page. Each code works once.
/// </summary>
/// <remarks>
/// Kept as a shim (no breaking change of the public API); <c>AccountController</c> serves <c>/account/loginwithrecoverycode</c> and
/// delegates to the same <c>RecoveryCodeLoginWorkflow</c>.
/// </remarks>
[Obsolete("The recovery code step is served by AccountController; this page model has no page any more and will be removed in the next major version.")]
public class LoginWithRecoveryCodeModel : AccountPageModel
{
    private readonly RecoveryCodeLoginViewModel _state = new RecoveryCodeLoginViewModel();

    [BindProperty]
    public string? RecoveryCode { get => _state.RecoveryCode; set => _state.RecoveryCode = value; }

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get => _state.ReturnUrl; set => _state.ReturnUrl = value; }

    public string? Error => _state.Error;

    public async Task<IActionResult> OnGetAsync() => OutcomeResult(await new RecoveryCodeLoginWorkflow(Screen).ShowAsync());

    public async Task<IActionResult> OnPostAsync() => OutcomeResult(await new RecoveryCodeLoginWorkflow(Screen).PostAsync(_state));
}
