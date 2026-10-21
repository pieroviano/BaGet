using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.DependencyInjection;
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

    public Package? Package { get; private set; }

    public bool IsDotnetTemplate { get; private set; }
    public bool IsDotnetTool { get; private set; }
    public DateTime LastUpdated { get; private set; }
    public long TotalDownloads { get; private set; }

    public IReadOnlyList<PackageDependent>? UsedBy { get; set; }
    public IReadOnlyList<DependencyGroupModel>? DependencyGroups { get; private set; }
    public IReadOnlyList<VersionModel>? Versions { get; private set; }

    public HtmlString? Readme { get; private set; }

    internal IReadOnlyList<FrameworkGroup> FrameworkGroups { get; private set; } = Array.Empty<FrameworkGroup>();
    internal IReadOnlyList<InstallSnippet> InstallTabs { get; private set; } = Array.Empty<InstallSnippet>();

    /// <summary>Owner names; empty unless accounts are enabled and the package has owners.</summary>
    public IReadOnlyList<string?> Owners { get; private set; } = Array.Empty<string>();

    /// <summary>
    /// Each owner's avatar address by name. Empty when Gravatar is off or accounts are disabled.
    /// </summary>
    public IReadOnlyDictionary<string, string> OwnerAvatars { get; private set; } = new Dictionary<string, string>();

    /// <summary>
    /// The management page's address when the signed in user may open it (an owner, directly or through an
    /// organization, or an administrator), else null and the page offers no link. Built by
    /// <see cref="RepositoryPageLinks.Manage"/> for the repository the request works in.
    /// </summary>
    public string? ManageUrl { get; private set; }

    public string? IconUrl { get; private set; }
    public string? LicenseUrl { get; private set; }
    public string? PackageDownloadUrl { get; private set; }

    /// <summary>
    /// This version on nuget.info, NuGet Package Explorer's web app, as nuget.org links it. nuget.info
    /// always downloads from nuget.org, so the link only works for a package that is also published there.
    /// </summary>
    public string? PackageExplorerUrl { get; private set; }

    public async Task OnGetAsync(string id, string? version, CancellationToken cancellationToken)
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
        #pragma warning disable CS8602, CS8604
        IsDotnetTemplate = Package.PackageTypes.Any(t => t.Name.Equals("Template", StringComparison.OrdinalIgnoreCase));
        #pragma warning restore CS8604
        IsDotnetTool = Package.PackageTypes.Any(t => t.Name.Equals("DotnetTool", StringComparison.OrdinalIgnoreCase));
        #pragma warning restore CS8602
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
        LicenseUrl = GetLicenseUrlOrNull(Package);
        FrameworkGroups = PackageFrameworks.Compute(Package.TargetFrameworks?.Select(f => f.Moniker));
        InstallTabs = InstallSnippets.Build(Package.Id, Package.NormalizedVersionString, IsDotnetTemplate, IsDotnetTool);

        // PackageOwnershipService is only registered when accounts are enabled.
        var ownership = HttpContext?.RequestServices.GetService<PackageOwnershipService>();
        if (ownership != null)
        {
            var owners = await ownership.GetOwnersAsync(Package.Id, cancellationToken);
            Owners = owners.Select(o => o.OwnerName).ToList();
            #pragma warning disable CS8602
            OwnerAvatars = await HttpContext.RequestServices.GetRequiredService<AvatarService>()
                .GetUrlsAsync(Owners, 32, cancellationToken);
            #pragma warning restore CS8602

            // A virtual repository holds no packages to manage: they are managed in the member that stores them.
            var repository = HttpContext?.RequestServices.GetService<IRepositoryContext>()?.Current;
            if (User.Identity?.IsAuthenticated == true
                && repository?.Kind != RepositoryKind.Virtual
                && await CanManageAsync(ownership, Package.Id, cancellationToken))
            {
                ManageUrl = Url.Content(RepositoryPageLinks.Manage(repository?.Name, Package.Id));
            }
        }
        PackageDownloadUrl = _url.GetPackageDownloadUrl(Package.Id, packageVersion);
        #pragma warning disable CS8604
        PackageExplorerUrl = "https://nuget.info/packages/"
            + Uri.EscapeDataString(Package.Id) + "/" + Uri.EscapeDataString(Package.NormalizedVersionString);
        #pragma warning restore CS8604
    }

    // The rule of the management page itself (ManageModel): an owner holding the Manage permission in this
    // repository, or an administrator.
    private async Task<bool> CanManageAsync(PackageOwnershipService ownership, string? packageId, CancellationToken cancellationToken)
    {
        var services = HttpContext!.RequestServices;
        var userId = services.GetService<UserManager<BaGetUser>>()?.GetUserId(User);
        if (await ownership.IsOwnerAsync(userId, User.Identity?.Name, packageId, cancellationToken)
            && await ManagePermission.HeldAsync(services, User, packageId, cancellationToken)) return true;

        var administrators = services.GetService<AdministratorService>();
        return administrators != null && await administrators.IsAdministratorAsync(User, cancellationToken);
    }

    /// <summary>
    /// The license address, or null when there is none. A package that declares a license
    /// expression or file carries only the deprecation placeholder here, which leads nowhere useful.
    /// </summary>
    private static string? GetLicenseUrlOrNull(Package package)
    {
        var url = package.LicenseUrlString;
        if (string.IsNullOrEmpty(url)) return null;
        if (url.StartsWith("https://aka.ms/deprecateLicenseUrl", StringComparison.OrdinalIgnoreCase)) return null;

        return url;
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

    private async Task<HtmlString?> GetReadmeHtmlStringOrNullAsync(
        string? packageId,
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
        public NuGetVersion? Version { get; init; }
        public long Downloads { get; init; }
        public bool Selected { get; init; }
        public DateTime LastUpdated { get; init; }
    }
}