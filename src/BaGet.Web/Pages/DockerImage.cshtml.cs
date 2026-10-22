using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.DependencyInjection;

namespace BaGet.Web;

/// <summary>
/// One image of a Docker repository: its tags, its manifests (single images and multi-platform indexes with each platform), and
/// with <c>?digest=</c> one manifest in full: config, layers with sizes, platforms, annotations and the document. Served at
/// <c>/r/{repo}/docker-images/{image}</c>; <see cref="RepositoryPageFilter"/> requires Read on the repository and on the image
/// (access patterns see the image name). The delete tools are on <c>/docker-manage/{image}</c>, offered to anyone who holds Delete.
/// </summary>
public class DockerImageModel : PageModel
{
    private readonly IOciRegistry _registry;
    private readonly IUrlGenerator _urls;
    private readonly IRepositoryContext _repository;

    public DockerImageModel(IOciRegistry registry, IUrlGenerator urls, IRepositoryContext repository)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _urls = urls ?? throw new ArgumentNullException(nameof(urls));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <summary>The image name from the address (it may contain slashes).</summary>
    [BindProperty(SupportsGet = true)]
    public string? Image { get; set; }

    /// <summary>The manifest to show in full (<c>?digest=</c>), or null for the overview.</summary>
    [BindProperty(SupportsGet = true, Name = "digest")]
    public string? Digest { get; set; }

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
        if (!OciNames.IsValidImage(Image)) return NotFound();
        if (Digest != null && !OciNames.IsValidDigest(Digest)) return NotFound();

        Info = await _registry.GetImageAsync(Image!, cancellationToken);
        if (Info == null)
        {
            // The page explains what is missing, with the status a link checker expects.
            Response.StatusCode = 404;
            return Page();
        }

        if (Digest != null)
        {
            Selected = await _registry.GetManifestDetailAsync(Image!, Digest, cancellationToken);
            if (Selected == null)
            {
                Response.StatusCode = 404;
                return Page();
            }
        }

        if (Selected != null)
        {
            Referrers = await _registry.GetReferrersAsync(Image!, Selected.Summary.Digest, null, cancellationToken) ?? Array.Empty<OciDescriptor>();
        }

        (Host, _) = _urls.GetOciRegistryHost();
        var tag = Selected?.Summary.Tags.FirstOrDefault() ?? Info.Tags.FirstOrDefault()?.Name;
        var tabs = new List<InstallSnippet>();
        if (tag != null) tabs.Add(new InstallSnippet { Name = "Pull by tag", Prefix = ">", Lines = new[] { DockerSnippets.Pull(Host, RepositoryName, Image!, tag: tag) } });
        var digest = Selected?.Summary.Digest ?? Info.Tags.FirstOrDefault()?.Digest ?? Info.Manifests.FirstOrDefault()?.Digest;
        if (digest != null) tabs.Add(new InstallSnippet { Name = "Pull by digest", Prefix = ">", Lines = new[] { DockerSnippets.Pull(Host, RepositoryName, Image!, digest: digest) } });
        if (!IsRemote) tabs.Add(new InstallSnippet { Name = "Push", Prefix = ">", Lines = DockerSnippets.Push(Host, RepositoryName, Image!) });
        PullTabs = tabs;

        CanManage = await DockerManageAccess.CanOpenAsync(HttpContext.RequestServices, User, _repository.Current, Image!, cancellationToken);

        return Page();
    }
}

/// <summary>Who is offered the Docker manage page: a signed-in user who holds Delete on the image (the rule the registry applies to a delete).</summary>
internal static class DockerManageAccess
{
    public static async Task<bool> CanOpenAsync(IServiceProvider services, System.Security.Claims.ClaimsPrincipal user, RepositoryInfo repository, string image, CancellationToken cancellationToken)
    {
        // The tools exist where images are pushed: only a local repository (the manage page of another kind is 404).
        if (user.Identity?.IsAuthenticated != true || repository.Kind != RepositoryKind.Local) return false;

        var sessions = services.GetService<ISessionPrincipalFactory>();
        var principal = sessions == null ? null : await sessions.CreateAsync(user);
        if (principal == null) return false;

        return await services.GetRequiredService<IOciAccess>().IsAllowedAsync(principal, repository, image, OciActions.Delete, cancellationToken);
    }
}
