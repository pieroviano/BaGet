using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace BaGet.Core;

/// <summary>
/// <see cref="IRepositoryService"/> over the relational database. When the database is not relational
/// (<see cref="NullContext"/>, as with Azure Table) only the default repository exists and creating another is
/// refused with a message saying why.
/// </summary>
public class RepositoryService : IRepositoryService
{
    private readonly IContext _context;

    public RepositoryService(IContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc/>
    public bool SupportsMultipleRepositories => !(_context is NullContext);

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
                "The configured database supports only the 'default' repository; use a relational database (SQLite, SQL Server, PostgreSQL or MySQL) to create more.");
        }

        if (format != RepositoryFormat.NuGet || kind != RepositoryKind.Local)
        {
            throw new NotSupportedException("Only local NuGet repositories can be created.");
        }

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

        // The default row must get key 1, which rows that predate repositories already carry. Auto-generated keys
        // start at 1 in an empty table on every provider, but say so explicitly where that is not guaranteed.
        var anyRow = await _context.Repositories!.AnyAsync(cancellationToken).ConfigureAwait(false);
        if (anyRow)
        {
            throw new InvalidOperationException(
                $"The database has repositories but none with key {Repository.DefaultKey}; the '{Repository.DefaultName}' repository cannot be restored.");
        }

        _context.Repositories!.Add(new Repository
        {
            Name = Repository.DefaultName,
            Format = RepositoryFormat.NuGet,
            Kind = RepositoryKind.Local,
            AnonymousRead = true,
            Created = DateTime.UtcNow,
        });

        try
        {
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException e) when (_context.IsUniqueConstraintViolationException(e))
        {
            // Another node or a concurrent start created it.
        }
    }
}
