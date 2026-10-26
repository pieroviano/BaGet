using System;
using System.Collections.Generic;
using System.Linq;
using BaGet.Core;
using BaGet.Identity.Pages.Admin;

namespace BaGet.Identity.ViewModels;

/// <summary>A package format a repository can be created for, with the extra fields its create form asks (the JSON form of an <see cref="IFormatModule"/>).</summary>
/// <param name="Format">The format.</param>
/// <param name="DisplayName">Its name for people (<c>npm</c>, <c>Maven</c>).</param>
/// <param name="CreateFields">What the administrator fills in to create a local repository of it, besides the name.</param>
public sealed record RepositoryFormatChoice(RepositoryFormat Format, string DisplayName, IReadOnlyList<FormatCreateField> CreateFields)
{
    /// <summary>The choice for <paramref name="module"/>.</summary>
    public static RepositoryFormatChoice Of(IFormatModule module)
        => new(module.Format, module.DisplayName, module.CreateFields);
}

/// <summary>
/// What the repositories screen (<c>/admin/repositories</c>) shows: the repositories with their feed addresses, the list filter and
/// the create forms. The posted fields of the create forms (<see cref="Name"/>, <see cref="Remote"/>) live here too, so a refused
/// creation shows again what was typed (the typed upstream password excepted: it is never sent back).
/// </summary>
public sealed class RepositoriesViewModel : ConfirmationModel, IFlashMessage
{
    private IReadOnlyDictionary<string, string> _feeds = new Dictionary<string, string>();
    private IReadOnlyDictionary<string, string> _bases = new Dictionary<string, string>();
    private IReadOnlyDictionary<string, IReadOnlyList<FormatLink>> _links = new Dictionary<string, IReadOnlyList<FormatLink>>();

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
    public IReadOnlyList<RepositoryFormatChoice> AllFormats { get; internal set; } = Array.Empty<RepositoryFormatChoice>();

    /// <summary>The formats a remote repository can be created for.</summary>
    public IReadOnlyList<RepositoryFormatChoice> RemoteFormats { get; internal set; } = Array.Empty<RepositoryFormatChoice>();

    /// <summary>The formats a virtual repository can be created for.</summary>
    public IReadOnlyList<RepositoryFormatChoice> VirtualFormats { get; internal set; } = Array.Empty<RepositoryFormatChoice>();

    /// <summary>The formats other than NuGet that can be created here, with their form fields.</summary>
    public IReadOnlyList<RepositoryFormatChoice> OtherFormats { get; internal set; } = Array.Empty<RepositoryFormatChoice>();

    /// <summary>The virtual repositories that have a deploy repository, so take uploads (a remote one takes none).</summary>
    public IReadOnlyList<string> Deployable { get; internal set; } = Array.Empty<string>();

    /// <summary>The feed address of each repository, by repository name.</summary>
    public IReadOnlyDictionary<string, string> Feeds { get => _feeds; internal set => _feeds = value; }

    /// <summary>The base address of each repository (where its format's routes start, ends with a slash), by repository name.</summary>
    public IReadOnlyDictionary<string, string> BaseUrls { get => _bases; internal set => _bases = value; }

    /// <summary>The links its format contributes for each repository (browse, upload...), without the upload link where it takes none, by repository name.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<FormatLink>> Links { get => _links; internal set => _links = value; }

    /// <summary>The feed address of <paramref name="repository"/>, by its format's <see cref="IFormatModule.FeedPath"/>.</summary>
    public string FeedUrl(RepositorySummary repository) => _feeds.TryGetValue(repository.Name, out var url) ? url : string.Empty;

    /// <summary>The base address of <paramref name="repository"/>, which a link given by a path (<see cref="FormatLink.Path"/>) is appended to.</summary>
    public string BaseUrl(RepositorySummary repository) => _bases.TryGetValue(repository.Name, out var url) ? url : string.Empty;

    /// <summary>The links its format contributes for <paramref name="repository"/> (browse, upload...), without the upload link where it takes none.</summary>
    public IReadOnlyList<FormatLink> LinksOf(RepositorySummary repository)
        => _links.TryGetValue(repository.Name, out var links) ? links : Array.Empty<FormatLink>();
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
