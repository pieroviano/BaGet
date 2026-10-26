using System;
using System.Collections.Generic;
using BaGet.Core;

namespace BaGet.Identity.ViewModels;

/// <summary>The account an administrator looks at, without anything that signs in as it (no password hash, security stamp or tokens).</summary>
/// <param name="UserName">The user name.</param>
/// <param name="Email">The email address, or null.</param>
/// <param name="Created">When the account was created.</param>
/// <param name="IsAdministrator">Whether it is a server administrator.</param>
/// <param name="PendingApproval">Whether it waits for an administrator to enable it.</param>
public sealed record UserAccountInfo(string? UserName, string? Email, DateTime Created, bool IsAdministrator, bool PendingApproval);

/// <summary>One of an account's API keys as the administrator sees it; the key itself and its hash are never shown.</summary>
/// <param name="Key">The key's number (what revoking names).</param>
/// <param name="Description">The description the owner gave it.</param>
/// <param name="Scopes">What it may do.</param>
/// <param name="PackageGlobPattern">The package IDs it applies to.</param>
/// <param name="Created">When it was created.</param>
/// <param name="Expires">When it expires, or null.</param>
/// <param name="Repositories">Where it may be used: the repositories it is limited to, or "all repositories".</param>
public sealed record UserKeyRow(int Key, string? Description, IReadOnlyList<string> Scopes, string? PackageGlobPattern, DateTime Created, DateTime? Expires, string Repositories);

/// <summary>What the user detail screen (<c>/admin/users/{name}</c>) shows: the account, its keys, organizations and access.</summary>
public sealed class UserDetailViewModel : ConfirmationModel, IFlashModel
{
    /// <summary>A package ID to evaluate the effective permissions for; empty means each repository as a whole.</summary>
    public string? Package { get; set; }

    /// <summary>The user name (route).</summary>
    public string? Name { get; set; }

    /// <summary>Whether the configured database has an access model (else the access sections are hidden).</summary>
    public bool ModelAvailable { get; internal set; }

    /// <summary>The names of the groups the account belongs to (the implicit built-ins excluded).</summary>
    public IReadOnlyList<string> Groups { get; internal set; } = Array.Empty<string>();

    /// <summary>The roles the account holds in projects, directly or through a group.</summary>
    public IReadOnlyList<UserProjectRole> ProjectRoles { get; internal set; } = Array.Empty<UserProjectRole>();

    /// <summary>What the account may do in each NuGet repository, and why.</summary>
    public IReadOnlyList<RepositoryAccessRow> Access { get; internal set; } = Array.Empty<RepositoryAccessRow>();

    /// <summary>The account.</summary>
    public UserAccountInfo? Account { get; internal set; }

    /// <summary>The account's API keys; the key and its hash are never shown.</summary>
    public IReadOnlyList<UserKeyRow> Keys { get; internal set; } = Array.Empty<UserKeyRow>();

    /// <summary>The organizations the account belongs to.</summary>
    public IReadOnlyList<Organization> Organizations { get; internal set; } = Array.Empty<Organization>();

    /// <summary>How many package IDs the account owns, in every repository.</summary>
    public int OwnedPackageIds { get; internal set; }

    /// <inheritdoc />
    public string? Message { get; set; }

    /// <inheritdoc />
    public string? Error { get; set; }
}
