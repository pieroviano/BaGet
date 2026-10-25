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
/// The web pages of an npm repository: the package list (<c>/npm-packages</c>), one package (<c>/npm-packages/{name}</c>) and the
/// browser upload (<c>/npm-upload</c>). They replace the Razor Pages <c>NpmPackages</c>, <c>NpmPackage</c> and <c>NpmUpload</c>: same URLs, same <c>asp-page</c> names
/// (<see cref="LegacyPageAttribute"/>), same repository rules (<see cref="RepositoryPageAttribute"/>: Read on the repository, on
/// the package for the package page, the right to publish for the upload; 404 in a repository that is not npm's). Views are in
/// <c>Views/NpmBrowse/</c>.
/// </summary>
public sealed class NpmBrowseController : UiControllerBase
{
    private NpmPackagesWorkflow PackagesWorkflow => HttpContext.RequestServices.GetRequiredService<NpmPackagesWorkflow>();

    private NpmPackageWorkflow PackageWorkflow => HttpContext.RequestServices.GetRequiredService<NpmPackageWorkflow>();

    private NpmUploadWorkflow UploadWorkflow => HttpContext.RequestServices.GetRequiredService<NpmUploadWorkflow>();

    /// <summary>The package list with a search box.</summary>
    [Route("npm-packages"), HttpGet, HttpHead, PageHandler, LegacyPage("/NpmPackages")]
    [RepositoryPage(Format = RepositoryFormat.Npm)]
    public async Task<IActionResult> Packages(
        string? q,
        [Range(1, int.MaxValue)] int p = 1,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid) return BadRequest();

        return View("Packages", await PackagesWorkflow.LoadAsync(q, p, User, cancellationToken));
    }

    /// <summary>One package: readme, versions, dist-tags, install commands.</summary>
    [Route("npm-packages/{*id}"), HttpGet, HttpHead, PageHandler, LegacyPage("/NpmPackage")]
    [RepositoryPage(Format = RepositoryFormat.Npm)]
    public async Task<IActionResult> Package(
        string? id,
        string? version,
        CancellationToken cancellationToken = default)
    {
        var model = await PackageWorkflow.LoadAsync(id, version, User, HttpContext.RequestServices, cancellationToken);
        if (model == null) return NotFound();

        if (model.StatusCode != StatusCodes.Status200OK) Response.StatusCode = model.StatusCode;

        return View("Package", model);
    }

    /// <summary>The upload form.</summary>
    [Route("npm-upload"), HttpGet, HttpHead, PageHandler, LegacyPage("/NpmUpload")]
    [RepositoryPage(Format = RepositoryFormat.Npm, Upload = true)]
    public IActionResult Upload() => View("Upload", UploadWorkflow.Show(User));

    /// <summary>Publishes the chosen tarball with the dist-tag <paramref name="tag"/> (default <c>latest</c>).</summary>
    [Route("npm-upload"), HttpPost, PageHandler, LegacyPage("/NpmUpload")]
    [RepositoryPage(Format = RepositoryFormat.Npm, Upload = true)]
    public async Task<IActionResult> UploadPost(IFormFile? package, string? tag, string? apiKey, CancellationToken cancellationToken)
    {
        var model = await UploadWorkflow.PublishAsync(User, HttpContext.RequestServices, package, tag, apiKey, cancellationToken);
        Response.StatusCode = model.StatusCode;

        return View("Upload", model);
    }
}
