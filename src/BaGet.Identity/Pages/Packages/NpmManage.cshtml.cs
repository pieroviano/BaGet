using System;
using System.Collections.Generic;
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
/// The owners' page for an npm package: its owners, dist-tags, per-version deprecation and unpublishing. Every change goes
/// through <see cref="INpmRegistry"/> as the signed-in user (the session's principal, never a typed key), so the page can do what
/// the same user's API key could through the registry routes and no more: the access model, ownership, and the unpublish
/// window apply identically. Opening it takes ownership (directly or through an organization) or administration; unpublishing
/// also asks for the password again, since it cannot be undone. Works in the request's repository.
/// </summary>
public class NpmManageModel : AccountPageModel
{
    internal const string ReadOnlyMessage = "This server is in read-only mode, so nothing can be changed.";

    protected override bool RequiresSignIn => true;

    /// <summary>The package name from the address.</summary>
    [BindProperty(SupportsGet = true)]
    public string? Id { get; set; }

    /// <summary>The package, or null before it is loaded.</summary>
    public NpmPackageInfo? Package { get; private set; }

    /// <summary>The package's owners.</summary>
    public IReadOnlyList<PackageOwner> Owners { get; private set; } = Array.Empty<PackageOwner>();

    /// <summary>The outcome of the last action.</summary>
    public string? Message { get; private set; }

    /// <summary>Whether <see cref="Message"/> reports a refusal.</summary>
    public bool Failed { get; private set; }

    /// <summary>Whether the signed-in user is an administrator.</summary>
    public bool IsAdministrator { get; private set; }

    /// <summary>Whether the user manages the package only as an administrator (they are not an owner).</summary>
    public bool ActingAsAdministrator { get; private set; }

    /// <summary>Whether the user has a password to confirm with.</summary>
    public bool HasPassword { get; private set; } = true;

    /// <summary>For an account without a password: it signed in again with its provider a moment ago.</summary>
    public bool Reauthenticated { get; private set; }

    /// <summary>Whether an external provider is available to sign in again with.</summary>
    public bool CanReauthenticate => Accounts.OpenIdConnect.IsConfigured;

    /// <summary>The address npm is configured with for this repository.</summary>
    public string RegistryUrl => HttpContext.RequestServices.GetRequiredService<IUrlGenerator>().GetNpmRegistryUrl();

    /// <summary>How long after publishing a version its owner may unpublish it with Deploy alone.</summary>
    public TimeSpan UnpublishWindow => HttpContext.RequestServices.GetRequiredService<IOptionsSnapshot<NpmOptions>>().Value.UnpublishWindow;

    private INpmRegistry Registry => HttpContext.RequestServices.GetRequiredService<INpmRegistry>();

    private bool IsReadOnlyMode => HttpContext.RequestServices.GetRequiredService<IOptionsSnapshot<BaGetOptions>>().Value.IsReadOnlyMode;

    public override async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        OnPageHandlerExecuting(context);
        if (context.Result != null) return;

        if (!NpmNames.TryParse(Id, out var name, out _))
        {
            context.Result = NotFound();
            return;
        }

        Id = name;

        // Owners and the owner tools exist where packages are published: a remote repository only caches an upstream's (every
        // change is refused there) and a virtual one holds none, so neither has this page (RepositoryPageFilter says the same;
        // the model's own filter can run before it, so the rule is kept here too).
        if (HttpContext.RequestServices.GetRequiredService<IRepositoryContext>().Current.Kind != RepositoryKind.Local)
        {
            context.Result = NotFound();
            return;
        }

        #pragma warning disable CS8602
        var isOwner = await Ownership.IsOwnerAsync(UserManager.GetUserId(User), User.Identity.Name, Id, HttpContext.RequestAborted);
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
                HttpContext.RequestServices.GetRequiredService<ILogger<NpmManageModel>>().LogWarning(
                    "Administrator {Admin} ran {Handler} on npm package {Package}",
                    User.Identity.Name,
                    context.HandlerMethod?.Name,
                    Id);
            }
        }

        await next();
    }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadAsync(cancellationToken);

        return Package == null ? NotFound() : Page();
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        var current = await UserManager.GetUserAsync(User);
        HasPassword = current == null || await UserManager.HasPasswordAsync(current);
        Reauthenticated = !HasPassword && await HasReauthenticatedRecentlyAsync();

        Package = await Registry.GetPackageAsync(Id!, cancellationToken);
        Owners = await Ownership.GetOwnersAsync(Id, cancellationToken);
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

    private Task<IActionResult> DoneAsync(NpmResult result, string success, CancellationToken cancellationToken)
        => DoneAsync(result.Succeeded ? success : result.Message ?? "Not done.", !result.Succeeded, cancellationToken);

    public async Task<IActionResult> OnPostAddOwnerAsync(string ownerName, CancellationToken cancellationToken)
    {
        var result = string.IsNullOrWhiteSpace(ownerName)
            ? OwnerChangeResult.UnknownAccount
            : await Ownership.AddOwnerAsync(Id, ownerName.Trim(), cancellationToken);

        return await DoneAsync(result == OwnerChangeResult.Success ? $"{ownerName} is now an owner." : Describe(result), result != OwnerChangeResult.Success, cancellationToken);
    }

    public async Task<IActionResult> OnPostRemoveOwnerAsync(string ownerName, CancellationToken cancellationToken)
    {
        var result = await Ownership.RemoveOwnerAsync(Id, ownerName, cancellationToken);

        return await DoneAsync(result == OwnerChangeResult.Success ? $"{ownerName} is no longer an owner." : Describe(result), result != OwnerChangeResult.Success, cancellationToken);
    }

    public async Task<IActionResult> OnPostSetTagAsync(string tag, string version, CancellationToken cancellationToken)
    {
        var result = await Registry.SetDistTagAsync(await CallerAsync(), Id!, tag?.Trim() ?? string.Empty, version ?? string.Empty, cancellationToken);

        return await DoneAsync(result, $"{tag} now points at {version}.", cancellationToken);
    }

    public async Task<IActionResult> OnPostRemoveTagAsync(string tag, CancellationToken cancellationToken)
    {
        var result = await Registry.RemoveDistTagAsync(await CallerAsync(), Id!, tag ?? string.Empty, cancellationToken);

        return await DoneAsync(result, $"The tag {tag} is removed.", cancellationToken);
    }

    public async Task<IActionResult> OnPostDeprecateAsync(string version, string? message, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return await DoneAsync("Write the deprecation message.", failed: true, cancellationToken);
        }

        var result = await Registry.DeprecateAsync(await CallerAsync(), Id!, new Dictionary<string, string?> { [version ?? string.Empty] = message.Trim() }, cancellationToken);

        return await DoneAsync(result, $"{Id}@{version} is deprecated.", cancellationToken);
    }

    public async Task<IActionResult> OnPostUndeprecateAsync(string version, CancellationToken cancellationToken)
    {
        var result = await Registry.DeprecateAsync(await CallerAsync(), Id!, new Dictionary<string, string?> { [version ?? string.Empty] = null }, cancellationToken);

        return await DoneAsync(result, $"{Id}@{version} is no longer deprecated.", cancellationToken);
    }

    /// <summary>Unpublishes one version and deletes its tarball, after the password is confirmed.</summary>
    public async Task<IActionResult> OnPostUnpublishAsync(string version, string? password, CancellationToken cancellationToken)
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
    public async Task<IActionResult> OnPostUnpublishAllAsync(string? password, CancellationToken cancellationToken)
    {
        var refusal = await RefuseUnconfirmedAsync(password, cancellationToken);
        if (refusal != null) return refusal;

        var result = await Registry.UnpublishPackageAsync(await CallerAsync(), Id!, cancellationToken);
        if (!result.Succeeded) return await DoneAsync(result, string.Empty, cancellationToken);

        Log().LogWarning("{User} unpublished npm package {Package}", User.Identity?.Name, Id);

        return RedirectToPage("/NpmPackages");
    }

    /// <summary>Null when the signed-in user confirmed with their password, otherwise the page to show.</summary>
    private async Task<IActionResult?> RefuseUnconfirmedAsync(string? password, CancellationToken cancellationToken)
    {
        var current = await UserManager.GetUserAsync(User);
        if (current == null) return Challenge();

        return await IsConfirmedAsync(current, password)
            ? null
            : await DoneAsync("The confirmation is incorrect, so nothing was unpublished.", failed: true, cancellationToken);
    }

    private ILogger<NpmManageModel> Log() => HttpContext.RequestServices.GetRequiredService<ILogger<NpmManageModel>>();

    private static string Describe(OwnerChangeResult result) => result switch
    {
        OwnerChangeResult.UnknownAccount => "There is no such user or organization.",
        OwnerChangeResult.AlreadyOwner => "That account already owns the package.",
        OwnerChangeResult.NotOwner => "That account is not an owner.",
        OwnerChangeResult.LastOwner => "A package keeps at least one owner.",
        _ => result.ToString(),
    };
}
