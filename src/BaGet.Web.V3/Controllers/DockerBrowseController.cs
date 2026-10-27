using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.Controllers.Base;
using BaGet.Identity.Mvc;
using BaGet.Web.V3.Controllers.UI;
using BaGet.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Web.V3.Controllers;

/// <summary>
/// The web pages of a Docker repository: the image list (<c>/docker-images</c>) and one image
/// (<c>/docker-images/{image}</c>, <c>?digest=</c> for a manifest). They replace the Razor Pages <c>DockerImages</c> and
/// <c>DockerImage</c>: same URLs, same <c>asp-page</c> names
/// (<see cref="LegacyPageAttribute"/>), same repository rules (<see cref="RepositoryPageAttribute"/>: Read on the repository and,
/// for the image page, on the image; 404 in a repository that is not Docker's). Views are in <c>Views/DockerBrowse/</c>. The work is <see cref="BaGet.Web.Application.DockerBrowseUseCase"/>, reached through the JSON twin
/// <see cref="DockerBrowseUiApiController"/> in process; this controller only renders what it answers.
/// </summary>
public sealed class DockerBrowseController : UiControllerBase
{
    /// <summary>The image list with a search box and the client commands.</summary>
    [Route("docker-images"), HttpGet, HttpHead, PageHandler, LegacyPage("/DockerImages")]
    [RepositoryPage(Format = RepositoryFormat.Docker)]
    public async Task<IActionResult> Images(
        string? q,
        [Range(1, int.MaxValue)] int p = 1,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid) return BadRequest();

        return Render(await Api<DockerBrowseUiApiController, DockerImagesViewModel>(api => api.Images(q, p, cancellationToken)), "Images");
    }

    /// <summary>One image: tags, manifests, and with <c>?digest=</c> one manifest in full.</summary>
    [Route("docker-images/{**image}"), HttpGet, HttpHead, PageHandler, LegacyPage("/DockerImage")]
    [RepositoryPage(Format = RepositoryFormat.Docker)]
    public async Task<IActionResult> Image(string? image, string? digest, string? artifactType, CancellationToken cancellationToken = default)
        => Render(await Api<DockerBrowseUiApiController, DockerImageViewModel>(api => api.Image(image, digest, artifactType, cancellationToken)), "Image");
}
