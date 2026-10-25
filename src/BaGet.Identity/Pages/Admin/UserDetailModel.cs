using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.Workflows;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Identity.Pages.Admin;

/// <summary>
/// The page model of the user detail page when it was a Razor Page: one account as an administrator sees it, with its API keys,
/// organizations and the access model's view of it.
/// </summary>
/// <remarks>
/// Kept as a shim (no breaking change of the public API); <c>AdminUsersController</c> serves <c>/admin/users/{name}</c>. Both call
/// <c>UserDetailWorkflow</c>.
/// </remarks>
[Obsolete("The user detail screen is served by AdminUsersController; this page model has no page any more and will be removed in the next major version.")]
public class UserDetailModel : AdminPageModel
{
    private UserDetailWorkflow? _workflow;

    private UserDetailWorkflow Workflow => _workflow ??= new UserDetailWorkflow(HttpContext, Url);

    /// <summary>A package ID to evaluate the effective permissions for; empty means each repository as a whole.</summary>
    [BindProperty(SupportsGet = true)]
    public string? Package { get => Workflow.Model.Package; set => Workflow.Model.Package = value; }

    /// <summary>Whether the configured database has an access model (else the access sections are hidden).</summary>
    public bool ModelAvailable => Workflow.Model.ModelAvailable;

    /// <summary>The names of the groups the account belongs to (the implicit built-ins excluded).</summary>
    public IReadOnlyList<string> Groups => Workflow.Model.Groups;

    /// <summary>The roles the account holds in projects, directly or through a group.</summary>
    public IReadOnlyList<UserProjectRole> ProjectRoles => Workflow.Model.ProjectRoles;

    /// <summary>What the account may do in each NuGet repository, and why.</summary>
    public IReadOnlyList<RepositoryAccessRow> Access => Workflow.Model.Access;

    /// <summary>The user name (route).</summary>
    [BindProperty(SupportsGet = true)]
    public string? Name { get => Workflow.Model.Name; set => Workflow.Model.Name = value; }

    /// <summary>The account.</summary>
    public BaGetUser? Account => Workflow.Model.Account;

    /// <summary>The account's API keys; the hash is never shown.</summary>
    public IReadOnlyList<ApiKey> Keys => Workflow.Model.Keys;

    /// <summary>Where the key may be used: the repositories it is limited to, or "all repositories".</summary>
    public string DescribeRepositories(ApiKey key) => Workflow.Model.DescribeRepositories(key);

    /// <summary>The organizations the account belongs to.</summary>
    public IReadOnlyList<Organization> Organizations => Workflow.Model.Organizations;

    /// <summary>How many package IDs the account owns, in every repository.</summary>
    public int OwnedPackageIds => Workflow.Model.OwnedPackageIds;

    /// <summary>Whether the signed in administrator has a password to confirm with.</summary>
    public bool HasPassword => Workflow.Model.HasPassword;

    /// <summary>For an account without a password: it signed in again with its provider a moment ago.</summary>
    public bool Reauthenticated => Workflow.Model.Reauthenticated;

    /// <summary>Whether an external provider is available to sign in again with.</summary>
    public bool CanReauthenticate => Accounts.OpenIdConnect.IsConfigured;

    /// <summary>Outcome of the last action.</summary>
    [TempData]
    public string? Message { get => Workflow.Model.Message; set => Workflow.Model.Message = value; }

    /// <summary>Why the last action was refused.</summary>
    [TempData]
    public string? Error { get => Workflow.Model.Error; set => Workflow.Model.Error = value; }

    /// <summary>Shows the account.</summary>
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
        => await Workflow.GetAsync(cancellationToken) ?? Page();

    /// <summary>Enables an account that waits for approval.</summary>
    public async Task<IActionResult> OnPostApproveAsync(string? password, CancellationToken cancellationToken)
        => await Workflow.ApproveAsync(password, cancellationToken) ?? Page();

    /// <summary>Revokes one of the account's API keys.</summary>
    public async Task<IActionResult> OnPostRevokeKeyAsync(int key, string? password, CancellationToken cancellationToken)
        => await Workflow.RevokeKeyAsync(key, password, cancellationToken) ?? Page();

    /// <summary>Adds the account to a group.</summary>
    public async Task<IActionResult> OnPostAddGroupAsync(string? group, string? password, CancellationToken cancellationToken)
        => await Workflow.AddGroupAsync(group, password, cancellationToken) ?? Page();

    /// <summary>Removes the account from a group.</summary>
    public async Task<IActionResult> OnPostRemoveGroupAsync(string? group, string? password, CancellationToken cancellationToken)
        => await Workflow.RemoveGroupAsync(group, password, cancellationToken) ?? Page();
}
