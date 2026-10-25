using System;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Identity.ViewModels;
using BaGet.Identity.Workflows;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace BaGet.Identity.Pages.Account;

/// <summary>
/// The page model of the registration page when it was a Razor Page.
/// </summary>
/// <remarks>
/// Kept as a shim (no breaking change of the public API); <c>AccountController</c> serves <c>/account/register</c> and delegates to the
/// same <c>RegisterWorkflow</c>.
/// </remarks>
[Obsolete("The registration screen is served by AccountController; this page model has no page any more and will be removed in the next major version.")]
public class RegisterModel : AccountPageModel
{
    private readonly RegisterViewModel _state = new RegisterViewModel();

    [BindProperty]
    public string? UserName { get => _state.UserName; set => _state.UserName = value; }

    [BindProperty]
    public string? Email { get => _state.Email; set => _state.Email = value; }

    [BindProperty]
    public string? Password { get => _state.Password; set => _state.Password = value; }

    [BindProperty]
    public string? ConfirmPassword { get => _state.ConfirmPassword; set => _state.ConfirmPassword = value; }

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get => _state.ReturnUrl; set => _state.ReturnUrl = value; }

    public override void OnPageHandlerExecuting(PageHandlerExecutingContext context)
    {
        base.OnPageHandlerExecuting(context);

        if (context.Result == null && !Accounts.AllowRegistration)
        {
            context.Result = NotFound();
        }
    }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
        => OutcomeResult(await new RegisterWorkflow(Screen).PostAsync(_state, cancellationToken));

    /// <summary>Whether the account was created but waits for an administrator (<c>Accounts:RequireApproval</c>).</summary>
    public bool AwaitingApproval => _state.AwaitingApproval;

    public bool ConfirmationSent => _state.ConfirmationSent;

    public bool EmailRequired => EmailConfigured;
}
