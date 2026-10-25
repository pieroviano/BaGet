using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Identity.Mvc;
using BaGet.Identity.Workflows;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace BaGet.Identity.Controllers;

/// <summary>
/// The owners' screen for a package ID (<c>/packages/{id}/manage</c>, also at <c>/r/{repo}/…</c>): owners, versions, deprecation,
/// vulnerabilities, symbols and abuse reports. It replaces the <c>/Packages/Manage</c> Razor Page (same URLs, same
/// <c>?handler=</c> forms, same gates); the logic is <see cref="ManageWorkflow"/>, shared with the obsolete <c>ManageModel</c>.
/// </summary>
/// <remarks>
/// The ownership / administrator check runs in <see cref="OnActionExecutionAsync"/>, before the global repository filter, as the
/// page model's check did.
/// </remarks>
[Route("packages/{id}/manage")]
[LegacyPage("/Packages/Manage")]
[RepositoryPage(NotOnVirtual = true)]
public sealed class PackageManageController : AccountControllerBase
{
    private const string ViewName = "Index";

    private ManageWorkflow? _workflow;

    /// <inheritdoc/>
    protected override bool RequiresSignIn => true;

    private ManageWorkflow Workflow => _workflow!;

    /// <inheritdoc/>
    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        OnActionExecuting(context);
        if (context.Result != null) return;

        _workflow = new ManageWorkflow(HttpContext, PackageControllerSupport.Route(context, "id"));
        var refusal = await _workflow.EnterAsync(PackageControllerSupport.HandlerName(context));
        if (refusal != null)
        {
            context.Result = Present(refusal);
            return;
        }

        OnActionExecuted(await next());
    }

    /// <summary>The screen.</summary>
    [HttpGet, HttpHead, PageHandler]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
        => Present(await Workflow.GetAsync(cancellationToken));

    /// <summary>Adds an owner.</summary>
    [HttpPost, PageHandler("AddOwner")]
    public async Task<IActionResult> AddOwner(string ownerName, CancellationToken cancellationToken)
        => Present(await Workflow.AddOwnerAsync(ownerName, cancellationToken));

    /// <summary>Removes an owner.</summary>
    [HttpPost, PageHandler("RemoveOwner")]
    public async Task<IActionResult> RemoveOwner(string ownerName, CancellationToken cancellationToken)
        => Present(await Workflow.RemoveOwnerAsync(ownerName, cancellationToken));

    /// <summary>Unlists a version.</summary>
    [HttpPost, PageHandler("Unlist")]
    public async Task<IActionResult> Unlist(string version, CancellationToken cancellationToken)
        => Present(await Workflow.UnlistAsync(version, cancellationToken));

    /// <summary>Relists a version.</summary>
    [HttpPost, PageHandler("Relist")]
    public async Task<IActionResult> Relist(string version, CancellationToken cancellationToken)
        => Present(await Workflow.RelistAsync(version, cancellationToken));

    /// <summary>Deletes a version as the server is configured to (unlist or permanent).</summary>
    [HttpPost, PageHandler("Delete")]
    public async Task<IActionResult> Delete(string version, CancellationToken cancellationToken)
        => Present(await Workflow.DeleteAsync(version, cancellationToken));

    /// <summary>Permanently deletes a version (administrators, after the password).</summary>
    [HttpPost, PageHandler("HardDelete")]
    public async Task<IActionResult> HardDelete(string version, string? password, CancellationToken cancellationToken)
        => Present(await Workflow.HardDeleteAsync(version, password, cancellationToken));

    /// <summary>Permanently deletes every version and the owners (administrators, after the password).</summary>
    [HttpPost, PageHandler("HardDeleteAll")]
    public async Task<IActionResult> HardDeleteAll(string? password, CancellationToken cancellationToken)
        => Present(await Workflow.HardDeleteAllAsync(password, cancellationToken));

    /// <summary>Deprecates a version.</summary>
    [HttpPost, PageHandler("Deprecate")]
    public async Task<IActionResult> Deprecate(
        string version,
        List<string>? reasons,
        string message,
        string alternatePackageId,
        string? alternateVersionRange,
        CancellationToken cancellationToken)
        => Present(await Workflow.DeprecateAsync(version, reasons, message, alternatePackageId, alternateVersionRange, cancellationToken));

    /// <summary>Removes a version's deprecation.</summary>
    [HttpPost, PageHandler("Undeprecate")]
    public async Task<IActionResult> Undeprecate(string version, CancellationToken cancellationToken)
        => Present(await Workflow.UndeprecateAsync(version, cancellationToken));

    /// <summary>Adds a vulnerability to a version.</summary>
    [HttpPost, PageHandler("AddVulnerability")]
    public async Task<IActionResult> AddVulnerability(
        string version,
        string? advisoryUrl,
        int severity,
        string? versionRange,
        CancellationToken cancellationToken)
        => Present(await Workflow.AddVulnerabilityAsync(version, advisoryUrl, severity, versionRange, cancellationToken));

    /// <summary>Removes a vulnerability from a version.</summary>
    [HttpPost, PageHandler("RemoveVulnerability")]
    public async Task<IActionResult> RemoveVulnerability(string version, int key, CancellationToken cancellationToken)
        => Present(await Workflow.RemoveVulnerabilityAsync(version, key, cancellationToken));

    /// <summary>Marks or unmarks the ID as verified (administrators).</summary>
    [HttpPost, PageHandler("SetVerified")]
    public async Task<IActionResult> SetVerified(bool verified, CancellationToken cancellationToken)
        => Present(await Workflow.SetVerifiedAsync(verified, cancellationToken));

    /// <summary>Deletes the PDBs recorded for a version.</summary>
    [HttpPost, PageHandler("DeleteSymbols")]
    public async Task<IActionResult> DeleteSymbols(string version, CancellationToken cancellationToken)
        => Present(await Workflow.DeleteSymbolsAsync(version, cancellationToken));

    /// <summary>Resolves an abuse report.</summary>
    [HttpPost, PageHandler("ResolveReport")]
    public async Task<IActionResult> ResolveReport(int key, CancellationToken cancellationToken)
        => Present(await Workflow.ResolveReportAsync(key, cancellationToken));

    private IActionResult Present(IActionResult result)
        => PackageControllerSupport.Resolve(this, result, ViewName, Workflow.Model);
}
