using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BaGet.Web;

/// <summary>
/// The page model of one npm package's page when it was a Razor Page.
/// </summary>
/// <remarks>
/// Kept as a shim so that code compiled against it still loads and runs (no breaking change of the public API); there is no page
/// behind it any more: <see cref="NpmBrowseController"/> serves <c>/npm-packages/{name}</c> (and its <c>/r/{repo}/</c> twin) and
/// shares the logic (<c>NpmPackageWorkflow</c>).
/// </remarks>
[Obsolete("The npm package page is served by NpmBrowseController; this page model has no page any more and will be removed in the next major version.")]
public class NpmPackageModel : PageModel
{
    private readonly NpmPackageWorkflow _workflow;
    private readonly IUrlGenerator _urls;

    public NpmPackageModel(INpmRegistry registry, IUrlGenerator urls)
    {
        _workflow = new NpmPackageWorkflow(registry, urls);
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
        var model = await _workflow.LoadAsync(Id, RequestedVersion, User, HttpContext.RequestServices, cancellationToken);
        if (model == null) return NotFound();

        Package = model.Package;
        Selected = model.Selected;
        Readme = model.Readme;
        SelectedTags = model.SelectedTags;
        InstallTabs = model.InstallTabs;
        Owners = model.Owners;
        CanManage = model.CanManage;
        if (model.StatusCode != 200) Response.StatusCode = model.StatusCode;

        return Page();
    }
}
