using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace BaGet.Core;

/// <summary>A change to the access model that cannot be made, with a message fit for an administrator.</summary>
public class AccessModelException : Exception
{
    public AccessModelException(string message) : base(message) { }
}

/// <summary>
/// Edits the access model: groups and their members, permission targets and grants, project members. Every change is
/// written to <see cref="AccessAuditEntry"/> in the same save, and goes through the database context, so it is refused
/// while the <see cref="MaintenanceGate"/> is closed and invalidates the evaluator's cache
/// (<see cref="AccessModelSaveChangesInterceptor"/>). What the admin pages use.
/// </summary>
public interface IAccessAdministration
{
    /// <summary>Creates a group.</summary>
    /// <exception cref="AccessModelException">The name is invalid or taken.</exception>
    Task<Group> CreateGroupAsync(string name, string? description, string? actor, CancellationToken cancellationToken);

    /// <summary>Deletes a group, its members and every grant naming it.</summary>
    /// <exception cref="AccessModelException">Unknown group, or a built-in one.</exception>
    Task DeleteGroupAsync(string name, string? actor, CancellationToken cancellationToken);

    /// <summary>Adds a user to a group (idempotent).</summary>
    /// <exception cref="AccessModelException">Unknown group, or a built-in one (membership is implicit).</exception>
    Task AddGroupMemberAsync(string group, string userId, string? actor, CancellationToken cancellationToken);

    /// <summary>Removes a user from a group (idempotent).</summary>
    Task RemoveGroupMemberAsync(string group, string userId, string? actor, CancellationToken cancellationToken);

    /// <summary>Creates a permission target without grants.</summary>
    /// <param name="name">Unique name.</param>
    /// <param name="repositoryPatterns">Repository names or <c>*</c> globs.</param>
    /// <param name="includePatterns">Package ID globs; null or empty means all.</param>
    /// <param name="excludePatterns">Package ID globs to leave out.</param>
    /// <param name="projectKey">The administering project, if any.</param>
    /// <param name="actor">The user making the change.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <exception cref="AccessModelException">The name is invalid or taken, or no repository pattern is given.</exception>
    Task<PermissionTarget> CreateTargetAsync(
        string name,
        IReadOnlyCollection<string> repositoryPatterns,
        IReadOnlyCollection<string>? includePatterns,
        IReadOnlyCollection<string>? excludePatterns,
        int? projectKey,
        string? actor,
        CancellationToken cancellationToken);

    /// <summary>Replaces a target's patterns.</summary>
    Task UpdateTargetAsync(
        string name,
        IReadOnlyCollection<string> repositoryPatterns,
        IReadOnlyCollection<string>? includePatterns,
        IReadOnlyCollection<string>? excludePatterns,
        string? actor,
        CancellationToken cancellationToken);

    /// <summary>Deletes a target and its grants (built-in ones too; <see cref="RestoreBuiltInsAsync"/> brings them back).</summary>
    Task DeleteTargetAsync(string name, string? actor, CancellationToken cancellationToken);

    /// <summary>
    /// Sets the actions a principal holds through a target, replacing any earlier grant to the same principal;
    /// <see cref="AccessAction.None"/> removes it.
    /// </summary>
    /// <param name="target">The target's name.</param>
    /// <param name="type">What kind of principal.</param>
    /// <param name="principalId">A user's id, or a group's or organization's key.</param>
    /// <param name="actions">The actions granted.</param>
    /// <param name="actor">The user making the change.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task SetGrantAsync(
        string target,
        PrincipalType type,
        string principalId,
        AccessAction actions,
        string? actor,
        CancellationToken cancellationToken);

    /// <summary>Creates a project (no repositories yet: set <see cref="Repository.ProjectKey"/>).</summary>
    Task<Project> CreateProjectAsync(string name, string? displayName, string? actor, CancellationToken cancellationToken);

    /// <summary>Gives a user or group a role in a project, replacing an earlier one.</summary>
    Task SetProjectMemberAsync(
        string project,
        PrincipalType type,
        string principalId,
        ProjectRole role,
        string? actor,
        CancellationToken cancellationToken);

    /// <summary>Removes a user or group from a project.</summary>
    Task RemoveProjectMemberAsync(
        string project,
        PrincipalType type,
        string principalId,
        string? actor,
        CancellationToken cancellationToken);

    /// <summary>Re-creates the built-in groups and targets with their upgrade defaults where they are missing.</summary>
    Task RestoreBuiltInsAsync(string? actor, CancellationToken cancellationToken);

    /// <summary>The most recent audit entries, newest first.</summary>
    Task<IReadOnlyList<AccessAuditEntry>> AuditAsync(int take, CancellationToken cancellationToken);
}

/// <summary><see cref="IAccessAdministration"/> over the relational database.</summary>
public class AccessAdministration : IAccessAdministration
{
    private readonly IContext _context;
    private readonly SystemTime _time;

    public AccessAdministration(IContext context, SystemTime time)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _time = time ?? throw new ArgumentNullException(nameof(time));
    }

    /// <summary>
    /// Adds an audit entry to the context without saving, so the caller's own save writes both.
    /// </summary>
    internal static void Audit(IContext context, DateTime now, string? actor, string action, string? subject, object? details = null)
    {
        context.AccessAuditEntries!.Add(new AccessAuditEntry
        {
            Created = now,
            Actor = actor,
            Action = action,
            Subject = subject,
            Details = details == null ? null : JsonSerializer.Serialize(details),
        });
    }

    private void Audit(string? actor, string action, string? subject, object? details = null)
        => Audit(_context, _time.UtcNow, actor, action, subject, details);

    /// <inheritdoc/>
    public async Task<Group> CreateGroupAsync(string name, string? description, string? actor, CancellationToken cancellationToken)
    {
        if (!AccessNames.IsValid(name, out var error)) throw new AccessModelException(error!);
        if (await FindGroupAsync(name, cancellationToken).ConfigureAwait(false) != null)
        {
            throw new AccessModelException($"There is already a group named '{name}'.");
        }

        var group = new Group { Name = name, Description = description, Created = _time.UtcNow };
        _context.Groups!.Add(group);
        Audit(actor, "group.create", name);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return group;
    }

    /// <inheritdoc/>
    public async Task DeleteGroupAsync(string name, string? actor, CancellationToken cancellationToken)
    {
        var group = await GroupAsync(name, cancellationToken).ConfigureAwait(false);
        if (group.IsBuiltIn) throw new AccessModelException($"'{group.Name}' is a built-in group and cannot be deleted.");

        var id = group.Key.ToString();
        var grants = await _context.PermissionGrants!
            .Where(g => g.PrincipalType == PrincipalType.Group && g.PrincipalId == id)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var projectRoles = await _context.ProjectMembers!
            .Where(m => m.PrincipalType == PrincipalType.Group && m.PrincipalId == id)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        _context.PermissionGrants!.RemoveRange(grants);
        _context.ProjectMembers!.RemoveRange(projectRoles);
        _context.Groups!.Remove(group);
        Audit(actor, "group.delete", group.Name, new { grants = grants.Count, projectMemberships = projectRoles.Count });
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task AddGroupMemberAsync(string group, string userId, string? actor, CancellationToken cancellationToken)
    {
        var row = await GroupAsync(group, cancellationToken).ConfigureAwait(false);
        if (row.IsBuiltIn) throw new AccessModelException($"Membership of the built-in group '{row.Name}' is implicit.");
        if (string.IsNullOrEmpty(userId)) throw new AccessModelException("A user is required.");

        if (await _context.GroupMembers!.AnyAsync(m => m.GroupKey == row.Key && m.UserId == userId, cancellationToken).ConfigureAwait(false)) return;

        _context.GroupMembers!.Add(new GroupMember { GroupKey = row.Key, UserId = userId });
        Audit(actor, "group.member.add", row.Name, new { userId });
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task RemoveGroupMemberAsync(string group, string userId, string? actor, CancellationToken cancellationToken)
    {
        var row = await GroupAsync(group, cancellationToken).ConfigureAwait(false);
        var member = await _context.GroupMembers!.FirstOrDefaultAsync(m => m.GroupKey == row.Key && m.UserId == userId, cancellationToken).ConfigureAwait(false);
        if (member == null) return;

        _context.GroupMembers!.Remove(member);
        Audit(actor, "group.member.remove", row.Name, new { userId });
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<PermissionTarget> CreateTargetAsync(
        string name,
        IReadOnlyCollection<string> repositoryPatterns,
        IReadOnlyCollection<string>? includePatterns,
        IReadOnlyCollection<string>? excludePatterns,
        int? projectKey,
        string? actor,
        CancellationToken cancellationToken)
    {
        if (!AccessNames.IsValid(name, out var error)) throw new AccessModelException(error!);
        if (await FindTargetAsync(name, cancellationToken).ConfigureAwait(false) != null)
        {
            throw new AccessModelException($"There is already a permission target named '{name}'.");
        }

        var target = new PermissionTarget { Name = name, ProjectKey = projectKey, Created = _time.UtcNow };
        Apply(target, repositoryPatterns, includePatterns, excludePatterns);
        _context.PermissionTargets!.Add(target);
        Audit(actor, "target.create", name, Describe(target));
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return target;
    }

    /// <inheritdoc/>
    public async Task UpdateTargetAsync(
        string name,
        IReadOnlyCollection<string> repositoryPatterns,
        IReadOnlyCollection<string>? includePatterns,
        IReadOnlyCollection<string>? excludePatterns,
        string? actor,
        CancellationToken cancellationToken)
    {
        var target = await TargetAsync(name, cancellationToken).ConfigureAwait(false);
        Apply(target, repositoryPatterns, includePatterns, excludePatterns);
        Audit(actor, "target.update", name, Describe(target));
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task DeleteTargetAsync(string name, string? actor, CancellationToken cancellationToken)
    {
        var target = await TargetAsync(name, cancellationToken).ConfigureAwait(false);
        var grants = await _context.PermissionGrants!.Where(g => g.TargetKey == target.Key).ToListAsync(cancellationToken).ConfigureAwait(false);

        _context.PermissionGrants!.RemoveRange(grants);
        _context.PermissionTargets!.Remove(target);
        Audit(actor, "target.delete", name, new { grants = grants.Count });
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task SetGrantAsync(
        string target,
        PrincipalType type,
        string principalId,
        AccessAction actions,
        string? actor,
        CancellationToken cancellationToken)
    {
        var row = await TargetAsync(target, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrEmpty(principalId)) throw new AccessModelException("A principal is required.");
        if ((actions & ~AccessAction.All) != 0) throw new AccessModelException("Unknown actions.");

        var grant = await _context.PermissionGrants!.FirstOrDefaultAsync(
            g => g.TargetKey == row.Key && g.PrincipalType == type && g.PrincipalId == principalId, cancellationToken).ConfigureAwait(false);

        if (actions == AccessAction.None)
        {
            if (grant == null) return;
            _context.PermissionGrants!.Remove(grant);
        }
        else if (grant == null)
        {
            _context.PermissionGrants!.Add(new PermissionGrant { TargetKey = row.Key, PrincipalType = type, PrincipalId = principalId, Actions = actions });
        }
        else
        {
            grant.Actions = actions;
        }

        Audit(actor, "grant.set", row.Name, new { principalType = type.ToString(), principalId, actions = actions.ToString() });
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<Project> CreateProjectAsync(string name, string? displayName, string? actor, CancellationToken cancellationToken)
    {
        if (!AccessNames.IsValid(name, out var error)) throw new AccessModelException(error!);
        if (await _context.Projects!.AnyAsync(p => p.Name == name, cancellationToken).ConfigureAwait(false))
        {
            throw new AccessModelException($"There is already a project named '{name}'.");
        }

        var project = new Project { Name = name, DisplayName = displayName, Created = _time.UtcNow };
        _context.Projects!.Add(project);
        Audit(actor, "project.create", name);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return project;
    }

    /// <inheritdoc/>
    public async Task SetProjectMemberAsync(
        string project,
        PrincipalType type,
        string principalId,
        ProjectRole role,
        string? actor,
        CancellationToken cancellationToken)
    {
        if (type == PrincipalType.Organization) throw new AccessModelException("A project member is a user or a group.");
        var row = await ProjectAsync(project, cancellationToken).ConfigureAwait(false);

        var member = await _context.ProjectMembers!.FirstOrDefaultAsync(
            m => m.ProjectKey == row.Key && m.PrincipalType == type && m.PrincipalId == principalId, cancellationToken).ConfigureAwait(false);
        if (member == null)
        {
            _context.ProjectMembers!.Add(new ProjectMember { ProjectKey = row.Key, PrincipalType = type, PrincipalId = principalId, Role = role });
        }
        else
        {
            member.Role = role;
        }

        Audit(actor, "project.member.set", row.Name, new { principalType = type.ToString(), principalId, role = role.ToString() });
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task RemoveProjectMemberAsync(string project, PrincipalType type, string principalId, string? actor, CancellationToken cancellationToken)
    {
        var row = await ProjectAsync(project, cancellationToken).ConfigureAwait(false);
        var member = await _context.ProjectMembers!.FirstOrDefaultAsync(
            m => m.ProjectKey == row.Key && m.PrincipalType == type && m.PrincipalId == principalId, cancellationToken).ConfigureAwait(false);
        if (member == null) return;

        _context.ProjectMembers!.Remove(member);
        Audit(actor, "project.member.remove", row.Name, new { principalType = type.ToString(), principalId });
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task RestoreBuiltInsAsync(string? actor, CancellationToken cancellationToken)
    {
        var everyone = await EnsureBuiltInGroupAsync(BuiltInNames.Everyone, "Every caller, anonymous ones included.", cancellationToken).ConfigureAwait(false);
        var authenticated = await EnsureBuiltInGroupAsync(BuiltInNames.Authenticated, "Every caller that presented valid credentials.", cancellationToken).ConfigureAwait(false);

        // Keys are generated here, so save the groups before naming them in grants.
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await EnsureBuiltInTargetAsync(BuiltInNames.AnyoneReads, everyone, AccessAction.Read, cancellationToken).ConfigureAwait(false);
        await EnsureBuiltInTargetAsync(
            BuiltInNames.AuthenticatedDeploy,
            authenticated,
            AccessAction.Annotate | AccessAction.Deploy | AccessAction.Delete | AccessAction.Manage,
            cancellationToken).ConfigureAwait(false);

        Audit(actor, "builtins.restore", null);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AccessAuditEntry>> AuditAsync(int take, CancellationToken cancellationToken)
        => await _context.AccessAuditEntries!.AsNoTracking()
            .OrderByDescending(e => e.Created).ThenByDescending(e => e.Key)
            .Take(Math.Max(1, take))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    private async Task<Group> EnsureBuiltInGroupAsync(string name, string description, CancellationToken cancellationToken)
    {
        var group = await FindGroupAsync(name, cancellationToken).ConfigureAwait(false);
        if (group != null)
        {
            group.IsBuiltIn = true;
            return group;
        }

        group = new Group { Name = name, Description = description, Created = _time.UtcNow, IsBuiltIn = true };
        _context.Groups!.Add(group);
        return group;
    }

    private async Task EnsureBuiltInTargetAsync(string name, Group group, AccessAction actions, CancellationToken cancellationToken)
    {
        var target = await FindTargetAsync(name, cancellationToken).ConfigureAwait(false);
        if (target == null)
        {
            target = new PermissionTarget
            {
                Name = name,
                RepositoryPatterns = new[] { "*" },
                IncludePatterns = new[] { "*" },
                ExcludePatterns = Array.Empty<string>(),
                IsBuiltIn = true,
                Created = _time.UtcNow,
            };
            _context.PermissionTargets!.Add(target);
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        var id = group.Key.ToString();
        if (!await _context.PermissionGrants!.AnyAsync(
                g => g.TargetKey == target.Key && g.PrincipalType == PrincipalType.Group && g.PrincipalId == id, cancellationToken).ConfigureAwait(false))
        {
            _context.PermissionGrants!.Add(new PermissionGrant { TargetKey = target.Key, PrincipalType = PrincipalType.Group, PrincipalId = id, Actions = actions });
        }
    }

    private static void Apply(
        PermissionTarget target,
        IReadOnlyCollection<string> repositoryPatterns,
        IReadOnlyCollection<string>? includePatterns,
        IReadOnlyCollection<string>? excludePatterns)
    {
        string[] Clean(IEnumerable<string>? patterns)
            => (patterns ?? Array.Empty<string>()).Select(p => p?.Trim() ?? string.Empty).Where(p => p.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

        var repositories = Clean(repositoryPatterns);
        if (repositories.Length == 0) throw new AccessModelException("A permission target needs at least one repository pattern.");

        target.RepositoryPatterns = repositories;
        var include = Clean(includePatterns);
        target.IncludePatterns = include.Length == 0 ? new[] { "*" } : include;
        target.ExcludePatterns = Clean(excludePatterns);
    }

    private static object Describe(PermissionTarget target) => new
    {
        repositories = target.RepositoryPatterns,
        include = target.IncludePatterns,
        exclude = target.ExcludePatterns,
        projectKey = target.ProjectKey,
    };

    private Task<Group?> FindGroupAsync(string name, CancellationToken cancellationToken)
    {
        var lower = name.ToLowerInvariant();
        return _context.Groups!.FirstOrDefaultAsync(g => g.Name!.ToLower() == lower, cancellationToken);
    }

    private Task<PermissionTarget?> FindTargetAsync(string name, CancellationToken cancellationToken)
    {
        var lower = name.ToLowerInvariant();
        return _context.PermissionTargets!.FirstOrDefaultAsync(t => t.Name!.ToLower() == lower, cancellationToken);
    }

    private async Task<Group> GroupAsync(string name, CancellationToken cancellationToken)
        => await FindGroupAsync(name, cancellationToken).ConfigureAwait(false) ?? throw new AccessModelException($"There is no group named '{name}'.");

    private async Task<PermissionTarget> TargetAsync(string name, CancellationToken cancellationToken)
        => await FindTargetAsync(name, cancellationToken).ConfigureAwait(false) ?? throw new AccessModelException($"There is no permission target named '{name}'.");

    private async Task<Project> ProjectAsync(string name, CancellationToken cancellationToken)
        => await _context.Projects!.FirstOrDefaultAsync(p => p.Name == name, cancellationToken).ConfigureAwait(false)
           ?? throw new AccessModelException($"There is no project named '{name}'.");
}
