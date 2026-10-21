using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NuGet.Versioning;

namespace BaGet.Core;

/// <summary>One member of a virtual repository as the administration page lists it.</summary>
/// <param name="Name">The member's name, as stored.</param>
/// <param name="Repository">The member repository, or null when no repository has that name any more.</param>
public sealed record VirtualMemberDetail(string Name, RepositoryInfo? Repository)
{
    /// <summary>Whether a repository of that name exists.</summary>
    public bool Exists => Repository != null;
}

/// <summary>A virtual repository as the administration pages edit it.</summary>
/// <param name="Repository">The virtual repository.</param>
/// <param name="Members">Its direct members in resolution order.</param>
/// <param name="DeployRepository">The name of the member that receives deployments, if any.</param>
/// <param name="DeployTarget">The local repository a push really goes to (the deploy repository, or where a virtual deploy repository deploys), or null.</param>
/// <param name="EffectiveMembers">The local and remote repositories that answer, in order, nested virtual members expanded.</param>
public sealed record VirtualRepositoryDetail(
    RepositoryInfo Repository,
    IReadOnlyList<VirtualMemberDetail> Members,
    string? DeployRepository,
    RepositoryInfo? DeployTarget,
    IReadOnlyList<RepositoryInfo> EffectiveMembers);

/// <summary>What asking one member about a package version found.</summary>
public enum ResolutionOutcome
{
    /// <summary>The member stores the version.</summary>
    Stored,

    /// <summary>A remote member that does not store it yet but whose upstream has it (it would be fetched).</summary>
    WouldFetch,

    /// <summary>The member does not have it.</summary>
    NotFound,

    /// <summary>A remote member whose upstream could not be asked.</summary>
    Unavailable,

    /// <summary>Not asked: an earlier member already answers (a member's upstream is not contacted then).</summary>
    NotAsked,

    /// <summary>The member does not store it and its upstream is in offline mode, so a request would not ask it.</summary>
    Offline,
}

/// <summary>One member's part in <see cref="ResolutionPreview"/>.</summary>
/// <param name="Member">The member asked.</param>
/// <param name="Outcome">What it found.</param>
public sealed record ResolutionStep(RepositoryInfo Member, ResolutionOutcome Outcome);

/// <summary>Where a package version would come from, member by member.</summary>
/// <param name="PackageId">The package id asked about.</param>
/// <param name="Version">The version asked about.</param>
/// <param name="Steps">The effective members in resolution order and what each found.</param>
/// <param name="Answers">The member that answers (the first that stores the version or whose upstream has it), or null when none.</param>
public sealed record ResolutionPreview(string PackageId, NuGetVersion Version, IReadOnlyList<ResolutionStep> Steps, RepositoryInfo? Answers);

/// <summary>
/// Creates, edits and explains virtual repositories. What the administration pages use; writes go through the database
/// context, so the <see cref="MaintenanceGate"/> refuses them while it is closed. The access model is not consulted:
/// the pages are administrators' tools (read access to a virtual repository's members is judged per request, see
/// <see cref="VirtualRepositoryReader"/>).
/// </summary>
public interface IVirtualRepositoryAdministration
{
    /// <summary>A virtual repository by exact name, or null when there is none (or it is not virtual).</summary>
    Task<VirtualRepositoryDetail?> FindAsync(string? name, CancellationToken cancellationToken);

    /// <summary>
    /// The NuGet repositories that can be added to the virtual repository <paramref name="name"/> (every other
    /// repository whose own members do not lead back to it), ordered by name; for a new repository pass null.
    /// </summary>
    Task<IReadOnlyList<RepositoryInfo>> CandidatesAsync(string? name, CancellationToken cancellationToken);

    /// <summary>The virtual repositories that list <paramref name="memberName"/> directly, ordered by name.</summary>
    Task<IReadOnlyList<string>> ReferencedByAsync(string memberName, CancellationToken cancellationToken);

    /// <summary>
    /// Creates a virtual NuGet repository. Audited.
    /// </summary>
    /// <exception cref="ArgumentException">The name or the settings are invalid; the message lists the problems.</exception>
    /// <exception cref="RepositoryExistsException">The name is taken.</exception>
    /// <exception cref="NotSupportedException">The configuration holds the default repository only.</exception>
    Task<RepositoryInfo> CreateAsync(string name, VirtualRepositorySettings settings, CancellationToken cancellationToken, string? actor = null, bool anonymousRead = true);

    /// <summary>
    /// Replaces the members and the deploy repository. Refused (nothing changes) for an unknown, remote-only or
    /// non-NuGet member, for a deploy repository that is not a member or does not end in a local repository, and for a
    /// change that would close a cycle between virtual repositories. Audited.
    /// </summary>
    /// <exception cref="KeyNotFoundException">No such virtual repository.</exception>
    /// <exception cref="ArgumentException">The settings are invalid; the message lists the problems.</exception>
    Task UpdateAsync(string name, VirtualRepositorySettings settings, CancellationToken cancellationToken, string? actor = null);

    /// <summary>
    /// Where <paramref name="packageId"/> <paramref name="version"/> would come from: each effective member is asked in
    /// order whether it stores the version (a remote member that does not is asked whether its upstream has it, unless an
    /// earlier member already answers). Stores nothing and fetches nothing.
    /// </summary>
    /// <exception cref="KeyNotFoundException">No such virtual repository.</exception>
    Task<ResolutionPreview> PreviewAsync(string name, string packageId, NuGetVersion version, CancellationToken cancellationToken);
}

/// <summary>
/// <see cref="IVirtualRepositoryAdministration"/> over the relational database.
/// </summary>
public sealed class VirtualRepositoryAdministration : IVirtualRepositoryAdministration
{
    private readonly IContext _context;
    private readonly IRepositoryService _repositories;
    private readonly IServiceScopeFactory _scopes;
    private readonly MaintenanceGate? _gate;
    private readonly SystemTime? _time;

    public VirtualRepositoryAdministration(
        IContext context,
        IRepositoryService repositories,
        IServiceScopeFactory scopes,
        MaintenanceGate? gate = null,
        SystemTime? time = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _repositories = repositories ?? throw new ArgumentNullException(nameof(repositories));
        _scopes = scopes ?? throw new ArgumentNullException(nameof(scopes));
        _gate = gate;
        _time = time;
    }

    private DateTime Now => _time?.UtcNow ?? DateTime.UtcNow;

    /// <inheritdoc/>
    public async Task<VirtualRepositoryDetail?> FindAsync(string? name, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(name) || _context.Repositories == null) return null;

        var rows = await LoadAsync(cancellationToken).ConfigureAwait(false);
        if (!rows.TryGetValue(name!, out var row) || row.Kind != RepositoryKind.Virtual) return null;

        var settings = VirtualSettingsCodec.Deserialize(row.Settings);
        var effective = new List<RepositoryInfo>();
        VirtualRepositories.Flatten(name!, rows, new HashSet<string>(StringComparer.Ordinal), effective, new HashSet<int>(), depth: 0);

        return new VirtualRepositoryDetail(
            row.ToInfo(),
            settings.Members.Select(m => new VirtualMemberDetail(m, rows.TryGetValue(m, out var member) ? member.ToInfo() : null)).ToList(),
            settings.DeployRepository,
            VirtualRepositories.ResolveDeploy(name!, rows),
            effective);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<RepositoryInfo>> CandidatesAsync(string? name, CancellationToken cancellationToken)
    {
        if (_context.Repositories == null) return Array.Empty<RepositoryInfo>();

        var rows = await LoadAsync(cancellationToken).ConfigureAwait(false);

        return rows.Values
            .Where(r => r.Format == RepositoryFormat.NuGet && r.Name != name && !(name != null && r.Kind == RepositoryKind.Virtual && Reaches(r.Name!, name, rows)))
            .OrderBy(r => r.Name, StringComparer.Ordinal)
            .Select(r => r.ToInfo())
            .ToList();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> ReferencedByAsync(string memberName, CancellationToken cancellationToken)
    {
        if (_context.Repositories == null) return Array.Empty<string>();

        var rows = await LoadAsync(cancellationToken).ConfigureAwait(false);

        return rows.Values
            .Where(r => r.Kind == RepositoryKind.Virtual && VirtualSettingsCodec.Deserialize(r.Settings).Members.Contains(memberName))
            .Select(r => r.Name!)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();
    }

    /// <inheritdoc/>
    public async Task<RepositoryInfo> CreateAsync(string name, VirtualRepositorySettings settings, CancellationToken cancellationToken, string? actor = null, bool anonymousRead = true)
    {
        if (settings == null) throw new ArgumentNullException(nameof(settings));
        if (!RepositoryNames.IsValid(name, out var error)) throw new ArgumentException(error, nameof(name));
        _gate?.ThrowIfClosed();

        if (!_repositories.SupportsMultipleRepositories)
        {
            throw new NotSupportedException(
                "Only the 'default' repository is available with this configuration; use a relational database (SQLite, SQL Server, PostgreSQL or MySQL) and a search type other than AzureSearch (whose index is not repository-aware) to create more.");
        }

        await _repositories.EnsureDefaultAsync(cancellationToken).ConfigureAwait(false);

        var rows = await LoadAsync(cancellationToken).ConfigureAwait(false);
        if (rows.ContainsKey(name)) throw new RepositoryExistsException(name);

        Validate(name, settings, rows);

        var row = new Repository
        {
            Name = name,
            Format = RepositoryFormat.NuGet,
            Kind = RepositoryKind.Virtual,
            Settings = VirtualSettingsCodec.Serialize(settings),
            AnonymousRead = anonymousRead,
            Created = Now,
        };
        _context.Repositories!.Add(row);
        AccessAdministration.Audit(_context, Now, actor, "repository.virtual.create", name, Describe(settings));

        try
        {
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException e) when (_context.IsUniqueConstraintViolationException(e))
        {
            throw new RepositoryExistsException(name);
        }

        return row.ToInfo();
    }

    /// <inheritdoc/>
    public async Task UpdateAsync(string name, VirtualRepositorySettings settings, CancellationToken cancellationToken, string? actor = null)
    {
        if (settings == null) throw new ArgumentNullException(nameof(settings));
        _gate?.ThrowIfClosed();

        var row = _context.Repositories == null
            ? null
            : await _context.Repositories.FirstOrDefaultAsync(r => r.Name == name && r.Kind == RepositoryKind.Virtual, cancellationToken).ConfigureAwait(false);
        if (row == null) throw new KeyNotFoundException($"There is no virtual repository named '{name}'.");

        var rows = await LoadAsync(cancellationToken).ConfigureAwait(false);
        Validate(name, settings, rows);

        row.Settings = VirtualSettingsCodec.Serialize(settings);
        AccessAdministration.Audit(_context, Now, actor, "repository.virtual.update", name, Describe(settings));
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<ResolutionPreview> PreviewAsync(string name, string packageId, NuGetVersion version, CancellationToken cancellationToken)
    {
        var detail = await FindAsync(name, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"There is no virtual repository named '{name}'.");

        var steps = new List<ResolutionStep>();
        RepositoryInfo? answers = null;

        foreach (var member in detail.EffectiveMembers)
        {
            // Once a member answers the later ones are not asked: a real request stops there, and probing an upstream
            // is a request to it.
            var outcome = ResolutionOutcome.NotAsked;
            if (answers != null)
            {
                // Only the stored copy is looked at (cheap and local): it shows a member the answering one shadows.
                using var scope = _scopes.CreateRepositoryScope(member);
                if (await scope.ServiceProvider.GetRequiredService<IPackageDatabase>().ExistsAsync(packageId, version, cancellationToken).ConfigureAwait(false))
                {
                    outcome = ResolutionOutcome.Stored;
                }
            }
            else
            {
                // The member's own package service decides, as it does for a real request: its stored copy first, then
                // its upstream (any member can have one: a remote repository, or the default repository's mirror).
                using var scope = _scopes.CreateRepositoryScope(member);
                try
                {
                    outcome = await scope.ServiceProvider.GetRequiredService<PackageService>()
                        .ProbeAsync(packageId, version, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    outcome = ResolutionOutcome.Unavailable;
                }
            }

            if (answers == null && outcome is ResolutionOutcome.Stored or ResolutionOutcome.WouldFetch) answers = member;
            steps.Add(new ResolutionStep(member, outcome));
        }

        return new ResolutionPreview(packageId, version, steps, answers);
    }

    private static object Describe(VirtualRepositorySettings settings)
        => new { members = settings.Members, deploy = settings.DeployRepository };

    private async Task<Dictionary<string, Repository>> LoadAsync(CancellationToken cancellationToken)
    {
        var rows = await _context.Repositories!.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);

        return rows.Where(r => r.Name != null).ToDictionary(r => r.Name!, StringComparer.Ordinal);
    }

    // Whether the virtual repository 'from' lists 'target' directly or through nested virtual members.
    private static bool Reaches(string from, string target, IReadOnlyDictionary<string, Repository> rows)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<string>();
        pending.Push(from);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            if (!seen.Add(current) || !rows.TryGetValue(current, out var row) || row.Kind != RepositoryKind.Virtual) continue;

            foreach (var member in VirtualSettingsCodec.Deserialize(row.Settings).Members)
            {
                if (member == target) return true;
                pending.Push(member);
            }
        }

        return false;
    }

    // Throws ArgumentException listing every problem of 'settings' as the settings of the virtual repository 'name'.
    private static void Validate(string name, VirtualRepositorySettings settings, Dictionary<string, Repository> rows)
    {
        var problems = settings.ValidateShape().ToList();

        foreach (var member in settings.Members)
        {
            if (member == name)
            {
                problems.Add("A virtual repository cannot contain itself.");
            }
            else if (!rows.TryGetValue(member, out var row))
            {
                problems.Add($"There is no repository named '{member}'.");
            }
            else if (row.Format != RepositoryFormat.NuGet)
            {
                problems.Add($"'{member}' is not a NuGet repository.");
            }
            else if (row.Kind == RepositoryKind.Virtual && Reaches(member, name, rows))
            {
                problems.Add($"Adding '{member}' would make '{name}' contain itself through '{member}'.");
            }
        }

        if (problems.Count == 0 && !string.IsNullOrEmpty(settings.DeployRepository))
        {
            // The deploy repository is judged in the graph with the new members in place of the stored ones.
            var proposed = new Dictionary<string, Repository>(rows, StringComparer.Ordinal)
            {
                [name] = new Repository
                {
                    Key = rows.TryGetValue(name, out var existing) ? existing.Key : 0,
                    Name = name,
                    Format = RepositoryFormat.NuGet,
                    Kind = RepositoryKind.Virtual,
                    Settings = VirtualSettingsCodec.Serialize(settings),
                },
            };

            if (VirtualRepositories.ResolveDeploy(name, proposed) == null)
            {
                problems.Add($"The deploy repository '{settings.DeployRepository}' must be a local repository (or a virtual one that deploys to a local repository); a remote repository takes no deployments.");
            }
        }

        if (problems.Count > 0) throw new ArgumentException(string.Join(" ", problems), nameof(settings));
    }
}
