using System.Text.Json.Serialization;
using BaGet.Core;

namespace BaGet.Identity.Application.Models;

// Enum members read their names (like the answers write them): a body says "Group", not 2.
// The bodies of the access-control screens' writes (groups, permission targets, projects). A page posts them as form fields (the view controllers bind the
// same names and build these), the JSON API takes them as the request body; the use cases never see how they arrived.

/// <summary>A new group.</summary>
/// <param name="Name">The group's name.</param>
/// <param name="Description">What the group is for, or null.</param>
public sealed record GroupCreation(string? Name, string? Description);

/// <summary>Renaming a group.</summary>
/// <param name="NewName">The new name.</param>
public sealed record GroupRename(string? NewName);

/// <summary>Adding a user to a group or removing them.</summary>
/// <param name="UserId">The user's id (<see cref="GroupMemberInfo.UserId"/>).</param>
/// <param name="Password">The administrator's password, asked again.</param>
public sealed record GroupMemberChange(string? UserId, string? Password);

/// <summary>The fields of the permission-target form (new target, or the patterns of an existing one) and its test box.</summary>
/// <param name="Name">The new target's name (ignored for an existing target: the route names it).</param>
/// <param name="RepositoryPatterns">Repository names or <c>*</c> globs, one per line.</param>
/// <param name="IncludePatterns">Package ID globs the target applies to, one per line; none means all.</param>
/// <param name="ExcludePatterns">Package ID globs the target leaves out, one per line.</param>
/// <param name="Project">For a new target: the project that administers it, or null.</param>
/// <param name="TestPackage">A package ID to test the patterns with.</param>
/// <param name="PrincipalType">For an existing target: the principal kind in the grant form (kept across a preview).</param>
/// <param name="PrincipalName">For an existing target: the principal name in the grant form (kept across a preview).</param>
/// <param name="Password">The administrator's password (saving the patterns of an existing target).</param>
public sealed record PermissionTargetForm(
    string? Name = null,
    string? RepositoryPatterns = null,
    string? IncludePatterns = null,
    string? ExcludePatterns = null,
    string? Project = null,
    string? TestPackage = null,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] PrincipalType? PrincipalType = null,
    string? PrincipalName = null,
    string? Password = null);

/// <summary>Giving a principal the ticked actions through a permission target, replacing an earlier grant to it.</summary>
/// <param name="PrincipalType">User, group or organization.</param>
/// <param name="PrincipalName">The principal's name.</param>
/// <param name="Actions">The ticked <see cref="AccessAction"/> names (<c>Read</c>, <c>Annotate</c>, <c>Deploy</c>, <c>Delete</c>, <c>Manage</c>); none removes the grant.</param>
/// <param name="Password">The administrator's password, asked again.</param>
public sealed record GrantForm([property: JsonConverter(typeof(JsonStringEnumConverter))] PrincipalType? PrincipalType, string? PrincipalName, string[]? Actions, string? Password);

/// <summary>Removing a principal's grant or project role.</summary>
/// <param name="Type">User, group or organization.</param>
/// <param name="PrincipalId">The principal's id.</param>
/// <param name="Password">The administrator's password, asked again.</param>
public sealed record PrincipalRemoval([property: JsonConverter(typeof(JsonStringEnumConverter))] PrincipalType Type, string? PrincipalId, string? Password);

/// <summary>A new project.</summary>
/// <param name="Name">The project's name.</param>
/// <param name="DisplayName">A name to show, or null.</param>
public sealed record ProjectCreation(string? Name, string? DisplayName);

/// <summary>Giving a user or group a role in a project.</summary>
/// <param name="Type">User or group.</param>
/// <param name="PrincipalName">The principal's name.</param>
/// <param name="Role">The role.</param>
/// <param name="Password">The administrator's password, asked again.</param>
public sealed record ProjectMemberForm([property: JsonConverter(typeof(JsonStringEnumConverter))] PrincipalType Type, string? PrincipalName, [property: JsonConverter(typeof(JsonStringEnumConverter))] ProjectRole Role, string? Password);

/// <summary>Putting a repository in a project or taking it out.</summary>
/// <param name="Repository">The repository's name.</param>
/// <param name="Password">The administrator's password, asked again.</param>
public sealed record ProjectRepositoryForm(string? Repository, string? Password);

/// <summary>The fields of a project's new permission target and its test box.</summary>
/// <param name="TargetName">The new target's name.</param>
/// <param name="RepositoryPatterns">Repository globs, one per line (they only ever reach the project's repositories).</param>
/// <param name="IncludePatterns">Package ID globs the target applies to, one per line.</param>
/// <param name="ExcludePatterns">Package ID globs the target leaves out, one per line.</param>
/// <param name="TestPackage">A package ID to test the patterns with.</param>
public sealed record ProjectTargetForm(string? TargetName, string? RepositoryPatterns, string? IncludePatterns, string? ExcludePatterns, string? TestPackage);

/// <summary>The audit log's filter.</summary>
/// <param name="Actor">The user name that made the change.</param>
/// <param name="Subject">Part of what the change was done to.</param>
/// <param name="From">First day, <c>yyyy-MM-dd</c>.</param>
/// <param name="To">Last day, <c>yyyy-MM-dd</c>.</param>
public sealed record AuditQuery(string? Actor, string? Subject, string? From, string? To);
