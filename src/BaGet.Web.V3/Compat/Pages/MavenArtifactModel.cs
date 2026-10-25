using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BaGet.Web;

/// <summary>
/// The page model of the page of one Maven artifact when it was a Razor Page.
/// </summary>
/// <remarks>
/// Kept as a shim so that code compiled against it still loads and runs (no breaking change of the public API); there is no page
/// behind it any more: <see cref="MavenBrowseController"/> serves <c>/maven-artifacts/{group}/{artifact}/{version?}</c> (and its
/// <c>/r/{repo}/</c> twin) and shares the logic (<c>MavenArtifactWorkflow</c>).
/// </remarks>
[Obsolete("The Maven artifact page is served by MavenBrowseController; this page model has no page any more and will be removed in the next major version.")]
public class MavenArtifactModel : PageModel
{
    private readonly MavenArtifactWorkflow _workflow;
    private readonly IUrlGenerator _urls;
    private readonly IRepositoryContext _repository;

    public MavenArtifactModel(IMavenRepository maven, IUrlGenerator urls, IRepositoryContext repository)
    {
        _workflow = new MavenArtifactWorkflow(maven, urls, repository);
        _urls = urls ?? throw new ArgumentNullException(nameof(urls));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <summary>The group id from the address.</summary>
    [BindProperty(SupportsGet = true)]
    public string? Group { get; set; }

    /// <summary>The artifact id from the address.</summary>
    [BindProperty(SupportsGet = true)]
    public string? Artifact { get; set; }

    /// <summary>The version from the address; the latest when absent.</summary>
    [BindProperty(SupportsGet = true)]
    public string? Version { get; set; }

    /// <summary>The artifact, or null when there is none of those coordinates.</summary>
    public MavenArtifactInfo? Info { get; private set; }

    /// <summary>The version the page shows, with its files and POM.</summary>
    public MavenVersionDetail? Selected { get; private set; }

    /// <summary>The address Maven is configured with for this repository.</summary>
    public string RepositoryUrl => _urls.GetMavenRepositoryUrl();

    /// <summary>The repository's name (the <c>id</c> in <c>settings.xml</c>).</summary>
    public string RepositoryName => _repository.Current.Name;

    internal IReadOnlyList<InstallSnippet> InstallTabs { get; private set; } = Array.Empty<InstallSnippet>();

    /// <summary>The owners, when accounts are on; empty otherwise.</summary>
    public IReadOnlyList<string> Owners { get; private set; } = Array.Empty<string>();

    /// <summary>Whether the signed-in user can open the manage page (an owner or an administrator).</summary>
    public bool CanManage { get; private set; }

    /// <summary>The address of a file of the selected version.</summary>
    public string FileUrl(string fileName)
        => RepositoryUrl + MavenNames.ClientPath(Info!.GroupId, Info.ArtifactId, Selected!.Summary.Version, fileName);

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var model = await _workflow.LoadAsync(Group, Artifact, Version, User, HttpContext.RequestServices, cancellationToken);
        if (model == null) return NotFound();

        Info = model.Info;
        Selected = model.Selected;
        InstallTabs = model.InstallTabs;
        Owners = model.Owners;
        CanManage = model.CanManage;
        if (model.StatusCode != 200) Response.StatusCode = model.StatusCode;

        return Page();
    }
}
