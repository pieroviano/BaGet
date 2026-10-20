using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace BaGet.Core;

/// <summary>
/// <see cref="IRepositoryAdministration"/> over the relational database. With a database that is not relational
/// (<see cref="NullContext"/>) only the default repository is listed, without a package count, and nothing can be edited.
/// </summary>
/// <remarks>
/// Repository-scoped tables are read with the query filter off (<c>IgnoreQueryFilters</c>) because the administrator
/// looks across repositories, not inside the current one.
/// </remarks>
public class RepositoryAdministration : IRepositoryAdministration
{
    private readonly IContext _context;

    public RepositoryAdministration(IContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    private bool Relational => !(_context is NullContext);

    private static RepositorySummary DefaultSummary => new(
        Repository.DefaultKey, Repository.DefaultName, RepositoryFormat.NuGet, RepositoryKind.Local, true, DateTime.UtcNow, 0);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<RepositorySummary>> ListAsync(CancellationToken cancellationToken)
    {
        if (!Relational) return new[] { DefaultSummary };

        var rows = await _context.Repositories!.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
        var counts = await _context.Packages!
            .IgnoreQueryFilters()
            .GroupBy(p => p.RepositoryKey)
            .Select(g => new { Key = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Key, g => g.Count, cancellationToken)
            .ConfigureAwait(false);

        var all = rows.Select(r => Summarize(r, counts.TryGetValue(r.Key, out var n) ? n : 0)).ToList();
        if (!all.Any(r => r.IsDefault)) all.Add(DefaultSummary);

        return all.OrderBy(r => r.Name, StringComparer.Ordinal).ToList();
    }

    /// <inheritdoc/>
    public async Task<RepositorySummary?> FindAsync(string? name, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(name)) return null;

        return (await ListAsync(cancellationToken).ConfigureAwait(false)).FirstOrDefault(r => r.Name == name);
    }

    /// <inheritdoc/>
    public async Task SetAnonymousReadAsync(string name, bool anonymousRead, CancellationToken cancellationToken)
    {
        var row = await FindRowAsync(name, cancellationToken).ConfigureAwait(false);
        row.AnonymousRead = anonymousRead;

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(string name, CancellationToken cancellationToken)
    {
        if (name == Repository.DefaultName) throw new RepositoryProtectedException();

        var row = await FindRowAsync(name, cancellationToken).ConfigureAwait(false);
        var key = row.Key;
        if (key == Repository.DefaultKey) throw new RepositoryProtectedException();

        // Empty means no packages. The catalog is an append-only log (deleting a package adds an entry) and owner rows
        // outlive their package, so those, the validations and the abuse reports are history of a repository that no
        // longer exists once it is deleted, and go with it. Catalog leaf files in storage are left (inert, named by GUID).
        var hasPackages = await _context.Packages!.IgnoreQueryFilters().AnyAsync(e => e.RepositoryKey == key, cancellationToken).ConfigureAwait(false);
        if (hasPackages) throw new RepositoryNotEmptyException(name);

        _context.CatalogLeafs!.RemoveRange(await _context.CatalogLeafs!.IgnoreQueryFilters().Where(e => e.RepositoryKey == key).ToListAsync(cancellationToken).ConfigureAwait(false));
        _context.PackageOwners!.RemoveRange(await _context.PackageOwners!.IgnoreQueryFilters().Where(e => e.RepositoryKey == key).ToListAsync(cancellationToken).ConfigureAwait(false));
        _context.PackageValidations!.RemoveRange(await _context.PackageValidations!.IgnoreQueryFilters().Where(e => e.RepositoryKey == key).ToListAsync(cancellationToken).ConfigureAwait(false));
        _context.AbuseReports!.RemoveRange(await _context.AbuseReports!.IgnoreQueryFilters().Where(e => e.RepositoryKey == key).ToListAsync(cancellationToken).ConfigureAwait(false));
        _context.Repositories!.Remove(row);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<Repository> FindRowAsync(string name, CancellationToken cancellationToken)
    {
        var row = Relational
            ? await _context.Repositories!.FirstOrDefaultAsync(r => r.Name == name, cancellationToken).ConfigureAwait(false)
            : null;

        return row ?? throw new KeyNotFoundException($"There is no repository named '{name}'.");
    }

    private static RepositorySummary Summarize(Repository r, int packages)
        => new(r.Key, r.Name ?? string.Empty, r.Format, r.Kind, r.AnonymousRead, r.Created, packages);
}
