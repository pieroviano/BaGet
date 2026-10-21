using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BaGet.Core;

/// <summary>
/// Decides what a caller may do in a repository, from the access model (groups, permission targets, project
/// roles). The first of two requirements for a write: package ownership (<see cref="IPackageAuthorizationService"/>)
/// is the second and still applies inside Deploy, Delete and Annotate.
/// </summary>
public interface IAccessEvaluator
{
    /// <summary>
    /// Whether <paramref name="principal"/> holds <paramref name="action"/> in <paramref name="repository"/>, on
    /// <paramref name="packageId"/> when given (the targets' include and exclude patterns apply), else on the
    /// repository as a whole (some package may match). A null principal is an anonymous caller.
    /// </summary>
    /// <remarks>
    /// The server's superuser (global API key) and administrators (D5 of the access plan) hold every action. Where the
    /// database is not relational (Azure Table) there is no access model: reading is open and any authenticated caller
    /// holds the write actions, as before the model existed.
    /// </remarks>
    Task<bool> IsAllowedAsync(
        BaGetPrincipal? principal,
        RepositoryInfo repository,
        string? packageId,
        AccessAction action,
        CancellationToken cancellationToken);

    /// <summary>
    /// The NuGet repositories <paramref name="principal"/> may read, ordered by name (<c>default</c> first).
    /// </summary>
    Task<IReadOnlyList<RepositoryInfo>> ReadableRepositoriesAsync(BaGetPrincipal? principal, CancellationToken cancellationToken);

    /// <summary>
    /// The NuGet repositories in which <paramref name="principal"/> holds <paramref name="action"/>, ordered like
    /// <see cref="ReadableRepositoriesAsync"/>.
    /// </summary>
    Task<IReadOnlyList<RepositoryInfo>> RepositoriesWithAsync(BaGetPrincipal? principal, AccessAction action, CancellationToken cancellationToken);
}

/// <summary>
/// <see cref="IAccessEvaluator"/> over the relational database, through the cached <see cref="AccessSnapshot"/>.
/// </summary>
public sealed class AccessEvaluator : IAccessEvaluator
{
    private readonly IContext _context;
    private readonly AccessModelCache _cache;
    private readonly IRepositoryService _repositories;

    public AccessEvaluator(IContext context, AccessModelCache cache, IRepositoryService repositories)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _repositories = repositories ?? throw new ArgumentNullException(nameof(repositories));
    }

    /// <inheritdoc/>
    public async Task<bool> IsAllowedAsync(
        BaGetPrincipal? principal,
        RepositoryInfo repository,
        string? packageId,
        AccessAction action,
        CancellationToken cancellationToken)
    {
        if (repository == null) throw new ArgumentNullException(nameof(repository));
        if (action == AccessAction.None) return true;
        if (_context is NullContext) return action == AccessAction.Read || principal != null;
        if (principal != null && (principal.IsSuperuser || principal.IsAdministrator)) return true;

        var snapshot = await _cache.GetAsync(_context, cancellationToken).ConfigureAwait(false);
        var membership = await MembershipAsync(snapshot, principal, cancellationToken).ConfigureAwait(false);

        return (snapshot.Granted(principal, membership, repository, packageId) & action) == action;
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<RepositoryInfo>> ReadableRepositoriesAsync(BaGetPrincipal? principal, CancellationToken cancellationToken)
        => RepositoriesWithAsync(principal, AccessAction.Read, cancellationToken);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<RepositoryInfo>> RepositoriesWithAsync(
        BaGetPrincipal? principal,
        AccessAction action,
        CancellationToken cancellationToken)
    {
        var all = await _repositories.ListAsync(cancellationToken).ConfigureAwait(false);
        var result = new List<RepositoryInfo>();

        foreach (var repository in all.Where(r => r.Format == RepositoryFormat.NuGet))
        {
            if (await IsAllowedAsync(principal, repository, packageId: null, action, cancellationToken).ConfigureAwait(false))
            {
                result.Add(repository);
            }
        }

        return result
            .OrderBy(r => r.IsDefault ? 0 : 1)
            .ThenBy(r => r.Name, StringComparer.Ordinal)
            .ToList();
    }

    private Task<Membership> MembershipAsync(AccessSnapshot snapshot, BaGetPrincipal? principal, CancellationToken cancellationToken)
        => principal?.UserId == null
            ? Task.FromResult(Membership.None)
            : snapshot.MembershipAsync(_context, principal.UserId, cancellationToken);
}
