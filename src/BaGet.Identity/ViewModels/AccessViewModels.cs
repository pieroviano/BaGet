using System;
using System.Collections.Generic;
using BaGet.Core;
using BaGet.Identity.Pages.Admin;

namespace BaGet.Identity.ViewModels;

/// <summary>
/// What every access-control screen (groups, permission targets, projects) shows besides its own data: the outcome of the last
/// action, why it was refused, whether the signed-in user is a server administrator (rather than only a project administrator)
/// and what the password confirmation partial needs.
/// </summary>
public abstract class AccessViewModel : ConfirmationModel, IFlashModel
{
    /// <inheritdoc />
    public string? Message { get; set; }

    /// <inheritdoc />
    public string? Error { get; set; }

    /// <summary>Whether the signed-in user is a server administrator.</summary>
    public bool IsServerAdministrator { get; internal set; }
}

/// <summary>What the groups screen (<c>/admin/groups</c>) shows: the groups and the new-group form.</summary>
public sealed class GroupsViewModel : AccessViewModel
{
    /// <summary>The new group name.</summary>
    public string? Name { get; set; }

    /// <summary>The new group description.</summary>
    public string? Description { get; set; }

    /// <summary>The groups.</summary>
    public IReadOnlyList<GroupSummary> Groups { get; internal set; } = Array.Empty<GroupSummary>();
}

/// <summary>What the group screen (<c>/admin/groups/{name}</c>) shows: the group, its members and the member search.</summary>
public sealed class GroupDetailViewModel : AccessViewModel
{
    /// <summary>The group name (route).</summary>
    public string? Name { get; set; }

    /// <summary>User-name search for the member picker (contains).</summary>
    public string? Q { get; set; }

    /// <summary>The group.</summary>
    public GroupDetails? Group { get; internal set; }

    /// <summary>The users found by <see cref="Q"/>, not yet members.</summary>
    public IReadOnlyList<GroupMemberInfo> Found { get; internal set; } = Array.Empty<GroupMemberInfo>();
}

/// <summary>What the permission targets screen (<c>/admin/permissions</c>) shows: the targets and the new-target form.</summary>
public sealed class PermissionsViewModel : AccessViewModel
{
    /// <summary>The new target name.</summary>
    public string? Name { get; set; }

    /// <summary>Repository names or <c>*</c> globs, one per line.</summary>
    public string? RepositoryPatterns { get; set; }

    /// <summary>Package ID globs the target applies to, one per line; none means all.</summary>
    public string? IncludePatterns { get; set; }

    /// <summary>Package ID globs the target leaves out, one per line.</summary>
    public string? ExcludePatterns { get; set; }

    /// <summary>The project that administers the target, if any.</summary>
    public string? Project { get; set; }

    /// <summary>A package ID to test the patterns with.</summary>
    public string? TestPackage { get; set; }

    /// <summary>The targets.</summary>
    public IReadOnlyList<TargetSummary> Targets { get; internal set; } = Array.Empty<TargetSummary>();

    /// <summary>The projects a target can belong to.</summary>
    public IReadOnlyList<ProjectSummary> Projects { get; internal set; } = Array.Empty<ProjectSummary>();

    /// <summary>The preview of the form patterns.</summary>
    public PatternPreview? Preview { get; internal set; }

    /// <summary>The model of the pattern fields partial.</summary>
    public PatternFieldsView Fields => new(RepositoryPatterns, IncludePatterns, ExcludePatterns, TestPackage, Preview);
}

/// <summary>What the permission target screen (<c>/admin/permissions/{name}</c>) shows: the patterns, the preview and the grants.</summary>
public sealed class PermissionDetailViewModel : AccessViewModel
{
    /// <summary>The target name (route).</summary>
    public string? Name { get; set; }

    /// <summary>Repository names or globs, one per line.</summary>
    public string? RepositoryPatterns { get; set; }

    /// <summary>Package ID globs the target applies to, one per line.</summary>
    public string? IncludePatterns { get; set; }

    /// <summary>Package ID globs the target leaves out, one per line.</summary>
    public string? ExcludePatterns { get; set; }

    /// <summary>A package ID to test the patterns with.</summary>
    public string? TestPackage { get; set; }

    /// <summary>The kind of principal in the grant form.</summary>
    public PrincipalType PrincipalType { get; set; } = PrincipalType.Group;

    /// <summary>The user, group or organization name in the grant form.</summary>
    public string? PrincipalName { get; set; }

    /// <summary>The target.</summary>
    public TargetDetails? Target { get; internal set; }

    /// <summary>What the patterns select.</summary>
    public PatternPreview? Preview { get; internal set; }

    /// <summary>The actions ticked in the grant form.</summary>
    public AccessAction FormActions { get; internal set; }

    /// <summary>The model of the pattern fields partial.</summary>
    public PatternFieldsView Fields => new(RepositoryPatterns, IncludePatterns, ExcludePatterns, TestPackage, Preview);
}

/// <summary>What the projects screen (<c>/admin/projects</c>) shows: the projects the user may open and the new-project form.</summary>
public sealed class ProjectsViewModel : AccessViewModel
{
    /// <summary>The new project name.</summary>
    public string? Name { get; set; }

    /// <summary>The new project display name.</summary>
    public string? DisplayName { get; set; }

    /// <summary>The projects the signed-in user may open.</summary>
    public IReadOnlyList<ProjectSummary> Projects { get; internal set; } = Array.Empty<ProjectSummary>();
}

/// <summary>What the project screen (<c>/admin/projects/{name}</c>) shows: repositories, members and the project targets.</summary>
public sealed class ProjectDetailViewModel : AccessViewModel
{
    /// <summary>The project name (route).</summary>
    public string? Name { get; set; }

    /// <summary>The project.</summary>
    public ProjectDetails? Project { get; internal set; }

    /// <summary>The NuGet repositories not yet in this project, for the assignment picker.</summary>
    public IReadOnlyList<string> Assignable { get; internal set; } = Array.Empty<string>();

    /// <summary>The new target name.</summary>
    public string? TargetName { get; set; }

    /// <summary>Repository globs of the new target, one per line (they only ever reach the project repositories).</summary>
    public string? RepositoryPatterns { get; set; }

    /// <summary>Include globs of the new target.</summary>
    public string? IncludePatterns { get; set; }

    /// <summary>Exclude globs of the new target.</summary>
    public string? ExcludePatterns { get; set; }

    /// <summary>A package ID to test the patterns with.</summary>
    public string? TestPackage { get; set; }

    /// <summary>What the new target patterns select.</summary>
    public PatternPreview? Preview { get; internal set; }

    /// <summary>The model of the pattern fields partial.</summary>
    public PatternFieldsView Fields => new(RepositoryPatterns, IncludePatterns, ExcludePatterns, TestPackage, Preview);
}

/// <summary>What the access audit screen (<c>/admin/audit</c>) shows: the filter and the matching entries.</summary>
public sealed class AuditViewModel
{
    /// <summary>The user name that made the change.</summary>
    public string? ActorFilter { get; set; }

    /// <summary>Part of what the change was done to (a group, target, project or repository name).</summary>
    public string? Subject { get; set; }

    /// <summary>First day, <c>yyyy-MM-dd</c>.</summary>
    public string? From { get; set; }

    /// <summary>Last day, <c>yyyy-MM-dd</c>.</summary>
    public string? To { get; set; }

    /// <summary>The matching entries.</summary>
    public IReadOnlyList<AccessAuditEntry> Entries { get; internal set; } = Array.Empty<AccessAuditEntry>();

    /// <summary>Why the filter was not applied, if a date could not be read.</summary>
    public string? FilterError { get; internal set; }
}
