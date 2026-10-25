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
/// The web pages of a Maven repository: the artifact list (<c>/maven-artifacts</c>), one artifact
/// (<c>/maven-artifacts/{group}/{artifact}/{version?}</c>) and the browser deployment (<c>/maven-upload</c>). They replace the Razor
/// Pages <c>MavenArtifacts</c>, <c>MavenArtifact</c> and <c>MavenUpload</c>: same URLs, same <c>asp-page</c> names (<see cref="LegacyPageAttribute"/>), same repository rules
/// (<see cref="RepositoryPageAttribute"/>: Read on the repository, on <c>group:artifact</c> for the artifact page, the right to
/// publish for the upload; 404 in a repository that is not Maven's). Views are in <c>Views/MavenBrowse/</c>.
/// </summary>
public sealed class MavenBrowseController : UiControllerBase
{
    private MavenArtifactsWorkflow ArtifactsWorkflow => HttpContext.RequestServices.GetRequiredService<MavenArtifactsWorkflow>();

    private MavenArtifactWorkflow ArtifactWorkflow => HttpContext.RequestServices.GetRequiredService<MavenArtifactWorkflow>();

    private MavenUploadWorkflow UploadWorkflow => HttpContext.RequestServices.GetRequiredService<MavenUploadWorkflow>();

    /// <summary>The artifact list with a search box and the client configuration.</summary>
    [Route("maven-artifacts"), HttpGet, HttpHead, PageHandler, LegacyPage("/MavenArtifacts")]
    [RepositoryPage(Format = RepositoryFormat.Maven)]
    public async Task<IActionResult> Artifacts(
        string? q,
        [Range(1, int.MaxValue)] int p = 1,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid) return BadRequest();

        return View("Artifacts", await ArtifactsWorkflow.LoadAsync(q, p, User, cancellationToken));
    }

    /// <summary>One artifact: versions, files, POM, the dependency to copy.</summary>
    [Route("maven-artifacts/{group}/{artifact}/{version?}"), HttpGet, HttpHead, PageHandler, LegacyPage("/MavenArtifact")]
    [RepositoryPage(Format = RepositoryFormat.Maven)]
    public async Task<IActionResult> Artifact(
        string? group,
        string? artifact,
        string? version,
        CancellationToken cancellationToken = default)
    {
        var model = await ArtifactWorkflow.LoadAsync(group, artifact, version, User, HttpContext.RequestServices, cancellationToken);
        if (model == null) return NotFound();

        if (model.StatusCode != StatusCodes.Status200OK) Response.StatusCode = model.StatusCode;

        return View("Artifact", model);
    }

    /// <summary>The upload form.</summary>
    [Route("maven-upload"), HttpGet, HttpHead, PageHandler, LegacyPage("/MavenUpload")]
    [RepositoryPage(Format = RepositoryFormat.Maven, Upload = true)]
    public async Task<IActionResult> Upload(CancellationToken cancellationToken)
        => View("Upload", await UploadWorkflow.ShowAsync(User, cancellationToken));

    /// <summary>Deploys the chosen files under the given coordinates.</summary>
    [Route("maven-upload"), HttpPost, PageHandler, LegacyPage("/MavenUpload")]
    [RepositoryPage(Format = RepositoryFormat.Maven, Upload = true)]
    public async Task<IActionResult> UploadPost(
        string? groupId,
        string? artifactId,
        string? version,
        string? packaging,
        string? classifier,
        IFormFile? file,
        IFormFile? pom,
        string? apiKey,
        CancellationToken cancellationToken)
    {
        var model = await UploadWorkflow.DeployAsync(User, HttpContext.RequestServices, groupId, artifactId, version, packaging, classifier, file, pom, apiKey, cancellationToken);
        Response.StatusCode = model.StatusCode;

        return View("Upload", model);
    }
}
