using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.Pages.Admin;
using BaGet.Identity.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BaGet.Identity.Workflows;

/// <summary>
/// The two package listings of the administrator: the package IDs that have no owner in any repository (those that predate
/// accounts, and those left behind when their only owner's account was deleted; only administrators and the global API key may
/// change them), and the IDs one user owns. Used by the controllers.
/// </summary>
internal sealed class AdminPackagesWorkflow : AdminWorkflow
{
    public AdminPackagesWorkflow(HttpContext httpContext, IUrlHelper url) : base(httpContext, url)
    {
    }

    /// <summary>The ownerless listing; the caller sets <c>PageNumber</c> first.</summary>
    public AdminPackagesViewModel Ownerless { get; } = new();

    /// <summary>One user's listing; the caller sets <c>Name</c> first.</summary>
    public UserPackagesViewModel OfUser { get; } = new();

    /// <summary>Lists the ownerless IDs of every repository.</summary>
    public async Task GetOwnerlessAsync(CancellationToken cancellationToken)
    {
        var context = Services.GetRequiredService<IContext>();

        // Ownership is per repository, so an ID is ownerless where nobody in that repository owns it.
        #pragma warning disable CS8602, CS8604
        var ownerless = context.Packages
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(p => !context.PackageOwners.IgnoreQueryFilters()
                .Any(o => o.RepositoryKey == p.RepositoryKey && o.PackageId.ToLower() == p.Id.ToLower()))
            .Select(p => new { p.RepositoryKey, Id = p.Id.ToLower() })
            .Distinct();
        #pragma warning restore CS8602, CS8604

        var total = await ownerless.CountAsync(cancellationToken);
        Ownerless.PageCount = Math.Max(1, (total + AdminPackagesViewModel.PageSize - 1) / AdminPackagesViewModel.PageSize);
        Ownerless.PageNumber = Math.Min(Math.Max(1, Ownerless.PageNumber), Ownerless.PageCount);

        var page = await ownerless
            .OrderBy(o => o.Id)
            .ThenBy(o => o.RepositoryKey)
            .Skip((Ownerless.PageNumber - 1) * AdminPackagesViewModel.PageSize)
            .Take(AdminPackagesViewModel.PageSize)
            .ToListAsync(cancellationToken);

        var repositories = (await Services.GetRequiredService<IRepositoryService>()
            .ListAsync(cancellationToken)).ToDictionary(r => r.Key);
        var scopes = Services.GetRequiredService<IServiceScopeFactory>();

        // Each repository's rows are read in a scope pinned to it, which supplies its versions and owners.
        var rows = new List<AdminPackageRow>();
        foreach (var group in page.GroupBy(o => o.RepositoryKey))
        {
            if (!repositories.TryGetValue(group.Key, out var repository)) continue;

            using var scope = scopes.CreateRepositoryScope(repository);
            rows.AddRange(await AdminPackageRows.BuildAsync(
                scope.ServiceProvider.GetRequiredService<IContext>(),
                group.Select(o => o.Id).ToList(),
                cancellationToken,
                repository.Name));
        }

        Ownerless.Packages = rows
            .OrderBy(r => r.Package.Id, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.RepositoryName, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>Lists the user's packages; 404 for an unknown user.</summary>
    public async Task<IActionResult?> GetUserAsync(CancellationToken cancellationToken)
    {
        var user = await UserManager.FindByNameAsync(OfUser.Name ?? string.Empty);
        if (user == null) return new NotFoundResult();

        OfUser.Name = user.UserName;

        var ids = await Ownership.GetOwnedPackageIdsAsync(user.Id, user.UserName, cancellationToken);
        OfUser.Packages = await AdminPackageRows.BuildAsync(Services.GetRequiredService<IContext>(), ids, cancellationToken);

        return null;
    }
}

internal static class AdminPackageRows
{
    /// <summary>
    /// Summarises <paramref name="ids"/> and attaches each one's owners: one query for the versions, one for the owners.
    /// Works in the repository <paramref name="context"/> is scoped to, whose name rows carry.
    /// </summary>
    internal static async Task<IReadOnlyList<AdminPackageRow>> BuildAsync(
        IContext context,
        IReadOnlyList<string>? ids,
        CancellationToken cancellationToken,
        string repositoryName = Repository.DefaultName)
    {
        #pragma warning disable CS8602
        if (ids.Count == 0) return Array.Empty<AdminPackageRow>();
        #pragma warning restore CS8602

        var summaries = Pages.Account.PackagesModel.Summarise(
            ids,
            await Pages.Account.PackagesModel.ListVersionsAsync(context, ids, cancellationToken));

        var lowered = ids.Select(id => id.ToLowerInvariant()).ToList();
        #pragma warning disable CS8602, CS8604
        var owners = await context.PackageOwners
            .AsNoTracking()
            .Where(o => lowered.Contains(o.PackageId.ToLower()))
            .Select(o => new { o.PackageId, o.OwnerName })
            .ToListAsync(cancellationToken);
        #pragma warning restore CS8602, CS8604
        #pragma warning disable CS8621, CS8714
        var byId = owners
            .GroupBy(o => o.PackageId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<string>)g.Select(o => o.OwnerName).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList(),
                StringComparer.OrdinalIgnoreCase);
        #pragma warning restore CS8621, CS8714

        return summaries
            .Select(p => new AdminPackageRow(p, byId.TryGetValue(p.Id, out var names) ? names : Array.Empty<string>(), repositoryName))
            .ToList();
    }
}
