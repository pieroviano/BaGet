using System;
using System.Collections.Generic;
using System.Linq;
using BaGet.Core;
using BaGet.Identity.Pages.Admin;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace BaGet.Identity.ViewModels;

/// <summary>
/// What the repositories screen (<c>/admin/repositories</c>) shows: the repositories with their feed addresses, the list filter and
/// the create forms. The posted fields of the create forms (<see cref="Name"/>, <see cref="Remote"/>) live here too, so a refused
/// creation shows again what was typed.
/// </summary>
public sealed class RepositoriesViewModel : ConfirmationModel, IFlashMessage
{
    private HashSet<string> _deployable = new();

    /// <summary>The name for the repository to create.</summary>
    public string? Name { get; set; }

    /// <summary>The upstream settings for the remote repository to create.</summary>
    public RemoteFormModel Remote { get; set; } = new();

    /// <summary>The repositories.</summary>
    public IReadOnlyList<RepositorySummary> Repositories { get; internal set; } = Array.Empty<RepositorySummary>();

    /// <summary>Whether the database can hold more than the default repository.</summary>
    public bool CanCreate { get; internal set; }

    /// <inheritdoc />
    public string? Message { get; set; }

    /// <summary>Why the last action was refused.</summary>
    public string? Error { get; internal set; }

    /// <summary>The format to keep selected after a refused creation.</summary>
    public string? Format { get; internal set; }

    /// <summary>The format the list is narrowed to (<c>?format=</c>), or null.</summary>
    public string? FilterFormat { get; internal set; }

    /// <summary>The kind the list is narrowed to (<c>?kind=</c>), or null.</summary>
    public string? FilterKind { get; internal set; }

    /// <summary>The name fragment the list is narrowed to (<c>?q=</c>), or null.</summary>
    public string? FilterName { get; internal set; }

    /// <summary>Every format this server knows, for the list filter.</summary>
    public IReadOnlyList<IFormatModule> AllFormats { get; internal set; } = Array.Empty<IFormatModule>();

    /// <summary>The formats a remote repository can be created for.</summary>
    public IReadOnlyList<IFormatModule> RemoteFormats { get; internal set; } = Array.Empty<IFormatModule>();

    /// <summary>The formats a virtual repository can be created for.</summary>
    public IReadOnlyList<IFormatModule> VirtualFormats { get; internal set; } = Array.Empty<IFormatModule>();

    /// <summary>The formats other than NuGet that can be created here, with their form fields.</summary>
    public IReadOnlyList<IFormatModule> OtherFormats { get; internal set; } = Array.Empty<IFormatModule>();

    internal HttpContext? Http { get; set; }

    internal IUrlHelper? UrlHelper { get; set; }

    internal void SetDeployable(HashSet<string> names) => _deployable = names;

    /// <summary>Whether uploads to <paramref name="repository"/> are possible at all (a remote one takes none; a virtual one needs a deploy repository).</summary>
    public bool TakesUploads(RepositorySummary repository) => repository.Kind switch
    {
        RepositoryKind.Remote => false,
        RepositoryKind.Virtual => _deployable.Contains(repository.Name),
        _ => true,
    };

    private FormatRegistry Formats => Http!.RequestServices.GetRequiredService<FormatRegistry>();

    /// <summary>The feed address of <paramref name="repository"/>, by its format's <see cref="IFormatModule.FeedPath"/>.</summary>
    public string FeedUrl(RepositorySummary repository)
        => RepositoryFeedUrl.For(Http!, repository.Name, Formats.Find(repository.Format)?.FeedPath ?? "v3/index.json");

    /// <summary>The links its format contributes for <paramref name="repository"/> (browse, upload...), without the upload link where it takes none.</summary>
    public IReadOnlyList<FormatLink> LinksOf(RepositorySummary repository)
        => (Formats.Find(repository.Format)?.RepositoryLinks(new RepositoryInfo(repository.Key, repository.Name, repository.Format, repository.Kind)) ?? Array.Empty<FormatLink>())
            .Where(l => !l.NeedsUploads || TakesUploads(repository))
            .ToList();

    /// <summary>Where <paramref name="link"/> leads for <paramref name="repository"/>.</summary>
    public string LinkUrl(RepositorySummary repository, FormatLink link)
        => link.Page != null
            ? UrlHelper!.Page(link.Page, new { repo = repository.IsDefault ? null : repository.Name }) ?? string.Empty
            : RepositoryFeedUrl.BaseFor(Http!, repository.Name) + link.Path;
}

/// <summary>
/// What the repository screen (<c>/admin/repositories/{name}</c>) shows: the repository, its access summary and, by kind, its
/// upstream and cache (remote), members and preview (virtual) or Maven settings. The posted fields (anonymous read, upstream, preview
/// and purge inputs) live here too, so a refused action shows again what was typed.
/// </summary>
public sealed class RepositoryEditViewModel : ConfirmationModel, IFlashMessage
{
    /// <summary>The repository's name (route).</summary>
    public string? Name { get; set; }

    /// <summary>Whether requests without credentials may read the repository.</summary>
    public bool AnonymousRead { get; set; }

    /// <summary>The repository.</summary>
    public RepositorySummary? Repository { get; internal set; }

    /// <summary>The upstream fields of a remote repository.</summary>
    public RemoteFormModel Remote { get; set; } = new();

    /// <summary>The remote repository's upstream, null for the other kinds.</summary>
    public RemoteRepositoryDetail? RemoteDetail { get; internal set; }

    /// <summary>The settings of a local Maven repository, null for the other repositories.</summary>
    public MavenRepositorySettings? MavenSettings { get; internal set; }

    /// <summary>What a remote repository stores and remembers.</summary>
    public RemoteCacheOverview? Cache { get; internal set; }

    /// <summary>The virtual repository's members and deploy repository, null for the other kinds.</summary>
    public VirtualRepositoryDetail? VirtualDetail { get; internal set; }

    /// <summary>The repositories that can still be added to a virtual repository.</summary>
    public IReadOnlyList<RepositoryInfo> MemberCandidates { get; internal set; } = Array.Empty<RepositoryInfo>();

    /// <summary>The virtual repositories that list this repository as a member.</summary>
    public IReadOnlyList<string> ReferencedBy { get; internal set; } = Array.Empty<string>();

    /// <summary>The package id to resolve in the preview.</summary>
    public string? PreviewPackageId { get; set; }

    /// <summary>The package version to resolve in the preview.</summary>
    public string? PreviewVersion { get; set; }

    /// <summary>Where the previewed package version would come from.</summary>
    public ResolutionPreview? Preview { get; internal set; }

    /// <summary>The outcome of "Test connection".</summary>
    public RemoteConnectionResult? TestResult { get; internal set; }

    /// <inheritdoc />
    public string? Message { get; set; }

    /// <summary>Why the last action was refused.</summary>
    public string? Error { get; internal set; }

    /// <summary>The package id to purge from the cache.</summary>
    public string? PackageId { get; set; }

    /// <summary>The version to purge (blank = every version).</summary>
    public string? PackageVersion { get; set; }

    /// <summary>Who effectively holds each action in the repository; null for the default repository when the database has no access model.</summary>
    public RepositoryAccessSummary? Access { get; internal set; }

    /// <summary>The page of the repository's format that lists its packages (<c>/Index</c> for NuGet, <c>/NpmPackages</c> for npm).</summary>
    public string BrowsePage { get; internal set; } = "/Index";

    /// <summary>The repository's feed address.</summary>
    public string FeedUrl { get; internal set; } = string.Empty;

    /// <summary>For the default repository: the remote repository it fetches through, when <c>Mirror:Enabled</c>.</summary>
    public RemoteRepositoryDetail? MirrorRepository { get; internal set; }

    /// <summary>Whether <c>Mirror:Enabled</c> makes the default repository fetch through <see cref="MirrorRepository"/>.</summary>
    public bool MirrorActive { get; internal set; }

    /// <summary>The name of what a package is called in this repository's format (shown in the labels).</summary>
    public string PackageWord => Repository?.Format switch { RepositoryFormat.Npm => "package name", RepositoryFormat.Maven => "artifact (group:artifact)", RepositoryFormat.Docker => "image name", _ => "package id" };

    /// <summary>What the "version" of a package is called in this repository's format (a Docker image has a tag or a digest).</summary>
    public string VersionWord => Repository?.Format == RepositoryFormat.Docker ? "Tag or digest" : "Version";

    /// <summary>The label of the field that names a package (a Docker image is an image name).</summary>
    public string PackageLabel => Repository?.Format switch { RepositoryFormat.Npm => "Package name", RepositoryFormat.Maven => "Artifact (group:artifact)", RepositoryFormat.Docker => "Image name", _ => "Package id" };
}

/// <summary>
/// The fields the repository screen's forms post (every handler binds the same set). The upstream fields come with the <c>Remote.</c> prefix.
/// </summary>
public sealed class RepositoryEditForm
{
    /// <summary>Whether requests without credentials may read the repository.</summary>
    public bool AnonymousRead { get; set; }

    /// <summary>The upstream fields of a remote repository.</summary>
    public RemoteFormModel Remote { get; set; } = new();

    /// <summary>The package id to resolve in the preview.</summary>
    public string? PreviewPackageId { get; set; }

    /// <summary>The package version to resolve in the preview.</summary>
    public string? PreviewVersion { get; set; }

    /// <summary>The package id to purge from the cache.</summary>
    public string? PackageId { get; set; }

    /// <summary>The version to purge (blank = every version).</summary>
    public string? PackageVersion { get; set; }
}

/// <summary>What the delete-repository screen (<c>/admin/repositories/{name}/delete</c>) shows.</summary>
public sealed class RepositoryDeleteViewModel
{
    /// <summary>The repository's name (route).</summary>
    public string? Name { get; set; }

    /// <summary>The repository.</summary>
    public RepositorySummary? Repository { get; internal set; }

    /// <summary>The virtual repositories that list this repository as a member (the deletion is refused while there are any).</summary>
    public IReadOnlyList<string> ReferencedBy { get; internal set; } = new List<string>();

    /// <summary>Why the deletion was refused.</summary>
    public string? Error { get; internal set; }
}
