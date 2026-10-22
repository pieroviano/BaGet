using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace BaGet.Identity.Pages.Admin;

/// <summary>
/// Shows a repository's feed address and the access model's view of it (who effectively holds each action) and edits what can
/// change: whether anonymous requests may read it. The name never
/// changes (it is in URLs and storage paths), and neither does the format or kind. A remote repository also shows and
/// edits its upstream (URL, protocol, credentials, cache periods, offline, store locally), tests the connection with
/// the unsaved values, shows its cache and purges it (whole, or one package). Saving and purging are refused with 503
/// while the <see cref="MaintenanceGate"/> is closed. Changing, purging and testing the connection of a remote
/// repository ask for the administrator's password again (a test saves nothing, but makes the server send a request to a
/// typed URL, with the stored password when the origin is unchanged). A virtual repository shows and
/// edits its members (add, remove, move up or down), its deploy repository, and previews where a package version would come
/// from (<see cref="IVirtualRepositoryAdministration.PreviewAsync(string, string, string, CancellationToken)"/>); those edits are saved at once, refused with 503 while
/// the gate is closed.
/// </summary>
public class RepositoryEditModel : RepositoryPageModel
{
    /// <summary>The repository's name (route).</summary>
    [BindProperty(SupportsGet = true)]
    public string? Name { get; set; }

    /// <summary>Whether requests without credentials may read the repository.</summary>
    [BindProperty]
    public bool AnonymousRead { get; set; }

    /// <summary>The repository.</summary>
    public RepositorySummary? Repository { get; private set; }

    /// <summary>The upstream fields of a remote repository.</summary>
    [BindProperty]
    public RemoteFormModel Remote { get; set; } = new();

    /// <summary>The remote repository's upstream, null for the other kinds.</summary>
    public RemoteRepositoryDetail? RemoteDetail { get; private set; }

    /// <summary>The settings of a local Maven repository (policy, redeploy, snapshot builds kept), null for the other repositories.</summary>
    public MavenRepositorySettings? MavenSettings { get; private set; }

    /// <summary>What a remote repository stores and remembers.</summary>
    public RemoteCacheOverview? Cache { get; private set; }

    /// <summary>The virtual repository's members and deploy repository, null for the other kinds.</summary>
    public VirtualRepositoryDetail? VirtualDetail { get; private set; }

    /// <summary>The repositories that can still be added to a virtual repository.</summary>
    public IReadOnlyList<RepositoryInfo> MemberCandidates { get; private set; } = Array.Empty<RepositoryInfo>();

    /// <summary>The virtual repositories that list this repository as a member.</summary>
    public IReadOnlyList<string> ReferencedBy { get; private set; } = Array.Empty<string>();

    /// <summary>The package id to resolve in the preview.</summary>
    [BindProperty]
    public string? PreviewPackageId { get; set; }

    /// <summary>The package version to resolve in the preview.</summary>
    [BindProperty]
    public string? PreviewVersion { get; set; }

    /// <summary>Where the previewed package version would come from.</summary>
    public ResolutionPreview? Preview { get; private set; }

    /// <summary>The outcome of "Test connection".</summary>
    public RemoteConnectionResult? TestResult { get; private set; }

    /// <summary>The outcome of the last action.</summary>
    [TempData]
    public string? Message { get; set; }

    /// <summary>The package id to purge from the cache.</summary>
    [BindProperty]
    public string? PackageId { get; set; }

    /// <summary>The version to purge (blank = every version).</summary>
    [BindProperty]
    public string? PackageVersion { get; set; }

    /// <summary>
    /// Who effectively holds Read, Annotate, Deploy, Delete and Manage in the repository, from the access model; null for
    /// the default repository when the database has no access model.
    /// </summary>
    public RepositoryAccessSummary? Access { get; private set; }

    /// <summary>The page of the repository's format that lists its packages (<c>/Index</c> for NuGet, <c>/NpmPackages</c> for npm).</summary>
    public string BrowsePage => HttpContext.RequestServices.GetRequiredService<FormatRegistry>().Find(Repository?.Format ?? RepositoryFormat.NuGet)
        ?.RepositoryLinks(new RepositoryInfo(Repository?.Key ?? 0, Repository?.Name ?? string.Empty, Repository?.Format ?? RepositoryFormat.NuGet, Repository?.Kind ?? RepositoryKind.Local))
        .FirstOrDefault(l => l.Id == "browse" && l.Page != null)?.Page ?? "/Index";

    /// <summary>The name of what a package is called in this repository's format (shown in the labels).</summary>
    public string PackageWord => Repository?.Format switch { RepositoryFormat.Npm => "package name", RepositoryFormat.Maven => "artifact (group:artifact)", RepositoryFormat.Docker => "image name", _ => "package id" };

    /// <summary>The repository's feed address.</summary>
    public string FeedUrl => RepositoryFeedUrl.For(
        HttpContext,
        Repository?.Name ?? string.Empty,
        HttpContext.RequestServices.GetRequiredService<FormatRegistry>().Find(Repository?.Format ?? RepositoryFormat.NuGet)?.FeedPath ?? "v3/index.json");

    /// <summary>Shows the repository.</summary>
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!await LoadAsync(cancellationToken)) return NotFound();

        AnonymousRead = Repository!.AnonymousRead;
        if (RemoteDetail != null) Remote = RemoteFormModel.From(RemoteDetail);

        return Page();
    }

    // Loads the repository, its access summary and, for a remote one, its upstream and cache. False: no such repository.
    private async Task<bool> LoadAsync(CancellationToken cancellationToken)
    {
        var services = HttpContext.RequestServices;
        await LoadConfirmationAsync();
        Repository = await services.GetRequiredService<IRepositoryAdministration>().FindAsync(Name, cancellationToken);
        if (Repository == null) return false;

        var info = (await services.GetRequiredService<IRepositoryService>().ListAsync(cancellationToken))
            .FirstOrDefault(r => r.Name == Repository.Name);
        if (info != null)
        {
            Access = await services.GetRequiredService<IAccessEvaluator>().DescribeAsync(info, cancellationToken);
        }

        if (Repository.Kind == RepositoryKind.Remote)
        {
            var remotes = services.GetRequiredService<IRemoteRepositoryAdministration>();
            RemoteDetail = await remotes.FindAsync(Name, cancellationToken);
            if (RemoteDetail != null) Cache = await remotes.GetCacheAsync(Name!, cancellationToken);
        }
        else if (Repository.Kind == RepositoryKind.Virtual)
        {
            var virtuals = services.GetRequiredService<IVirtualRepositoryAdministration>();
            VirtualDetail = await virtuals.FindAsync(Name, cancellationToken);
            var members = VirtualDetail?.Members.Select(m => m.Name).ToHashSet() ?? new System.Collections.Generic.HashSet<string>();
            MemberCandidates = (await virtuals.CandidatesAsync(Name, cancellationToken)).Where(c => !members.Contains(c.Name)).ToList();
        }

        if (Repository.Kind != RepositoryKind.Virtual)
        {
            ReferencedBy = await services.GetRequiredService<IVirtualRepositoryAdministration>().ReferencedByAsync(Repository.Name, cancellationToken);
        }

        if (Repository.Format == RepositoryFormat.Maven && Repository.Kind == RepositoryKind.Local)
        {
            MavenSettings = await services.GetRequiredService<IMavenRepositoryAdministration>().GetSettingsAsync(Repository.Name, cancellationToken);
        }

        if (Repository.IsDefault)
        {
            MirrorRepository = await services.GetRequiredService<IRemoteRepositoryAdministration>().FindAsync(BaGet.Core.Repository.MirrorName, cancellationToken);
            MirrorActive = MirrorRepository?.IsMirror == true;
        }

        return true;
    }

    /// <summary>For the default repository: the remote repository it fetches through, when <c>Mirror:Enabled</c>.</summary>
    public RemoteRepositoryDetail? MirrorRepository { get; private set; }

    /// <summary>Whether <c>Mirror:Enabled</c> makes the default repository fetch through <see cref="MirrorRepository"/>.</summary>
    public bool MirrorActive { get; private set; }

    /// <summary>Saves the repository.</summary>
    public async Task<IActionResult> OnPostAsync(string? password, CancellationToken cancellationToken)
    {
        HttpContext.RequestServices.GetService<MaintenanceGate>()?.ThrowIfClosed();

        var administration = HttpContext.RequestServices.GetRequiredService<IRepositoryAdministration>();
        if (await administration.FindAsync(Name, cancellationToken) == null) return NotFound();

        var summary = await administration.FindAsync(Name, cancellationToken);
        if (summary!.Kind == RepositoryKind.Remote)
        {
            if (!await ConfirmedAsync(password)) return await RefuseAsync(NotConfirmed, cancellationToken);

            try
            {
                await HttpContext.RequestServices.GetRequiredService<IRemoteRepositoryAdministration>()
                    .UpdateAsync(Name!, Remote.ToSettings(), Remote.ClearPassword, cancellationToken, User.Identity?.Name);
            }
            catch (Exception e) when (e is ArgumentException or InvalidOperationException)
            {
                return await RefuseAsync(e.Message, cancellationToken);
            }
        }

        await administration.SetAnonymousReadAsync(Name!, AnonymousRead, cancellationToken, User.Identity?.Name);
        TempData["Message"] = $"Repository {Name} was saved.";

        return RedirectToPage("/Admin/Repositories");
    }

    /// <summary>
    /// Saves the policy of a local Maven repository: which versions it accepts, whether a release may be redeployed and how many
    /// snapshot builds are kept. Audited; a stricter policy applies to later deploys and deletes nothing.
    /// </summary>
    public async Task<IActionResult> OnPostMavenSettingsAsync(string? policy, string? redeploy, string? snapshotBuilds, CancellationToken cancellationToken)
    {
        HttpContext.RequestServices.GetService<MaintenanceGate>()?.ThrowIfClosed();

        MavenRepositorySettings settings;
        try
        {
            settings = MavenRepositorySettings.FromFields(new Dictionary<string, string?> { ["policy"] = policy, ["redeploy"] = redeploy, ["snapshotBuilds"] = snapshotBuilds });
        }
        catch (ArgumentException e)
        {
            return await RefuseAsync(e.Message, cancellationToken);
        }

        if (!await HttpContext.RequestServices.GetRequiredService<IMavenRepositoryAdministration>().SaveSettingsAsync(Name!, settings, User.Identity?.Name, cancellationToken))
        {
            return NotFound();
        }

        TempData["Message"] = $"The settings of repository {Name} were saved.";

        return RedirectToPage(new { name = Name });
    }

    /// <summary>Asks the upstream with the posted, unsaved values (a blank password means the stored one) and saves nothing.</summary>
    public async Task<IActionResult> OnPostTestAsync(string? password, CancellationToken cancellationToken)
    {
        if (!await LoadAsync(cancellationToken)) return NotFound();
        if (RemoteDetail == null) return NotFound();

        // The probe makes this server send a request to a URL the administrator typed (and the stored password, if the
        // origin is unchanged): it asks for the password again, like the changes that hand out credentials.
        if (!await ConfirmedAsync(password)) return await RefuseAsync(NotConfirmed, cancellationToken);

        AnonymousRead = Repository!.AnonymousRead;
        var settings = Remote.ToSettings();
        if (RemoteDetail.IsMirror)
        {
            settings.Url = RemoteDetail.Settings.Url;
            settings.Protocol = RemoteDetail.Settings.Protocol;
        }

        TestResult = await HttpContext.RequestServices.GetRequiredService<IRemoteRepositoryAdministration>()
            .TestConnectionAsync(Name, settings, cancellationToken);
        Remote.HasPassword = RemoteDetail.HasPassword;
        Remote.EndpointFromMirror = RemoteDetail.IsMirror;
        Remote.Format = RemoteDetail.Repository.Format;
        Remote.Password = null;

        return Page();
    }

    /// <summary>Deletes every package the remote repository stored and forgets what it remembered of its upstream.</summary>
    public async Task<IActionResult> OnPostPurgeAsync(string? password, CancellationToken cancellationToken)
    {
        HttpContext.RequestServices.GetService<MaintenanceGate>()?.ThrowIfClosed();

        var remotes = HttpContext.RequestServices.GetRequiredService<IRemoteRepositoryAdministration>();
        if (await remotes.FindAsync(Name, cancellationToken) == null) return NotFound();
        if (!await ConfirmedAsync(password)) return await RefuseAsync(NotConfirmed, cancellationToken);

        var result = await remotes.PurgeAsync(Name!, cancellationToken, User.Identity?.Name);
        TempData["Message"] = $"The cache of {Name} was purged: {result.Packages} package version(s) deleted, {result.CachedAnswers} remembered answer(s) dropped.";

        return RedirectToPage(new { name = Name });
    }

    /// <summary>Deletes one cached package (every version, or the one given) and forgets what was remembered about it.</summary>
    public async Task<IActionResult> OnPostPurgePackageAsync(string? password, CancellationToken cancellationToken)
    {
        HttpContext.RequestServices.GetService<MaintenanceGate>()?.ThrowIfClosed();

        var remotes = HttpContext.RequestServices.GetRequiredService<IRemoteRepositoryAdministration>();
        var found = await remotes.FindAsync(Name, cancellationToken);
        if (found == null) return NotFound();
        if (!await ConfirmedAsync(password)) return await RefuseAsync(NotConfirmed, cancellationToken);

        var id = PackageId?.Trim();
        if (string.IsNullOrEmpty(id))
        {
            return await RefuseAsync("Enter the id of the package to purge.", cancellationToken);
        }

        // A Maven version is any version folder name, not always a NuGet version (5.3.20.RELEASE): it travels as text.
        var isMaven = found.Repository.Format == RepositoryFormat.Maven;
        NuGet.Versioning.NuGetVersion? version = null;
        string? versionText = string.IsNullOrWhiteSpace(PackageVersion) ? null : PackageVersion.Trim();
        if (!isMaven && versionText != null)
        {
            if (!NuGet.Versioning.NuGetVersion.TryParse(versionText, out version))
            {
                return await RefuseAsync($"'{PackageVersion}' is not a valid version.", cancellationToken);
            }

            versionText = version.ToNormalizedString();
        }

        PurgeResult result;
        try
        {
            result = isMaven
                ? await remotes.PurgeArtifactAsync(Name!, id, versionText, cancellationToken, User.Identity?.Name)
                : await remotes.PurgePackageAsync(Name!, id, version, cancellationToken, User.Identity?.Name);
        }
        catch (ArgumentException e)
        {
            // A name the repository's format would not accept (an npm name must be lower case...).
            return await RefuseAsync(e.Message, cancellationToken);
        }

        var shown = versionText == null ? string.Empty : " " + versionText;
        TempData["Message"] = result.Packages == 0 && result.CachedAnswers == 0
            ? $"Nothing was cached for {id}{shown}."
            : $"{id}{shown} was purged: {result.Packages} {(isMaven ? "file(s)" : "package version(s)")} deleted, {result.CachedAnswers} remembered answer(s) dropped.";

        return RedirectToPage(new { name = Name });
    }

    /// <summary>Adds a repository at the end of the virtual repository's members.</summary>
    public Task<IActionResult> OnPostAddMemberAsync(string? member, CancellationToken cancellationToken)
        => ChangeMembersAsync(
            settings =>
            {
                if (string.IsNullOrEmpty(member)) return "Choose the repository to add.";
                if (settings.Members.Contains(member!)) return $"'{member}' is already a member.";

                settings.Members.Add(member!);

                return null;
            },
            $"{member} was added.",
            cancellationToken);

    /// <summary>Removes a member (and the deploy repository too when it was that member).</summary>
    public Task<IActionResult> OnPostRemoveMemberAsync(string? member, CancellationToken cancellationToken)
        => ChangeMembersAsync(
            settings =>
            {
                if (!settings.Members.Remove(member ?? string.Empty)) return $"'{member}' is not a member.";
                if (settings.DeployRepository == member) settings.DeployRepository = null;

                return null;
            },
            $"{member} was removed.",
            cancellationToken);

    /// <summary>Moves a member one place earlier (<c>up</c>) or later (<c>down</c>) in the resolution order.</summary>
    public Task<IActionResult> OnPostMoveMemberAsync(string? member, string? direction, CancellationToken cancellationToken)
        => ChangeMembersAsync(
            settings =>
            {
                var index = settings.Members.IndexOf(member ?? string.Empty);
                if (index < 0) return $"'{member}' is not a member.";

                var target = string.Equals(direction, "up", StringComparison.OrdinalIgnoreCase) ? index - 1 : index + 1;
                if (target < 0 || target >= settings.Members.Count) return null;

                (settings.Members[index], settings.Members[target]) = (settings.Members[target], settings.Members[index]);

                return null;
            },
            $"{member} was moved {(string.Equals(direction, "up", StringComparison.OrdinalIgnoreCase) ? "up" : "down")}.",
            cancellationToken);

    /// <summary>Sets the member that receives uploads (blank: the virtual repository takes none).</summary>
    public Task<IActionResult> OnPostDeployAsync(string? deploy, CancellationToken cancellationToken)
        => ChangeMembersAsync(
            settings =>
            {
                settings.DeployRepository = string.IsNullOrWhiteSpace(deploy) ? null : deploy;

                return null;
            },
            string.IsNullOrWhiteSpace(deploy) ? "The virtual repository no longer takes uploads." : $"Uploads now go to {deploy}.",
            cancellationToken);

    /// <summary>Shows where the package version typed would come from, member by member (nothing is fetched or stored).</summary>
    public async Task<IActionResult> OnPostPreviewAsync(CancellationToken cancellationToken)
    {
        if (!await LoadAsync(cancellationToken) || VirtualDetail == null) return NotFound();

        AnonymousRead = Repository!.AnonymousRead;
        var id = PreviewPackageId?.Trim();
        if (string.IsNullOrEmpty(id))
        {
            Error = "Enter the id of the package to resolve.";

            return Page();
        }

        var isMaven = Repository.Format == RepositoryFormat.Maven;
        if (!isMaven && !NuGet.Versioning.NuGetVersion.TryParse(PreviewVersion?.Trim(), out _))
        {
            Error = $"'{PreviewVersion}' is not a valid version.";

            return Page();
        }

        if (Repository.Format == RepositoryFormat.Npm)
        {
            // An npm name is checked by the one rule every npm route and page uses (scoped names; a lookup accepts legacy upper case).
            if (!NpmNames.TryParseLookup(id, out id, out var invalidName))
            {
                Error = invalidName;

                return Page();
            }
        }

        try
        {
            Preview = await HttpContext.RequestServices.GetRequiredService<IVirtualRepositoryAdministration>()
                .PreviewAsync(Name!, id, PreviewVersion!.Trim(), cancellationToken);
        }
        catch (ArgumentException e)
        {
            // An artifact that is not group:artifact, or a version that is not a folder name.
            Error = e.Message;
        }

        return Page();
    }

    // Applies 'change' to a copy of the virtual repository's settings and saves them; 'change' returns a reason to refuse.
    private async Task<IActionResult> ChangeMembersAsync(Func<VirtualRepositorySettings, string?> change, string done, CancellationToken cancellationToken)
    {
        HttpContext.RequestServices.GetService<MaintenanceGate>()?.ThrowIfClosed();

        var virtuals = HttpContext.RequestServices.GetRequiredService<IVirtualRepositoryAdministration>();
        var detail = await virtuals.FindAsync(Name, cancellationToken);
        if (detail == null) return NotFound();

        var settings = new VirtualRepositorySettings
        {
            Members = detail.Members.Select(m => m.Name).ToList(),
            DeployRepository = detail.DeployRepository,
        };

        var refusal = change(settings);
        if (refusal != null) return await RefuseAsync(refusal, cancellationToken);

        try
        {
            await virtuals.UpdateAsync(Name!, settings, cancellationToken, User.Identity?.Name);
        }
        catch (ArgumentException e)
        {
            return await RefuseAsync(e.Message, cancellationToken);
        }

        TempData["Message"] = done;

        return RedirectToPage(null, null, new { name = Name }, "repository-section-virtual");
    }

    // Shows the page again with a reason, keeping what was typed.
    private async Task<IActionResult> RefuseAsync(string error, CancellationToken cancellationToken)
    {
        if (!await LoadAsync(cancellationToken)) return NotFound();

        Error = error;
        if (RemoteDetail != null)
        {
            Remote.HasPassword = RemoteDetail.HasPassword;
            Remote.EndpointFromMirror = RemoteDetail.IsMirror;
            Remote.Format = RemoteDetail.Repository.Format;
            Remote.Password = null;
        }

        return Page();
    }
}
