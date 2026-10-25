using System;
using System.Threading.Tasks;
using BaGet.Identity.ViewModels;
using BaGet.Identity.Workflows;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Identity.Pages.Account;

/// <summary>
/// The page model of the target of the links in confirmation emails when it was a Razor Page: a new account's address, or, when
/// <c>email</c> is present, a change of address.
/// </summary>
/// <remarks>
/// Kept as a shim (no breaking change of the public API); <c>AccountController</c> serves <c>/account/confirmemail</c> and delegates to
/// the same <c>ConfirmEmailWorkflow</c>.
/// </remarks>
[Obsolete("The email confirmation is served by AccountController; this page model has no page any more and will be removed in the next major version.")]
public class ConfirmEmailModel : AccountPageModel
{
    private readonly ConfirmEmailViewModel _state = new ConfirmEmailViewModel();

    protected override bool RequiresEmail => true;

    public bool Succeeded => _state.Succeeded;

    public bool EmailChanged => _state.EmailChanged;

    /// <summary>Whether the account still waits for an administrator (<c>Accounts:RequireApproval</c>) after the confirmation.</summary>
    public bool PendingApproval => _state.PendingApproval;

    public async Task<IActionResult> OnGetAsync(string userId, string code, string email)
        => OutcomeResult(await new ConfirmEmailWorkflow(Screen).ConfirmAsync(_state, userId, code, email));
}
