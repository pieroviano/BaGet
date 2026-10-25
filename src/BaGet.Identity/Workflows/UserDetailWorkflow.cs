using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BaGet.Identity.Workflows;

/// <summary>
/// One account as an administrator sees it: the details, its API keys (revocable), its organizations and a link to its packages,
/// and the access model's view of it (groups, project roles, effective permissions per repository). Revoking a key, approving the
/// account and changing group membership ask for the administrator's password and are refused with 503 while the
/// <see cref="MaintenanceGate"/> is closed. Used by <c>AdminUsersController</c>.
/// </summary>
internal sealed class UserDetailWorkflow : AdminWorkflow
{
    private const string NotConfirmed = "The confirmation is incorrect, so nothing was changed.";

    public UserDetailWorkflow(HttpContext httpContext, IUrlHelper url) : base(httpContext, url)
    {
    }

    /// <summary>The state the screen renders; the caller sets <c>Name</c> and <c>Package</c> first.</summary>
    public UserDetailViewModel Model { get; } = new();

    /// <summary>Shows the account; 404 for an unknown user.</summary>
    public async Task<IActionResult?> GetAsync(CancellationToken cancellationToken)
        => await LoadAsync(cancellationToken) ? null : new NotFoundResult();

    /// <summary>Enables an account that waits for approval, like the Users screen's Approve (password, gate).</summary>
    public async Task<IActionResult?> ApproveAsync(string? password, CancellationToken cancellationToken)
    {
        ThrowIfClosed();

        var user = await FindAsync();
        if (user == null) return new NotFoundResult();

        var current = await UserManager.GetUserAsync(User);
        if (current == null) return new ChallengeResult();

        if (!await IsConfirmedAsync(current, password))
        {
            Model.Error = NotConfirmed;
        }
        else
        {
            switch (await Services.GetRequiredService<AccountApprovalService>().ApproveAsync(user.UserName, current.UserName, cancellationToken))
            {
                case AccountApprovalResult.Approved:
                    Model.Message = $"{user.UserName} was approved and can sign in.";
                    break;
                case AccountApprovalResult.NotPending:
                    Model.Message = $"{user.UserName} was not waiting for approval.";
                    break;
                default:
                    Model.Error = $"{user.UserName} was changed or removed meanwhile, so nothing was approved.";
                    break;
            }
        }

        return Back(user);
    }

    /// <summary>Revokes one of the account's API keys.</summary>
    public async Task<IActionResult?> RevokeKeyAsync(int key, string? password, CancellationToken cancellationToken)
    {
        ThrowIfClosed();

        var user = await FindAsync();
        if (user == null) return new NotFoundResult();

        var current = await UserManager.GetUserAsync(User);
        if (current == null) return new ChallengeResult();

        if (!await IsConfirmedAsync(current, password))
        {
            Model.Error = NotConfirmed;
        }
        else if (await ApiKeys.RevokeAsync(user.Id, key, cancellationToken))
        {
            Services.GetRequiredService<ILogger<UserDetailWorkflow>>()
                .LogWarning("{Admin} revoked API key {Key} of {User}", current.UserName, key, user.UserName);
            Model.Message = "The API key was revoked.";
        }
        else
        {
            Model.Error = $"{user.UserName} has no such API key.";
        }

        return Back(user);
    }

    /// <summary>Adds the account to a group.</summary>
    public Task<IActionResult?> AddGroupAsync(string? group, string? password, CancellationToken cancellationToken)
        => ChangeGroupAsync(group, password, add: true, cancellationToken);

    /// <summary>Removes the account from a group.</summary>
    public Task<IActionResult?> RemoveGroupAsync(string? group, string? password, CancellationToken cancellationToken)
        => ChangeGroupAsync(group, password, add: false, cancellationToken);

    private IActionResult Back(BaGetUser user) => RedirectToScreen("/Admin/UserDetail", new { name = user.UserName });

    private async Task<BaGetUser?> FindAsync()
        => string.IsNullOrEmpty(Model.Name) ? null : await UserManager.FindByNameAsync(Model.Name);

    private async Task<IActionResult?> ChangeGroupAsync(string? group, string? password, bool add, CancellationToken cancellationToken)
    {
        ThrowIfClosed();

        var user = await FindAsync();
        if (user == null || Services.GetRequiredService<IContext>() is NullContext) return new NotFoundResult();

        var current = await UserManager.GetUserAsync(User);
        if (current == null) return new ChallengeResult();

        if (!await IsConfirmedAsync(current, password))
        {
            Model.Error = NotConfirmed;
        }
        else
        {
            var administration = Services.GetRequiredService<IAccessAdministration>();
            try
            {
                if (add)
                {
                    await administration.AddGroupMemberAsync(group ?? string.Empty, user.Id, current.UserName, cancellationToken);
                    Model.Message = $"{user.UserName} was added to {group}.";
                }
                else
                {
                    await administration.RemoveGroupMemberAsync(group ?? string.Empty, user.Id, current.UserName, cancellationToken);
                    Model.Message = $"{user.UserName} was removed from {group}.";
                }
            }
            catch (AccessModelException e)
            {
                Model.Error = e.Message;
            }
        }

        return Back(user);
    }

    private async Task<bool> LoadAsync(CancellationToken cancellationToken)
    {
        Model.Account = await FindAsync();
        if (Model.Account == null) return false;

        await LoadAccessAsync(Model.Account, cancellationToken);

        Model.Keys = await ApiKeys.ListAsync(Model.Account.Id, cancellationToken);
        Model.SetKeyRepositories(await ApiKeys.RepositoryNamesAsync(Model.Keys, cancellationToken));
        Model.Organizations = await Ownership.GetOrganizationsAsync(Model.Account.Id, cancellationToken) ?? (IReadOnlyList<Organization>)Array.Empty<Organization>();
        Model.OwnedPackageIds = await Ownership.CountOwnedPackageIdsAsync(Model.Account.UserName, cancellationToken);

        await LoadConfirmationAsync(Model);

        return true;
    }

    // The access model's view of the account: groups, project roles and the effective permissions per repository.
    private async Task LoadAccessAsync(BaGetUser account, CancellationToken cancellationToken)
    {
        Model.ModelAvailable = Services.GetRequiredService<IContext>() is not NullContext;
        if (!Model.ModelAvailable) return;

        var administration = Services.GetRequiredService<IAccessAdministration>();
        var evaluator = Services.GetRequiredService<IAccessEvaluator>();
        var package = string.IsNullOrWhiteSpace(Model.Package) ? null : Model.Package.Trim();

        Model.Groups = await administration.GroupsOfUserAsync(account.Id, cancellationToken);
        Model.ProjectRoles = await administration.ProjectRolesOfUserAsync(account.Id, cancellationToken);

        // What the account would be allowed through a session: its administrator flag is the only override.
        var principal = new BaGetPrincipal(account.Id, account.UserName, isSuperuser: false, ApiKeyScopes.All, "*", account.IsAdministrator);
        var rows = new List<RepositoryAccessRow>();
        foreach (var repository in await Services.GetRequiredService<IRepositoryService>().ListAsync(cancellationToken))
        {
            rows.Add(new RepositoryAccessRow(repository, await evaluator.ExplainAsync(principal, repository, package, cancellationToken)));
        }

        Model.Access = rows
            .OrderBy(r => r.Repository.IsDefault ? 0 : 1)
            .ThenBy(r => r.Repository.Name, StringComparer.Ordinal)
            .ToList();
    }
}
