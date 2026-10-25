using System;
using System.Collections.Generic;
using BaGet.Core;
using Microsoft.AspNetCore.Html;
using NuGet.Versioning;

namespace BaGet.Web;

/// <summary>What the package page shows; filled by <see cref="PackageWorkflow"/>.</summary>
public sealed class PackageViewModel
{
    /// <summary>False when the package (or any listed version of it) does not exist; <see cref="Package"/> then only names the ID.</summary>
    public bool Found { get; internal set; }

    /// <summary>The version shown.</summary>
    public Package? Package { get; internal set; }

    /// <summary>The package is a dotnet template.</summary>
    public bool IsDotnetTemplate { get; internal set; }

    /// <summary>The package is a dotnet tool.</summary>
    public bool IsDotnetTool { get; internal set; }

    /// <summary>When any version was last published.</summary>
    public DateTime LastUpdated { get; internal set; }

    /// <summary>Downloads of every version.</summary>
    public long TotalDownloads { get; internal set; }

    /// <summary>The packages that depend on this one.</summary>
    public IReadOnlyList<PackageDependent>? UsedBy { get; internal set; }

    /// <summary>The dependencies, by framework.</summary>
    public IReadOnlyList<DependencyGroupModel>? DependencyGroups { get; internal set; }

    /// <summary>The listed versions, newest first.</summary>
    public IReadOnlyList<PackageVersionRow>? Versions { get; internal set; }

    /// <summary>The readme, rendered safely.</summary>
    public HtmlString? Readme { get; internal set; }

    internal IReadOnlyList<FrameworkGroup> FrameworkGroups { get; set; } = Array.Empty<FrameworkGroup>();
    internal IReadOnlyList<InstallSnippet> InstallTabs { get; set; } = Array.Empty<InstallSnippet>();

    /// <summary>Owner names; empty unless accounts are enabled and the package has owners.</summary>
    public IReadOnlyList<string?> Owners { get; internal set; } = Array.Empty<string>();

    /// <summary>The avatar address of each owner by name; empty when Gravatar is off or accounts are disabled.</summary>
    public IReadOnlyDictionary<string, string> OwnerAvatars { get; internal set; } = new Dictionary<string, string>();

    /// <summary>
    /// The address of the management page when the signed in user may open it (an owner, directly or through an organization, or an
    /// administrator), else null and the page offers no link.
    /// </summary>
    public string? ManageUrl { get; internal set; }

    /// <summary>The icon address.</summary>
    public string? IconUrl { get; internal set; }

    /// <summary>The license address, null for a license expression or file.</summary>
    public string? LicenseUrl { get; internal set; }

    /// <summary>Where the package file is downloaded.</summary>
    public string? PackageDownloadUrl { get; internal set; }

    /// <summary>
    /// This version on nuget.info, the web app of NuGet Package Explorer, as nuget.org links it. nuget.info always downloads from
    /// nuget.org, so the link only works for a package that is also published there.
    /// </summary>
    public string? PackageExplorerUrl { get; internal set; }
}

/// <summary>One row of the versions table of the package page.</summary>
public sealed record PackageVersionRow
{
    /// <summary>The version.</summary>
    public NuGetVersion? Version { get; init; }

    /// <summary>Its downloads.</summary>
    public long Downloads { get; init; }

    /// <summary>Whether it is the version shown.</summary>
    public bool Selected { get; init; }

    /// <summary>When it was published.</summary>
    public DateTime LastUpdated { get; init; }
}
