using System;
using System.Collections.Generic;
using System.Linq;
using BaGet.Core;

namespace BaGet.Identity.Application.Models;

/// <summary>The signed-in user's organizations screen.</summary>
public sealed class OrganizationsViewModel
{
    /// <summary>The organizations the user belongs to.</summary>
    public IReadOnlyList<AccountOrganization> Organizations { get; set; } = Array.Empty<AccountOrganization>();

    /// <summary>The answer to the last action.</summary>
    public string? Message { get; set; }
}

/// <summary>One organization with its members.</summary>
public class AccountOrganization
{
    /// <summary>The organization's name.</summary>
    public string? Name { get; set; }

    /// <summary>Whether the current user administers it.</summary>
    public bool IsAdmin { get; set; }

    /// <summary>The members, by user name.</summary>
    public IReadOnlyList<AccountOrganizationMember>? Members { get; set; }
}

/// <summary>One member of an organization (a flat record, so the screen's JSON names its fields).</summary>
/// <param name="UserName">The member's user name.</param>
/// <param name="IsAdmin">Whether the member administers the organization.</param>
public sealed record AccountOrganizationMember(string UserName, bool IsAdmin);

/// <summary>The "My packages" screen: the packages the user owns in each repository.</summary>
public sealed class AccountPackagesViewModel
{
    /// <summary>The owned packages of each repository that has any, <c>default</c> first.</summary>
    public IReadOnlyList<PackagesModel.RepositoryPackages> Groups { get; set; } = Array.Empty<PackagesModel.RepositoryPackages>();

    /// <summary>Whether the user owns nothing in any repository.</summary>
    public bool IsEmpty => Groups.Count == 0;

    /// <summary>Whether the list needs repository headings: more than the default repository holds packages of the user.</summary>
    public bool ShowRepositoryNames => Groups.Any(g => !g.Repository.IsDefault);
}

/// <summary>The "My access" screen: the signed-in user's own effective permissions.</summary>
public sealed class AccountAccessViewModel
{
    /// <summary>A package ID to evaluate the grants for; empty means the repository as a whole.</summary>
    public string? Package { get; set; }

    /// <summary>The repositories where the user holds some action.</summary>
    public IReadOnlyList<RepositoryAccessRow> Rows { get; set; } = Array.Empty<RepositoryAccessRow>();

    /// <summary>The projects the user administers.</summary>
    public IReadOnlyList<string> AdministeredProjects { get; set; } = Array.Empty<string>();

    /// <summary>Whether the configured database has an access model at all.</summary>
    public bool ModelAvailable { get; set; } = true;
}

/// <summary>A user's or an organization's public page: the packages it owns.</summary>
public sealed class ProfileViewModel
{
    /// <summary>The account's name.</summary>
    public string? Name { get; set; }

    /// <summary>Whether the account is an organization.</summary>
    public bool IsOrganization { get; set; }

    /// <summary>The owned packages with their downloads.</summary>
    public IReadOnlyList<PackageDownloads> Packages { get; set; } = Array.Empty<PackageDownloads>();

    /// <summary>The members of an organization.</summary>
    public IReadOnlyList<string?> Members { get; set; } = Array.Empty<string>();

    /// <summary>Whether the profile is the signed-in user's own.</summary>
    public bool IsCurrentUser { get; set; }

    /// <summary>The account's avatar address, or null when Gravatar is off.</summary>
    public string? AvatarUrl { get; set; }
}
