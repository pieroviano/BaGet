using System;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.Application;
using BaGet.Identity.Mvc;
using BaGet.Web.Application;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Web;

/// <summary>
/// The JSON twin of the npm repository screens: <c>GET /api/ui/npm-packages</c> (list, <c>q</c> and <c>p</c>), <c>GET /api/ui/npm-packages/{name}</c> (one package;
/// <c>version</c>), <c>GET /api/ui/npm-upload</c> (the form's state) and <c>POST /api/ui/npm-upload</c> (multipart: <c>package</c>, <c>tag</c> and, while accounts are
/// off, <c>apiKey</c>). Also under <c>/api/ui/r/{repo}/...</c>, with the pages' repository rules (<see cref="RepositoryPageAttribute"/>: Read on the repository and
/// the package, the right to publish for the upload, 404 in a repository that is not npm's). A package that does not exist answers <c>404</c> with the explaining
/// screen; a refused upload answers its status (401, 403, 404, 409) with the form's state. An API key in the request authenticates the uploader within its scopes.
/// <see cref="NpmBrowseController"/> calls it in process.
/// </summary>
[Route("api/ui")]
public sealed class NpmBrowseUiApiController : UiApiControllerBase
{
    private readonly NpmBrowseUseCase _npm;

    /// <summary>Creates the controller.</summary>
    public NpmBrowseUiApiController(NpmBrowseUseCase npm)
    {
        _npm = npm ?? throw new ArgumentNullException(nameof(npm));
    }

    /// <summary>The package list with a search box.</summary>
    [HttpGet("npm-packages"), UiApi("/NpmPackages")]
    [RepositoryPage(Format = RepositoryFormat.Npm)]
    public async Task<ActionResult<ScreenResult<NpmPackagesViewModel>>> Packages(string? q, int p = 1, CancellationToken cancellationToken = default)
        => Respond(await _npm.PackagesAsync(q, p, User, cancellationToken));

    /// <summary>One package: readme, versions, dist-tags, install commands.</summary>
    [HttpGet("npm-packages/{*id}"), UiApi("/NpmPackage")]
    [RepositoryPage(Format = RepositoryFormat.Npm)]
    public async Task<ActionResult<ScreenResult<NpmPackageViewModel>>> Package(string? id, string? version, CancellationToken cancellationToken = default)
        => Respond(await _npm.PackageAsync(id, version, User, cancellationToken));

    /// <summary>The upload form's state.</summary>
    [HttpGet("npm-upload"), UiApi("/NpmUpload")]
    [RepositoryPage(Format = RepositoryFormat.Npm, Upload = true)]
    public Task<ActionResult<ScreenResult<NpmUploadViewModel>>> Upload()
        => Task.FromResult(Respond(_npm.UploadForm(User)));

    /// <summary>Publishes the chosen tarball with the dist-tag <paramref name="tag"/> (default <c>latest</c>).</summary>
    [HttpPost("npm-upload"), UiApi("/NpmUpload")]
    [RepositoryPage(Format = RepositoryFormat.Npm, Upload = true)]
    public async Task<ActionResult<ScreenResult<NpmUploadViewModel>>> UploadPost(IFormFile? package, string? tag, string? apiKey, CancellationToken cancellationToken)
        => Respond(await _npm.PublishAsync(User, KeyCaller, package.ToUploadedFile(), tag, apiKey, cancellationToken));
}
