using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace BaGet.Web;

/// <summary>
/// The logic of the Docker front page: the images whose name contains the text (only those the visitor may read) and the commands
/// that point Docker at the registry. Used by <c>DockerBrowseController</c>.
/// </summary>
internal sealed class DockerImagesWorkflow
{
    /// <summary>Images per page.</summary>
    public const int ResultsPerPage = 20;

    private readonly IOciRegistry _registry;
    private readonly IUrlGenerator _urls;
    private readonly IRepositoryAccess _access;
    private readonly IRepositoryContext _repository;

    public DockerImagesWorkflow(IOciRegistry registry, IUrlGenerator urls, IRepositoryAccess access, IRepositoryContext repository)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _urls = urls ?? throw new ArgumentNullException(nameof(urls));
        _access = access ?? throw new ArgumentNullException(nameof(access));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<DockerImagesViewModel> LoadAsync(string? query, int pageIndex, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var repository = _repository.Current;
        var result = await _registry.ListImagesAsync(
            query,
            (pageIndex - 1) * ResultsPerPage,
            ResultsPerPage,
            (name, token) => _access.CanReadPackageAsync(user, repository, name, token),
            cancellationToken);

        var (host, isHttp) = _urls.GetOciRegistryHost();
        var repositoryName = repository.Name;
        var isRemote = repository.Kind == RepositoryKind.Remote;
        var tabs = new List<InstallSnippet>
        {
            new() { Name = "Login", Prefix = ">", Lines = new[] { DockerSnippets.Login(host) } },
            new() { Name = "Pull", Prefix = ">", Lines = new[] { DockerSnippets.Pull(host, repositoryName, "team/app", "latest") } },
        };
        if (isRemote)
        {
            // A cache of Docker Hub serves the official images as single names: nginx is library/nginx there.
            tabs.Add(new InstallSnippet { Name = "Pull an official image", Prefix = ">", Lines = new[] { DockerSnippets.Pull(host, repositoryName, "nginx", "latest") } });
        }
        else
        {
            tabs.Add(new InstallSnippet { Name = "Push", Prefix = ">", Lines = DockerSnippets.Push(host, repositoryName, "team/app") });
            tabs.Add(new InstallSnippet { Name = "Multi-platform", Prefix = ">", Lines = new[] { DockerSnippets.BuildxPush(host, repositoryName, "team/app") } });
        }

        if (isHttp) tabs.Add(new InstallSnippet { Name = "Plain http (daemon.json)", Lines = DockerSnippets.InsecureRegistry(host) });

        return new DockerImagesViewModel
        {
            Query = query,
            PageIndex = pageIndex,
            Result = result,
            Host = host,
            IsHttp = isHttp,
            RepositoryName = repositoryName,
            IsRemote = isRemote,
            IsVirtual = repository.Kind == RepositoryKind.Virtual,
            CommandTabs = tabs,
        };
    }
}

/// <summary>
/// The logic of the page of one Docker image: its tags, manifests and, with a digest, one manifest in full. Shared by
/// <c>DockerBrowseController</c>.
/// </summary>
internal sealed class DockerImageWorkflow
{
    private readonly IOciRegistry _registry;
    private readonly IUrlGenerator _urls;
    private readonly IRepositoryContext _repository;

    public DockerImageWorkflow(IOciRegistry registry, IUrlGenerator urls, IRepositoryContext repository)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _urls = urls ?? throw new ArgumentNullException(nameof(urls));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <summary>The page's data; null when the image name or the digest is not valid (the page answers a plain 404).</summary>
    public async Task<DockerImageViewModel?> LoadAsync(string? image, string? digest, string? artifactType, ClaimsPrincipal user, IServiceProvider services, CancellationToken cancellationToken)
    {
        if (!OciNames.IsValidImage(image)) return null;
        if (digest != null && !OciNames.IsValidDigest(digest)) return null;

        var repository = _repository.Current;
        var repositoryName = repository.Name;
        var isRemote = repository.Kind == RepositoryKind.Remote;
        var registryUrl = $"{_urls.GetOciRegistryUrl()}{repositoryName}/{image}/";
        DockerImageViewModel Missing(OciImageInfo? info) => new()
        {
            Image = image,
            Digest = digest,
            ArtifactType = artifactType,
            Info = info,
            RepositoryName = repositoryName,
            IsRemote = isRemote,
            RegistryUrl = registryUrl,
            StatusCode = 404,
        };

        var info = await _registry.GetImageAsync(image!, cancellationToken);
        if (info == null)
        {
            // The page explains what is missing, with the status a link checker expects.
            return Missing(null);
        }

        OciManifestDetail? selected = null;
        if (digest != null)
        {
            selected = await _registry.GetManifestDetailAsync(image!, digest, cancellationToken);
            if (selected == null) return Missing(info);
        }

        IReadOnlyList<OciDescriptor> referrers = Array.Empty<OciDescriptor>();
        if (selected != null)
        {
            referrers = await _registry.GetReferrersAsync(image!, selected.Summary.Digest, string.IsNullOrWhiteSpace(artifactType) ? null : artifactType.Trim(), cancellationToken) ?? Array.Empty<OciDescriptor>();
        }

        var (host, _) = _urls.GetOciRegistryHost();
        var tag = selected?.Summary.Tags.FirstOrDefault() ?? info.Tags.FirstOrDefault()?.Name;
        var tabs = new List<InstallSnippet>();
        if (tag != null) tabs.Add(new InstallSnippet { Name = "Pull by tag", Prefix = ">", Lines = new[] { DockerSnippets.Pull(host, repositoryName, image!, tag: tag) } });
        var pullDigest = selected?.Summary.Digest ?? info.Tags.FirstOrDefault()?.Digest ?? info.Manifests.FirstOrDefault()?.Digest;
        if (pullDigest != null) tabs.Add(new InstallSnippet { Name = "Pull by digest", Prefix = ">", Lines = new[] { DockerSnippets.Pull(host, repositoryName, image!, digest: pullDigest) } });
        if (!isRemote) tabs.Add(new InstallSnippet { Name = "Push", Prefix = ">", Lines = DockerSnippets.Push(host, repositoryName, image!) });

        return new DockerImageViewModel
        {
            Image = image,
            Digest = digest,
            ArtifactType = artifactType,
            Info = info,
            Selected = selected,
            Referrers = referrers,
            Host = host,
            RepositoryName = repositoryName,
            IsRemote = isRemote,
            RegistryUrl = registryUrl,
            PullTabs = tabs,
            CanManage = await DockerManageAccess.CanOpenAsync(services, user, repository, image!, cancellationToken),
        };
    }
}

/// <summary>Who is offered the Docker manage page: a signed-in user who holds Delete on the image (the rule the registry applies to a delete).</summary>
internal static class DockerManageAccess
{
    public static async Task<bool> CanOpenAsync(IServiceProvider services, ClaimsPrincipal user, RepositoryInfo repository, string image, CancellationToken cancellationToken)
    {
        // The tools exist where images are pushed: only a local repository (the manage page of another kind is 404).
        if (user.Identity?.IsAuthenticated != true || repository.Kind != RepositoryKind.Local) return false;

        var sessions = services.GetService<ISessionPrincipalFactory>();
        var principal = sessions == null ? null : await sessions.CreateAsync(user);
        if (principal == null) return false;

        return await services.GetRequiredService<IOciAccess>().IsAllowedAsync(principal, repository, image, OciActions.Delete, cancellationToken);
    }
}

/// <summary>Who sees the link to the manage page of an npm package or a Maven artifact.</summary>
internal static class NpmManageAccess
{
    /// <summary>An owner (directly or through an organization) or an administrator, signed in.</summary>
    public static async Task<bool> CanOpenAsync(IServiceProvider services, ClaimsPrincipal user, string name, CancellationToken cancellationToken)
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
