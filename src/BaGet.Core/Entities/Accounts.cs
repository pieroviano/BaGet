using System;
using Microsoft.AspNetCore.Identity;

namespace BaGet.Core;

/// <summary>
/// A signed in user. User names and organization names share one namespace, since
/// either can own a package.
/// </summary>
public class BaGetUser : IdentityUser
{
    public DateTime Created { get; set; }

    /// <summary>
    /// May change the server's settings and promote or demote administrators. It grants
    /// nothing over packages: ownership rules apply to an administrator as to anyone.
    /// </summary>
    public bool IsAdministrator { get; set; }
}

/// <summary>
/// An account that several users own packages through.
/// </summary>
public class Organization
{
    public int Key { get; set; }

    public string Name { get; set; }

    public DateTime Created { get; set; }
}

public class OrganizationMember
{
    public int Key { get; set; }

    /// <summary>
    /// Nullable so that the relationship is additive; see <see cref="PackageDeprecation"/>.
    /// </summary>
    public int? OrganizationKey { get; set; }

    public Organization Organization { get; set; }

    /// <summary>
    /// The member's <see cref="IdentityUser.Id"/>.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// An administrator can add and remove members.
    /// </summary>
    public bool IsAdmin { get; set; }
}

/// <summary>
/// An owner of every version of a package ID.
/// </summary>
/// <remarks>
/// A package ID with no owners predates accounts, and only the server's global API key
/// may change it.
/// </remarks>
public class PackageOwner
{
    public int Key { get; set; }

    public string PackageId { get; set; }

    /// <summary>
    /// A user name or an organization name.
    /// </summary>
    public string OwnerName { get; set; }

    /// <summary>
    /// Set by the server's administrator to mark the ID as reserved for this owner,
    /// which search reports as "verified".
    /// </summary>
    public bool IsVerified { get; set; }
}

/// <summary>
/// A user's API key. Only a hash of the key is kept.
/// </summary>
public class ApiKey
{
    public int Key { get; set; }

    public string UserId { get; set; }

    public string Description { get; set; }

    /// <summary>
    /// The key's SHA-256 hash, as lowercase hex.
    /// </summary>
    public string HashedKey { get; set; }

    /// <summary>
    /// What the key may do; see <see cref="ApiKeyScopes"/>.
    /// </summary>
    public string[] Scopes { get; set; }

    /// <summary>
    /// The package IDs the key applies to, with "*" as a wildcard.
    /// </summary>
    public string PackageGlobPattern { get; set; }

    public DateTime Created { get; set; }

    public DateTime? Expires { get; set; }
}

/// <summary>
/// The scopes an <see cref="ApiKey"/> may hold, named as nuget.org names them.
/// </summary>
public static class ApiKeyScopes
{
    /// <summary>
    /// Push new package IDs and new versions of owned ones.
    /// </summary>
    public const string Push = "package:push";

    /// <summary>
    /// Push new versions of owned package IDs only.
    /// </summary>
    public const string PushVersion = "package:pushversion";

    /// <summary>
    /// Unlist and relist owned versions, and edit their deprecation and vulnerabilities.
    /// </summary>
    public const string Unlist = "package:unlist";

    public static readonly string[] All = { Push, PushVersion, Unlist };
}