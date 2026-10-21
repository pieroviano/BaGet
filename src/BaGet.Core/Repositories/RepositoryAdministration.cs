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
    private readonly IStorageService? _storage;
    private readonly SystemTime? _time;

    /// <param name="context">The database.</param>
    /// <param name="storage">The store whose <c>repos/{name}/</c> folder is emptied when a repository is deleted. Null leaves storage alone.</param>
    /// <param name="time">The clock for audit entries and created targets; null uses the system's.</param>
    public RepositoryAdministration(IContext context, IStorageService? storage = null, SystemTime? time = null)
    {
        _time = time;
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _storage = storage;
    }

    private DateTime Now => _time?.UtcNow ?? DateTime.UtcNow;

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
    public async Task SetAnonymousReadAsync(string name, bool anonymousRead, CancellationToken cancellationToken, string? actor = null)
    {
        var row = await FindRowAsync(name, cancellationToken).ConfigureAwait(false);
        row.AnonymousRead = anonymousRead;

        var created = false;
        if (anonymousRead && !await EveryoneReadsAsync(row, cancellationToken).ConfigureAwait(false))
        {
            await GrantEveryoneReadAsync(row, cancellationToken).ConfigureAwait(false);
            created = true;
        }

        AccessAdministration.Audit(_context, Now, actor, "repository.anonymous-read", name, new { anonymousRead, grantCreated = created });
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    // Whether some target grants the Everyone group Read on every package of the repository.
    private async Task<bool> EveryoneReadsAsync(Repository row, CancellationToken cancellationToken)
    {
        var everyone = await _context.Groups!.AsNoTracking()
            .Where(g => g.IsBuiltIn && g.Name == BuiltInNames.Everyone)
            .Select(g => (int?)g.Key).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (everyone == null) return false;

        var id = everyone.Value.ToString();
        var targets = await _context.PermissionGrants!.AsNoTracking()
            .Where(g => g.PrincipalType == PrincipalType.Group && g.PrincipalId == id && (g.Actions & AccessAction.Read) != 0)
            .Select(g => g.Target!)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return targets.Any(t =>
            (t.ProjectKey == null || t.ProjectKey == row.ProjectKey)
            && GlobSet.Parse(t.RepositoryPatterns, emptyMeansAll: false).IsMatch(row.Name)
            && GlobSet.Parse(t.IncludePatterns, emptyMeansAll: true).IsMatch("*")
            && !GlobSet.Parse(t.ExcludePatterns, emptyMeansAll: false).IsMatch("*"));
    }

    private async Task GrantEveryoneReadAsync(Repository row, CancellationToken cancellationToken)
    {
        var everyone = await _context.Groups!.AsNoTracking()
            .Where(g => g.IsBuiltIn && g.Name == BuiltInNames.Everyone)
            .Select(g => (int?)g.Key).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (everyone == null) return;

        var targetName = "Anonymous read " + row.Name;
        var target = await _context.PermissionTargets!.FirstOrDefaultAsync(t => t.Name == targetName, cancellationToken).ConfigureAwait(false);
        if (target == null)
        {
            target = new PermissionTarget
            {
                Name = targetName,
                RepositoryPatterns = new[] { row.Name! },
                IncludePatterns = new[] { "*" },
                ExcludePatterns = Array.Empty<string>(),
                Created = Now,
            };
            _context.PermissionTargets!.Add(target);
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        var id = everyone.Value.ToString();
        var grant = await _context.PermissionGrants!.FirstOrDefaultAsync(
            g => g.TargetKey == target.Key && g.PrincipalType == PrincipalType.Group && g.PrincipalId == id, cancellationToken).ConfigureAwait(false);
        if (grant == null)
        {
            _context.PermissionGrants!.Add(new PermissionGrant { TargetKey = target.Key, PrincipalType = PrincipalType.Group, PrincipalId = id, Actions = AccessAction.Read });
        }
        else
        {
            grant.Actions |= AccessAction.Read;
        }
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(string name, CancellationToken cancellationToken)
    {
        if (name == Repository.DefaultName) throw new RepositoryProtectedException();

        var row = await FindRowAsync(name, cancellationToken).ConfigureAwait(false);
        var key = row.Key;
        if (key == Repository.DefaultKey) throw new RepositoryProtectedException();

        // Empty means no packages and no submission awaiting validation (a pushed package the client already got an
        // accepted response for, staged but not indexed yet; deleting would lose it). The catalog is an append-only log
        // (deleting a package adds an entry) and owner rows outlive their package, so those, the finished validations
        // and the abuse reports are history of a repository that no longer exists once it is deleted, and go with it.
        var hasPackages = await _context.Packages!.IgnoreQueryFilters().AnyAsync(e => e.RepositoryKey == key, cancellationToken).ConfigureAwait(false);
        if (hasPackages) throw new RepositoryNotEmptyException(name);

        var hasPending = await _context.PackageValidations!.IgnoreQueryFilters()
            .AnyAsync(e => e.RepositoryKey == key && e.State == PackageValidationState.Pending, cancellationToken).ConfigureAwait(false);
        if (hasPending) throw new RepositoryNotEmptyException(name, awaitingValidation: true);

        _context.CatalogLeafs!.RemoveRange(await _context.CatalogLeafs!.IgnoreQueryFilters().Where(e => e.RepositoryKey == key).ToListAsync(cancellationToken).ConfigureAwait(false));
        _context.PackageOwners!.RemoveRange(await _context.PackageOwners!.IgnoreQueryFilters().Where(e => e.RepositoryKey == key).ToListAsync(cancellationToken).ConfigureAwait(false));
        _context.PackageValidations!.RemoveRange(await _context.PackageValidations!.IgnoreQueryFilters().Where(e => e.RepositoryKey == key).ToListAsync(cancellationToken).ConfigureAwait(false));
        _context.AbuseReports!.RemoveRange(await _context.AbuseReports!.IgnoreQueryFilters().Where(e => e.RepositoryKey == key).ToListAsync(cancellationToken).ConfigureAwait(false));
        _context.PackageSymbols!.RemoveRange(await _context.PackageSymbols!.IgnoreQueryFilters().Where(e => e.RepositoryKey == key).ToListAsync(cancellationToken).ConfigureAwait(false));
        _context.Repositories!.Remove(row);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await PurgeStorageAsync(key, row.Name ?? name, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Deletes every file under <c>repos/{name}/</c> (symbols, catalog leaves, staged and quarantined uploads, leftovers of
    /// hard-deleted packages): storage paths are keyed by name, so a repository created later under the same name would
    /// otherwise inherit them. Runs after the database commit; a failure surfaces and a retry is not possible (the row is
    /// gone), so the leftovers stay and the administrator is told.
    /// </summary>
    private async Task PurgeStorageAsync(int key, string name, CancellationToken cancellationToken)
    {
        // A store that cannot list (or no store) cannot be swept; paths are not recorded anywhere else.
        if (!(_storage is IListableStorageService listable)) return;

        var prefix = RepositoryStorage.Prefix(new RepositoryInfo(key, name, RepositoryFormat.NuGet, RepositoryKind.Local));
        foreach (var path in await listable.ListAsync(prefix, cancellationToken).ConfigureAwait(false))
        {
            await _storage!.DeleteAsync(path, cancellationToken).ConfigureAwait(false);
        }
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
