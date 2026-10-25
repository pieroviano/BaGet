using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using NuGet.Versioning;

namespace BaGet.Web;

/// <summary>
/// The logic of the package page, shared by <see cref="PackageController"/>: finds the
/// version to show (the requested one, else the newest listed) and gathers what the page displays.
/// </summary>
public sealed class PackageWorkflow
{
    private readonly IPackageService _packages;
    private readonly IPackageContentService _content;
    private readonly ISearchService _search;
    private readonly IUrlGenerator _url;

    /// <summary>Creates the workflow.</summary>
    public PackageWorkflow(
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

    /// <summary>Loads the page of <paramref name="id"/> (and <paramref name="version"/>, else the newest listed one).</summary>
    /// <param name="id">The package ID.</param>
    /// <param name="version">The requested version, or null for the newest listed one.</param>
    /// <param name="http">The request, for the optional account services and the signed in user; null outside a request.</param>
    /// <param name="urlHelper">Resolves <c>~/</c> addresses; null outside a request.</param>
    /// <param name="cancellationToken">Cancels the load.</param>
    public async Task<PackageViewModel> LoadAsync(
        string id,
        string? version,
        HttpContext? http,
        IUrlHelper? urlHelper,
        CancellationToken cancellationToken)
    {
        var model = new PackageViewModel();
        var packages = await _packages.FindPackagesAsync(id, cancellationToken);
        var listedPackages = packages.Where(p => p.Listed).ToList();

        // Try to find the requested version.
        Package? package = null;
        if (NuGetVersion.TryParse(version, out var requestedVersion))
        {
            package = packages.SingleOrDefault(p => p.Version == requestedVersion);
        }

        // Otherwise try to display the latest version.
        if (package == null)
        {
            package = listedPackages.OrderByDescending(p => p.Version).FirstOrDefault();
        }

        if (package == null)
        {
            model.Package = new Package { Id = id };
            model.Found = false;
            return model;
        }

        model.Package = package;
        var packageVersion = package.Version;

        model.Found = true;
        #pragma warning disable CS8602, CS8604
        model.IsDotnetTemplate = package.PackageTypes.Any(t => t.Name.Equals("Template", StringComparison.OrdinalIgnoreCase));
        #pragma warning restore CS8604
        model.IsDotnetTool = package.PackageTypes.Any(t => t.Name.Equals("DotnetTool", StringComparison.OrdinalIgnoreCase));
        #pragma warning restore CS8602
        model.LastUpdated = packages.Max(p => p.Published);
        model.TotalDownloads = packages.Sum(p => p.Downloads);

        var dependents = await _search.FindDependentsAsync(package.Id, cancellationToken);

        model.UsedBy = dependents.Data;
        model.DependencyGroups = PackageDependencies.GroupByFramework(package.Dependencies);
        model.Versions = ToVersions(listedPackages, packageVersion);

        if (package.HasReadme)
        {
            model.Readme = await GetReadmeHtmlStringOrNullAsync(package.Id, packageVersion, cancellationToken);
        }

        model.IconUrl = package.HasEmbeddedIcon
            ? _url.GetPackageIconDownloadUrl(package.Id, packageVersion)
            : package.IconUrlString;
        model.LicenseUrl = GetLicenseUrlOrNull(package);
        model.FrameworkGroups = PackageFrameworks.Compute(package.TargetFrameworks?.Select(f => f.Moniker));
        model.InstallTabs = InstallSnippets.Build(package.Id, package.NormalizedVersionString, model.IsDotnetTemplate, model.IsDotnetTool);

        // PackageOwnershipService is only registered when accounts are enabled.
        var ownership = http?.RequestServices.GetService<PackageOwnershipService>();
        if (ownership != null)
        {
            var owners = await ownership.GetOwnersAsync(package.Id, cancellationToken);
            model.Owners = owners.Select(o => o.OwnerName).ToList();
            #pragma warning disable CS8602
            model.OwnerAvatars = await http.RequestServices.GetRequiredService<AvatarService>()
                .GetUrlsAsync(model.Owners, 32, cancellationToken);
            #pragma warning restore CS8602

            // A virtual repository holds no packages to manage: they are managed in the member that stores them.
            var repository = http.RequestServices.GetService<IRepositoryContext>()?.Current;
            if (http.User.Identity?.IsAuthenticated == true
                && repository?.Kind != RepositoryKind.Virtual
                && await CanManageAsync(http, ownership, package.Id, cancellationToken))
            {
                var manage = RepositoryPageLinks.Manage(repository?.Name, package.Id);
                model.ManageUrl = urlHelper?.Content(manage) ?? manage;
            }
        }
        model.PackageDownloadUrl = _url.GetPackageDownloadUrl(package.Id, packageVersion);
        #pragma warning disable CS8604
        model.PackageExplorerUrl = "https://nuget.info/packages/"
            + Uri.EscapeDataString(package.Id) + "/" + Uri.EscapeDataString(package.NormalizedVersionString);
        #pragma warning restore CS8604

        return model;
    }

    // The rule of the management page itself (ManageModel): an owner holding the Manage permission in this
    // repository, or an administrator.
    private static async Task<bool> CanManageAsync(HttpContext http, PackageOwnershipService ownership, string? packageId, CancellationToken cancellationToken)
    {
        var services = http.RequestServices;
        var user = http.User;
        var userId = services.GetService<UserManager<BaGetUser>>()?.GetUserId(user);
        if (await ownership.IsOwnerAsync(userId, user.Identity?.Name, packageId, cancellationToken)
            && await ManagePermission.HeldAsync(services, user, packageId, cancellationToken)) return true;

        var administrators = services.GetService<AdministratorService>();
        return administrators != null && await administrators.IsAdministratorAsync(user, cancellationToken);
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

    private static IReadOnlyList<PackageVersionRow> ToVersions(IReadOnlyList<Package> packages, NuGetVersion selectedVersion)
    {
        return packages
            .Select(p => new PackageVersionRow
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
}
