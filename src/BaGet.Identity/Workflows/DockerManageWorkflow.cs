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
/// The delete tools of a Docker image: delete a tag, a manifest (with its tags), remove the layers no manifest uses, or delete the
/// whole image. Every change goes through <see cref="IOciRegistry"/> after the same check the registry applies to a
/// <c>DELETE</c> (<see cref="IOciAccess"/>: the Delete action on the image, for the signed-in user's session principal, never a
/// typed key), so the screen can do what the same user's API key could through the registry routes and no more. Opening it needs
/// Delete; every change also asks for the password again, since it cannot be undone. Works in the request's repository. Served by
/// <c>DockerManageController</c>.
/// </summary>
internal sealed class DockerManageWorkflow : PackageWorkflowBase
{
    /// <summary>What every write handler answers on a read-only server.</summary>
    internal const string ReadOnlyMessage = "This server is in read-only mode, so nothing can be changed.";

    /// <summary>Creates the workflow of one request.</summary>
    public DockerManageWorkflow(HttpContext httpContext, string? image)
        : base(httpContext)
    {
        Image = image;
    }

    /// <summary>The image name from the address (it may contain slashes).</summary>
    public string? Image { get; set; }

    /// <summary>What the screen shows.</summary>
    public DockerManageViewModel Model { get; } = new();

    private IOciRegistry Registry => HttpContext.RequestServices.GetRequiredService<IOciRegistry>();

    /// <summary>
    /// The check that runs before every handler: a valid image name, a local Docker repository, the Delete action on the image,
    /// the maintenance gate and read-only mode for a write. Returns the result that ends the request, or null to run the handler.
    /// </summary>
    public async Task<IActionResult?> EnterAsync()
    {
        if (!OciNames.IsValidImage(Image))
        {
            return NotFound();
        }

        // The tools exist where images are pushed: only a local Docker repository (a page of another format is 404, as the
        // repository check says; the controller's own check can run before it).
        var current = HttpContext.RequestServices.GetRequiredService<IRepositoryContext>().Current;
        if (current.Kind != RepositoryKind.Local || current.Format != RepositoryFormat.Docker)
        {
            return NotFound();
        }

        // The rule of a DELETE on the registry: the Delete action on the image, for this user.
        var principal = await CallerAsync();
        if (principal == null || !await HttpContext.RequestServices.GetRequiredService<IOciAccess>().IsAllowedAsync(principal, current, Image!, OciActions.Delete, HttpContext.RequestAborted))
        {
            return StatusCode(403);
        }

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
        }

        return null;
    }

    /// <summary>The GET handler: loads the image, 404 when it does not exist.</summary>
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
        Model.Image = Image;

        Model.Info = await Registry.GetImageAsync(Image!, cancellationToken);
    }

    private async Task<IActionResult> DoneAsync(string message, bool failed, CancellationToken cancellationToken)
    {
        await LoadAsync(cancellationToken);
        Model.Message = message;
        Model.Failed = failed;

        return Show;
    }

    /// <summary>Deletes a tag (the manifest it points at stays), after the password is confirmed.</summary>
    public async Task<IActionResult> DeleteTagAsync(string? tag, string? password, CancellationToken cancellationToken)
    {
        var refusal = await RefuseUnconfirmedAsync(password, cancellationToken);
        if (refusal != null) return refusal;

        if (!OciNames.IsValidTag(tag)) return await DoneAsync("Choose a tag.", failed: true, cancellationToken);

        var deleted = await Registry.DeleteTagAsync(Image!, tag!, cancellationToken);
        if (deleted) Log().LogWarning("{User} deleted tag {Tag} of Docker image {Image}", User.Identity?.Name, tag, Image);

        return await DoneAsync(deleted ? $"Tag {tag} is deleted." : "There is no such tag.", !deleted, cancellationToken);
    }

    /// <summary>Deletes a manifest with the tags that point at it, after the password is confirmed.</summary>
    public async Task<IActionResult> DeleteManifestAsync(string? digest, string? password, CancellationToken cancellationToken)
    {
        var refusal = await RefuseUnconfirmedAsync(password, cancellationToken);
        if (refusal != null) return refusal;

        if (!OciNames.IsValidDigest(digest)) return await DoneAsync("Choose a manifest.", failed: true, cancellationToken);

        var deleted = await Registry.DeleteManifestAsync(Image!, digest!, cancellationToken);
        if (deleted) Log().LogWarning("{User} deleted manifest {Digest} of Docker image {Image}", User.Identity?.Name, digest, Image);

        return await DoneAsync(deleted ? "The manifest and its tags are deleted." : "There is no such manifest.", !deleted, cancellationToken);
    }

    /// <summary>Removes the layers no manifest uses (older than the grace period), after the password is confirmed.</summary>
    public async Task<IActionResult> PruneAsync(string? password, CancellationToken cancellationToken)
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
    public async Task<IActionResult> DeleteImageAsync(string? password, CancellationToken cancellationToken)
    {
        var refusal = await RefuseUnconfirmedAsync(password, cancellationToken);
        if (refusal != null) return refusal;

        if (!await Registry.DeleteImageAsync(Image!, cancellationToken)) return await DoneAsync("There is no such image.", failed: true, cancellationToken);

        Log().LogWarning("{User} deleted Docker image {Image}", User.Identity?.Name, Image);

        return RedirectToPage("/DockerImages");
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

    private ILogger<DockerManageWorkflow> Log() => HttpContext.RequestServices.GetRequiredService<ILogger<DockerManageWorkflow>>();
}
