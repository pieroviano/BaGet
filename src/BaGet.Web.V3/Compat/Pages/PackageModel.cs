using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.RazorPages;
using NuGet.Versioning;

namespace BaGet.Web;

/// <summary>The page model of the package page when it was a Razor Page.</summary>
/// <remarks>
/// Kept as a shim so that code compiled against it still loads and runs (no breaking change of the public API); there is no page
/// behind it any more: <see cref="PackageController"/> serves it. Both share <see cref="PackageWorkflow"/>.
/// </remarks>
[Obsolete("The package page is served by PackageController; this page model has no page any more and will be removed in the next major version.")]
public class PackageModel : PageModel
{
    private readonly PackageWorkflow _workflow;

    /// <summary>Creates the page model.</summary>
    public PackageModel(
        IPackageService packages,
        IPackageContentService content,
        ISearchService search,
        IUrlGenerator url)
    {
        _workflow = new PackageWorkflow(packages, content, search, url);
    }

    /// <summary>False when the package does not exist.</summary>
    public bool Found { get; private set; }

    /// <summary>The version shown.</summary>
    public Package? Package { get; private set; }

    /// <summary>The package is a dotnet template.</summary>
    public bool IsDotnetTemplate { get; private set; }

    /// <summary>The package is a dotnet tool.</summary>
    public bool IsDotnetTool { get; private set; }

    /// <summary>When any version was last published.</summary>
    public DateTime LastUpdated { get; private set; }

    /// <summary>Downloads of every version.</summary>
    public long TotalDownloads { get; private set; }

    /// <summary>The packages that depend on this one.</summary>
    public IReadOnlyList<PackageDependent>? UsedBy { get; set; }

    /// <summary>The dependencies, by framework.</summary>
    public IReadOnlyList<DependencyGroupModel>? DependencyGroups { get; private set; }

    /// <summary>The listed versions, newest first.</summary>
    public IReadOnlyList<VersionModel>? Versions { get; private set; }

    /// <summary>The readme, rendered safely.</summary>
    public HtmlString? Readme { get; private set; }

    internal IReadOnlyList<FrameworkGroup> FrameworkGroups { get; private set; } = Array.Empty<FrameworkGroup>();
    internal IReadOnlyList<InstallSnippet> InstallTabs { get; private set; } = Array.Empty<InstallSnippet>();

    /// <summary>Owner names; empty unless accounts are enabled and the package has owners.</summary>
    public IReadOnlyList<string?> Owners { get; private set; } = Array.Empty<string>();

    /// <summary>The avatar address of each owner by name; empty when Gravatar is off or accounts are disabled.</summary>
    public IReadOnlyDictionary<string, string> OwnerAvatars { get; private set; } = new Dictionary<string, string>();

    /// <summary>The address of the management page when the signed in user may open it, else null.</summary>
    public string? ManageUrl { get; private set; }

    /// <summary>The icon address.</summary>
    public string? IconUrl { get; private set; }

    /// <summary>The license address, null for a license expression or file.</summary>
    public string? LicenseUrl { get; private set; }

    /// <summary>Where the package file is downloaded.</summary>
    public string? PackageDownloadUrl { get; private set; }

    /// <summary>This version on nuget.info (works only for a package that is also published on nuget.org).</summary>
    public string? PackageExplorerUrl { get; private set; }

    /// <summary>Loads the package.</summary>
    public async Task OnGetAsync(string id, string? version, CancellationToken cancellationToken)
    {
        var model = await _workflow.LoadAsync(id, version, HttpContext, Url, cancellationToken);

        Found = model.Found;
        Package = model.Package;
        IsDotnetTemplate = model.IsDotnetTemplate;
        IsDotnetTool = model.IsDotnetTool;
        LastUpdated = model.LastUpdated;
        TotalDownloads = model.TotalDownloads;
        UsedBy = model.UsedBy;
        DependencyGroups = model.DependencyGroups;
        Versions = model.Versions?
            .Select(v => new VersionModel
            {
                Version = v.Version,
                Downloads = v.Downloads,
                Selected = v.Selected,
                LastUpdated = v.LastUpdated,
            })
            .ToList();
        Readme = model.Readme;
        FrameworkGroups = model.FrameworkGroups;
        InstallTabs = model.InstallTabs;
        Owners = model.Owners;
        OwnerAvatars = model.OwnerAvatars;
        ManageUrl = model.ManageUrl;
        IconUrl = model.IconUrl;
        LicenseUrl = model.LicenseUrl;
        PackageDownloadUrl = model.PackageDownloadUrl;
        PackageExplorerUrl = model.PackageExplorerUrl;
    }

    /// <summary>One row of the versions table.</summary>
    public record VersionModel
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
}
