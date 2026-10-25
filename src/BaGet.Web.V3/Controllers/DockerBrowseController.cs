using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace BaGet.Web;

/// <summary>
/// The web pages of a Docker repository: the image list (<c>/docker-images</c>) and one image
/// (<c>/docker-images/{image}</c>, <c>?digest=</c> for a manifest). They replace the Razor Pages <c>DockerImages</c> and
/// <c>DockerImage</c> (their page models are obsolete shims over the same workflows): same URLs, same <c>asp-page</c> names
/// (<see cref="LegacyPageAttribute"/>), same repository rules (<see cref="RepositoryPageAttribute"/>: Read on the repository and,
/// for the image page, on the image; 404 in a repository that is not Docker's). Views are in <c>Views/DockerBrowse/</c>.
/// </summary>
public sealed class DockerBrowseController : UiControllerBase
{
    private DockerImagesWorkflow ImagesWorkflow => HttpContext.RequestServices.GetRequiredService<DockerImagesWorkflow>();

    private DockerImageWorkflow ImageWorkflow => HttpContext.RequestServices.GetRequiredService<DockerImageWorkflow>();

    /// <summary>The image list with a search box and the client commands.</summary>
    [Route("docker-images"), HttpGet, HttpHead, PageHandler, LegacyPage("/DockerImages")]
    [RepositoryPage(Format = RepositoryFormat.Docker)]
    public async Task<IActionResult> Images(
        string? q,
        [Range(1, int.MaxValue)] int p = 1,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid) return BadRequest();

        return View("Images", await ImagesWorkflow.LoadAsync(q, p, User, cancellationToken));
    }

    /// <summary>One image: tags, manifests, and with <c>?digest=</c> one manifest in full.</summary>
    [Route("docker-images/{**image}"), HttpGet, HttpHead, PageHandler, LegacyPage("/DockerImage")]
    [RepositoryPage(Format = RepositoryFormat.Docker)]
    public async Task<IActionResult> Image(
        string? image,
        string? digest,
        string? artifactType,
        CancellationToken cancellationToken = default)
    {
        var model = await ImageWorkflow.LoadAsync(image, digest, artifactType, User, HttpContext.RequestServices, cancellationToken);
        if (model == null) return NotFound();

        if (model.StatusCode != StatusCodes.Status200OK) Response.StatusCode = model.StatusCode;

        return View("Image", model);
    }
}
