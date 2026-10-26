using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.Application;
using BaGet.Identity.Mvc;
using BaGet.Identity.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace BaGet.Identity.Controllers;

/// <summary>The form of the Docker management handlers; every one asks for the caller's password.</summary>
public sealed class DockerManageForm
{
    /// <summary>The tag to delete (<c>DeleteTag</c>).</summary>
    public string? Tag { get; set; }

    /// <summary>The manifest digest to delete (<c>DeleteManifest</c>).</summary>
    public string? Digest { get; set; }

    /// <summary>The caller's password.</summary>
    public string? Password { get; set; }
}

/// <summary>
/// The JSON twin of the Docker image management screen: <c>GET /api/ui/docker-manage/{**image}</c> and, for each handler of the page,
/// <c>POST /api/ui/docker-manage/{handler-in-kebab-case}/{**image}</c> with a JSON body (the image name has slashes, so the handler comes
/// first; also under <c>/api/ui/r/{repo}/…</c>). Cookie session only. <see cref="DockerManageController"/> calls it in process and renders
/// the view.
/// </summary>
[Route("api/ui/docker-manage")]
[RepositoryPage(Format = RepositoryFormat.Docker, NotOnVirtual = true)]
public sealed class DockerManageUiApiController : AccountUiApiControllerBase
{
    private const string Screen = "/Packages/DockerManage";

    /// <inheritdoc/>
    protected override bool RequiresSignIn => true;

    private DockerManageUseCase UseCase => HttpContext.RequestServices.GetRequiredService<DockerManageUseCase>();

    /// <summary>The image-name / repository-kind / Delete-permission check that opens every handler (see <see cref="PackageManageUiApiController.EnterAsync"/>).</summary>
    [NonAction]
    public Task<ScreenResult<DockerManageViewModel>?> EnterAsync(string? image, bool isWrite, CancellationToken cancellationToken)
        => UseCase.EnterAsync(User, image, isWrite, cancellationToken);

    /// <summary>The screen.</summary>
    [HttpGet("{**image}"), UiApi(Screen)]
    public async Task<ActionResult<ScreenResult<DockerManageViewModel>>> Get(string? image, CancellationToken cancellationToken)
        => Respond(await UseCase.GetAsync(User, image, cancellationToken));

    /// <summary>Deletes a tag, after the password.</summary>
    [HttpPost("delete-tag/{**image}"), UiApi(Screen, Handler = "DeleteTag")]
    public async Task<ActionResult<ScreenResult<DockerManageViewModel>>> DeleteTag(string? image, [FromBody] DockerManageForm? form, CancellationToken cancellationToken)
        => Respond(await UseCase.DeleteTagAsync(User, image, form?.Tag, form?.Password, cancellationToken));

    /// <summary>Deletes a manifest with its tags, after the password.</summary>
    [HttpPost("delete-manifest/{**image}"), UiApi(Screen, Handler = "DeleteManifest")]
    public async Task<ActionResult<ScreenResult<DockerManageViewModel>>> DeleteManifest(string? image, [FromBody] DockerManageForm? form, CancellationToken cancellationToken)
        => Respond(await UseCase.DeleteManifestAsync(User, image, form?.Digest, form?.Password, cancellationToken));

    /// <summary>Removes the layers no manifest uses, after the password.</summary>
    [HttpPost("prune/{**image}"), UiApi(Screen, Handler = "Prune")]
    public async Task<ActionResult<ScreenResult<DockerManageViewModel>>> Prune(string? image, [FromBody] DockerManageForm? form, CancellationToken cancellationToken)
        => Respond(await UseCase.PruneAsync(User, image, form?.Password, cancellationToken));

    /// <summary>Deletes the whole image, after the password.</summary>
    [HttpPost("delete-image/{**image}"), UiApi(Screen, Handler = "DeleteImage")]
    public async Task<ActionResult<ScreenResult<DockerManageViewModel>>> DeleteImage(string? image, [FromBody] DockerManageForm? form, CancellationToken cancellationToken)
        => Respond(await UseCase.DeleteImageAsync(User, image, form?.Password, cancellationToken));
}
