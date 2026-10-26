using System;
using System.Collections.Generic;
using BaGet.Core;

namespace BaGet.Web;

/// <summary>What the package page shows about the version it displays (a flat copy of the stored package, with no navigation properties).</summary>
public sealed record PackageDetails
{
    /// <summary>The package ID.</summary>
    public string? Id { get; init; }

    /// <summary>The normalized version.</summary>
    public string? Version { get; init; }

    /// <summary>The description.</summary>
    public string? Description { get; init; }

    /// <summary>The release notes.</summary>
    public string? ReleaseNotes { get; init; }

    /// <summary>The project website, or an empty string.</summary>
    public string? ProjectUrl { get; init; }

    /// <summary>The source repository, or an empty string.</summary>
    public string? RepositoryUrl { get; init; }

    /// <summary>The downloads of this version.</summary>
    public long Downloads { get; init; }

    /// <summary>The authors.</summary>
    public IReadOnlyList<string> Authors { get; init; } = Array.Empty<string>();
}

/// <summary>One row of the versions table of the package page.</summary>
/// <param name="Version">The normalized version.</param>
/// <param name="Downloads">Its downloads.</param>
/// <param name="Selected">Whether it is the version shown.</param>
/// <param name="LastUpdated">When it was published.</param>
public sealed record PackageVersionEntry(string Version, long Downloads, bool Selected, DateTime LastUpdated);

/// <summary>What the package page shows.</summary>
public sealed record PackageScreen
{
    /// <summary>False when the package (or any listed version of it) does not exist; <see cref="Package"/> then only names the ID.</summary>
    public bool Found { get; init; }

    /// <summary>The version shown.</summary>
    public PackageDetails Package { get; init; } = new();

    /// <summary>The package is a dotnet template.</summary>
    public bool IsDotnetTemplate { get; init; }

    /// <summary>The package is a dotnet tool.</summary>
    public bool IsDotnetTool { get; init; }

    /// <summary>When any version was last published.</summary>
    public DateTime LastUpdated { get; init; }

    /// <summary>Downloads of every version.</summary>
    public long TotalDownloads { get; init; }

    /// <summary>The packages that depend on this one.</summary>
    public IReadOnlyList<PackageDependent> UsedBy { get; init; } = Array.Empty<PackageDependent>();

    /// <summary>The dependencies, by framework.</summary>
    public IReadOnlyList<DependencyGroupModel> DependencyGroups { get; init; } = Array.Empty<DependencyGroupModel>();

    /// <summary>The listed versions, newest first.</summary>
    public IReadOnlyList<PackageVersionEntry> Versions { get; init; } = Array.Empty<PackageVersionEntry>();

    /// <summary>The readme as safe HTML (<see cref="ReadmeMarkdown"/>), or null.</summary>
    public string? ReadmeHtml { get; init; }

    /// <summary>The frameworks the package supports, by family.</summary>
    public IReadOnlyList<FrameworkGroup> FrameworkGroups { get; init; } = Array.Empty<FrameworkGroup>();

    /// <summary>The install commands, one tab per tool.</summary>
    public IReadOnlyList<InstallSnippet> InstallTabs { get; init; } = Array.Empty<InstallSnippet>();

    /// <summary>Owner names; empty unless accounts are enabled and the package has owners.</summary>
    public IReadOnlyList<string?> Owners { get; init; } = Array.Empty<string>();

    /// <summary>The avatar address of each owner by name; empty when Gravatar is off or accounts are disabled.</summary>
    public IReadOnlyDictionary<string, string> OwnerAvatars { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// Where the management page is, when the caller may open it (an owner holding the Manage permission, or an administrator), else null.
    /// The use case answers an application-relative address (<c>~/packages/…</c>); the controller resolves it against the path base.
    /// </summary>
    public string? ManageUrl { get; init; }

    /// <summary>The icon address.</summary>
    public string? IconUrl { get; init; }

    /// <summary>The license address, null for a license expression or file.</summary>
    public string? LicenseUrl { get; init; }

    /// <summary>Where the package file is downloaded.</summary>
    public string? PackageDownloadUrl { get; init; }

    /// <summary>
    /// This version on nuget.info, the web app of NuGet Package Explorer, as nuget.org links it. nuget.info always downloads from
    /// nuget.org, so the link only works for a package that is also published there.
    /// </summary>
    public string? PackageExplorerUrl { get; init; }
}
