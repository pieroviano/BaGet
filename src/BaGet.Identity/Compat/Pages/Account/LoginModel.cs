using System;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Identity.ViewModels;
using BaGet.Identity.Workflows;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Identity.Pages.Account;

/// <summary>
/// The page model of the sign-in page when it was a Razor Page.
/// </summary>
/// <remarks>
/// Kept as a shim so that code compiled against it still loads and runs (no breaking change of the public API); there is no page
/// behind it any more: <c>AccountController</c> serves <c>/account/login</c> and delegates to the same <c>LoginWorkflow</c>.
/// </remarks>
[Obsolete("The sign-in screen is served by AccountController; this page model has no page any more and will be removed in the next major version.")]
public class LoginModel : AccountPageModel
{
    private readonly LoginViewModel _state = new LoginViewModel();

    [BindProperty]
    public string? UserName { get => _state.UserName; set => _state.UserName = value; }

    [BindProperty]
    public string? Password { get => _state.Password; set => _state.Password = value; }

    [BindProperty]
    public bool RememberMe { get => _state.RememberMe; set => _state.RememberMe = value; }

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get => _state.ReturnUrl; set => _state.ReturnUrl = value; }

    public bool AllowRegistration => Accounts.AllowRegistration;

    public bool HasExternalProvider => Accounts.OpenIdConnect.IsConfigured;

    public string ExternalProviderName => Accounts.OpenIdConnect.DisplayName;

    public string? Error { get => _state.Error; set => _state.Error = value; }

    public string? Notice { get => _state.Notice; set => _state.Notice = value; }

    public bool EmailIsConfigured => EmailConfigured;

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync() => OutcomeResult(await new LoginWorkflow(Screen).PostAsync(_state));

    public IActionResult OnPostExternal() => OutcomeResult(new LoginWorkflow(Screen).External(_state));

    /// <summary>
    /// The provider has signed the user in. The first time, an account is created for
    /// them: the provider is the gate, so this does not depend on AllowRegistration.
    /// </summary>
    public async Task<IActionResult> OnGetCallbackAsync(CancellationToken cancellationToken)
        => OutcomeResult(await new LoginWorkflow(Screen).CallbackAsync(_state, cancellationToken));
}
