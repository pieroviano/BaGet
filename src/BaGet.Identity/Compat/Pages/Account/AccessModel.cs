using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Identity.ViewModels;
using BaGet.Identity.Workflows;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Identity.Pages.Account;

/// <summary>
/// The page model of the "My access" page when it was a Razor Page: the signed-in user's own effective permissions, for each
/// repository where they hold any action.
/// </summary>
/// <remarks>
/// Kept as a shim (no breaking change of the public API); <c>AccountPackagesController</c> serves <c>/account/access</c> and delegates to
/// the same <c>AccountAccessWorkflow</c>.
/// </remarks>
[Obsolete("The My access screen is served by AccountPackagesController; this page model has no page any more and will be removed in the next major version.")]
public class AccessModel : AccountPageModel
{
    private readonly AccountAccessViewModel _state = new AccountAccessViewModel();

    /// <inheritdoc />
    protected override bool RequiresSignIn => true;

    /// <summary>A package ID to evaluate the grants for; empty means the repository as a whole.</summary>
    [BindProperty(SupportsGet = true)]
    public string? Package { get => _state.Package; set => _state.Package = value; }

    /// <summary>The repositories where the user holds some action.</summary>
    public IReadOnlyList<RepositoryAccessRow> Rows => _state.Rows;

    /// <summary>The projects the user administers.</summary>
    public IReadOnlyList<string> AdministeredProjects => _state.AdministeredProjects;

    /// <summary>Whether the configured database has an access model at all.</summary>
    public bool ModelAvailable => _state.ModelAvailable;

    /// <summary>Shows the user's access.</summary>
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
        => OutcomeResult(await new AccountAccessWorkflow(Screen).ShowAsync(_state, cancellationToken));
}
