using System;
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
/// The delete tools of a Docker image: delete a tag, a manifest (with its tags), remove the layers no manifest uses, or delete the
/// whole image. Every change goes through <see cref="IOciRegistry"/> after the same check the registry applies to a
/// <c>DELETE</c> (<see cref="IOciAccess"/>: the Delete action on the image, for the signed-in user's session principal, never a
/// typed key), so the page can do what the same user's API key could through the registry routes and no more. Opening it needs
/// Delete; every change also asks for the password again, since it cannot be undone. Works in the request's repository.
/// </summary>
public class DockerManageModel : AccountPageModel
{
    internal const string ReadOnlyMessage = "This server is in read-only mode, so nothing can be changed.";

    protected override bool RequiresSignIn => true;

    /// <summary>The image name from the address (it may contain slashes).</summary>
    [BindProperty(SupportsGet = true)]
    public string? Image { get; set; }

    /// <summary>The image, or null before it is loaded (or after it was deleted).</summary>
    public OciImageInfo? Info { get; private set; }

    /// <summary>The outcome of the last action.</summary>
    public string? Message { get; private set; }

    /// <summary>Whether <see cref="Message"/> reports a refusal.</summary>
    public bool Failed { get; private set; }

    /// <summary>Whether the user has a password to confirm with.</summary>
    public bool HasPassword { get; private set; } = true;

    /// <summary>For an account without a password: it signed in again with its provider a moment ago.</summary>
    public bool Reauthenticated { get; private set; }

    /// <summary>Whether an external provider is available to sign in again with.</summary>
    public bool CanReauthenticate => Accounts.OpenIdConnect.IsConfigured;

    private IOciRegistry Registry => HttpContext.RequestServices.GetRequiredService<IOciRegistry>();

    private bool IsReadOnlyMode => HttpContext.RequestServices.GetRequiredService<IOptionsSnapshot<BaGetOptions>>().Value.IsReadOnlyMode;

    public override async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        OnPageHandlerExecuting(context);
        if (context.Result != null) return;

        if (!OciNames.IsValidImage(Image))
        {
            context.Result = NotFound();
            return;
        }

        // The tools exist where images are pushed: only a local Docker repository (a page of another format is 404, as
        // RepositoryPageFilter says; the model filter can run before it).
        var current = HttpContext.RequestServices.GetRequiredService<IRepositoryContext>().Current;
        if (current.Kind != RepositoryKind.Local || current.Format != RepositoryFormat.Docker)
        {
            context.Result = NotFound();
            return;
        }

        // The rule of a DELETE on the registry: the Delete action on the image, for this user.
        var principal = await HttpContext.RequestServices.GetRequiredService<ISessionPrincipalFactory>().CreateAsync(User);
        if (principal == null || !await HttpContext.RequestServices.GetRequiredService<IOciAccess>().IsAllowedAsync(principal, current, Image!, OciActions.Delete, HttpContext.RequestAborted))
        {
            context.Result = StatusCode(403);
            return;
        }

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

        Info = await Registry.GetImageAsync(Image!, cancellationToken);
    }

    private async Task<IActionResult> DoneAsync(string message, bool failed, CancellationToken cancellationToken)
    {
        await LoadAsync(cancellationToken);
        Message = message;
        Failed = failed;

        return Page();
    }

    /// <summary>Deletes a tag (the manifest it points at stays), after the password is confirmed.</summary>
    public async Task<IActionResult> OnPostDeleteTagAsync(string? tag, string? password, CancellationToken cancellationToken)
    {
        var refusal = await RefuseUnconfirmedAsync(password, cancellationToken);
        if (refusal != null) return refusal;

        if (!OciNames.IsValidTag(tag)) return await DoneAsync("Choose a tag.", failed: true, cancellationToken);

        var deleted = await Registry.DeleteTagAsync(Image!, tag!, cancellationToken);
        if (deleted) Log().LogWarning("{User} deleted tag {Tag} of Docker image {Image}", User.Identity?.Name, tag, Image);

        return await DoneAsync(deleted ? $"Tag {tag} is deleted." : "There is no such tag.", !deleted, cancellationToken);
    }

    /// <summary>Deletes a manifest with the tags that point at it, after the password is confirmed.</summary>
    public async Task<IActionResult> OnPostDeleteManifestAsync(string? digest, string? password, CancellationToken cancellationToken)
    {
        var refusal = await RefuseUnconfirmedAsync(password, cancellationToken);
        if (refusal != null) return refusal;

        if (!OciNames.IsValidDigest(digest)) return await DoneAsync("Choose a manifest.", failed: true, cancellationToken);

        var deleted = await Registry.DeleteManifestAsync(Image!, digest!, cancellationToken);
        if (deleted) Log().LogWarning("{User} deleted manifest {Digest} of Docker image {Image}", User.Identity?.Name, digest, Image);

        return await DoneAsync(deleted ? "The manifest and its tags are deleted." : "There is no such manifest.", !deleted, cancellationToken);
    }

    /// <summary>Removes the layers no manifest uses (older than the grace period), after the password is confirmed.</summary>
    public async Task<IActionResult> OnPostPruneAsync(string? password, CancellationToken cancellationToken)
    {
        var refusal = await RefuseUnconfirmedAsync(password, cancellationToken);
        if (refusal != null) return refusal;

        var result = await Registry.PruneAsync(Image!, cancellationToken);
        if (result.Blobs > 0) Log().LogWarning("{User} pruned {Count} unused layers of Docker image {Image}", User.Identity?.Name, result.Blobs, Image);

        return await DoneAsync(
            result.Blobs == 0
                ? "There was no unused layer old enough to remove."
                : $"Removed {result.Blobs} unused layer{(result.Blobs == 1 ? string.Empty : "s")} ({result.Bytes:N0} bytes).",
            failed: false,
            cancellationToken);
    }

    /// <summary>Deletes the image with every manifest, tag and layer only it holds, after the password is confirmed.</summary>
    public async Task<IActionResult> OnPostDeleteImageAsync(string? password, CancellationToken cancellationToken)
    {
        var refusal = await RefuseUnconfirmedAsync(password, cancellationToken);
        if (refusal != null) return refusal;

        if (!await Registry.DeleteImageAsync(Image!, cancellationToken)) return await DoneAsync("There is no such image.", failed: true, cancellationToken);

        Log().LogWarning("{User} deleted Docker image {Image}", User.Identity?.Name, Image);

        return RedirectToPage("/DockerImages");
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

    private ILogger<DockerManageModel> Log() => HttpContext.RequestServices.GetRequiredService<ILogger<DockerManageModel>>();
}
