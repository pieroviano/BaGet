using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BaGet.Identity.Pages.Admin;

/// <summary>A package ID as the administrator pages list it.</summary>
/// <param name="Package">The ID summarised across its versions.</param>
/// <param name="Owners">The names of its owners; empty for an ownerless ID.</param>
/// <param name="RepositoryName">The repository the ID is in; ownership and versions are per repository.</param>
public record AdminPackageRow(
    Account.PackagesModel.OwnedPackage Package,
    IReadOnlyList<string> Owners,
    string RepositoryName = Repository.DefaultName);

/// <summary>
/// The package IDs one user owns, directly or through an organization, with a way into each
/// one's management page. For administrators.
/// </summary>
public class UserPackagesModel : AdminPageModel
{
    /// <summary>The user name, from the route.</summary>
    [BindProperty(SupportsGet = true)]
    public string? Name { get; set; }

    /// <summary>The user's packages.</summary>
    public IReadOnlyList<AdminPackageRow> Packages { get; private set; } = Array.Empty<AdminPackageRow>();

    /// <summary>Lists the user's packages; 404 for an unknown user.</summary>
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var user = await UserManager.FindByNameAsync(Name ?? string.Empty);
        if (user == null) return NotFound();

        Name = user.UserName;

        var ids = await Ownership.GetOwnedPackageIdsAsync(user.Id, user.UserName, cancellationToken);
        Packages = await AdminPackageRows.BuildAsync(
            HttpContext.RequestServices.GetRequiredService<IContext>(), ids, cancellationToken);

        return Page();
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

        var summaries = Account.PackagesModel.Summarise(
            ids,
            await Account.PackagesModel.ListVersionsAsync(context, ids, cancellationToken));

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
