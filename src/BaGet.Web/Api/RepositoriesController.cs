using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Web.Api;

/// <summary>A repository in the API's lists.</summary>
/// <param name="Key">The repository key.</param>
/// <param name="Name">The name (URLs and storage paths use it; it never changes).</param>
/// <param name="Format">NuGet, Npm, Maven or Docker.</param>
/// <param name="Kind">Local, Remote or Virtual.</param>
/// <param name="AnonymousRead">Whether requests without credentials may read it.</param>
/// <param name="Created">When it was created (UTC).</param>
/// <param name="PackageCount">How many package versions (artifacts) it holds.</param>
/// <param name="FeedUrl">The address a client of its format is pointed at.</param>
public sealed record RepositoryDto(int Key, string Name, RepositoryFormat Format, RepositoryKind Kind, bool AnonymousRead, DateTime Created, int PackageCount, string FeedUrl);

/// <summary>A remote repository's upstream; the password is never returned.</summary>
public sealed record RemoteSettingsDto(
    string? Url,
    string Protocol,
    string? Username,
    bool HasPassword,
    int MetadataTtlSeconds,
    int NegativeTtlSeconds,
    int DownloadTimeoutSeconds,
    bool Offline,
    bool StoreLocally,
    bool IsMirror);

/// <summary>One member of a virtual repository.</summary>
public sealed record VirtualMemberDto(string Name, bool Exists);

/// <summary>A virtual repository's members.</summary>
/// <param name="Members">The direct members in resolution order.</param>
/// <param name="DeployRepository">The member that receives deployments, if any.</param>
/// <param name="DeployTarget">The local repository a deployment really reaches, if any.</param>
/// <param name="EffectiveMembers">The local and remote repositories that answer, in order, nested virtual members expanded.</param>
public sealed record VirtualDto(IReadOnlyList<VirtualMemberDto> Members, string? DeployRepository, string? DeployTarget, IReadOnlyList<string> EffectiveMembers);

/// <summary>A repository with what its kind adds.</summary>
public sealed record RepositoryDetailDto(
    RepositoryDto Repository,
    string? Project,
    RemoteSettingsDto? Remote,
    RemoteCacheOverview? Cache,
    VirtualDto? Virtual,
    IReadOnlyList<string> ReferencedBy);

/// <summary>An upstream, as the API takes it. Fields left out keep the defaults of a new remote repository.</summary>
public sealed class RemoteSettingsRequest
{
    /// <summary>The upstream URL (an npm registry's, Maven repository's or Docker registry's base; a NuGet V3 <c>index.json</c> or V2 feed).</summary>
    public string? Url { get; set; }

    /// <summary><c>V3</c> or <c>V2</c> for NuGet; the other formats fix it.</summary>
    public string? Protocol { get; set; }

    /// <summary>The HTTP Basic user name (a password with no user name is a bearer token).</summary>
    public string? Username { get; set; }

    /// <summary>The password or token; on update blank keeps the stored one.</summary>
    public string? Password { get; set; }

    /// <summary>On update: remove the stored password.</summary>
    public bool ClearPassword { get; set; }

    /// <summary>How long upstream answers are reused, in seconds.</summary>
    public int? MetadataTtlSeconds { get; set; }

    /// <summary>How long "not found" is remembered, in seconds.</summary>
    public int? NegativeTtlSeconds { get; set; }

    /// <summary>The download timeout in seconds.</summary>
    public int? DownloadTimeoutSeconds { get; set; }

    /// <summary>Never contact the upstream.</summary>
    public bool Offline { get; set; }

    /// <summary>Keep what is fetched (false = pass-through).</summary>
    public bool? StoreLocally { get; set; }

    internal RemoteRepositorySettings ToSettings(RemoteRepositorySettings? start = null)
    {
        var settings = start ?? new RemoteRepositorySettings();
        settings.Url = Url?.Trim();
        if (!string.IsNullOrWhiteSpace(Protocol))
        {
            if (!Enum.TryParse<RemoteProtocol>(Protocol, ignoreCase: true, out var protocol))
            {
                throw new ArgumentException($"Unknown protocol '{Protocol}'. Use {string.Join(", ", Enum.GetNames(typeof(RemoteProtocol)))}.");
            }

            settings.Protocol = protocol;
        }

        settings.Username = string.IsNullOrWhiteSpace(Username) ? null : Username.Trim();
        settings.Password = string.IsNullOrEmpty(Password) ? null : Password;
        if (MetadataTtlSeconds is int metadata) settings.MetadataTtlSeconds = metadata;
        if (NegativeTtlSeconds is int negative) settings.NegativeTtlSeconds = negative;
        if (DownloadTimeoutSeconds is int timeout) settings.DownloadTimeoutSeconds = timeout;
        settings.Offline = Offline;
        if (StoreLocally is bool store) settings.StoreLocally = store;

        return settings;
    }
}

/// <summary>A virtual repository's members, as the API takes them.</summary>
public sealed class VirtualRequest
{
    /// <summary>The member repositories' names in resolution order.</summary>
    public List<string> Members { get; set; } = new();

    /// <summary>The member that receives deployments, or null for none.</summary>
    public string? DeployRepository { get; set; }

    internal VirtualRepositorySettings ToSettings() => new() { Members = Members ?? new List<string>(), DeployRepository = string.IsNullOrWhiteSpace(DeployRepository) ? null : DeployRepository };
}

/// <summary>The body that creates a repository.</summary>
public sealed class CreateRepositoryRequest
{
    /// <summary>The name (lowercase letters, digits and hyphens).</summary>
    public string? Name { get; set; }

    /// <summary><c>NuGet</c> (the default), <c>Npm</c>, <c>Maven</c> or <c>Docker</c>.</summary>
    public string? Format { get; set; }

    /// <summary><c>Local</c> (the default), <c>Remote</c> or <c>Virtual</c>.</summary>
    public string? Kind { get; set; }

    /// <summary>Whether requests without credentials may read it. Default true, except a remote repository with credentials.</summary>
    public bool? AnonymousRead { get; set; }

    /// <summary>The upstream of a remote repository.</summary>
    public RemoteSettingsRequest? Remote { get; set; }

    /// <summary>The members of a virtual repository (they can be set later).</summary>
    public VirtualRequest? Virtual { get; set; }

    /// <summary>The fields a format asks for when creating a local repository (<c>/api/v1/formats</c> lists them).</summary>
    public Dictionary<string, string?>? Fields { get; set; }
}

/// <summary>A change of a repository's anonymous-read switch.</summary>
public sealed class PatchRepositoryRequest
{
    /// <summary>The new value.</summary>
    public bool? AnonymousRead { get; set; }
}

/// <summary>Asks where a package version would come from in a virtual repository.</summary>
public sealed class PreviewRequest
{
    /// <summary>The package id (<c>group:artifact</c> for Maven, the image name for Docker).</summary>
    public string? PackageId { get; set; }

    /// <summary>The version as text.</summary>
    public string? Version { get; set; }
}

/// <summary>What a preview found.</summary>
public sealed record PreviewDto(string PackageId, string Version, IReadOnlyList<PreviewStepDto> Steps, string? Answers);

/// <summary>One member's answer.</summary>
public sealed record PreviewStepDto(string Member, ResolutionOutcome Outcome);

/// <summary>Names a stored package of a remote repository to purge.</summary>
public sealed class PurgePackageRequest
{
    /// <summary>The package id (<c>group:artifact</c> for Maven, the image for Docker).</summary>
    public string? PackageId { get; set; }

    /// <summary>One version (a tag or digest for Docker); null purges every version.</summary>
    public string? Version { get; set; }
}

/// <summary>A format a repository can have.</summary>
public sealed record FormatDto(string Format, string DisplayName, IReadOnlyList<string> CreatableKinds, bool SupportsVirtual, string FeedPath, IReadOnlyList<FormatFieldDto> CreateFields);

/// <summary>A field a format asks for when creating a local repository.</summary>
public sealed record FormatFieldDto(string Name, string Label, string? Help, bool Required, IReadOnlyList<string>? Options);

/// <summary>
/// Repositories: list, create (local, remote, virtual; any format), switch anonymous read, delete, and the kind-specific parts
/// (a remote repository's upstream, cache and purge; a virtual repository's members and resolution preview). The services are the
/// ones the Repositories, RepositoryEdit and RepositoryDelete pages call, and the rules are theirs: a remote repository's create,
/// save, test and purge ask for the owner's password as those pages do; deleting asks for nothing, as RepositoryDelete does.
/// </summary>
[Route(ApiCatalog.RootTemplate + "/repositories")]
public class RepositoriesController : AdminApiController
{
    private readonly IRepositoryService _repositories;
    private readonly IRepositoryAdministration _administration;
    private readonly FormatRegistry _formats;

    /// <summary>Creates the controller.</summary>
    public RepositoriesController(IRepositoryService repositories, IRepositoryAdministration administration, FormatRegistry formats)
    {
        _repositories = repositories;
        _administration = administration;
        _formats = formats;
    }

    /// <summary>Lists repositories, optionally by format, kind or a name fragment.</summary>
    [HttpGet]
    [ApiOperation("List repositories (filter by format, kind or name)", "/Admin/Repositories", Response = typeof(RepositoryDto), PagedList = true)]
    public async Task<IActionResult> List(string? format, string? kind, string? q, int? page, int? pageSize)
    {
        var all = await _administration.ListAsync(Cancellation);
        var filtered = RepositorySummaryFilter.Apply(all, format, kind, q)
            .Select(Dto)
            .ToList();

        return RespondPage(filtered, page, pageSize);
    }

    /// <summary>The formats and what each asks for.</summary>
    [HttpGet("/api/v1/formats")]
    [ApiOperation("List the package formats and the kinds and fields each offers", "/Admin/Formats", Response = typeof(FormatDto))]
    public IActionResult Formats()
        => Respond(_formats.Modules.Select(m => new FormatDto(
            m.Format.ToString(),
            m.DisplayName,
            m.CreatableKinds.Select(k => k.ToString()).ToList(),
            m.SupportsVirtual,
            m.FeedPath,
            m.CreateFields.Select(f => new FormatFieldDto(f.Name, f.Label, f.Help, f.Required, f.Options)).ToList())).ToList());

    /// <summary>One repository with its kind's details.</summary>
    [HttpGet("{name}")]
    [ApiOperation("Show a repository with its upstream, cache or members", "/Admin/RepositoryEdit", Response = typeof(RepositoryDetailDto))]
    public async Task<IActionResult> Get(string name)
    {
        var summary = await _administration.FindAsync(name, Cancellation);
        if (summary == null) return ApiProblems.NotFound($"repository '{name}'");

        RemoteSettingsDto? remote = null;
        RemoteCacheOverview? cache = null;
        VirtualDto? virtualDto = null;
        var virtuals = Service<IVirtualRepositoryAdministration>();
        if (summary.Kind == RepositoryKind.Remote)
        {
            var remotes = Service<IRemoteRepositoryAdministration>();
            var detail = await remotes.FindAsync(name, Cancellation);
            if (detail != null)
            {
                remote = ToDto(detail);
                cache = await remotes.GetCacheAsync(name, Cancellation);
            }
        }
        else if (summary.Kind == RepositoryKind.Virtual)
        {
            var detail = await virtuals.FindAsync(name, Cancellation);
            if (detail != null)
            {
                virtualDto = new VirtualDto(
                    detail.Members.Select(m => new VirtualMemberDto(m.Name, m.Exists)).ToList(),
                    detail.DeployRepository,
                    detail.DeployTarget?.Name,
                    detail.EffectiveMembers.Select(m => m.Name).ToList());
            }
        }

        string? project = null;
        var info = await _repositories.FindAsync(name, Cancellation);
        if (info != null) project = (await Service<IAccessEvaluator>().DescribeAsync(info, Cancellation)).Project;

        var referencedBy = summary.Kind == RepositoryKind.Virtual ? Array.Empty<string>() : await virtuals.ReferencedByAsync(name, Cancellation);

        return Respond(new RepositoryDetailDto(Dto(summary), project, remote, cache, virtualDto, referencedBy));
    }

    /// <summary>Who may do what in the repository (the effective-access panel of RepositoryEdit).</summary>
    [HttpGet("{name}/access")]
    [ApiOperation("Show who holds which action in a repository, and why", "/Admin/RepositoryEdit", Response = typeof(RepositoryAccessSummary))]
    public async Task<IActionResult> Access(string name)
    {
        var info = await _repositories.FindAsync(name, Cancellation);
        if (info == null) return ApiProblems.NotFound($"repository '{name}'");

        return Respond(await Service<IAccessEvaluator>().DescribeAsync(info, Cancellation));
    }

    /// <summary>Creates a repository.</summary>
    [HttpPost]
    [ApiOperation("Create a local, remote or virtual repository of any format (remote asks for the password)", "/Admin/Repositories", Confirm = true, Response = typeof(RepositoryDto), Status = 201)]
    public async Task<IActionResult> Create([FromBody] CreateRepositoryRequest request)
    {
        ThrowIfClosed();

        var name = request.Name?.Trim();
        if (!RepositoryNames.IsValid(name, out var invalid)) return ApiProblems.BadRequest(invalid ?? "The name is not valid.");

        if (!TryParse<RepositoryFormat>(request.Format, RepositoryFormat.NuGet, out var format)) return ApiProblems.BadRequest($"Unknown format '{request.Format}'.");
        if (!TryParse<RepositoryKind>(request.Kind, RepositoryKind.Local, out var kind)) return ApiProblems.BadRequest($"Unknown kind '{request.Kind}'.");

        var module = _formats.Find(format);
        if (module == null) return ApiProblems.BadRequest($"No module serves the format {format}.");

        switch (kind)
        {
            case RepositoryKind.Remote:
            {
                if (!module.CreatableKinds.Contains(RepositoryKind.Remote)) return ApiProblems.BadRequest($"{module.DisplayName} has no remote repositories.");
                if (request.Remote == null) return ApiProblems.BadRequest("A remote repository needs 'remote' (its upstream).");
                var refusal = await RequireConfirmationAsync();
                if (refusal != null) return refusal;

                var settings = request.Remote.ToSettings();
                var anonymous = request.AnonymousRead ?? !settings.HasCredentials;
                await _repositories.CreateAsync(name!, format, RepositoryKind.Remote, settings, anonymous, Cancellation);
                break;
            }

            case RepositoryKind.Virtual:
                if (!module.SupportsVirtual) return ApiProblems.BadRequest($"{module.DisplayName} has no virtual repositories.");
                await Service<IVirtualRepositoryAdministration>().CreateAsync(
                    name!, request.Virtual?.ToSettings() ?? new VirtualRepositorySettings(), Cancellation, Actor, request.AnonymousRead ?? true, format);
                break;

            default:
                if (format == RepositoryFormat.NuGet)
                {
                    await _repositories.CreateAsync(name!, RepositoryFormat.NuGet, RepositoryKind.Local, Cancellation);
                    if (request.AnonymousRead == false) await _administration.SetAnonymousReadAsync(name!, false, Cancellation, Actor);
                }
                else
                {
                    if (!module.CreatableKinds.Contains(RepositoryKind.Local)) return ApiProblems.BadRequest($"{module.DisplayName} has no local repositories.");
                    await _repositories.CreateAsync(name!, format, RepositoryKind.Local, request.Fields ?? new Dictionary<string, string?>(), request.AnonymousRead ?? true, Cancellation);
                }

                break;
        }

        var created = await _administration.FindAsync(name, Cancellation);

        return Respond(Dto(created!), 201, $"{ApiCatalog.Root}/repositories/{Uri.EscapeDataString(name!)}");
    }

    /// <summary>Changes a repository's anonymous-read switch.</summary>
    [HttpPatch("{name}")]
    [ApiOperation("Switch a repository's anonymous read (a remote repository asks for the password)", "/Admin/RepositoryEdit", Confirm = true, Response = typeof(RepositoryDto))]
    public async Task<IActionResult> Patch(string name, [FromBody] PatchRepositoryRequest request)
    {
        ThrowIfClosed();
        var summary = await _administration.FindAsync(name, Cancellation);
        if (summary == null) return ApiProblems.NotFound($"repository '{name}'");
        if (request.AnonymousRead == null) return ApiProblems.BadRequest("Nothing to change: send 'anonymousRead'.");

        if (summary.Kind == RepositoryKind.Remote)
        {
            var refusal = await RequireConfirmationAsync();
            if (refusal != null) return refusal;
        }

        await _administration.SetAnonymousReadAsync(name, request.AnonymousRead.Value, Cancellation, Actor);

        return Respond(Dto((await _administration.FindAsync(name, Cancellation))!));
    }

    /// <summary>Deletes an empty repository.</summary>
    [HttpDelete("{name}")]
    [ApiOperation("Delete an empty repository (refused for default, a repository with packages, or a virtual member)", "/Admin/RepositoryDelete", Status = 204)]
    public async Task<IActionResult> Delete(string name)
    {
        ThrowIfClosed();
        if (await _administration.FindAsync(name, Cancellation) == null) return ApiProblems.NotFound($"repository '{name}'");

        var referencedBy = await Service<IVirtualRepositoryAdministration>().ReferencedByAsync(name, Cancellation);
        if (referencedBy.Count > 0) throw new RepositoryReferencedException(name, referencedBy);

        await _administration.DeleteAsync(name, Cancellation);

        return Done();
    }

    /// <summary>Replaces a remote repository's upstream.</summary>
    [HttpPut("{name}/remote")]
    [ApiOperation("Change a remote repository's upstream and settings", "/Admin/RepositoryEdit", Confirm = true, Response = typeof(RemoteSettingsDto))]
    public async Task<IActionResult> UpdateRemote(string name, [FromBody] RemoteSettingsRequest request)
    {
        ThrowIfClosed();
        var remotes = Service<IRemoteRepositoryAdministration>();
        if (await remotes.FindAsync(name, Cancellation) == null) return ApiProblems.NotFound($"remote repository '{name}'");
        var refusal = await RequireConfirmationAsync();
        if (refusal != null) return refusal;

        await remotes.UpdateAsync(name, request.ToSettings(), request.ClearPassword, Cancellation, Actor);

        return Respond(ToDto((await remotes.FindAsync(name, Cancellation))!));
    }

    /// <summary>Asks an upstream whether it answers; saves nothing.</summary>
    [HttpPost("{name}/remote/test")]
    [ApiOperation("Test a remote repository's upstream with unsaved settings (saves nothing)", "/Admin/RepositoryEdit", Confirm = true, Response = typeof(RemoteConnectionResult))]
    public async Task<IActionResult> TestRemote(string name, [FromBody] RemoteSettingsRequest request)
    {
        var remotes = Service<IRemoteRepositoryAdministration>();
        var detail = await remotes.FindAsync(name, Cancellation);
        if (detail == null) return ApiProblems.NotFound($"remote repository '{name}'");
        var refusal = await RequireConfirmationAsync();
        if (refusal != null) return refusal;

        var settings = request.ToSettings();
        if (detail.IsMirror)
        {
            settings.Url = detail.Settings.Url;
            settings.Protocol = detail.Settings.Protocol;
        }

        return Respond(await remotes.TestConnectionAsync(name, settings, Cancellation));
    }

    /// <summary>What a remote repository stores and remembers.</summary>
    [HttpGet("{name}/remote/cache")]
    [ApiOperation("Show what a remote repository stores and remembers", "/Admin/RepositoryEdit", Response = typeof(RemoteCacheOverview))]
    public async Task<IActionResult> Cache(string name)
    {
        var remotes = Service<IRemoteRepositoryAdministration>();
        if (await remotes.FindAsync(name, Cancellation) == null) return ApiProblems.NotFound($"remote repository '{name}'");

        return Respond(await remotes.GetCacheAsync(name, Cancellation));
    }

    /// <summary>Deletes everything a remote repository stored.</summary>
    [HttpPost("{name}/remote/purge")]
    [ApiOperation("Purge a remote repository's cache (deletes what it stored)", "/Admin/RepositoryEdit", Confirm = true, Response = typeof(PurgeResult))]
    public async Task<IActionResult> Purge(string name)
    {
        ThrowIfClosed();
        var remotes = Service<IRemoteRepositoryAdministration>();
        if (await remotes.FindAsync(name, Cancellation) == null) return ApiProblems.NotFound($"remote repository '{name}'");
        var refusal = await RequireConfirmationAsync();
        if (refusal != null) return refusal;

        return Respond(await remotes.PurgeAsync(name, Cancellation, Actor));
    }

    /// <summary>Deletes one cached package of a remote repository.</summary>
    [HttpPost("{name}/remote/purge-package")]
    [ApiOperation("Purge one cached package of a remote repository", "/Admin/RepositoryEdit", Confirm = true, Response = typeof(PurgeResult))]
    public async Task<IActionResult> PurgePackage(string name, [FromBody] PurgePackageRequest request)
    {
        ThrowIfClosed();
        var remotes = Service<IRemoteRepositoryAdministration>();
        var found = await remotes.FindAsync(name, Cancellation);
        if (found == null) return ApiProblems.NotFound($"remote repository '{name}'");
        var refusal = await RequireConfirmationAsync();
        if (refusal != null) return refusal;

        var id = request.PackageId?.Trim();
        if (string.IsNullOrEmpty(id)) return ApiProblems.BadRequest("'packageId' is required.");

        var versionText = string.IsNullOrWhiteSpace(request.Version) ? null : request.Version.Trim();
        switch (found.Repository.Format)
        {
            case RepositoryFormat.Maven:
                return Respond(await remotes.PurgeArtifactAsync(name, id, versionText, Cancellation, Actor));
            case RepositoryFormat.Docker:
                return Respond(await remotes.PurgeImageAsync(name, id, versionText, Cancellation, Actor));
            default:
                NuGet.Versioning.NuGetVersion? version = null;
                if (versionText != null)
                {
                    if (!NuGet.Versioning.NuGetVersion.TryParse(versionText, out version)) return ApiProblems.BadRequest($"'{versionText}' is not a valid version.");
                }

                return Respond(await remotes.PurgePackageAsync(name, id, version, Cancellation, Actor));
        }
    }

    /// <summary>Replaces a virtual repository's members and deploy repository.</summary>
    [HttpPut("{name}/virtual")]
    [ApiOperation("Replace a virtual repository's members, their order and its deploy repository", "/Admin/RepositoryEdit", Response = typeof(VirtualDto))]
    public async Task<IActionResult> UpdateVirtual(string name, [FromBody] VirtualRequest request)
    {
        ThrowIfClosed();
        var virtuals = Service<IVirtualRepositoryAdministration>();
        if (await virtuals.FindAsync(name, Cancellation) == null) return ApiProblems.NotFound($"virtual repository '{name}'");

        await virtuals.UpdateAsync(name, request.ToSettings(), Cancellation, Actor);
        var detail = (await virtuals.FindAsync(name, Cancellation))!;

        return Respond(new VirtualDto(
            detail.Members.Select(m => new VirtualMemberDto(m.Name, m.Exists)).ToList(),
            detail.DeployRepository,
            detail.DeployTarget?.Name,
            detail.EffectiveMembers.Select(m => m.Name).ToList()));
    }

    /// <summary>Which member would answer for a package version.</summary>
    [HttpPost("{name}/virtual/preview")]
    [ApiOperation("Show where a package version would come from in a virtual repository (fetches and stores nothing)", "/Admin/RepositoryEdit", Response = typeof(PreviewDto))]
    public async Task<IActionResult> Preview(string name, [FromBody] PreviewRequest request)
    {
        var virtuals = Service<IVirtualRepositoryAdministration>();
        var detail = await virtuals.FindAsync(name, Cancellation);
        if (detail == null) return ApiProblems.NotFound($"virtual repository '{name}'");

        var id = request.PackageId?.Trim();
        if (string.IsNullOrEmpty(id)) return ApiProblems.BadRequest("'packageId' is required.");
        if (string.IsNullOrWhiteSpace(request.Version)) return ApiProblems.BadRequest("'version' is required.");

        var preview = await virtuals.PreviewAsync(name, id, request.Version.Trim(), Cancellation);

        return Respond(new PreviewDto(
            preview.PackageId,
            preview.Version,
            preview.Steps.Select(s => new PreviewStepDto(s.Member.Name, s.Outcome)).ToList(),
            preview.Answers?.Name));
    }

    private RepositoryDto Dto(RepositorySummary r)
        => new(r.Key, r.Name, r.Format, r.Kind, r.AnonymousRead, r.Created, r.PackageCount, FeedUrl(r.Name, r.Format));

    private string FeedUrl(string name, RepositoryFormat format)
    {
        var feedPath = _formats.Find(format)?.FeedPath ?? "v3/index.json";
        var generator = Service<IUrlGenerator>();
        var root = generator.GetServiceIndexUrl() ?? string.Empty;
        const string index = "v3/index.json";
        var prefix = root.EndsWith(index, StringComparison.Ordinal) ? root.Substring(0, root.Length - index.Length) : root.TrimEnd('/') + "/";

        return feedPath.Contains("{repo}", StringComparison.Ordinal)
            ? prefix + feedPath.Replace("{repo}", name)
            : (name == Repository.DefaultName ? prefix : $"{prefix}r/{name}/") + feedPath;
    }

    private static RemoteSettingsDto ToDto(RemoteRepositoryDetail d)
        => new(d.Settings.Url, d.Settings.Protocol.ToString(), d.Settings.Username, d.HasPassword, d.Settings.MetadataTtlSeconds, d.Settings.NegativeTtlSeconds,
            d.Settings.DownloadTimeoutSeconds, d.Settings.Offline, d.Settings.StoreLocally, d.IsMirror);

    private static bool TryParse<T>(string? text, T fallback, out T value) where T : struct, Enum
    {
        value = fallback;

        return string.IsNullOrWhiteSpace(text) || Enum.TryParse(text, ignoreCase: true, out value);
    }
}
