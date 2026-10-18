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
/// The package IDs that have no owner: those that predate accounts, and those left behind
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

    /// <summary>The number of pages.</summary>
    public int PageCount { get; private set; } = 1;

    /// <summary>Lists the ownerless IDs.</summary>
    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var context = HttpContext.RequestServices.GetRequiredService<IContext>();

        #pragma warning disable CS8602, CS8604
        var ownerless = context.Packages
            .AsNoTracking()
            .Where(p => !context.PackageOwners.Any(o => o.PackageId.ToLower() == p.Id.ToLower()))
            .Select(p => p.Id.ToLower())
            .Distinct();
        #pragma warning restore CS8602, CS8604

        var total = await ownerless.CountAsync(cancellationToken);
        PageCount = Math.Max(1, (total + PageSize - 1) / PageSize);
        PageNumber = Math.Min(Math.Max(1, PageNumber), PageCount);

        var ids = await ownerless
            .OrderBy(id => id)
            .Skip((PageNumber - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync(cancellationToken);

        Packages = await AdminPackageRows.BuildAsync(context, ids, cancellationToken);
    }
}
