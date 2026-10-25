using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.Pages.Admin;
using BaGet.Identity.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace BaGet.Identity.Workflows;

/// <summary>
/// Shows a repository's feed address and the access model's view of it (who effectively holds each action) and edits what can
/// change: whether anonymous requests may read it. The name never changes (it is in URLs and storage paths), and neither does the
/// format or kind. A remote repository also shows and edits its upstream (URL, protocol, credentials, cache periods, offline, store
/// locally), tests the connection with the unsaved values, shows its cache and purges it (whole, or one package). Saving and purging
/// are refused with 503 while the <see cref="MaintenanceGate"/> is closed. Changing, purging and testing the connection of a remote
/// repository ask for the administrator's password again (a test saves nothing, but makes the server send a request to a typed URL,
/// with the stored password when the origin is unchanged). A virtual repository shows and edits its members (add, remove, move up or
/// down), its deploy repository, and previews where a package version would come from
/// (<see cref="IVirtualRepositoryAdministration.PreviewAsync(string, string, string, CancellationToken)"/>); those edits are saved at
/// once, refused with 503 while the gate is closed. Used by <c>AdminRepositoriesController</c>.
/// </summary>
internal sealed class RepositoryEditWorkflow : AdminWorkflow
{
    private const string NotConfirmed = "The confirmation is incorrect, so nothing was changed.";

    public RepositoryEditWorkflow(HttpContext httpContext, IUrlHelper url) : base(httpContext, url)
    {
    }

    /// <summary>The state the screen renders; the caller sets <c>Name</c> and the posted fields first.</summary>
    public RepositoryEditViewModel Model { get; } = new();

    /// <summary>Shows the repository.</summary>
    public async Task<IActionResult?> GetAsync(CancellationToken cancellationToken)
    {
        if (!await LoadAsync(cancellationToken)) return new NotFoundResult();

        Model.AnonymousRead = Model.Repository!.AnonymousRead;
        if (Model.RemoteDetail != null) Model.Remote = RemoteFormModel.From(Model.RemoteDetail);

        return null;
    }

    // Loads the repository, its access summary and, for a remote one, its upstream and cache. False: no such repository.
    private async Task<bool> LoadAsync(CancellationToken cancellationToken)
    {
        await LoadConfirmationAsync(Model, Http.Request.Path + Http.Request.QueryString);
        var repository = Model.Repository = await Services.GetRequiredService<IRepositoryAdministration>().FindAsync(Model.Name, cancellationToken);
        if (repository == null) return false;

        var formats = Services.GetRequiredService<FormatRegistry>();
        var module = formats.Find(repository.Format);
        Model.BrowsePage = module
            ?.RepositoryLinks(new RepositoryInfo(repository.Key, repository.Name, repository.Format, repository.Kind))
            .FirstOrDefault(l => l.Id == "browse" && l.Page != null)?.Page ?? "/Index";
        Model.FeedUrl = RepositoryFeedUrl.For(Http, repository.Name, module?.FeedPath ?? "v3/index.json");

        var info = (await Services.GetRequiredService<IRepositoryService>().ListAsync(cancellationToken))
            .FirstOrDefault(r => r.Name == repository.Name);
        if (info != null)
        {
            Model.Access = await Services.GetRequiredService<IAccessEvaluator>().DescribeAsync(info, cancellationToken);
        }

        if (repository.Kind == RepositoryKind.Remote)
        {
            var remotes = Services.GetRequiredService<IRemoteRepositoryAdministration>();
            Model.RemoteDetail = await remotes.FindAsync(Model.Name, cancellationToken);
            if (Model.RemoteDetail != null) Model.Cache = await remotes.GetCacheAsync(Model.Name!, cancellationToken);
        }
        else if (repository.Kind == RepositoryKind.Virtual)
        {
            var virtuals = Services.GetRequiredService<IVirtualRepositoryAdministration>();
            Model.VirtualDetail = await virtuals.FindAsync(Model.Name, cancellationToken);
            var members = Model.VirtualDetail?.Members.Select(m => m.Name).ToHashSet() ?? new HashSet<string>();
            Model.MemberCandidates = (await virtuals.CandidatesAsync(Model.Name, cancellationToken)).Where(c => !members.Contains(c.Name)).ToList();
        }

        if (repository.Kind != RepositoryKind.Virtual)
        {
            Model.ReferencedBy = await Services.GetRequiredService<IVirtualRepositoryAdministration>().ReferencedByAsync(repository.Name, cancellationToken);
        }

        if (repository.Format == RepositoryFormat.Maven && repository.Kind == RepositoryKind.Local)
        {
            Model.MavenSettings = await Services.GetRequiredService<IMavenRepositoryAdministration>().GetSettingsAsync(repository.Name, cancellationToken);
        }

        if (repository.IsDefault)
        {
            Model.MirrorRepository = await Services.GetRequiredService<IRemoteRepositoryAdministration>().FindAsync(Repository.MirrorName, cancellationToken);
            Model.MirrorActive = Model.MirrorRepository?.IsMirror == true;
        }

        return true;
    }

    private async Task<bool> ConfirmedAsync(string? password)
    {
        var current = await UserManager.GetUserAsync(User);

        return current != null && await IsConfirmedAsync(current, password);
    }

    /// <summary>Saves the repository.</summary>
    public async Task<IActionResult?> PostAsync(string? password, CancellationToken cancellationToken)
    {
        ThrowIfClosed();

        var administration = Services.GetRequiredService<IRepositoryAdministration>();
        if (await administration.FindAsync(Model.Name, cancellationToken) == null) return new NotFoundResult();

        var summary = await administration.FindAsync(Model.Name, cancellationToken);
        if (summary!.Kind == RepositoryKind.Remote)
        {
            if (!await ConfirmedAsync(password)) return await RefuseAsync(NotConfirmed, cancellationToken);

            try
            {
                await Services.GetRequiredService<IRemoteRepositoryAdministration>()
                    .UpdateAsync(Model.Name!, Model.Remote.ToSettings(), Model.Remote.ClearPassword, cancellationToken, User.Identity?.Name);
            }
            catch (Exception e) when (e is ArgumentException or InvalidOperationException)
            {
                return await RefuseAsync(e.Message, cancellationToken);
            }
        }

        await administration.SetAnonymousReadAsync(Model.Name!, Model.AnonymousRead, cancellationToken, User.Identity?.Name);
        Model.Message = $"Repository {Model.Name} was saved.";

        return RedirectToScreen("/Admin/Repositories");
    }

    /// <summary>
    /// Saves the policy of a local Maven repository: which versions it accepts, whether a release may be redeployed and how many
    /// snapshot builds are kept. Audited; a stricter policy applies to later deploys and deletes nothing.
    /// </summary>
    public async Task<IActionResult?> PostMavenSettingsAsync(string? policy, string? redeploy, string? snapshotBuilds, CancellationToken cancellationToken)
    {
        ThrowIfClosed();

        MavenRepositorySettings settings;
        try
        {
            settings = MavenRepositorySettings.FromFields(new Dictionary<string, string?> { ["policy"] = policy, ["redeploy"] = redeploy, ["snapshotBuilds"] = snapshotBuilds });
        }
        catch (ArgumentException e)
        {
            return await RefuseAsync(e.Message, cancellationToken);
        }

        if (!await Services.GetRequiredService<IMavenRepositoryAdministration>().SaveSettingsAsync(Model.Name!, settings, User.Identity?.Name, cancellationToken))
        {
            return new NotFoundResult();
        }

        Model.Message = $"The settings of repository {Model.Name} were saved.";

        return RedirectToScreen("/Admin/RepositoryEdit", new { name = Model.Name });
    }

    /// <summary>Asks the upstream with the posted, unsaved values (a blank password means the stored one) and saves nothing.</summary>
    public async Task<IActionResult?> PostTestAsync(string? password, CancellationToken cancellationToken)
    {
        if (!await LoadAsync(cancellationToken)) return new NotFoundResult();
        if (Model.RemoteDetail == null) return new NotFoundResult();

        // The probe makes this server send a request to a URL the administrator typed (and the stored password, if the
        // origin is unchanged): it asks for the password again, like the changes that hand out credentials.
        if (!await ConfirmedAsync(password)) return await RefuseAsync(NotConfirmed, cancellationToken);

        Model.AnonymousRead = Model.Repository!.AnonymousRead;
        var settings = Model.Remote.ToSettings();
        if (Model.RemoteDetail.IsMirror)
        {
            settings.Url = Model.RemoteDetail.Settings.Url;
            settings.Protocol = Model.RemoteDetail.Settings.Protocol;
        }

        Model.TestResult = await Services.GetRequiredService<IRemoteRepositoryAdministration>()
            .TestConnectionAsync(Model.Name, settings, cancellationToken);
        KeepRemoteFields();

        return null;
    }

    /// <summary>Deletes every package the remote repository stored and forgets what it remembered of its upstream.</summary>
    public async Task<IActionResult?> PostPurgeAsync(string? password, CancellationToken cancellationToken)
    {
        ThrowIfClosed();

        var remotes = Services.GetRequiredService<IRemoteRepositoryAdministration>();
        if (await remotes.FindAsync(Model.Name, cancellationToken) == null) return new NotFoundResult();
        if (!await ConfirmedAsync(password)) return await RefuseAsync(NotConfirmed, cancellationToken);

        var result = await remotes.PurgeAsync(Model.Name!, cancellationToken, User.Identity?.Name);
        Model.Message = $"The cache of {Model.Name} was purged: {result.Packages} {((await remotes.FindAsync(Model.Name, cancellationToken))?.Repository.Format == RepositoryFormat.Docker ? "manifest(s)" : "package version(s)")} deleted, {result.CachedAnswers} remembered answer(s) dropped.";

        return RedirectToScreen("/Admin/RepositoryEdit", new { name = Model.Name });
    }

    /// <summary>Deletes one cached package (every version, or the one given) and forgets what was remembered about it.</summary>
    public async Task<IActionResult?> PostPurgePackageAsync(string? password, CancellationToken cancellationToken)
    {
        ThrowIfClosed();

        var remotes = Services.GetRequiredService<IRemoteRepositoryAdministration>();
        var found = await remotes.FindAsync(Model.Name, cancellationToken);
        if (found == null) return new NotFoundResult();
        if (!await ConfirmedAsync(password)) return await RefuseAsync(NotConfirmed, cancellationToken);

        var id = Model.PackageId?.Trim();
        if (string.IsNullOrEmpty(id))
        {
            return await RefuseAsync("Enter the id of the package to purge.", cancellationToken);
        }

        // A Maven version is any version folder name, not always a NuGet version (5.3.20.RELEASE), and a Docker one is a tag or a digest:
        // they travel as text.
        var isMaven = found.Repository.Format == RepositoryFormat.Maven;
        var isDocker = found.Repository.Format == RepositoryFormat.Docker;
        NuGet.Versioning.NuGetVersion? version = null;
        string? versionText = string.IsNullOrWhiteSpace(Model.PackageVersion) ? null : Model.PackageVersion.Trim();
        if (!isMaven && !isDocker && versionText != null)
        {
            if (!NuGet.Versioning.NuGetVersion.TryParse(versionText, out version))
            {
                return await RefuseAsync($"'{Model.PackageVersion}' is not a valid version.", cancellationToken);
            }

            versionText = version.ToNormalizedString();
        }

        PurgeResult result;
        try
        {
            result = isMaven
                ? await remotes.PurgeArtifactAsync(Model.Name!, id, versionText, cancellationToken, User.Identity?.Name)
                : isDocker
                    ? await remotes.PurgeImageAsync(Model.Name!, id, versionText, cancellationToken, User.Identity?.Name)
                    : await remotes.PurgePackageAsync(Model.Name!, id, version, cancellationToken, User.Identity?.Name);
        }
        catch (ArgumentException e)
        {
            // A name the repository's format would not accept (an npm name must be lower case...).
            return await RefuseAsync(e.Message, cancellationToken);
        }

        var shown = versionText == null ? string.Empty : " " + versionText;
        Model.Message = result.Packages == 0 && result.CachedAnswers == 0
            ? $"Nothing was cached for {id}{shown}."
            : $"{id}{shown} was purged: {result.Packages} {(isMaven ? "file(s)" : isDocker ? "manifest(s)" : "package version(s)")} deleted, {result.CachedAnswers} remembered answer(s) dropped.";

        return RedirectToScreen("/Admin/RepositoryEdit", new { name = Model.Name });
    }

    /// <summary>Adds a repository at the end of the virtual repository's members.</summary>
    public Task<IActionResult?> PostAddMemberAsync(string? member, CancellationToken cancellationToken)
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
    public Task<IActionResult?> PostRemoveMemberAsync(string? member, CancellationToken cancellationToken)
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
    public Task<IActionResult?> PostMoveMemberAsync(string? member, string? direction, CancellationToken cancellationToken)
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
    public Task<IActionResult?> PostDeployAsync(string? deploy, CancellationToken cancellationToken)
        => ChangeMembersAsync(
            settings =>
            {
                settings.DeployRepository = string.IsNullOrWhiteSpace(deploy) ? null : deploy;

                return null;
            },
            string.IsNullOrWhiteSpace(deploy) ? "The virtual repository no longer takes uploads." : $"Uploads now go to {deploy}.",
            cancellationToken);

    /// <summary>Shows where the package version typed would come from, member by member (nothing is fetched or stored).</summary>
    public async Task<IActionResult?> PostPreviewAsync(CancellationToken cancellationToken)
    {
        if (!await LoadAsync(cancellationToken) || Model.VirtualDetail == null) return new NotFoundResult();

        Model.AnonymousRead = Model.Repository!.AnonymousRead;
        var id = Model.PreviewPackageId?.Trim();
        if (string.IsNullOrEmpty(id))
        {
            Model.Error = "Enter the id of the package to resolve.";

            return null;
        }

        var repository = Model.Repository;
        var isMaven = repository.Format == RepositoryFormat.Maven;
        var isDocker = repository.Format == RepositoryFormat.Docker;
        if (!isMaven && !isDocker && !NuGet.Versioning.NuGetVersion.TryParse(Model.PreviewVersion?.Trim(), out _))
        {
            Model.Error = $"'{Model.PreviewVersion}' is not a valid version.";

            return null;
        }

        if (repository.Format == RepositoryFormat.Npm)
        {
            // An npm name is checked by the one rule every npm route and page uses (scoped names; a lookup accepts legacy upper case).
            if (!NpmNames.TryParseLookup(id, out id, out var invalidName))
            {
                Model.Error = invalidName;

                return null;
            }
        }

        try
        {
            Model.Preview = await Services.GetRequiredService<IVirtualRepositoryAdministration>()
                .PreviewAsync(Model.Name!, id, Model.PreviewVersion!.Trim(), cancellationToken);
        }
        catch (ArgumentException e)
        {
            // An artifact that is not group:artifact, or a version that is not a folder name.
            Model.Error = e.Message;
        }

        return null;
    }

    // Applies 'change' to a copy of the virtual repository's settings and saves them; 'change' returns a reason to refuse.
    private async Task<IActionResult?> ChangeMembersAsync(Func<VirtualRepositorySettings, string?> change, string done, CancellationToken cancellationToken)
    {
        ThrowIfClosed();

        var virtuals = Services.GetRequiredService<IVirtualRepositoryAdministration>();
        var detail = await virtuals.FindAsync(Model.Name, cancellationToken);
        if (detail == null) return new NotFoundResult();

        var settings = new VirtualRepositorySettings
        {
            Members = detail.Members.Select(m => m.Name).ToList(),
            DeployRepository = detail.DeployRepository,
        };

        var refusal = change(settings);
        if (refusal != null) return await RefuseAsync(refusal, cancellationToken);

        try
        {
            await virtuals.UpdateAsync(Model.Name!, settings, cancellationToken, User.Identity?.Name);
        }
        catch (ArgumentException e)
        {
            return await RefuseAsync(e.Message, cancellationToken);
        }

        Model.Message = done;

        return RedirectToScreen("/Admin/RepositoryEdit", new { name = Model.Name }, "repository-section-virtual");
    }

    // Shows the screen again with a reason, keeping what was typed.
    private async Task<IActionResult?> RefuseAsync(string error, CancellationToken cancellationToken)
    {
        if (!await LoadAsync(cancellationToken)) return new NotFoundResult();

        Model.Error = error;
        if (Model.RemoteDetail != null) KeepRemoteFields();

        return null;
    }

    // The display-only fields of the upstream form, which a posted form does not carry; the typed password is never shown again.
    private void KeepRemoteFields()
    {
        Model.Remote.HasPassword = Model.RemoteDetail!.HasPassword;
        Model.Remote.EndpointFromMirror = Model.RemoteDetail.IsMirror;
        Model.Remote.Format = Model.RemoteDetail.Repository.Format;
        Model.Remote.Password = null;
    }
}
