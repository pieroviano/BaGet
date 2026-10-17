using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.RazorPages;
using NuGet.Versioning;

namespace BaGet.Web;

public class PackageModel : PageModel
{
    private readonly IPackageService _packages;
    private readonly IPackageContentService _content;
    private readonly ISearchService _search;
    private readonly IUrlGenerator _url;

    public PackageModel(
        IPackageService packages,
        IPackageContentService content,
        ISearchService search,
        IUrlGenerator url)
    {
        _packages = packages ?? throw new ArgumentNullException(nameof(packages));
        _content = content ?? throw new ArgumentNullException(nameof(content));
        _search = search ?? throw new ArgumentNullException(nameof(search));
        _url = url ?? throw new ArgumentNullException(nameof(url));
    }

    public bool Found { get; private set; }

    public Package Package { get; private set; }

    public bool IsDotnetTemplate { get; private set; }
    public bool IsDotnetTool { get; private set; }
    public DateTime LastUpdated { get; private set; }
    public long TotalDownloads { get; private set; }

    public IReadOnlyList<PackageDependent> UsedBy { get; set; }
    public IReadOnlyList<DependencyGroupModel> DependencyGroups { get; private set; }
    public IReadOnlyList<VersionModel> Versions { get; private set; }

    public HtmlString Readme { get; private set; }

    public string IconUrl { get; private set; }
    public string LicenseUrl { get; private set; }
    public string PackageDownloadUrl { get; private set; }

    public async Task OnGetAsync(string id, string version, CancellationToken cancellationToken)
    {
        var packages = await _packages.FindPackagesAsync(id, cancellationToken);
        var listedPackages = packages.Where(p => p.Listed).ToList();

        // Try to find the requested version.
        if (NuGetVersion.TryParse(version, out var requestedVersion))
        {
            Package = packages.SingleOrDefault(p => p.Version == requestedVersion);
        }

        // Otherwise try to display the latest version.
        if (Package == null)
        {
            Package = listedPackages.OrderByDescending(p => p.Version).FirstOrDefault();
        }

        if (Package == null)
        {
            Package = new Package { Id = id };
            Found = false;
            return;
        }

        var packageVersion = Package.Version;

        Found = true;
        IsDotnetTemplate = Package.PackageTypes.Any(t => t.Name.Equals("Template", StringComparison.OrdinalIgnoreCase));
        IsDotnetTool = Package.PackageTypes.Any(t => t.Name.Equals("DotnetTool", StringComparison.OrdinalIgnoreCase));
        LastUpdated = packages.Max(p => p.Published);
        TotalDownloads = packages.Sum(p => p.Downloads);

        var dependents = await _search.FindDependentsAsync(Package.Id, cancellationToken);

        UsedBy = dependents.Data;
        DependencyGroups = PackageDependencies.GroupByFramework(Package.Dependencies);
        Versions = ToVersions(listedPackages, packageVersion);

        if (Package.HasReadme)
        {
            Readme = await GetReadmeHtmlStringOrNullAsync(Package.Id, packageVersion, cancellationToken);
        }

        IconUrl = Package.HasEmbeddedIcon
            ? _url.GetPackageIconDownloadUrl(Package.Id, packageVersion)
            : Package.IconUrlString;
        LicenseUrl = Package.LicenseUrlString;
        PackageDownloadUrl = _url.GetPackageDownloadUrl(Package.Id, packageVersion);
    }

    private IReadOnlyList<VersionModel> ToVersions(IReadOnlyList<Package> packages, NuGetVersion selectedVersion)
    {
        return packages
            .Select(p => new VersionModel
            {
                Version = p.Version,
                Downloads = p.Downloads,
                Selected = p.Version == selectedVersion,
                LastUpdated = p.Published,
            })
            .OrderByDescending(m => m.Version)
            .ToList();
    }

    private async Task<HtmlString> GetReadmeHtmlStringOrNullAsync(
        string packageId,
        NuGetVersion packageVersion,
        CancellationToken cancellationToken)
    {
        string readme;
        using (var readmeStream = await _content.GetPackageReadmeStreamOrNullAsync(packageId, packageVersion, cancellationToken))
        {
            if (readmeStream == null) return null;

            using (var reader = new StreamReader(readmeStream))
            {
                readme = await reader.ReadToEndAsync();
            }
        }

        return ReadmeMarkdown.ToHtml(readme);
    }

    public record VersionModel
    {
        public NuGetVersion Version { get; init; }
        public long Downloads { get; init; }
        public bool Selected { get; init; }
        public DateTime LastUpdated { get; init; }
    }
}