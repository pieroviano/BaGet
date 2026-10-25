using System;
using System.Threading.Tasks;
using BaGet.Identity.ViewModels;
using BaGet.Identity.Workflows;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Identity.Pages.Account;

/// <summary>What the "sign in again" prompt shows.</summary>
public record ReauthenticateView(bool Done, bool Available, string ReturnUrl);

/// <summary>
/// The page model of the "confirm it is you" page when it was a Razor Page: proof of presence for an account without a password,
/// a fresh sign-in with its external provider. The time is kept in the sign-in cookie's properties and checked by
/// <see cref="AccountPageModel.IsConfirmedAsync"/> for <see cref="AccountPageModel.ReauthenticationWindow"/>.
/// </summary>
/// <remarks>
/// Kept as a shim (no breaking change of the public API); <c>AccountController</c> serves <c>/account/reauthenticate</c> and delegates
/// to the same <c>ReauthenticateWorkflow</c>. <see cref="ReauthenticateView"/> is not part of the shim: the
/// <c>_Reauthenticate</c> partial of every sensitive form takes it.
/// </remarks>
[Obsolete("The re-authentication screen is served by AccountController; this page model has no page any more and will be removed in the next major version.")]
public class ReauthenticateModel : AccountPageModel
{
    private readonly ReauthenticateViewModel _state = new ReauthenticateViewModel();

    protected override bool RequiresSignIn => true;

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get => _state.ReturnUrl; set => _state.ReturnUrl = value; }

    public string? Error => _state.Error;

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync() => OutcomeResult(await new ReauthenticateWorkflow(Screen).PostAsync(_state));

    public async Task<IActionResult> OnGetCallbackAsync() => OutcomeResult(await new ReauthenticateWorkflow(Screen).CallbackAsync(_state));
}
