using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BaGet.Identity.Pages.Admin;

/// <summary>
/// One account as an administrator sees it: the details, its API keys (revocable), its organizations and a link to
/// its packages, and the access model's view of it: its groups (added and removed here), its project roles and its
/// effective permissions per repository with the grants behind them (for one package ID too). The page is built from
/// independent sections (<c>user-section-*</c>). Revoking a key and changing group membership ask for the
/// administrator's password and are refused with 503 while the <see cref="MaintenanceGate"/> is closed; the access
/// sections are absent where the database has no access model (Azure Table).
/// </summary>
public class UserDetailModel : AdminPageModel
{
    /// <summary>A package ID to evaluate the effective permissions for; empty means each repository as a whole.</summary>
    [BindProperty(SupportsGet = true)]
    public string? Package { get; set; }

    /// <summary>Whether the configured database has an access model (else the access sections are hidden).</summary>
    public bool ModelAvailable { get; private set; }

    /// <summary>The names of the groups the account belongs to (the implicit built-ins excluded).</summary>
    public IReadOnlyList<string> Groups { get; private set; } = Array.Empty<string>();

    /// <summary>The roles the account holds in projects, directly or through a group.</summary>
    public IReadOnlyList<UserProjectRole> ProjectRoles { get; private set; } = Array.Empty<UserProjectRole>();

    /// <summary>What the account may do in each NuGet repository, and why.</summary>
    public IReadOnlyList<RepositoryAccessRow> Access { get; private set; } = Array.Empty<RepositoryAccessRow>();

    /// <summary>The user name (route).</summary>
    [BindProperty(SupportsGet = true)]
    public string? Name { get; set; }

    /// <summary>The account.</summary>
    public BaGetUser? Account { get; private set; }

    /// <summary>The account's API keys; the hash is never shown.</summary>
    public IReadOnlyList<ApiKey> Keys { get; private set; } = Array.Empty<ApiKey>();

    private IReadOnlyDictionary<int, IReadOnlyList<string>> _keyRepositories = new Dictionary<int, IReadOnlyList<string>>();

    /// <summary>Where the key may be used: the repositories it is limited to, or "all repositories".</summary>
    public string DescribeRepositories(ApiKey key)
        => ApiKeyService.DescribeRepositories(_keyRepositories.GetValueOrDefault(key.Key));

    /// <summary>The organizations the account belongs to.</summary>
    public IReadOnlyList<Organization> Organizations { get; private set; } = Array.Empty<Organization>();

    /// <summary>How many package IDs the account owns, in every repository.</summary>
    public int OwnedPackageIds { get; private set; }

    /// <summary>Whether the signed in administrator has a password to confirm with.</summary>
    public bool HasPassword { get; private set; } = true;

    /// <summary>For an account without a password: it signed in again with its provider a moment ago.</summary>
    public bool Reauthenticated { get; private set; }

    /// <summary>Whether an external provider is available to sign in again with.</summary>
    public bool CanReauthenticate => Accounts.OpenIdConnect.IsConfigured;

    /// <summary>Outcome of the last action.</summary>
    [TempData]
    public string? Message { get; set; }

    /// <summary>Why the last action was refused.</summary>
    [TempData]
    public string? Error { get; set; }

    /// <summary>Shows the account.</summary>
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!await LoadAsync(cancellationToken)) return NotFound();

        return Page();
    }

    /// <summary>Revokes one of the account's API keys.</summary>
    public async Task<IActionResult> OnPostRevokeKeyAsync(int key, string? password, CancellationToken cancellationToken)
    {
        HttpContext.RequestServices.GetService<MaintenanceGate>()?.ThrowIfClosed();

        var user = string.IsNullOrEmpty(Name) ? null : await UserManager.FindByNameAsync(Name);
        if (user == null) return NotFound();

        var current = await UserManager.GetUserAsync(User);
        if (current == null) return Challenge();

        if (!await IsConfirmedAsync(current, password))
        {
            Error = "The confirmation is incorrect, so nothing was changed.";
        }
        else if (await ApiKeys.RevokeAsync(user.Id, key, cancellationToken))
        {
            HttpContext.RequestServices.GetRequiredService<ILogger<UserDetailModel>>()
                .LogWarning("{Admin} revoked API key {Key} of {User}", current.UserName, key, user.UserName);
            Message = "The API key was revoked.";
        }
        else
        {
            Error = $"{user.UserName} has no such API key.";
        }

        return RedirectToPage("/Admin/UserDetail", new { name = user.UserName });
    }

    /// <summary>Adds the account to a group.</summary>
    public Task<IActionResult> OnPostAddGroupAsync(string? group, string? password, CancellationToken cancellationToken)
        => ChangeGroupAsync(group, password, add: true, cancellationToken);

    /// <summary>Removes the account from a group.</summary>
    public Task<IActionResult> OnPostRemoveGroupAsync(string? group, string? password, CancellationToken cancellationToken)
        => ChangeGroupAsync(group, password, add: false, cancellationToken);

    private async Task<IActionResult> ChangeGroupAsync(string? group, string? password, bool add, CancellationToken cancellationToken)
    {
        HttpContext.RequestServices.GetService<MaintenanceGate>()?.ThrowIfClosed();

        var user = string.IsNullOrEmpty(Name) ? null : await UserManager.FindByNameAsync(Name);
        if (user == null || HttpContext.RequestServices.GetRequiredService<IContext>() is NullContext) return NotFound();

        var current = await UserManager.GetUserAsync(User);
        if (current == null) return Challenge();

        if (!await IsConfirmedAsync(current, password))
        {
            Error = "The confirmation is incorrect, so nothing was changed.";
        }
        else
        {
            var administration = HttpContext.RequestServices.GetRequiredService<IAccessAdministration>();
            try
            {
                if (add)
                {
                    await administration.AddGroupMemberAsync(group ?? string.Empty, user.Id, current.UserName, cancellationToken);
                    Message = $"{user.UserName} was added to {group}.";
                }
                else
                {
                    await administration.RemoveGroupMemberAsync(group ?? string.Empty, user.Id, current.UserName, cancellationToken);
                    Message = $"{user.UserName} was removed from {group}.";
                }
            }
            catch (AccessModelException e)
            {
                Error = e.Message;
            }
        }

        return RedirectToPage("/Admin/UserDetail", new { name = user.UserName });
    }

    private async Task<bool> LoadAsync(CancellationToken cancellationToken)
    {
        Account = string.IsNullOrEmpty(Name) ? null : await UserManager.FindByNameAsync(Name);
        if (Account == null) return false;

        await LoadAccessAsync(Account, cancellationToken);

        Keys = await ApiKeys.ListAsync(Account.Id, cancellationToken);
        _keyRepositories = await ApiKeys.RepositoryNamesAsync(Keys, cancellationToken);
        Organizations = await Ownership.GetOrganizationsAsync(Account.Id, cancellationToken) ?? (IReadOnlyList<Organization>)Array.Empty<Organization>();
        OwnedPackageIds = await Ownership.CountOwnedPackageIdsAsync(Account.UserName, cancellationToken);

        var current = await UserManager.GetUserAsync(User);
        HasPassword = current == null || await UserManager.HasPasswordAsync(current);
        Reauthenticated = !HasPassword && await HasReauthenticatedRecentlyAsync();

        return true;
    }

    // The access model's view of the account: groups, project roles and the effective permissions per repository.
    private async Task LoadAccessAsync(BaGetUser account, CancellationToken cancellationToken)
    {
        var services = HttpContext.RequestServices;
        ModelAvailable = services.GetRequiredService<IContext>() is not NullContext;
        if (!ModelAvailable) return;

        var administration = services.GetRequiredService<IAccessAdministration>();
        var evaluator = services.GetRequiredService<IAccessEvaluator>();
        var package = string.IsNullOrWhiteSpace(Package) ? null : Package.Trim();

        Groups = await administration.GroupsOfUserAsync(account.Id, cancellationToken);
        ProjectRoles = await administration.ProjectRolesOfUserAsync(account.Id, cancellationToken);

        // What the account would be allowed through a session: its administrator flag is the only override.
        var principal = new BaGetPrincipal(account.Id, account.UserName, isSuperuser: false, ApiKeyScopes.All, "*", account.IsAdministrator);
        var rows = new List<RepositoryAccessRow>();
        foreach (var repository in await services.GetRequiredService<IRepositoryService>().ListAsync(cancellationToken))
        {
            if (repository.Format != RepositoryFormat.NuGet) continue;

            rows.Add(new RepositoryAccessRow(repository, await evaluator.ExplainAsync(principal, repository, package, cancellationToken)));
        }

        Access = rows
            .OrderBy(r => r.Repository.IsDefault ? 0 : 1)
            .ThenBy(r => r.Repository.Name, StringComparer.Ordinal)
            .ToList();
    }
}
