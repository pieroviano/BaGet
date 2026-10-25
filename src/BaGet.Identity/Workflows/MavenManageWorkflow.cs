using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BaGet.Identity.Workflows;

/// <summary>
/// The owners' screen for a Maven artifact: its owners, versions and the files of the chosen one, with deletion of a file, a version
/// or the whole artifact, and the clean-up of old snapshot builds. Every change goes through <see cref="IMavenRepository"/> as the
/// signed-in user (the session's principal, never a typed key), so the screen can do what the same user's API key could through the
/// repository routes and no more: the access model and ownership apply identically. Opening it takes ownership (directly or
/// through an organization) or administration; deleting also asks for the password again, since it cannot be undone. Works in
/// the request's repository. Served by <c>MavenManageController</c>.
/// </summary>
internal sealed class MavenManageWorkflow : PackageWorkflowBase
{
    /// <summary>What every write handler answers on a read-only server.</summary>
    internal const string ReadOnlyMessage = "This server is in read-only mode, so nothing can be changed.";

    /// <summary>Creates the workflow of one request.</summary>
    public MavenManageWorkflow(HttpContext httpContext, string? group, string? artifact, string? version)
        : base(httpContext)
    {
        Group = group;
        Artifact = artifact;
        Version = version;
    }

    /// <summary>The group id from the address.</summary>
    public string? Group { get; set; }

    /// <summary>The artifact id from the address.</summary>
    public string? Artifact { get; set; }

    /// <summary>The version whose files are listed (<c>?version=</c>), or null.</summary>
    public string? Version { get; set; }

    /// <summary>What the screen shows.</summary>
    public MavenManageViewModel Model { get; } = new();

    /// <summary><c>group:artifact</c>.</summary>
    public string Name => MavenNames.ArtifactName(Group ?? string.Empty, Artifact ?? string.Empty);

    private IMavenRepository Maven => HttpContext.RequestServices.GetRequiredService<IMavenRepository>();

    /// <summary>
    /// The check that runs before every handler: valid coordinates, a local Maven repository, ownership or administration, the
    /// maintenance gate and read-only mode for a write. Returns the result that ends the request, or null to run the handler.
    /// </summary>
    /// <param name="handlerName">The handler about to run (for the administrator audit line).</param>
    public async Task<IActionResult?> EnterAsync(string? handlerName)
    {
        if (!MavenNames.IsValidGroupId(Group) || !MavenNames.IsValidArtifactId(Artifact) || (Version != null && !MavenNames.IsValidVersion(Version)))
        {
            return NotFound();
        }

        // Owners and the owner tools exist where artifacts are deployed: only a local Maven repository (a page of another format is
        // 404, as the repository check says; the controller's own check can run before it).
        var current = HttpContext.RequestServices.GetRequiredService<IRepositoryContext>().Current;
        if (current.Kind != RepositoryKind.Local || current.Format != RepositoryFormat.Maven)
        {
            return NotFound();
        }

        #pragma warning disable CS8602
        var isOwner = await Ownership.IsOwnerAsync(UserManager.GetUserId(User), User.Identity.Name, Name, HttpContext.RequestAborted);
        #pragma warning restore CS8602
        Model.IsAdministrator = await HttpContext.RequestServices.GetRequiredService<AdministratorService>()
            .IsAdministratorAsync(User, HttpContext.RequestAborted);
        if (!isOwner && !Model.IsAdministrator)
        {
            return StatusCode(403);
        }

        Model.ActingAsAdministrator = Model.IsAdministrator && !isOwner;

        if (HttpMethods.IsPost(HttpContext.Request.Method))
        {
            // A write: refused while a database copy has the gate closed, and in read-only mode, for every handler at once.
            HttpContext.RequestServices.GetService<MaintenanceGate>()?.ThrowIfClosed();

            if (IsReadOnlyMode)
            {
                await LoadAsync(HttpContext.RequestAborted);
                Model.Message = ReadOnlyMessage;
                Model.Failed = true;
                return Show;
            }

            if (Model.ActingAsAdministrator)
            {
                #pragma warning disable CS8602
                Log().LogWarning(
                    "Administrator {Admin} ran {Handler} on Maven artifact {Artifact}",
                    User.Identity.Name,
                    handlerName,
                    Name);
                #pragma warning restore CS8602
            }
        }

        return null;
    }

    /// <summary>The GET handler: loads the artifact, 404 when it does not exist.</summary>
    public async Task<IActionResult> GetAsync(CancellationToken cancellationToken)
    {
        await LoadAsync(cancellationToken);

        return Model.Info == null ? NotFound() : Show;
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        var current = await UserManager.GetUserAsync(User);
        Model.HasPassword = current == null || await UserManager.HasPasswordAsync(current);
        Model.Reauthenticated = !Model.HasPassword && await HasReauthenticatedRecentlyAsync();
        Model.CanReauthenticate = CanReauthenticate;
        Model.Group = Group;
        Model.Artifact = Artifact;
        Model.Version = Version;

        Model.Info = await Maven.GetArtifactAsync(Group!, Artifact!, cancellationToken);
        Model.Selected = Model.Info != null && Version != null && Model.Info.Versions.Any(v => v.Version == Version)
            ? await Maven.GetVersionAsync(Group!, Artifact!, Version!, cancellationToken)
            : null;
        Model.Owners = await Ownership.GetOwnersAsync(Name, cancellationToken);
    }

    private async Task<IActionResult> DoneAsync(string message, bool failed, CancellationToken cancellationToken)
    {
        await LoadAsync(cancellationToken);
        Model.Message = message;
        Model.Failed = failed;

        return Show;
    }

    private Task<IActionResult> DoneAsync(MavenResult result, string success, CancellationToken cancellationToken)
        => DoneAsync(result.Succeeded ? success : result.Message ?? "Not done.", !result.Succeeded, cancellationToken);

    /// <summary>Adds an owner.</summary>
    public async Task<IActionResult> AddOwnerAsync(string ownerName, CancellationToken cancellationToken)
    {
        var result = string.IsNullOrWhiteSpace(ownerName)
            ? OwnerChangeResult.UnknownAccount
            : await Ownership.AddOwnerAsync(Name, ownerName.Trim(), cancellationToken);

        return await DoneAsync(result == OwnerChangeResult.Success ? $"{ownerName} is now an owner." : Describe(result, "artifact"), result != OwnerChangeResult.Success, cancellationToken);
    }

    /// <summary>Removes an owner.</summary>
    public async Task<IActionResult> RemoveOwnerAsync(string ownerName, CancellationToken cancellationToken)
    {
        var result = await Ownership.RemoveOwnerAsync(Name, ownerName, cancellationToken);

        return await DoneAsync(result == OwnerChangeResult.Success ? $"{ownerName} is no longer an owner." : Describe(result, "artifact"), result != OwnerChangeResult.Success, cancellationToken);
    }

    /// <summary>Deletes one file of the chosen version (and its checksums with it), after the password is confirmed.</summary>
    public async Task<IActionResult> DeleteFileAsync(string? fileName, string? password, CancellationToken cancellationToken)
    {
        var refusal = await RefuseUnconfirmedAsync(password, cancellationToken);
        if (refusal != null) return refusal;

        if (Version == null) return await DoneAsync("Choose a version first.", failed: true, cancellationToken);

        var result = await Maven.DeleteFileAsync(await CallerAsync(), new MavenCoordinate(Group!, Artifact!, Version), fileName ?? string.Empty, cancellationToken);
        if (result.Succeeded) Log().LogWarning("{User} deleted {File} of {Artifact} {Version}", User.Identity?.Name, fileName, Name, Version);

        return await AfterDeleteAsync(result, $"{fileName} is deleted.", cancellationToken);
    }

    /// <summary>Deletes one version with its files, after the password is confirmed.</summary>
    public async Task<IActionResult> DeleteVersionAsync(string? version, string? password, CancellationToken cancellationToken)
    {
        var refusal = await RefuseUnconfirmedAsync(password, cancellationToken);
        if (refusal != null) return refusal;

        if (!MavenNames.IsValidVersion(version)) return await DoneAsync("Choose a version.", failed: true, cancellationToken);

        var result = await Maven.DeleteVersionAsync(await CallerAsync(), new MavenCoordinate(Group!, Artifact!, version), cancellationToken);
        if (result.Succeeded) Log().LogWarning("{User} deleted {Artifact} {Version}", User.Identity?.Name, Name, version);
        if (Version == version) Version = null;

        return await AfterDeleteAsync(result, $"{Name} {version} is deleted.", cancellationToken);
    }

    /// <summary>Deletes every version of the artifact, after the password is confirmed.</summary>
    public async Task<IActionResult> DeleteArtifactAsync(string? password, CancellationToken cancellationToken)
    {
        var refusal = await RefuseUnconfirmedAsync(password, cancellationToken);
        if (refusal != null) return refusal;

        var result = await Maven.DeleteArtifactAsync(await CallerAsync(), Group!, Artifact!, cancellationToken);
        if (!result.Succeeded) return await DoneAsync(result, string.Empty, cancellationToken);

        Log().LogWarning("{User} deleted Maven artifact {Artifact}", User.Identity?.Name, Name);

        return RedirectToPage("/MavenArtifacts");
    }

    /// <summary>Deletes the builds of a snapshot version older than the newest <paramref name="keep"/>.</summary>
    public async Task<IActionResult> CleanupAsync(string? version, string? keep, string? password, CancellationToken cancellationToken)
    {
        var refusal = await RefuseUnconfirmedAsync(password, cancellationToken);
        if (refusal != null) return refusal;

        if (!MavenNames.IsValidVersion(version)) return await DoneAsync("Choose a snapshot version.", failed: true, cancellationToken);
        if (!int.TryParse(keep, NumberStyles.None, CultureInfo.InvariantCulture, out var count) || count < 1)
        {
            return await DoneAsync("Enter how many builds to keep (at least 1).", failed: true, cancellationToken);
        }

        var result = await Maven.CleanupSnapshotsAsync(await CallerAsync(), new MavenCoordinate(Group!, Artifact!, version), count, cancellationToken);
        if (result.Succeeded) Log().LogWarning("{User} cleaned up the snapshot builds of {Artifact} {Version}, keeping {Keep}", User.Identity?.Name, Name, version, count);

        return await DoneAsync(result, result.Message ?? "Done.", cancellationToken);
    }

    // The last file takes the version, the last version the artifact: nothing left to manage.
    private async Task<IActionResult> AfterDeleteAsync(MavenResult result, string success, CancellationToken cancellationToken)
    {
        if (result.Succeeded && await Maven.GetArtifactAsync(Group!, Artifact!, cancellationToken) == null)
        {
            return RedirectToPage("/MavenArtifacts");
        }

        return await DoneAsync(result, success, cancellationToken);
    }

    /// <summary>Null when the signed-in user confirmed with their password, otherwise the screen to show.</summary>
    private async Task<IActionResult?> RefuseUnconfirmedAsync(string? password, CancellationToken cancellationToken)
    {
        var current = await UserManager.GetUserAsync(User);
        if (current == null) return Challenge();

        return await IsConfirmedAsync(current, password)
            ? null
            : await DoneAsync("The confirmation is incorrect, so nothing was deleted.", failed: true, cancellationToken);
    }

    private ILogger<MavenManageWorkflow> Log() => HttpContext.RequestServices.GetRequiredService<ILogger<MavenManageWorkflow>>();
}
