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

/// <summary>
/// The package IDs that have no owner, in every repository: those that predate accounts, and those left behind
/// when their only owner's account was deleted. Only administrators and the global API key
/// may change them, so this is where an administrator finds them.
/// </summary>
public class PackagesModel : AdminPageModel
{
    /// <summary>How many IDs a page lists.</summary>
    public const int PageSize = 50;

    /// <summary>One-based page number. Not "page": Razor Pages reserves that route value for the page path.</summary>
    [BindProperty(SupportsGet = true, Name = "p")]
    public int PageNumber { get; set; } = 1;

    /// <summary>The ownerless IDs on this page.</summary>
    public IReadOnlyList<AdminPackageRow> Packages { get; private set; } = Array.Empty<AdminPackageRow>();

    /// <summary>Whether a Repository column is worth showing: some listed ID is outside the default repository.</summary>
    public bool ShowRepository => Packages.Any(r => r.RepositoryName != Repository.DefaultName);

    /// <summary>The number of pages.</summary>
    public int PageCount { get; private set; } = 1;

    /// <summary>Lists the ownerless IDs of every repository.</summary>
    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var context = HttpContext.RequestServices.GetRequiredService<IContext>();

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
        PageCount = Math.Max(1, (total + PageSize - 1) / PageSize);
        PageNumber = Math.Min(Math.Max(1, PageNumber), PageCount);

        var page = await ownerless
            .OrderBy(o => o.Id)
            .ThenBy(o => o.RepositoryKey)
            .Skip((PageNumber - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync(cancellationToken);

        var repositories = (await HttpContext.RequestServices.GetRequiredService<IRepositoryService>()
            .ListAsync(cancellationToken)).ToDictionary(r => r.Key);
        var scopes = HttpContext.RequestServices.GetRequiredService<IServiceScopeFactory>();

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

        Packages = rows
            .OrderBy(r => r.Package.Id, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.RepositoryName, StringComparer.Ordinal)
            .ToList();
    }
}
