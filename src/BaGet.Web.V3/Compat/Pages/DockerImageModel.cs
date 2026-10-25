using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BaGet.Web;

/// <summary>
/// The page model of the page of one Docker image when it was a Razor Page.
/// </summary>
/// <remarks>
/// Kept as a shim so that code compiled against it still loads and runs (no breaking change of the public API); there is no page
/// behind it any more: <see cref="DockerBrowseController"/> serves <c>/docker-images/{image}</c> (and its <c>/r/{repo}/</c> twin) and
/// shares the logic (<c>DockerImageWorkflow</c>).
/// </remarks>
[Obsolete("The Docker image page is served by DockerBrowseController; this page model has no page any more and will be removed in the next major version.")]
public class DockerImageModel : PageModel
{
    private readonly DockerImageWorkflow _workflow;
    private readonly IUrlGenerator _urls;
    private readonly IRepositoryContext _repository;

    public DockerImageModel(IOciRegistry registry, IUrlGenerator urls, IRepositoryContext repository)
    {
        _workflow = new DockerImageWorkflow(registry, urls, repository);
        _urls = urls ?? throw new ArgumentNullException(nameof(urls));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <summary>The image name from the address (it may contain slashes).</summary>
    [BindProperty(SupportsGet = true)]
    public string? Image { get; set; }

    /// <summary>The manifest to show in full (<c>?digest=</c>), or null for the overview.</summary>
    [BindProperty(SupportsGet = true, Name = "digest")]
    public string? Digest { get; set; }

    /// <summary>Narrows the referrers to one artifact type (<c>?artifactType=</c>), like <c>GET referrers/{digest}?artifactType=</c>; empty keeps them all.</summary>
    [BindProperty(SupportsGet = true, Name = "artifactType")]
    public string? ArtifactType { get; set; }

    /// <summary>The image, or null when there is none of that name.</summary>
    public OciImageInfo? Info { get; private set; }

    /// <summary>The chosen manifest, or null.</summary>
    public OciManifestDetail? Selected { get; private set; }

    /// <summary>The manifests whose <c>subject</c> is the chosen manifest (signatures, SBOMs): what <c>GET referrers/{digest}</c> answers. Empty without a selection.</summary>
    public IReadOnlyList<OciDescriptor> Referrers { get; private set; } = Array.Empty<OciDescriptor>();

    /// <summary>The registry host as a Docker client writes it.</summary>
    public string Host { get; private set; } = string.Empty;

    /// <summary>The repository's name.</summary>
    public string RepositoryName => _repository.Current.Name;

    /// <summary>Whether the repository caches an upstream registry (it takes no pushes).</summary>
    public bool IsRemote => _repository.Current.Kind == RepositoryKind.Remote;

    /// <summary>Whether the signed-in user holds Delete on the image, so the manage page is offered.</summary>
    public bool CanManage { get; private set; }

    internal IReadOnlyList<InstallSnippet> PullTabs { get; private set; } = Array.Empty<InstallSnippet>();

    /// <summary>The address of the registry API for the image (what a script calls).</summary>
    public string RegistryUrl => $"{_urls.GetOciRegistryUrl()}{RepositoryName}/{Image}/";

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var model = await _workflow.LoadAsync(Image, Digest, ArtifactType, User, HttpContext.RequestServices, cancellationToken);
        if (model == null) return NotFound();

        Info = model.Info;
        Selected = model.Selected;
        Referrers = model.Referrers;
        Host = model.Host;
        CanManage = model.CanManage;
        PullTabs = model.PullTabs;
        if (model.StatusCode != 200) Response.StatusCode = model.StatusCode;

        return Page();
    }
}
