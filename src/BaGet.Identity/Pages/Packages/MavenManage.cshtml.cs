using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.Pages.Account;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BaGet.Identity.Pages.Packages;

/// <summary>
/// The owners' page for a Maven artifact: its owners, versions and the files of the chosen one, with deletion of a file, a version
/// or the whole artifact, and the clean-up of old snapshot builds. Every change goes through <see cref="IMavenRepository"/> as the
/// signed-in user (the session's principal, never a typed key), so the page can do what the same user's API key could through the
/// repository routes and no more: the access model and ownership apply identically. Opening it takes ownership (directly or
/// through an organization) or administration; deleting also asks for the password again, since it cannot be undone. Works in
/// the request's repository.
/// </summary>
public class MavenManageModel : AccountPageModel
{
    internal const string ReadOnlyMessage = "This server is in read-only mode, so nothing can be changed.";

    protected override bool RequiresSignIn => true;

    /// <summary>The group id from the address.</summary>
    [BindProperty(SupportsGet = true)]
    public string? Group { get; set; }

    /// <summary>The artifact id from the address.</summary>
    [BindProperty(SupportsGet = true)]
    public string? Artifact { get; set; }

    /// <summary>The version whose files are listed (<c>?version=</c>), or null.</summary>
    [BindProperty(SupportsGet = true, Name = "version")]
    public string? Version { get; set; }

    /// <summary>The artifact, or null before it is loaded (or after its last version went).</summary>
    public MavenArtifactInfo? Info { get; private set; }

    /// <summary>The chosen version with its files, or null.</summary>
    public MavenVersionDetail? Selected { get; private set; }

    /// <summary>The artifact's owners.</summary>
    public IReadOnlyList<PackageOwner> Owners { get; private set; } = Array.Empty<PackageOwner>();

    /// <summary>The outcome of the last action.</summary>
    public string? Message { get; private set; }

    /// <summary>Whether <see cref="Message"/> reports a refusal.</summary>
    public bool Failed { get; private set; }

    /// <summary>Whether the signed-in user is an administrator.</summary>
    public bool IsAdministrator { get; private set; }

    /// <summary>Whether the user manages the artifact only as an administrator (they are not an owner).</summary>
    public bool ActingAsAdministrator { get; private set; }

    /// <summary>Whether the user has a password to confirm with.</summary>
    public bool HasPassword { get; private set; } = true;

    /// <summary>For an account without a password: it signed in again with its provider a moment ago.</summary>
    public bool Reauthenticated { get; private set; }

    /// <summary>Whether an external provider is available to sign in again with.</summary>
    public bool CanReauthenticate => Accounts.OpenIdConnect.IsConfigured;

    /// <summary><c>group:artifact</c>.</summary>
    public string Name => MavenNames.ArtifactName(Group ?? string.Empty, Artifact ?? string.Empty);

    private IMavenRepository Maven => HttpContext.RequestServices.GetRequiredService<IMavenRepository>();

    private bool IsReadOnlyMode => HttpContext.RequestServices.GetRequiredService<IOptionsSnapshot<BaGetOptions>>().Value.IsReadOnlyMode;

    public override async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        OnPageHandlerExecuting(context);
        if (context.Result != null) return;

        if (!MavenNames.IsValidGroupId(Group) || !MavenNames.IsValidArtifactId(Artifact) || (Version != null && !MavenNames.IsValidVersion(Version)))
        {
            context.Result = NotFound();
            return;
        }

        // Owners and the owner tools exist where artifacts are deployed: only a local repository (the only kind Maven has).
        if (HttpContext.RequestServices.GetRequiredService<IRepositoryContext>().Current.Kind != RepositoryKind.Local)
        {
            context.Result = NotFound();
            return;
        }

        #pragma warning disable CS8602
        var isOwner = await Ownership.IsOwnerAsync(UserManager.GetUserId(User), User.Identity.Name, Name, HttpContext.RequestAborted);
        #pragma warning restore CS8602
        IsAdministrator = await HttpContext.RequestServices.GetRequiredService<AdministratorService>()
            .IsAdministratorAsync(User, HttpContext.RequestAborted);
        if (!isOwner && !IsAdministrator)
        {
            context.Result = StatusCode(403);
            return;
        }

        ActingAsAdministrator = IsAdministrator && !isOwner;

        if (HttpMethods.IsPost(HttpContext.Request.Method))
        {
            // A write: refused while a database copy has the gate closed, and in read-only mode, for every handler at once.
            HttpContext.RequestServices.GetService<MaintenanceGate>()?.ThrowIfClosed();

            if (IsReadOnlyMode)
            {
                await LoadAsync(HttpContext.RequestAborted);
                Message = ReadOnlyMessage;
                Failed = true;
                context.Result = Page();
                return;
            }

            if (ActingAsAdministrator)
            {
                Log().LogWarning(
                    "Administrator {Admin} ran {Handler} on Maven artifact {Artifact}",
                    User.Identity.Name,
                    context.HandlerMethod?.Name,
                    Name);
            }
        }

        await next();
    }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadAsync(cancellationToken);

        return Info == null ? NotFound() : Page();
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        var current = await UserManager.GetUserAsync(User);
        HasPassword = current == null || await UserManager.HasPasswordAsync(current);
        Reauthenticated = !HasPassword && await HasReauthenticatedRecentlyAsync();

        Info = await Maven.GetArtifactAsync(Group!, Artifact!, cancellationToken);
        Selected = Info != null && Version != null && Info.Versions.Any(v => v.Version == Version)
            ? await Maven.GetVersionAsync(Group!, Artifact!, Version!, cancellationToken)
            : null;
        Owners = await Ownership.GetOwnersAsync(Name, cancellationToken);
    }

    private async Task<BaGetPrincipal?> CallerAsync()
        => await HttpContext.RequestServices.GetRequiredService<ISessionPrincipalFactory>().CreateAsync(User);

    private async Task<IActionResult> DoneAsync(string message, bool failed, CancellationToken cancellationToken)
    {
        await LoadAsync(cancellationToken);
        Message = message;
        Failed = failed;

        return Page();
    }

    private Task<IActionResult> DoneAsync(MavenResult result, string success, CancellationToken cancellationToken)
        => DoneAsync(result.Succeeded ? success : result.Message ?? "Not done.", !result.Succeeded, cancellationToken);

    public async Task<IActionResult> OnPostAddOwnerAsync(string ownerName, CancellationToken cancellationToken)
    {
        var result = string.IsNullOrWhiteSpace(ownerName)
            ? OwnerChangeResult.UnknownAccount
            : await Ownership.AddOwnerAsync(Name, ownerName.Trim(), cancellationToken);

        return await DoneAsync(result == OwnerChangeResult.Success ? $"{ownerName} is now an owner." : Describe(result), result != OwnerChangeResult.Success, cancellationToken);
    }

    public async Task<IActionResult> OnPostRemoveOwnerAsync(string ownerName, CancellationToken cancellationToken)
    {
        var result = await Ownership.RemoveOwnerAsync(Name, ownerName, cancellationToken);

        return await DoneAsync(result == OwnerChangeResult.Success ? $"{ownerName} is no longer an owner." : Describe(result), result != OwnerChangeResult.Success, cancellationToken);
    }

    /// <summary>Deletes one file of the chosen version (and its checksums with it), after the password is confirmed.</summary>
    public async Task<IActionResult> OnPostDeleteFileAsync(string? fileName, string? password, CancellationToken cancellationToken)
    {
        var refusal = await RefuseUnconfirmedAsync(password, cancellationToken);
        if (refusal != null) return refusal;

        if (Version == null) return await DoneAsync("Choose a version first.", failed: true, cancellationToken);

        var result = await Maven.DeleteFileAsync(await CallerAsync(), new MavenCoordinate(Group!, Artifact!, Version), fileName ?? string.Empty, cancellationToken);
        if (result.Succeeded) Log().LogWarning("{User} deleted {File} of {Artifact} {Version}", User.Identity?.Name, fileName, Name, Version);

        return await AfterDeleteAsync(result, $"{fileName} is deleted.", cancellationToken);
    }

    /// <summary>Deletes one version with its files, after the password is confirmed.</summary>
    public async Task<IActionResult> OnPostDeleteVersionAsync(string? version, string? password, CancellationToken cancellationToken)
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
    public async Task<IActionResult> OnPostDeleteArtifactAsync(string? password, CancellationToken cancellationToken)
    {
        var refusal = await RefuseUnconfirmedAsync(password, cancellationToken);
        if (refusal != null) return refusal;

        var result = await Maven.DeleteArtifactAsync(await CallerAsync(), Group!, Artifact!, cancellationToken);
        if (!result.Succeeded) return await DoneAsync(result, string.Empty, cancellationToken);

        Log().LogWarning("{User} deleted Maven artifact {Artifact}", User.Identity?.Name, Name);

        return RedirectToPage("/MavenArtifacts");
    }

    /// <summary>Deletes the builds of a snapshot version older than the newest <paramref name="keep"/>.</summary>
    public async Task<IActionResult> OnPostCleanupAsync(string? version, string? keep, string? password, CancellationToken cancellationToken)
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

    /// <summary>Null when the signed-in user confirmed with their password, otherwise the page to show.</summary>
    private async Task<IActionResult?> RefuseUnconfirmedAsync(string? password, CancellationToken cancellationToken)
    {
        var current = await UserManager.GetUserAsync(User);
        if (current == null) return Challenge();

        return await IsConfirmedAsync(current, password)
            ? null
            : await DoneAsync("The confirmation is incorrect, so nothing was deleted.", failed: true, cancellationToken);
    }

    private ILogger<MavenManageModel> Log() => HttpContext.RequestServices.GetRequiredService<ILogger<MavenManageModel>>();

    private static string Describe(OwnerChangeResult result) => result switch
    {
        OwnerChangeResult.UnknownAccount => "There is no such user or organization.",
        OwnerChangeResult.AlreadyOwner => "That account already owns the artifact.",
        OwnerChangeResult.NotOwner => "That account is not an owner.",
        OwnerChangeResult.LastOwner => "An artifact keeps at least one owner.",
        _ => result.ToString(),
    };
}
