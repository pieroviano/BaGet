using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BaGet.Web;

/// <summary>
/// The page model of the front page of a Docker repository when it was a Razor Page.
/// </summary>
/// <remarks>
/// Kept as a shim so that code compiled against it still loads and runs (no breaking change of the public API); there is no page
/// behind it any more: <see cref="DockerBrowseController"/> serves <c>/docker-images</c> (and its <c>/r/{repo}/</c> twin) and
/// shares the logic (<c>DockerImagesWorkflow</c>).
/// </remarks>
[Obsolete("The Docker image list is served by DockerBrowseController; this page model has no page any more and will be removed in the next major version.")]
public class DockerImagesModel : PageModel
{
    /// <summary>Images per page.</summary>
    public const int ResultsPerPage = DockerImagesWorkflow.ResultsPerPage;

    private readonly DockerImagesWorkflow _workflow;
    private readonly IRepositoryContext _repository;

    public DockerImagesModel(IOciRegistry registry, IUrlGenerator urls, IRepositoryAccess access, IRepositoryContext repository)
    {
        _workflow = new DockerImagesWorkflow(registry, urls, access, repository);
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <summary>The search text.</summary>
    [BindProperty(Name = "q", SupportsGet = true)]
    public string? Query { get; set; }

    /// <summary>The page, from 1.</summary>
    [BindProperty(Name = "p", SupportsGet = true)]
    [Range(1, int.MaxValue)]
    public int PageIndex { get; set; } = 1;

    /// <summary>This page of results.</summary>
    public OciImagePage Result { get; private set; } = new(0, Array.Empty<OciImageSummary>());

    /// <summary>The registry host as a Docker client writes it (<c>host:port</c>).</summary>
    public string Host { get; private set; } = string.Empty;

    /// <summary>Whether the registry is reached over plain http (the Docker daemon then needs it as an insecure registry).</summary>
    public bool IsHttp { get; private set; }

    /// <summary>The repository's name, the first path segment of every image reference.</summary>
    public string RepositoryName => _repository.Current.Name;

    /// <summary>Whether the repository caches an upstream registry (it takes no pushes).</summary>
    public bool IsRemote => _repository.Current.Kind == RepositoryKind.Remote;

    /// <summary>Whether the repository merges other repositories (a push goes to its deploy repository).</summary>
    public bool IsVirtual => _repository.Current.Kind == RepositoryKind.Virtual;

    /// <summary>Whether another page follows.</summary>
    public bool HasNext => PageIndex * ResultsPerPage < Result.Total;

    internal IReadOnlyList<InstallSnippet> CommandTabs { get; private set; } = Array.Empty<InstallSnippet>();

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest();

        var model = await _workflow.LoadAsync(Query, PageIndex, User, cancellationToken);
        Result = model.Result;
        Host = model.Host;
        IsHttp = model.IsHttp;
        CommandTabs = model.CommandTabs;

        return Page();
    }
}
