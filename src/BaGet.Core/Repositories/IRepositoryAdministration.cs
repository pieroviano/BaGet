using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace BaGet.Core;

/// <summary>
/// A repository as the administration pages list it.
/// </summary>
/// <param name="Key">The <see cref="Repository.Key"/>.</param>
/// <param name="Name">The <see cref="Repository.Name"/>.</param>
/// <param name="Format">The package format served.</param>
/// <param name="Kind">How the repository gets its content.</param>
/// <param name="AnonymousRead">Whether requests without credentials may read it.</param>
/// <param name="Created">When the repository was created (UTC).</param>
/// <param name="PackageCount">How many package versions it holds.</param>
public sealed record RepositorySummary(
    int Key,
    string Name,
    RepositoryFormat Format,
    RepositoryKind Kind,
    bool AnonymousRead,
    DateTime Created,
    int PackageCount)
{
    /// <summary>
    /// Whether this is the <see cref="Repository.DefaultName"/> repository, which can be neither renamed nor deleted.
    /// </summary>
    public bool IsDefault => Key == Repository.DefaultKey;
}

/// <summary>
/// Lists, edits and deletes repositories (creating goes through <see cref="IRepositoryService.CreateAsync"/>). What the
/// administrator pages use; writes go through the database context, so they are refused while the
/// <see cref="MaintenanceGate"/> is closed.
/// </summary>
public interface IRepositoryAdministration
{
    /// <summary>
    /// Every repository with its package count, ordered by name; the default one included.
    /// </summary>
    Task<IReadOnlyList<RepositorySummary>> ListAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Finds a repository by exact name.
    /// </summary>
    /// <returns>The repository, or null.</returns>
    Task<RepositorySummary?> FindAsync(string? name, CancellationToken cancellationToken);

    /// <summary>
    /// Sets <see cref="Repository.AnonymousRead"/>. A repository is never renamed: its name is in URLs and storage paths.
    /// </summary>
    /// <exception cref="KeyNotFoundException">No such repository.</exception>
    Task SetAnonymousReadAsync(string name, bool anonymousRead, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes a repository that holds no packages and no submission awaiting validation, together with its catalog
    /// entries, owner rows, validations and abuse reports, and every file it kept in storage (<c>repos/{name}/</c>) when
    /// the store can list its content.
    /// </summary>
    /// <exception cref="KeyNotFoundException">No such repository.</exception>
    /// <exception cref="RepositoryProtectedException">It is the default repository.</exception>
    /// <exception cref="RepositoryNotEmptyException">It still holds packages (delete them first) or a submission awaiting
    /// validation (wait for it or let it fail). Its catalog entries, owners, finished validations and abuse reports are
    /// history and are removed with the repository.</exception>
    Task DeleteAsync(string name, CancellationToken cancellationToken);
}

/// <summary>
/// The default repository cannot be renamed or deleted.
/// </summary>
public sealed class RepositoryProtectedException : InvalidOperationException
{
    public RepositoryProtectedException()
        : base($"The '{Repository.DefaultName}' repository is served at the root and cannot be deleted.")
    { }
}

/// <summary>
/// The repository still has content, so it cannot be deleted.
/// </summary>
public sealed class RepositoryNotEmptyException : InvalidOperationException
{
    public RepositoryNotEmptyException(string name, bool awaitingValidation = false)
        : base(awaitingValidation
            ? $"The repository '{name}' still has submissions awaiting validation. Wait for them to finish; its history (catalog entries, owners) is removed with the repository."
            : $"The repository '{name}' still holds packages. Delete them first; its history (catalog entries, owners) is removed with the repository.")
    { }
}
