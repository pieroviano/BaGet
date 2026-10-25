using System;
using System.Threading.Tasks;
using BaGet.Identity.ViewModels;
using BaGet.Identity.Workflows;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Identity.Pages.Account;

/// <summary>
/// The page model of the "forgot password" page when it was a Razor Page.
/// </summary>
/// <remarks>
/// Kept as a shim (no breaking change of the public API); <c>AccountController</c> serves <c>/account/forgotpassword</c> and delegates
/// to the same <c>ForgotPasswordWorkflow</c>.
/// </remarks>
[Obsolete("The forgot-password screen is served by AccountController; this page model has no page any more and will be removed in the next major version.")]
public class ForgotPasswordModel : AccountPageModel
{
    private readonly ForgotPasswordViewModel _state = new ForgotPasswordViewModel();

    protected override bool RequiresEmail => true;

    [BindProperty]
    public string? Identifier { get => _state.Identifier; set => _state.Identifier = value; }

    public bool Sent => _state.Sent;

    public void OnGet()
    {
    }

    /// <summary>
    /// Answers the same, in the same time, whether or not an account matches, whether or
    /// not the mail could be sent and whether or not the request was rate limited, so the
    /// page cannot be used to find out who has an account.
    /// </summary>
    public async Task<IActionResult> OnPostAsync() => OutcomeResult(await new ForgotPasswordWorkflow(Screen).PostAsync(_state));
}
