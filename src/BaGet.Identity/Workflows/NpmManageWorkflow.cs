using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BaGet.Identity.Workflows;

/// <summary>
/// The owners' screen for an npm package: its owners, dist-tags, per-version deprecation and unpublishing. Every change goes
/// through <see cref="INpmRegistry"/> as the signed-in user (the session's principal, never a typed key), so the screen can do what
/// the same user's API key could through the registry routes and no more: the access model, ownership, and the unpublish
/// window apply identically. Opening it takes ownership (directly or through an organization) or administration; unpublishing
/// also asks for the password again, since it cannot be undone. Works in the request's repository. Served by
/// <c>NpmManageController</c>; the obsolete <c>NpmManageModel</c> runs the same code.
/// </summary>
internal sealed class NpmManageWorkflow : PackageWorkflowBase
{
    /// <summary>What every write handler answers on a read-only server.</summary>
    internal const string ReadOnlyMessage = "This server is in read-only mode, so nothing can be changed.";

    /// <summary>Creates the workflow of one request.</summary>
    public NpmManageWorkflow(HttpContext httpContext, string? id)
        : base(httpContext)
    {
        Id = id;
    }

    /// <summary>The package name from the address (normalized by <see cref="EnterAsync"/>).</summary>
    public string? Id { get; set; }

    /// <summary>What the screen shows.</summary>
    public NpmManageViewModel Model { get; } = new();

    /// <summary>The address npm is configured with for this repository.</summary>
    public string RegistryUrl => HttpContext.RequestServices.GetRequiredService<IUrlGenerator>().GetNpmRegistryUrl();

    /// <summary>How long after publishing a version its owner may unpublish it with Deploy alone.</summary>
    public TimeSpan UnpublishWindow => HttpContext.RequestServices.GetRequiredService<IOptionsSnapshot<NpmOptions>>().Value.UnpublishWindow;

    private INpmRegistry Registry => HttpContext.RequestServices.GetRequiredService<INpmRegistry>();

    /// <summary>
    /// The check that runs before every handler: a valid name, a local repository, ownership or administration, the maintenance
    /// gate and read-only mode for a write. Returns the result that ends the request, or null to run the handler.
    /// </summary>
    /// <param name="handlerName">The handler about to run (for the administrator audit line).</param>
    public async Task<IActionResult?> EnterAsync(string? handlerName)
    {
        if (!NpmNames.TryParse(Id, out var name, out _))
        {
            return NotFound();
        }

        Id = name;

        // Owners and the owner tools exist where packages are published: a remote repository only caches an upstream's (every
        // change is refused there) and a virtual one holds none, so neither has this screen (the repository check says the same;
        // the controller's own check can run before it, so the rule is kept here too).
        if (HttpContext.RequestServices.GetRequiredService<IRepositoryContext>().Current.Kind != RepositoryKind.Local)
        {
            return NotFound();
        }

        #pragma warning disable CS8602
        var isOwner = await Ownership.IsOwnerAsync(UserManager.GetUserId(User), User.Identity.Name, Id, HttpContext.RequestAborted);
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
                HttpContext.RequestServices.GetRequiredService<ILogger<NpmManageWorkflow>>().LogWarning(
                    "Administrator {Admin} ran {Handler} on npm package {Package}",
                    User.Identity.Name,
                    handlerName,
                    Id);
                #pragma warning restore CS8602
            }
        }

        return null;
    }

    /// <summary>The GET handler: loads the package, 404 when it does not exist.</summary>
    public async Task<IActionResult> GetAsync(CancellationToken cancellationToken)
    {
        await LoadAsync(cancellationToken);

        return Model.Package == null ? NotFound() : Show;
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        var current = await UserManager.GetUserAsync(User);
        Model.HasPassword = current == null || await UserManager.HasPasswordAsync(current);
        Model.Reauthenticated = !Model.HasPassword && await HasReauthenticatedRecentlyAsync();
        Model.CanReauthenticate = CanReauthenticate;
        Model.RegistryUrl = RegistryUrl;
        Model.UnpublishWindow = UnpublishWindow;
        Model.Id = Id;

        Model.Package = await Registry.GetPackageAsync(Id!, cancellationToken);
        Model.Owners = await Ownership.GetOwnersAsync(Id, cancellationToken);
    }

    private async Task<IActionResult> DoneAsync(string message, bool failed, CancellationToken cancellationToken)
    {
        await LoadAsync(cancellationToken);
        Model.Message = message;
        Model.Failed = failed;

        return Show;
    }

    private Task<IActionResult> DoneAsync(NpmResult result, string success, CancellationToken cancellationToken)
        => DoneAsync(result.Succeeded ? success : result.Message ?? "Not done.", !result.Succeeded, cancellationToken);

    /// <summary>Adds an owner.</summary>
    public async Task<IActionResult> AddOwnerAsync(string ownerName, CancellationToken cancellationToken)
    {
        var result = string.IsNullOrWhiteSpace(ownerName)
            ? OwnerChangeResult.UnknownAccount
            : await Ownership.AddOwnerAsync(Id, ownerName.Trim(), cancellationToken);

        return await DoneAsync(result == OwnerChangeResult.Success ? $"{ownerName} is now an owner." : Describe(result, "package"), result != OwnerChangeResult.Success, cancellationToken);
    }

    /// <summary>Removes an owner.</summary>
    public async Task<IActionResult> RemoveOwnerAsync(string ownerName, CancellationToken cancellationToken)
    {
        var result = await Ownership.RemoveOwnerAsync(Id, ownerName, cancellationToken);

        return await DoneAsync(result == OwnerChangeResult.Success ? $"{ownerName} is no longer an owner." : Describe(result, "package"), result != OwnerChangeResult.Success, cancellationToken);
    }

    /// <summary>Points a dist-tag at a version.</summary>
    public async Task<IActionResult> SetTagAsync(string tag, string version, CancellationToken cancellationToken)
    {
        var result = await Registry.SetDistTagAsync(await CallerAsync(), Id!, tag?.Trim() ?? string.Empty, version ?? string.Empty, cancellationToken);

        return await DoneAsync(result, $"{tag} now points at {version}.", cancellationToken);
    }

    /// <summary>Removes a dist-tag.</summary>
    public async Task<IActionResult> RemoveTagAsync(string tag, CancellationToken cancellationToken)
    {
        var result = await Registry.RemoveDistTagAsync(await CallerAsync(), Id!, tag ?? string.Empty, cancellationToken);

        return await DoneAsync(result, $"The tag {tag} is removed.", cancellationToken);
    }

    /// <summary>Deprecates a version.</summary>
    public async Task<IActionResult> DeprecateAsync(string version, string? message, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return await DoneAsync("Write the deprecation message.", failed: true, cancellationToken);
        }

        var result = await Registry.DeprecateAsync(await CallerAsync(), Id!, new Dictionary<string, string?> { [version ?? string.Empty] = message.Trim() }, cancellationToken);

        return await DoneAsync(result, $"{Id}@{version} is deprecated.", cancellationToken);
    }

    /// <summary>Removes a version's deprecation.</summary>
    public async Task<IActionResult> UndeprecateAsync(string version, CancellationToken cancellationToken)
    {
        var result = await Registry.DeprecateAsync(await CallerAsync(), Id!, new Dictionary<string, string?> { [version ?? string.Empty] = null }, cancellationToken);

        return await DoneAsync(result, $"{Id}@{version} is no longer deprecated.", cancellationToken);
    }

    /// <summary>Unpublishes one version and deletes its tarball, after the password is confirmed.</summary>
    public async Task<IActionResult> UnpublishAsync(string version, string? password, CancellationToken cancellationToken)
    {
        var refusal = await RefuseUnconfirmedAsync(password, cancellationToken);
        if (refusal != null) return refusal;

        var result = await Registry.UnpublishVersionsAsync(await CallerAsync(), Id!, new[] { version ?? string.Empty }, newTags: null, cancellationToken);
        if (result.Succeeded) Log().LogWarning("{User} unpublished {Package}@{Version}", User.Identity?.Name, Id, version);

        // The last version takes the package with it: nothing left to manage.
        if (result.Succeeded && await Registry.GetPackageAsync(Id!, cancellationToken) == null)
        {
            return RedirectToPage("/NpmPackages");
        }

        return await DoneAsync(result, $"{Id}@{version} is unpublished. Its tarball is deleted.", cancellationToken);
    }

    /// <summary>Unpublishes every version of the package, after the password is confirmed.</summary>
    public async Task<IActionResult> UnpublishAllAsync(string? password, CancellationToken cancellationToken)
    {
        var refusal = await RefuseUnconfirmedAsync(password, cancellationToken);
        if (refusal != null) return refusal;

        var result = await Registry.UnpublishPackageAsync(await CallerAsync(), Id!, cancellationToken);
        if (!result.Succeeded) return await DoneAsync(result, string.Empty, cancellationToken);

        Log().LogWarning("{User} unpublished npm package {Package}", User.Identity?.Name, Id);

        return RedirectToPage("/NpmPackages");
    }

    /// <summary>Null when the signed-in user confirmed with their password, otherwise the screen to show.</summary>
    private async Task<IActionResult?> RefuseUnconfirmedAsync(string? password, CancellationToken cancellationToken)
    {
        var current = await UserManager.GetUserAsync(User);
        if (current == null) return Challenge();

        return await IsConfirmedAsync(current, password)
            ? null
            : await DoneAsync("The confirmation is incorrect, so nothing was unpublished.", failed: true, cancellationToken);
    }

    private ILogger<NpmManageWorkflow> Log() => HttpContext.RequestServices.GetRequiredService<ILogger<NpmManageWorkflow>>();
}
