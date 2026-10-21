using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BaGet.Web;

/// <summary>
/// One npm package: its readme, versions, dist-tags and dependencies, and the commands to install it from this repository.
/// Served at <c>/r/{repo}/npm-packages/{name}</c> (a scoped name keeps its slash); <see cref="RepositoryPageFilter"/>
/// requires Read on the repository and on the package (the decoded name, so no spelling gets around an access pattern).
/// </summary>
public class NpmPackageModel : PageModel
{
    private readonly INpmRegistry _registry;
    private readonly IUrlGenerator _urls;

    public NpmPackageModel(INpmRegistry registry, IUrlGenerator urls)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _urls = urls ?? throw new ArgumentNullException(nameof(urls));
    }

    /// <summary>The package name from the address (the page decodes and validates it).</summary>
    [BindProperty(SupportsGet = true)]
    public string? Id { get; set; }

    /// <summary>The version asked for with <c>?version=</c>; the latest when absent or unknown.</summary>
    [BindProperty(Name = "version", SupportsGet = true)]
    public string? RequestedVersion { get; set; }

    /// <summary>The package, or null when there is none of that name.</summary>
    public NpmPackageInfo? Package { get; private set; }

    /// <summary>The version the page shows.</summary>
    public NpmVersionInfo? Selected { get; private set; }

    /// <summary>The selected version's readme as safe HTML, or null.</summary>
    public HtmlString? Readme { get; private set; }

    /// <summary>The dist-tags that point at the selected version.</summary>
    public IReadOnlyList<string> SelectedTags { get; private set; } = Array.Empty<string>();

    /// <summary>The address npm is configured with for this repository.</summary>
    public string RegistryUrl => _urls.GetNpmRegistryUrl();

    /// <summary>Where the selected version's tarball is.</summary>
    public string? TarballUrl => Selected == null || Package == null
        ? null
        : RegistryUrl + Package.Name + "/-/" + NpmNames.TarballFileName(Package.Name, Selected.Version);

    internal IReadOnlyList<InstallSnippet> InstallTabs { get; private set; } = Array.Empty<InstallSnippet>();

    /// <summary>The owners, when accounts are on; empty otherwise.</summary>
    public IReadOnlyList<string> Owners { get; private set; } = Array.Empty<string>();

    /// <summary>Whether the signed-in user can open the manage page (an owner or an administrator).</summary>
    public bool CanManage { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!NpmNames.TryParse(Id, out var name, out _)) return NotFound();

        Package = await _registry.GetPackageAsync(name, cancellationToken);
        if (Package == null)
        {
            // The page explains what is missing, with the status a link checker expects.
            Response.StatusCode = 404;
            return Page();
        }

        Selected = (RequestedVersion == null ? null : Package.Versions.FirstOrDefault(v => v.Version == RequestedVersion))
                   ?? Package.Latest;
        if (Selected == null) return Page();

        Readme = ReadmeMarkdown.ToHtml(Selected.Readme);
        SelectedTags = Package.DistTags.Where(t => t.Value == Selected.Version).Select(t => t.Key).OrderBy(t => t, StringComparer.Ordinal).ToList();
        InstallTabs = NpmInstallSnippets.Build(Package.Name, Selected.Version, SelectedTags.FirstOrDefault(t => t != "latest"), RegistryUrl);

        // Owners and the manage page belong to a repository that holds packages of its own; a remote one caches an upstream's and a
        // virtual one holds none (it is managed where the package is stored).
        var services = HttpContext.RequestServices;
        var kind = services.GetService<IRepositoryContext>()?.Current.Kind ?? RepositoryKind.Local;
        if (kind == RepositoryKind.Local && services.GetService<IOptions<AccountsOptions>>()?.Value.Enabled == true)
        {
            var ownership = services.GetService<PackageOwnershipService>();
            if (ownership != null)
            {
                Owners = (await ownership.GetOwnersAsync(Package.Name, cancellationToken)).Select(o => o.OwnerName ?? string.Empty).ToList();
                CanManage = await NpmManageAccess.CanOpenAsync(services, User, Package.Name, cancellationToken);
            }
        }

        return Page();
    }
}

/// <summary>Who sees the link to an npm package's manage page.</summary>
internal static class NpmManageAccess
{
    /// <summary>An owner (directly or through an organization) or an administrator, signed in.</summary>
    public static async Task<bool> CanOpenAsync(IServiceProvider services, System.Security.Claims.ClaimsPrincipal user, string name, CancellationToken cancellationToken)
    {
        if (user.Identity?.IsAuthenticated != true) return false;

        var sessions = services.GetService<ISessionPrincipalFactory>();
        var principal = sessions == null ? null : await sessions.CreateAsync(user);
        if (principal == null) return false;
        if (principal.IsAdministrator) return true;

        var ownership = services.GetService<PackageOwnershipService>();

        return ownership != null && await ownership.IsOwnerAsync(principal.UserId, principal.UserName, name, cancellationToken);
    }
}
