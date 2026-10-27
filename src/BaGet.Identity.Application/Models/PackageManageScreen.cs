using System;
using System.Collections.Generic;
using System.Linq;
using BaGet.Core;

namespace BaGet.Identity.Application.Models;

/// <summary>An owner of a package ID.</summary>
/// <param name="OwnerName">The user or organization name.</param>
/// <param name="IsVerified">Whether the ID is reserved for this owner.</param>
public sealed record PackageManageOwner(string? OwnerName, bool IsVerified);

/// <summary>A version's deprecation.</summary>
/// <param name="Reasons">Why (<c>Legacy</c>, <c>CriticalBugs</c>, <c>Other</c>).</param>
/// <param name="Message">Free text, or null.</param>
/// <param name="AlternatePackageId">The package to use instead, or null.</param>
/// <param name="AlternateVersionRange">The versions of it that may be used, or null.</param>
public sealed record PackageManageDeprecation(IReadOnlyList<string> Reasons, string? Message, string? AlternatePackageId, string? AlternateVersionRange);

/// <summary>A vulnerability recorded for a version.</summary>
/// <param name="Key">The row's key (what <c>RemoveVulnerability</c> takes).</param>
/// <param name="AdvisoryUrl">The advisory.</param>
/// <param name="Severity">0 low, 1 moderate, 2 high, 3 critical.</param>
/// <param name="VersionRange">The affected range, or null.</param>
/// <param name="Source">Null for a manual entry, <c>osv</c> for one the scanner wrote (not editable here).</param>
public sealed record PackageManageVulnerability(int Key, string? AdvisoryUrl, int Severity, string? VersionRange, string? Source);

/// <summary>One version of the ID.</summary>
/// <param name="Version">The version as published (original text).</param>
/// <param name="NormalizedVersion">The normalized version, which the forms post back.</param>
/// <param name="Downloads">The download count.</param>
/// <param name="Listed">Whether the version is listed.</param>
/// <param name="Deprecation">Its deprecation, or null.</param>
/// <param name="Vulnerabilities">Its vulnerabilities.</param>
public sealed record PackageManageVersion(
    string Version,
    string? NormalizedVersion,
    long Downloads,
    bool Listed,
    PackageManageDeprecation? Deprecation,
    IReadOnlyList<PackageManageVulnerability> Vulnerabilities);

/// <summary>An indexed PDB of a version.</summary>
/// <param name="FileName">The PDB file name.</param>
/// <param name="Key">Its signature key.</param>
public sealed record PackageManageSymbol(string FileName, string Key);

/// <summary>An open abuse report.</summary>
/// <param name="Key">The report's key (what <c>ResolveReport</c> takes).</param>
/// <param name="PackageVersion">The reported version.</param>
/// <param name="Reason">The reason given.</param>
/// <param name="Details">The details given.</param>
/// <param name="ReporterEmail">Where to reply, if the reporter gave an address.</param>
public sealed record PackageManageReport(int Key, string? PackageVersion, string? Reason, string? Details, string? ReporterEmail);

/// <summary>
/// What the package management screen (<c>/packages/{id}/manage</c>) shows, as plain data (the JSON model of <c>/api/ui/packages/{id}/manage</c>).
/// The page's own <c>ManageViewModel</c> is built from it for the view.
/// </summary>
public sealed class PackageManageScreen
{
    /// <summary>The package ID from the address.</summary>
    public string? Id { get; set; }

    /// <summary>The ID's owners.</summary>
    public IReadOnlyList<PackageManageOwner> Owners { get; set; } = Array.Empty<PackageManageOwner>();

    /// <summary>The ID's versions, newest first, unlisted ones included.</summary>
    public IReadOnlyList<PackageManageVersion> Versions { get; set; } = Array.Empty<PackageManageVersion>();

    /// <summary>The open abuse reports against the ID.</summary>
    public IReadOnlyList<PackageManageReport> Reports { get; set; } = Array.Empty<PackageManageReport>();

    /// <summary>The outcome of the last action.</summary>
    public string? Message { get; set; }

    /// <summary>Whether validation has submissions for the ID.</summary>
    public bool HasValidation { get; set; }

    /// <summary>Whether the ID is verified (reserved).</summary>
    public bool IsVerified => Owners.Any(o => o.IsVerified);

    /// <summary>The PDBs indexed from each version's symbol package, by normalized version.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<PackageManageSymbol>> Symbols { get; set; }
        = new Dictionary<string, IReadOnlyList<PackageManageSymbol>>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Whether the database records symbols per package version.</summary>
    public bool SymbolsSupported { get; set; }

    /// <summary>The severity names a vulnerability can have, by their protocol number.</summary>
    public IReadOnlyList<string> SeverityNames { get; set; } = Array.Empty<string>();

    /// <summary>Whether the caller is an administrator.</summary>
    public bool IsAdministrator { get; set; }

    /// <summary>Whether the caller manages this ID only as an administrator (not as an owner).</summary>
    public bool ActingAsAdministrator { get; set; }

    /// <summary>Whether the caller has a password to confirm with.</summary>
    public bool HasPassword { get; set; } = true;

    /// <summary>For an account without a password: it signed in again with its provider a moment ago.</summary>
    public bool Reauthenticated { get; set; }

    /// <summary>Whether an external provider is available to sign in again with.</summary>
    public bool CanReauthenticate { get; set; }

    /// <summary>What a deletion does on this server.</summary>
    public PackageDeletionBehavior DeletionBehavior { get; set; }

    /// <summary>Whether the server refuses writes.</summary>
    public bool IsReadOnlyMode { get; set; }
}

/// <summary>The form of a handler that names an owner.</summary>
public sealed class PackageManageOwnerForm
{
    /// <summary>The user or organization.</summary>
    public string? OwnerName { get; set; }
}

/// <summary>The form of a handler that names a version.</summary>
public sealed class PackageManageVersionForm
{
    /// <summary>The (normalized) version.</summary>
    public string? Version { get; set; }
}

/// <summary>The form of a permanent deletion of a version (administrators; the password confirms).</summary>
public sealed class PackageManageHardDeleteForm
{
    /// <summary>The version.</summary>
    public string? Version { get; set; }

    /// <summary>The caller's password.</summary>
    public string? Password { get; set; }
}

/// <summary>The form of a handler that only asks for the password.</summary>
public sealed class PackageManagePasswordForm
{
    /// <summary>The caller's password.</summary>
    public string? Password { get; set; }
}

/// <summary>The form of <c>Deprecate</c>.</summary>
public sealed class PackageManageDeprecateForm
{
    /// <summary>The version.</summary>
    public string? Version { get; set; }

    /// <summary>The reasons (at least one of <c>Legacy</c>, <c>CriticalBugs</c>, <c>Other</c>).</summary>
    public List<string>? Reasons { get; set; }

    /// <summary>Free text, optional.</summary>
    public string? Message { get; set; }

    /// <summary>The package to use instead, optional.</summary>
    public string? AlternatePackageId { get; set; }

    /// <summary>The versions of it that may be used, optional (needs <see cref="AlternatePackageId"/>).</summary>
    public string? AlternateVersionRange { get; set; }
}

/// <summary>The form of <c>AddVulnerability</c>.</summary>
public sealed class PackageManageVulnerabilityForm
{
    /// <summary>The version.</summary>
    public string? Version { get; set; }

    /// <summary>The advisory URL.</summary>
    public string? AdvisoryUrl { get; set; }

    /// <summary>0 low, 1 moderate, 2 high, 3 critical.</summary>
    public int Severity { get; set; }

    /// <summary>The affected range, optional.</summary>
    public string? VersionRange { get; set; }
}

/// <summary>The form of <c>RemoveVulnerability</c>.</summary>
public sealed class PackageManageRemoveVulnerabilityForm
{
    /// <summary>The version.</summary>
    public string? Version { get; set; }

    /// <summary>The vulnerability's key.</summary>
    public int Key { get; set; }
}

/// <summary>The form of <c>SetVerified</c>.</summary>
public sealed class PackageManageVerifiedForm
{
    /// <summary>Whether the ID is verified.</summary>
    public bool Verified { get; set; }
}

/// <summary>The form of <c>ResolveReport</c>.</summary>
public sealed class PackageManageReportForm
{
    /// <summary>The report's key.</summary>
    public int Key { get; set; }
}
