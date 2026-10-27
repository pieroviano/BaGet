using System.Threading;
using System.Threading.Tasks;
using BaGet.Frontend.Controllers.Base;
using BaGet.Frontend.Mvc.Attributes;
using BaGet.Frontend.Mvc.UI;
using BaGet.Identity.Application.Screens;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using BaGet.Identity.Application.Models.Packages;
using BaGet.Identity.Application.UseCases.Packages;
using BaGet.Identity.Controllers.FrontEnd;

namespace BaGet.Identity.Controllers.UI;

/// <summary>
/// The JSON twin of the package management screen: <c>GET /api/ui/packages/{id}/manage</c> and, for each handler of the page, <c>POST
/// /api/ui/packages/{id}/manage/{handler-in-kebab-case}</c> with a JSON body (also under <c>/api/ui/r/{repo}/…</c>). Screen only (403 over HTTP to every caller; the web screen calls it in process):
/// an API key never manages a package through this screen. <see cref="PackageManageController"/> calls it in process and renders the view.
/// </summary>
[Route("api/ui/packages/{id}/manage")]
[RepositoryPage(NotOnVirtual = true)]
public sealed class PackageManageUiApiController : AccountUiApiControllerBase
{
    /// <inheritdoc/>
    protected override bool RequiresSignIn => true;

    // Resolved per request, after the gate: the use case exists only while accounts are enabled.
    private PackageManageUseCase UseCase => HttpContext.RequestServices.GetRequiredService<PackageManageUseCase>();

    /// <summary>The owner / administrator check that opens every handler (the view controller asks it first; the use case remembers the answer).</summary>
    [NonAction]
    public Task<ScreenResult<PackageManageScreen>?> EnterAsync(string? id, string? handler, bool isWrite, CancellationToken cancellationToken)
        => UseCase.EnterAsync(User, id, handler, isWrite, cancellationToken);

    /// <summary>The screen.</summary>
    [HttpGet, UiApi(typeof(PackageManageController), nameof(PackageManageController.Index))]
    public async Task<ActionResult<ScreenResult<PackageManageScreen>>> Get(string id, CancellationToken cancellationToken)
        => Respond(await UseCase.GetAsync(User, id, cancellationToken));

    /// <summary>Adds an owner.</summary>
    [HttpPost("add-owner"), UiApi(typeof(PackageManageController), nameof(PackageManageController.Index), Handler = "AddOwner")]
    public async Task<ActionResult<ScreenResult<PackageManageScreen>>> AddOwner(string id, [FromBody] PackageManageOwnerForm? form, CancellationToken cancellationToken)
        => Respond(await UseCase.AddOwnerAsync(User, id, form?.OwnerName, cancellationToken));

    /// <summary>Removes an owner.</summary>
    [HttpPost("remove-owner"), UiApi(typeof(PackageManageController), nameof(PackageManageController.Index), Handler = "RemoveOwner")]
    public async Task<ActionResult<ScreenResult<PackageManageScreen>>> RemoveOwner(string id, [FromBody] PackageManageOwnerForm? form, CancellationToken cancellationToken)
        => Respond(await UseCase.RemoveOwnerAsync(User, id, form?.OwnerName, cancellationToken));

    /// <summary>Unlists a version.</summary>
    [HttpPost("unlist"), UiApi(typeof(PackageManageController), nameof(PackageManageController.Index), Handler = "Unlist")]
    public async Task<ActionResult<ScreenResult<PackageManageScreen>>> Unlist(string id, [FromBody] PackageManageVersionForm? form, CancellationToken cancellationToken)
        => Respond(await UseCase.UnlistAsync(User, id, form?.Version, cancellationToken));

    /// <summary>Relists a version.</summary>
    [HttpPost("relist"), UiApi(typeof(PackageManageController), nameof(PackageManageController.Index), Handler = "Relist")]
    public async Task<ActionResult<ScreenResult<PackageManageScreen>>> Relist(string id, [FromBody] PackageManageVersionForm? form, CancellationToken cancellationToken)
        => Respond(await UseCase.RelistAsync(User, id, form?.Version, cancellationToken));

    /// <summary>Deletes a version as the server is configured to (unlist or permanent).</summary>
    [HttpPost("delete"), UiApi(typeof(PackageManageController), nameof(PackageManageController.Index), Handler = "Delete")]
    public async Task<ActionResult<ScreenResult<PackageManageScreen>>> Delete(string id, [FromBody] PackageManageVersionForm? form, CancellationToken cancellationToken)
        => Respond(await UseCase.DeleteAsync(User, id, form?.Version, cancellationToken));

    /// <summary>Permanently deletes a version (administrators, after the password).</summary>
    [HttpPost("hard-delete"), UiApi(typeof(PackageManageController), nameof(PackageManageController.Index), Handler = "HardDelete")]
    public async Task<ActionResult<ScreenResult<PackageManageScreen>>> HardDelete(string id, [FromBody] PackageManageHardDeleteForm? form, CancellationToken cancellationToken)
        => Respond(await UseCase.HardDeleteAsync(User, id, form?.Version, form?.Password, cancellationToken));

    /// <summary>Permanently deletes every version and the owners (administrators, after the password).</summary>
    [HttpPost("hard-delete-all"), UiApi(typeof(PackageManageController), nameof(PackageManageController.Index), Handler = "HardDeleteAll")]
    public async Task<ActionResult<ScreenResult<PackageManageScreen>>> HardDeleteAll(string id, [FromBody] PackageManagePasswordForm? form, CancellationToken cancellationToken)
        => Respond(await UseCase.HardDeleteAllAsync(User, id, form?.Password, cancellationToken));

    /// <summary>Deprecates a version.</summary>
    [HttpPost("deprecate"), UiApi(typeof(PackageManageController), nameof(PackageManageController.Index), Handler = "Deprecate")]
    public async Task<ActionResult<ScreenResult<PackageManageScreen>>> Deprecate(string id, [FromBody] PackageManageDeprecateForm? form, CancellationToken cancellationToken)
        => Respond(await UseCase.DeprecateAsync(User, id, form?.Version, form?.Reasons, form?.Message, form?.AlternatePackageId, form?.AlternateVersionRange, cancellationToken));

    /// <summary>Removes a version's deprecation.</summary>
    [HttpPost("undeprecate"), UiApi(typeof(PackageManageController), nameof(PackageManageController.Index), Handler = "Undeprecate")]
    public async Task<ActionResult<ScreenResult<PackageManageScreen>>> Undeprecate(string id, [FromBody] PackageManageVersionForm? form, CancellationToken cancellationToken)
        => Respond(await UseCase.UndeprecateAsync(User, id, form?.Version, cancellationToken));

    /// <summary>Adds a vulnerability to a version.</summary>
    [HttpPost("add-vulnerability"), UiApi(typeof(PackageManageController), nameof(PackageManageController.Index), Handler = "AddVulnerability")]
    public async Task<ActionResult<ScreenResult<PackageManageScreen>>> AddVulnerability(string id, [FromBody] PackageManageVulnerabilityForm? form, CancellationToken cancellationToken)
        => Respond(await UseCase.AddVulnerabilityAsync(User, id, form?.Version, form?.AdvisoryUrl, form?.Severity ?? 0, form?.VersionRange, cancellationToken));

    /// <summary>Removes a vulnerability from a version.</summary>
    [HttpPost("remove-vulnerability"), UiApi(typeof(PackageManageController), nameof(PackageManageController.Index), Handler = "RemoveVulnerability")]
    public async Task<ActionResult<ScreenResult<PackageManageScreen>>> RemoveVulnerability(string id, [FromBody] PackageManageRemoveVulnerabilityForm? form, CancellationToken cancellationToken)
        => Respond(await UseCase.RemoveVulnerabilityAsync(User, id, form?.Version, form?.Key ?? 0, cancellationToken));

    /// <summary>Marks or unmarks the ID as verified (administrators).</summary>
    [HttpPost("set-verified"), UiApi(typeof(PackageManageController), nameof(PackageManageController.Index), Handler = "SetVerified")]
    public async Task<ActionResult<ScreenResult<PackageManageScreen>>> SetVerified(string id, [FromBody] PackageManageVerifiedForm? form, CancellationToken cancellationToken)
        => Respond(await UseCase.SetVerifiedAsync(User, id, form?.Verified ?? false, cancellationToken));

    /// <summary>Deletes the PDBs recorded for a version.</summary>
    [HttpPost("delete-symbols"), UiApi(typeof(PackageManageController), nameof(PackageManageController.Index), Handler = "DeleteSymbols")]
    public async Task<ActionResult<ScreenResult<PackageManageScreen>>> DeleteSymbols(string id, [FromBody] PackageManageVersionForm? form, CancellationToken cancellationToken)
        => Respond(await UseCase.DeleteSymbolsAsync(User, id, form?.Version, cancellationToken));

    /// <summary>Resolves an abuse report.</summary>
    [HttpPost("resolve-report"), UiApi(typeof(PackageManageController), nameof(PackageManageController.Index), Handler = "ResolveReport")]
    public async Task<ActionResult<ScreenResult<PackageManageScreen>>> ResolveReport(string id, [FromBody] PackageManageReportForm? form, CancellationToken cancellationToken)
        => Respond(await UseCase.ResolveReportAsync(User, id, form?.Key ?? 0, cancellationToken));
}
