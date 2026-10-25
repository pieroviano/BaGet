using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.Mvc;
using BaGet.Identity.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using BaGet.Identity.Pages.Account;

namespace BaGet.Identity.Workflows;

/// <summary>
/// The signed in user's organizations: create one, and manage the members of those the user administers. Shared by
/// <c>AccountOrganizationsController</c>.
/// </summary>
internal sealed class OrganizationsWorkflow
{
    private readonly AccountScreen _screen;

    public OrganizationsWorkflow(AccountScreen screen)
    {
        _screen = screen ?? throw new ArgumentNullException(nameof(screen));
    }

    /// <summary>The screen was requested.</summary>
    public async Task<ScreenOutcome> ShowAsync(OrganizationsViewModel model, CancellationToken cancellationToken)
    {
        await LoadAsync(model, cancellationToken);

        return ScreenOutcome.Show;
    }

    public async Task<ScreenOutcome> CreateAsync(OrganizationsViewModel model, string name, CancellationToken cancellationToken)
    {
        var created = await _screen.Ownership.CreateOrganizationAsync(name, _screen.UserManager.GetUserId(_screen.User), cancellationToken);

        return await DoneAsync(model, created ? $"Created {name}." : "That name is taken.", cancellationToken);
    }

    public async Task<ScreenOutcome> AddMemberAsync(OrganizationsViewModel model, string organization, string userName, bool isAdmin, CancellationToken cancellationToken)
    {
        if (!await _screen.Ownership.IsOrganizationAdminAsync(organization, _screen.UserManager.GetUserId(_screen.User), cancellationToken))
        {
            return ScreenOutcome.Status(403);
        }

        var user = string.IsNullOrWhiteSpace(userName) ? null : await _screen.UserManager.FindByNameAsync(userName.Trim());
        if (user == null)
        {
            return await DoneAsync(model, "There is no user with that name.", cancellationToken);
        }

        var added = await _screen.Ownership.AddMemberAsync(organization, user.Id, isAdmin, cancellationToken);

        return await DoneAsync(model, added ? $"{user.UserName} joined {organization}." : $"{user.UserName} is already a member.", cancellationToken);
    }

    public async Task<ScreenOutcome> RemoveMemberAsync(OrganizationsViewModel model, string organization, string? userName, CancellationToken cancellationToken)
    {
        var currentUserId = _screen.UserManager.GetUserId(_screen.User);
        var user = await _screen.UserManager.FindByNameAsync(userName ?? string.Empty);

        // An administrator may remove anyone; any member may leave.
        if (user == null
            || (user.Id != currentUserId && !await _screen.Ownership.IsOrganizationAdminAsync(organization, currentUserId, cancellationToken)))
        {
            return ScreenOutcome.Status(403);
        }

        var removed = await _screen.Ownership.RemoveMemberAsync(organization, user.Id, cancellationToken);

        return await DoneAsync(
            model,
            removed ? $"{user.UserName} left {organization}." : "An organization must keep at least one administrator.",
            cancellationToken);
    }

    private async Task<ScreenOutcome> DoneAsync(OrganizationsViewModel model, string message, CancellationToken cancellationToken)
    {
        model.Message = message;
        await LoadAsync(model, cancellationToken);

        return ScreenOutcome.Show;
    }

    private async Task LoadAsync(OrganizationsViewModel model, CancellationToken cancellationToken)
    {
        var users = _screen.UserManager;
        var ownership = _screen.Ownership;
        var userId = users.GetUserId(_screen.User);
        var views = new List<AccountOrganization>();

        #pragma warning disable CS8602
        foreach (var organization in await ownership.GetOrganizationsAsync(userId, cancellationToken))
        #pragma warning restore CS8602
        {
            var members = await ownership.GetMembersAsync(organization.Name, cancellationToken);
            var names = new List<(string, bool)>();
            foreach (var member in members)
            {
                #pragma warning disable CS8604
                var user = await users.FindByIdAsync(member.UserId);
                #pragma warning restore CS8604
                #pragma warning disable CS8620
                if (user != null) names.Add((user.UserName, member.IsAdmin));
                #pragma warning restore CS8620
            }

            views.Add(new AccountOrganization
            {
                Name = organization.Name,
                IsAdmin = members.Any(m => m.UserId == userId && m.IsAdmin),
                Members = names.OrderBy(n => n.Item1, StringComparer.OrdinalIgnoreCase).ToList(),
            });
        }

        model.Organizations = views;
    }
}

/// <summary>
/// The package IDs the signed-in user owns, directly or through an organization, and the way into each one's management page.
/// Ownership is per repository, so the IDs are grouped by the repositories the user may read (<see cref="IRepositoryAccess"/>);
/// a repository where the user owns nothing is not listed. Used by <c>AccountPackagesController</c>.
/// </summary>
internal sealed class AccountPackagesWorkflow
{
    private readonly AccountScreen _screen;

    public AccountPackagesWorkflow(AccountScreen screen)
    {
        _screen = screen ?? throw new ArgumentNullException(nameof(screen));
    }

    /// <summary>The screen was requested.</summary>
    public async Task<ScreenOutcome> ShowAsync(AccountPackagesViewModel model, CancellationToken cancellationToken)
    {
        var services = _screen.Services;
        var user = _screen.User;
        var access = services.GetRequiredService<IRepositoryAccess>();
        var scopes = services.GetRequiredService<IServiceScopeFactory>();
        var userId = _screen.UserManager.GetUserId(user);
        var groups = new List<PackagesModel.RepositoryPackages>();

        foreach (var repository in await access.ReadableAsync(user, cancellationToken))
        {
            // A scope pinned to the repository: ownership and packages are repository-scoped tables.
            using var scope = scopes.CreateRepositoryScope(repository);

            // Ownership, not the package table, decides what is listed: an ID whose every
            // version was deleted is still the user's, and hiding it would hide the page
            // where its owners are managed.
            var ids = await scope.ServiceProvider.GetRequiredService<PackageOwnershipService>().GetOwnedPackageIdsAsync(
                userId,
                user.Identity?.Name,
                cancellationToken);

            if (ids == null || ids.Count == 0) continue;

            if (repository.Format == RepositoryFormat.Npm)
            {
                groups.Add(new PackagesModel.RepositoryPackages(repository, await SummariseNpmAsync(scope.ServiceProvider.GetRequiredService<INpmRegistry>(), ids, cancellationToken)));
                continue;
            }

            if (repository.Format == RepositoryFormat.Maven)
            {
                groups.Add(new PackagesModel.RepositoryPackages(repository, await SummariseMavenAsync(scope.ServiceProvider.GetRequiredService<IMavenRepository>(), ids, cancellationToken)));
                continue;
            }

            groups.Add(new PackagesModel.RepositoryPackages(
                repository,
                PackagesModel.Summarise(ids, await PackagesModel.ListVersionsAsync(
                    scope.ServiceProvider.GetRequiredService<IContext>(), ids, cancellationToken))));
        }

        model.Groups = groups;

        return ScreenOutcome.Show;
    }

    // A Maven repository keeps its own tables too: the owned ids are group:artifact, summarised one artifact at a time.
    private static async Task<List<PackagesModel.OwnedPackage>> SummariseMavenAsync(IMavenRepository maven, IReadOnlyList<string> ids, CancellationToken cancellationToken)
    {
        var owned = new List<PackagesModel.OwnedPackage>(ids.Count);
        foreach (var id in ids)
        {
            var found = MavenNames.TryParseArtifactName(id, out var groupId, out var artifactId);
            var artifact = found ? await maven.GetArtifactAsync(groupId, artifactId, cancellationToken) : null;
            owned.Add(new PackagesModel.OwnedPackage
            {
                Id = artifact?.Name ?? id,
                LatestVersion = artifact?.Latest?.Version,
                LatestVersionIsListed = artifact != null,
                VersionCount = artifact?.Versions.Count ?? 0,
                Downloads = 0,
            });
        }

        return owned;
    }

    // An npm repository keeps its own tables: the summary comes from the registry, one package at a time (a user owns few).
    private static async Task<List<PackagesModel.OwnedPackage>> SummariseNpmAsync(INpmRegistry registry, IReadOnlyList<string> ids, CancellationToken cancellationToken)
    {
        var owned = new List<PackagesModel.OwnedPackage>(ids.Count);
        foreach (var id in ids)
        {
            var package = await registry.GetPackageAsync(id.ToLowerInvariant(), cancellationToken);
            owned.Add(new PackagesModel.OwnedPackage
            {
                Id = package?.Name ?? id,
                LatestVersion = package?.Latest?.Version,
                LatestVersionIsListed = package != null,
                VersionCount = package?.Versions.Count ?? 0,
                Downloads = 0,
            });
        }

        return owned;
    }
}

/// <summary>
/// The signed-in user's own effective permissions: for each repository where they hold any action, which of Read, Annotate,
/// Deploy, Delete and Manage, and the permission targets or project roles that grant them (optionally for one package ID, to see
/// how include/exclude patterns apply). Repositories where the user holds nothing are not listed, so the page discloses nothing
/// about private repositories. Package ownership is a second requirement for changing existing package IDs and is not shown
/// here. Used by <c>AccountPackagesController</c>.
/// </summary>
internal sealed class AccountAccessWorkflow
{
    private readonly AccountScreen _screen;

    public AccountAccessWorkflow(AccountScreen screen)
    {
        _screen = screen ?? throw new ArgumentNullException(nameof(screen));
    }

    /// <summary>Shows the user's access.</summary>
    public async Task<ScreenOutcome> ShowAsync(AccountAccessViewModel model, CancellationToken cancellationToken)
    {
        var services = _screen.Services;
        var principal = await services.GetRequiredService<ISessionPrincipalFactory>().CreateAsync(_screen.User);
        if (principal == null) return ScreenOutcome.Challenge;

        model.ModelAvailable = services.GetRequiredService<IContext>() is not NullContext;
        var evaluator = services.GetRequiredService<IAccessEvaluator>();
        var package = string.IsNullOrWhiteSpace(model.Package) ? null : model.Package.Trim();

        var rows = new List<RepositoryAccessRow>();
        foreach (var repository in await services.GetRequiredService<IRepositoryService>().ListAsync(cancellationToken))
        {
            var explanation = await evaluator.ExplainAsync(principal, repository, package, cancellationToken);
            if (explanation.Actions != AccessAction.None) rows.Add(new RepositoryAccessRow(repository, explanation));
        }

        model.Rows = rows
            .OrderBy(r => r.Repository.IsDefault ? 0 : 1)
            .ThenBy(r => r.Repository.Name, StringComparer.Ordinal)
            .ToList();

        if (model.ModelAvailable && principal.UserId != null)
        {
            model.AdministeredProjects = (await services.GetRequiredService<IAccessAdministration>().ProjectRolesOfUserAsync(principal.UserId, cancellationToken))
                .Where(r => r.Role == ProjectRole.Admin)
                .Select(r => r.Project)
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        return ScreenOutcome.Show;
    }
}

/// <summary>
/// A user's or an organization's public page: the packages it owns; and the signed-in user's own profile, which only forwards
/// to it. Used by <c>ProfileController</c>.
/// </summary>
internal sealed class ProfileWorkflow
{
    private readonly AccountScreen _screen;

    public ProfileWorkflow(AccountScreen screen)
    {
        _screen = screen ?? throw new ArgumentNullException(nameof(screen));
    }

    /// <summary>The signed in user's own profile: a redirect to <c>/profiles/{name}</c>.</summary>
    public ScreenOutcome Mine()
    {
        #pragma warning disable CS8604
        return ScreenOutcome.LocalExact(_screen.Url.Content($"~/profiles/{Uri.EscapeDataString(_screen.User.Identity?.Name)}"));
        #pragma warning restore CS8604
    }

    /// <summary>Shows the profile named <see cref="ProfileViewModel.Name"/>; 404 when neither a user nor an organization has it.</summary>
    public async Task<ScreenOutcome> ShowAsync(ProfileViewModel model, CancellationToken cancellationToken)
    {
        var services = _screen.Services;
        var users = _screen.UserManager;
        var context = services.GetRequiredService<IContext>();
        #pragma warning disable CS8604
        var user = await users.FindByNameAsync(model.Name);
        #pragma warning restore CS8604

        IReadOnlyList<string?> ids;
        if (user != null)
        {
            model.Name = user.UserName;
            model.IsCurrentUser = _screen.User.Identity?.IsAuthenticated == true && users.GetUserId(_screen.User) == user.Id;

            // Only the packages owned in the user's own name: an organization's are on its page.
            #pragma warning disable CS8602
            var lowerName = user.UserName.ToLowerInvariant();
            #pragma warning disable CS8604
            ids = await context.PackageOwners
                .AsNoTracking()
                .Where(o => o.OwnerName.ToLower() == lowerName)
                .Select(o => o.PackageId)
                .ToListAsync(cancellationToken);
            #pragma warning restore CS8602, CS8604
        }
        else
        {
            var members = await _screen.Ownership.GetMembersAsync(model.Name, cancellationToken);
            var lowerName = model.Name.ToLowerInvariant();
            #pragma warning disable CS8602, CS8604
            var organization = await context.Organizations.AsNoTracking().FirstOrDefaultAsync(o => o.Name.ToLower() == lowerName, cancellationToken);
            #pragma warning restore CS8602, CS8604
            if (organization == null) return ScreenOutcome.NotFound;

            model.Name = organization.Name;
            model.IsOrganization = true;

            var memberIds = members.Select(m => m.UserId).ToList();
            model.Members = await context.Users
                .AsNoTracking()
                .Where(u => memberIds.Contains(u.Id))
                .Select(u => u.UserName)
                .OrderBy(n => n)
                .ToListAsync(cancellationToken);

            #pragma warning disable CS8602, CS8604
            ids = await context.PackageOwners
                .AsNoTracking()
                .Where(o => o.OwnerName.ToLower() == lowerName)
                .Select(o => o.PackageId)
                .ToListAsync(cancellationToken);
            #pragma warning restore CS8602, CS8604
        }

        var avatars = await services.GetRequiredService<AvatarService>()
            .GetUrlsAsync(new[] { model.Name }, 64, cancellationToken);
        #pragma warning disable CS8620
        model.AvatarUrl = avatars.GetValueOrDefault(model.Name);
        #pragma warning restore CS8620

        #pragma warning disable CS8602
        var lowerIds = ids.Select(i => i.ToLowerInvariant()).Distinct().ToList();
        #pragma warning disable CS8604
        model.Packages = await context.Packages
            .AsNoTracking()
            .Where(p => lowerIds.Contains(p.Id.ToLower()))
            .GroupBy(p => p.Id)
            .Select(g => new PackageDownloads { Id = g.Key, Downloads = g.Sum(p => p.Downloads) })
            .OrderBy(p => p.Id)
            .ToListAsync(cancellationToken);
        #pragma warning restore CS8602, CS8604

        return ScreenOutcome.Show;
    }
}
