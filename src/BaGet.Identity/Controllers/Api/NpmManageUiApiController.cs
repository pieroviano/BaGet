using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.Application;
using BaGet.Identity.Mvc;
using BaGet.Identity.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace BaGet.Identity.Controllers;

/// <summary>The form of the npm management handlers that name an owner.</summary>
public sealed class NpmManageOwnerForm
{
    /// <summary>The user or organization.</summary>
    public string? OwnerName { get; set; }
}

/// <summary>The form of <c>SetTag</c>.</summary>
public sealed class NpmManageTagForm
{
    /// <summary>The dist-tag.</summary>
    public string? Tag { get; set; }

    /// <summary>The version it points at (not used by <c>RemoveTag</c>).</summary>
    public string? Version { get; set; }
}

/// <summary>The form of <c>Deprecate</c> and <c>Undeprecate</c>.</summary>
public sealed class NpmManageDeprecateForm
{
    /// <summary>The version.</summary>
    public string? Version { get; set; }

    /// <summary>The deprecation message (<c>Deprecate</c> only).</summary>
    public string? Message { get; set; }
}

/// <summary>The form of <c>Unpublish</c> (one version) and <c>UnpublishAll</c> (the version is ignored); the password confirms.</summary>
public sealed class NpmManageUnpublishForm
{
    /// <summary>The version (<c>Unpublish</c> only).</summary>
    public string? Version { get; set; }

    /// <summary>The caller's password.</summary>
    public string? Password { get; set; }
}

/// <summary>
/// The JSON twin of the npm package management screen: <c>GET /api/ui/npm-manage/{*id}</c> and, for each handler of the page, <c>POST
/// /api/ui/npm-manage/{handler-in-kebab-case}/{*id}</c> with a JSON body (the package name is a catch-all, so the handler comes first; also
/// under <c>/api/ui/r/{repo}/…</c>). Cookie session only. <see cref="NpmManageController"/> calls it in process and renders the view.
/// </summary>
[Route("api/ui/npm-manage")]
[RepositoryPage(Format = RepositoryFormat.Npm, NotOnVirtual = true, NotOnRemote = true)]
public sealed class NpmManageUiApiController : AccountUiApiControllerBase
{
    private const string Screen = "/Packages/NpmManage";

    /// <inheritdoc/>
    protected override bool RequiresSignIn => true;

    private NpmManageUseCase UseCase => HttpContext.RequestServices.GetRequiredService<NpmManageUseCase>();

    /// <summary>The name / repository-kind / ownership check that opens every handler (see <see cref="PackageManageUiApiController.EnterAsync"/>).</summary>
    [NonAction]
    public Task<ScreenResult<NpmManageViewModel>?> EnterAsync(string? id, string? handler, bool isWrite, CancellationToken cancellationToken)
        => UseCase.EnterAsync(User, id, handler, isWrite, cancellationToken);

    /// <summary>The screen.</summary>
    [HttpGet("{*id}"), UiApi(Screen)]
    public async Task<ActionResult<ScreenResult<NpmManageViewModel>>> Get(string? id, CancellationToken cancellationToken)
        => Respond(await UseCase.GetAsync(User, id, cancellationToken));

    /// <summary>Adds an owner.</summary>
    [HttpPost("add-owner/{*id}"), UiApi(Screen, Handler = "AddOwner")]
    public async Task<ActionResult<ScreenResult<NpmManageViewModel>>> AddOwner(string? id, [FromBody] NpmManageOwnerForm? form, CancellationToken cancellationToken)
        => Respond(await UseCase.AddOwnerAsync(User, id, form?.OwnerName, cancellationToken));

    /// <summary>Removes an owner.</summary>
    [HttpPost("remove-owner/{*id}"), UiApi(Screen, Handler = "RemoveOwner")]
    public async Task<ActionResult<ScreenResult<NpmManageViewModel>>> RemoveOwner(string? id, [FromBody] NpmManageOwnerForm? form, CancellationToken cancellationToken)
        => Respond(await UseCase.RemoveOwnerAsync(User, id, form?.OwnerName, cancellationToken));

    /// <summary>Points a dist-tag at a version.</summary>
    [HttpPost("set-tag/{*id}"), UiApi(Screen, Handler = "SetTag")]
    public async Task<ActionResult<ScreenResult<NpmManageViewModel>>> SetTag(string? id, [FromBody] NpmManageTagForm? form, CancellationToken cancellationToken)
        => Respond(await UseCase.SetTagAsync(User, id, form?.Tag, form?.Version, cancellationToken));

    /// <summary>Removes a dist-tag.</summary>
    [HttpPost("remove-tag/{*id}"), UiApi(Screen, Handler = "RemoveTag")]
    public async Task<ActionResult<ScreenResult<NpmManageViewModel>>> RemoveTag(string? id, [FromBody] NpmManageTagForm? form, CancellationToken cancellationToken)
        => Respond(await UseCase.RemoveTagAsync(User, id, form?.Tag, cancellationToken));

    /// <summary>Deprecates a version.</summary>
    [HttpPost("deprecate/{*id}"), UiApi(Screen, Handler = "Deprecate")]
    public async Task<ActionResult<ScreenResult<NpmManageViewModel>>> Deprecate(string? id, [FromBody] NpmManageDeprecateForm? form, CancellationToken cancellationToken)
        => Respond(await UseCase.DeprecateAsync(User, id, form?.Version, form?.Message, cancellationToken));

    /// <summary>Removes a version's deprecation.</summary>
    [HttpPost("undeprecate/{*id}"), UiApi(Screen, Handler = "Undeprecate")]
    public async Task<ActionResult<ScreenResult<NpmManageViewModel>>> Undeprecate(string? id, [FromBody] NpmManageDeprecateForm? form, CancellationToken cancellationToken)
        => Respond(await UseCase.UndeprecateAsync(User, id, form?.Version, cancellationToken));

    /// <summary>Unpublishes one version, after the password is confirmed.</summary>
    [HttpPost("unpublish/{*id}"), UiApi(Screen, Handler = "Unpublish")]
    public async Task<ActionResult<ScreenResult<NpmManageViewModel>>> Unpublish(string? id, [FromBody] NpmManageUnpublishForm? form, CancellationToken cancellationToken)
        => Respond(await UseCase.UnpublishAsync(User, id, form?.Version, form?.Password, cancellationToken));

    /// <summary>Unpublishes every version, after the password is confirmed.</summary>
    [HttpPost("unpublish-all/{*id}"), UiApi(Screen, Handler = "UnpublishAll")]
    public async Task<ActionResult<ScreenResult<NpmManageViewModel>>> UnpublishAll(string? id, [FromBody] NpmManageUnpublishForm? form, CancellationToken cancellationToken)
        => Respond(await UseCase.UnpublishAllAsync(User, id, form?.Password, cancellationToken));
}
