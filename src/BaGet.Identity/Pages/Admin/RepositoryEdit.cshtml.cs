using System;
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
/// while the <see cref="MaintenanceGate"/> is closed. Changing or purging a remote repository asks for the
/// administrator's password again; testing the connection does not (it saves nothing).
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

    /// <summary>What a remote repository stores and remembers.</summary>
    public RemoteCacheOverview? Cache { get; private set; }

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
    /// a repository that is not a NuGet one.
    /// </summary>
    public RepositoryAccessSummary? Access { get; private set; }

    /// <summary>The repository's feed address.</summary>
    public string FeedUrl => RepositoryFeedUrl.For(HttpContext, Repository?.Name ?? string.Empty);

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
        if (info != null && info.Format == RepositoryFormat.NuGet)
        {
            Access = await services.GetRequiredService<IAccessEvaluator>().DescribeAsync(info, cancellationToken);
        }

        if (Repository.Kind == RepositoryKind.Remote)
        {
            var remotes = services.GetRequiredService<IRemoteRepositoryAdministration>();
            RemoteDetail = await remotes.FindAsync(Name, cancellationToken);
            if (RemoteDetail != null) Cache = await remotes.GetCacheAsync(Name!, cancellationToken);
        }
        else if (Repository.IsDefault)
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

    /// <summary>Asks the upstream with the posted, unsaved values (a blank password means the stored one) and saves nothing.</summary>
    public async Task<IActionResult> OnPostTestAsync(CancellationToken cancellationToken)
    {
        if (!await LoadAsync(cancellationToken)) return NotFound();
        if (RemoteDetail == null) return NotFound();

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
        if (await remotes.FindAsync(Name, cancellationToken) == null) return NotFound();
        if (!await ConfirmedAsync(password)) return await RefuseAsync(NotConfirmed, cancellationToken);

        var id = PackageId?.Trim();
        if (string.IsNullOrEmpty(id))
        {
            return await RefuseAsync("Enter the id of the package to purge.", cancellationToken);
        }

        NuGet.Versioning.NuGetVersion? version = null;
        if (!string.IsNullOrWhiteSpace(PackageVersion) && !NuGet.Versioning.NuGetVersion.TryParse(PackageVersion.Trim(), out version))
        {
            return await RefuseAsync($"'{PackageVersion}' is not a valid version.", cancellationToken);
        }

        var result = await remotes.PurgePackageAsync(Name!, id, version, cancellationToken, User.Identity?.Name);
        TempData["Message"] = result.Packages == 0 && result.CachedAnswers == 0
            ? $"Nothing was cached for {id}{(version == null ? string.Empty : " " + version.ToNormalizedString())}."
            : $"{id}{(version == null ? string.Empty : " " + version.ToNormalizedString())} was purged: {result.Packages} package version(s) deleted, {result.CachedAnswers} remembered answer(s) dropped.";

        return RedirectToPage(new { name = Name });
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
            Remote.Password = null;
        }

        return Page();
    }
}
