using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.Pages.Account;
using BaGet.Identity.ViewModels;
using BaGet.Identity.Workflows;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Identity.Pages.Profiles;

/// <summary>
/// The page model of a user's or an organization's public page when it was a Razor Page: the packages it owns.
/// </summary>
/// <remarks>
/// Kept as a shim (no breaking change of the public API); <c>ProfileController</c> serves <c>/profiles/{name}</c> (and under
/// <c>/r/{repo}/</c>) and delegates to the same <c>ProfileWorkflow</c>.
/// </remarks>
[Obsolete("The profile screen is served by ProfileController; this page model has no page any more and will be removed in the next major version.")]
public class ProfileModel : AccountPageModel
{
    private readonly ProfileViewModel _state = new ProfileViewModel();

    [BindProperty(SupportsGet = true)]
    public string? Name { get => _state.Name; set => _state.Name = value; }

    public bool IsOrganization => _state.IsOrganization;

    public IReadOnlyList<PackageDownloads> Packages => _state.Packages;

    public IReadOnlyList<string?> Members => _state.Members;

    public bool IsCurrentUser => _state.IsCurrentUser;

    /// <summary>
    /// The account's avatar address, or null when Gravatar is off.
    /// </summary>
    public string? AvatarUrl => _state.AvatarUrl;

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
        => OutcomeResult(await new ProfileWorkflow(Screen).ShowAsync(_state, cancellationToken));
}
