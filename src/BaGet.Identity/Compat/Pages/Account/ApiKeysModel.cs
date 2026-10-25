using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.ViewModels;
using BaGet.Identity.Workflows;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Identity.Pages.Account;

/// <summary>
/// The page model of the API keys page when it was a Razor Page.
/// </summary>
/// <remarks>
/// Kept as a shim (no breaking change of the public API); <c>AccountSettingsController</c> serves <c>/account/apikeys</c> and delegates to
/// the same <c>ApiKeysWorkflow</c>.
/// </remarks>
[Obsolete("The API keys screen is served by AccountSettingsController; this page model has no page any more and will be removed in the next major version.")]
public class ApiKeysModel : AccountPageModel
{
    private readonly ApiKeysViewModel _state = new ApiKeysViewModel();

    protected override bool RequiresSignIn => true;

    [BindProperty]
    public string? Description { get => _state.Description; set => _state.Description = value; }

    [BindProperty]
    public List<string> Scopes { get => _state.Scopes; set => _state.Scopes = value; }

    [BindProperty]
    public string PackageGlobPattern { get => _state.PackageGlobPattern; set => _state.PackageGlobPattern = value; }

    [BindProperty]
    public int? ExpiresInDays { get => _state.ExpiresInDays; set => _state.ExpiresInDays = value; }

    /// <summary>
    /// The keys of the repositories the new key is limited to; none (the default) means every repository.
    /// </summary>
    [BindProperty]
    public List<int> RepositoryKeys { get => _state.RepositoryKeys; set => _state.RepositoryKeys = value; }

    /// <summary>
    /// The repositories offered in the picker: those the user can read. Empty (picker hidden) where the server holds
    /// the default repository only.
    /// </summary>
    public IReadOnlyList<RepositoryInfo> Repositories => _state.Repositories;

    public IReadOnlyList<ApiKey> Keys => _state.Keys;

    /// <summary>
    /// The key just created. Shown once: only its hash is kept.
    /// </summary>
    public string? CreatedKey => _state.CreatedKey;

    public async Task OnGetAsync(CancellationToken cancellationToken)
        => await new ApiKeysWorkflow(Screen).ShowAsync(_state, cancellationToken);

    public async Task<IActionResult> OnPostCreateAsync(CancellationToken cancellationToken)
        => OutcomeResult(await new ApiKeysWorkflow(Screen).CreateAsync(_state, cancellationToken));

    public async Task<IActionResult> OnPostRevokeAsync(int key, CancellationToken cancellationToken)
        => OutcomeResult(await new ApiKeysWorkflow(Screen).RevokeAsync(key, cancellationToken));

    /// <summary>Where the key may be used: the repositories it is limited to, or "all repositories".</summary>
    public string DescribeRepositories(ApiKey key) => _state.DescribeRepositories(key);

    public static string Describe(ApiKey key) => ApiKeysViewModel.Describe(key);

    /// <summary>
    /// The scopes the new-key form offers: the package scopes, plus <see cref="ApiKeyScopes.Admin"/> (the REST API under
    /// <c>/api/v1</c>) for an administrator.
    /// </summary>
    public IReadOnlyList<string> AllScopes => _state.AllScopes;
}
