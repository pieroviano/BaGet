using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BaGet.Core;

/// <summary>
/// <see cref="IRepositoryService"/> over the relational database. When the database is not relational
/// (<see cref="NullContext"/>, as with Azure Table) or the search index is not repository-aware
/// (<c>Search:Type</c> = <c>AzureSearch</c>: one global index with package-id document keys) only the default
/// repository exists, others stored in the database are not served, and creating another is refused with a
/// message saying why.
/// </summary>
public class RepositoryService : IRepositoryService
{
    private readonly IContext _context;
    private readonly IOptions<SearchOptions>? _search;

    public RepositoryService(IContext context, IOptions<SearchOptions>? search = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _search = search;
    }

    /// <inheritdoc/>
    public bool SupportsMultipleRepositories
        => !(_context is NullContext)
        && !string.Equals(_search?.Value?.Type, "AzureSearch", StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public async Task<RepositoryInfo?> FindAsync(string? name, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(name)) return null;

        if (SupportsMultipleRepositories)
        {
            var row = await _context.Repositories!
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Name == name, cancellationToken)
                .ConfigureAwait(false);
            if (row != null) return row.ToInfo();
        }

        // A database that has not been through EnsureDefaultAsync yet still serves the default repository.
        return name == Repository.DefaultName ? RepositoryInfo.Default : null;
    }

    /// <inheritdoc/>
    public async Task<RepositoryInfo?> FindAsync(int key, CancellationToken cancellationToken)
    {
        if (SupportsMultipleRepositories)
        {
            var row = await _context.Repositories!
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Key == key, cancellationToken)
                .ConfigureAwait(false);
            if (row != null) return row.ToInfo();
        }

        return key == Repository.DefaultKey ? RepositoryInfo.Default : null;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<RepositoryInfo>> ListAsync(CancellationToken cancellationToken)
    {
        var all = new List<RepositoryInfo>();
        if (SupportsMultipleRepositories)
        {
            var rows = await _context.Repositories!.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
            all.AddRange(rows.Select(r => r.ToInfo()));
        }

        if (!all.Any(r => r.IsDefault)) all.Add(RepositoryInfo.Default);

        return all.OrderBy(r => r.Name, StringComparer.Ordinal).ToList();
    }

    /// <inheritdoc/>
    public async Task<RepositoryInfo> CreateAsync(
        string name,
        RepositoryFormat format,
        RepositoryKind kind,
        CancellationToken cancellationToken)
    {
        if (!RepositoryNames.IsValid(name, out var error)) throw new ArgumentException(error, nameof(name));

        if (!SupportsMultipleRepositories)
        {
            throw new NotSupportedException(
                "Only the 'default' repository is available with this configuration; use a relational database (SQLite, SQL Server, PostgreSQL or MySQL) and a search type other than AzureSearch (whose index is not repository-aware) to create more.");
        }

        if (format != RepositoryFormat.NuGet || kind != RepositoryKind.Local)
        {
            throw new NotSupportedException("Only local NuGet repositories can be created.");
        }

        // Migrations create the default row; restore it if it is missing (key 1 is what every existing row carries, so
        // a new repository must never take it) and refuse when it cannot be restored.
        await EnsureDefaultAsync(cancellationToken).ConfigureAwait(false);

        if (await _context.Repositories!.AnyAsync(r => r.Name == name, cancellationToken).ConfigureAwait(false))
        {
            throw new RepositoryExistsException(name);
        }

        var repository = new Repository
        {
            Name = name,
            Format = format,
            Kind = kind,
            AnonymousRead = true,
            Created = DateTime.UtcNow,
        };

        _context.Repositories!.Add(repository);

        try
        {
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException e) when (_context.IsUniqueConstraintViolationException(e))
        {
            throw new RepositoryExistsException(name);
        }

        return repository.ToInfo();
    }

    /// <inheritdoc/>
    public async Task EnsureDefaultAsync(CancellationToken cancellationToken)
    {
        if (!SupportsMultipleRepositories) return;

        if (await _context.Repositories!.AnyAsync(r => r.Key == Repository.DefaultKey, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        // The default row must have key 1, which rows that predate repositories already carry. Generated keys only
        // start at 1 in a table that never held a row (SQLite and MySQL keep counting after a delete), so the key is
        // given explicitly.
        var anyRow = await _context.Repositories!.AnyAsync(cancellationToken).ConfigureAwait(false);
        if (anyRow)
        {
            throw new InvalidOperationException(
                $"The database has repositories but none with key {Repository.DefaultKey}; the '{Repository.DefaultName}' repository cannot be restored.");
        }

        _context.Repositories!.Add(new Repository
        {
            Key = Repository.DefaultKey,
            Name = Repository.DefaultName,
            Format = RepositoryFormat.NuGet,
            Kind = RepositoryKind.Local,
            AnonymousRead = true,
            Created = DateTime.UtcNow,
        });

        var entityType = (_context as DbContext)?.Model.FindEntityType(typeof(Repository));
        if (entityType != null) await _context.BeginKeyedInsertAsync(entityType, cancellationToken).ConfigureAwait(false);
        try
        {
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException e) when (_context.IsUniqueConstraintViolationException(e))
        {
            // Another node or a concurrent start created it.
        }
        finally
        {
            if (entityType != null) await _context.EndKeyedInsertAsync(entityType, CancellationToken.None).ConfigureAwait(false);
        }
    }
}
