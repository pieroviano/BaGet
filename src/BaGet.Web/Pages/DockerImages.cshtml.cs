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
/// The front page of a Docker repository: a search box and the images whose name contains the text, with the commands that point
/// Docker at the registry (<c>docker login</c>, pull, push, multi-platform push). Served at <c>/r/{repo}/docker-images</c>
/// (<see cref="RepositoryPageRouteConvention"/>); <see cref="RepositoryPageFilter"/> requires Read on the repository and answers 404
/// in a repository that is not Docker's. An image is listed only when the visitor may read it.
/// </summary>
public class DockerImagesModel : PageModel
{
    /// <summary>Images per page.</summary>
    public const int ResultsPerPage = 20;

    private readonly IOciRegistry _registry;
    private readonly IUrlGenerator _urls;
    private readonly IRepositoryAccess _access;
    private readonly IRepositoryContext _repository;

    public DockerImagesModel(IOciRegistry registry, IUrlGenerator urls, IRepositoryAccess access, IRepositoryContext repository)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _urls = urls ?? throw new ArgumentNullException(nameof(urls));
        _access = access ?? throw new ArgumentNullException(nameof(access));
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

    /// <summary>Whether another page follows.</summary>
    public bool HasNext => PageIndex * ResultsPerPage < Result.Total;

    internal IReadOnlyList<InstallSnippet> CommandTabs { get; private set; } = Array.Empty<InstallSnippet>();

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest();

        var repository = _repository.Current;
        Result = await _registry.ListImagesAsync(
            Query,
            (PageIndex - 1) * ResultsPerPage,
            ResultsPerPage,
            (name, token) => _access.CanReadPackageAsync(User, repository, name, token),
            cancellationToken);

        (Host, IsHttp) = _urls.GetOciRegistryHost();
        var tabs = new List<InstallSnippet>
        {
            new() { Name = "Login", Prefix = ">", Lines = new[] { DockerSnippets.Login(Host) } },
            new() { Name = "Pull", Prefix = ">", Lines = new[] { DockerSnippets.Pull(Host, RepositoryName, "team/app", "latest") } },
            new() { Name = "Push", Prefix = ">", Lines = DockerSnippets.Push(Host, RepositoryName, "team/app") },
            new() { Name = "Multi-platform", Prefix = ">", Lines = new[] { DockerSnippets.BuildxPush(Host, RepositoryName, "team/app") } },
        };
        if (IsHttp) tabs.Add(new InstallSnippet { Name = "Plain http (daemon.json)", Lines = DockerSnippets.InsecureRegistry(Host) });
        CommandTabs = tabs;

        return Page();
    }
}
