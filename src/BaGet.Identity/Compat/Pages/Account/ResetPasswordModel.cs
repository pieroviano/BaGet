using System;
using System.Threading.Tasks;
using BaGet.Identity.ViewModels;
using BaGet.Identity.Workflows;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Identity.Pages.Account;

/// <summary>
/// The page model of the reset-password page (the target of the link in a reset email) when it was a Razor Page.
/// </summary>
/// <remarks>
/// Kept as a shim (no breaking change of the public API); <c>AccountController</c> serves <c>/account/resetpassword</c> and delegates
/// to the same <c>ResetPasswordWorkflow</c>.
/// </remarks>
[Obsolete("The reset-password screen is served by AccountController; this page model has no page any more and will be removed in the next major version.")]
public class ResetPasswordModel : AccountPageModel
{
    private readonly ResetPasswordViewModel _state = new ResetPasswordViewModel();

    protected override bool RequiresEmail => true;

    [BindProperty(SupportsGet = true)]
    public string? UserId { get => _state.UserId; set => _state.UserId = value; }

    [BindProperty(SupportsGet = true)]
    public string? Code { get => _state.Code; set => _state.Code = value; }

    [BindProperty]
    public string? Password { get => _state.Password; set => _state.Password = value; }

    [BindProperty]
    public string? ConfirmPassword { get => _state.ConfirmPassword; set => _state.ConfirmPassword = value; }

    public string? Error => _state.Error;

    public bool LinkIsUsable => _state.LinkIsUsable;

    public void OnGet() => new ResetPasswordWorkflow(Screen).Show(_state);

    public async Task<IActionResult> OnPostAsync() => OutcomeResult(await new ResetPasswordWorkflow(Screen).PostAsync(_state));
}
