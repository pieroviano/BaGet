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
/// <c>?handler=</c> forms, same gates); the logic is <see cref="ManageWorkflow"/>.
/// </summary>
/// <remarks>
/// <see cref="LegacyPageAttribute"/> sits on the actions, not on the class: the fallback <c>NoHandler</c> is not a page handler.
/// The ownership / administrator check runs in <see cref="OnActionExecutionAsync"/>, before the global repository filter.
/// </remarks>
[Route("packages/{id}/manage")]
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
    [HttpGet, HttpHead, PageHandler, LegacyPage("/Packages/Manage")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
        => Present(await Workflow.GetAsync(cancellationToken));

    /// <summary>Adds an owner.</summary>
    [HttpPost, PageHandler("AddOwner"), LegacyPage("/Packages/Manage")]
    public async Task<IActionResult> AddOwner(string ownerName, CancellationToken cancellationToken)
        => Present(await Workflow.AddOwnerAsync(ownerName, cancellationToken));

    /// <summary>Removes an owner.</summary>
    [HttpPost, PageHandler("RemoveOwner"), LegacyPage("/Packages/Manage")]
    public async Task<IActionResult> RemoveOwner(string ownerName, CancellationToken cancellationToken)
        => Present(await Workflow.RemoveOwnerAsync(ownerName, cancellationToken));

    /// <summary>Unlists a version.</summary>
    [HttpPost, PageHandler("Unlist"), LegacyPage("/Packages/Manage")]
    public async Task<IActionResult> Unlist(string version, CancellationToken cancellationToken)
        => Present(await Workflow.UnlistAsync(version, cancellationToken));

    /// <summary>Relists a version.</summary>
    [HttpPost, PageHandler("Relist"), LegacyPage("/Packages/Manage")]
    public async Task<IActionResult> Relist(string version, CancellationToken cancellationToken)
        => Present(await Workflow.RelistAsync(version, cancellationToken));

    /// <summary>Deletes a version as the server is configured to (unlist or permanent).</summary>
    [HttpPost, PageHandler("Delete"), LegacyPage("/Packages/Manage")]
    public async Task<IActionResult> Delete(string version, CancellationToken cancellationToken)
        => Present(await Workflow.DeleteAsync(version, cancellationToken));

    /// <summary>Permanently deletes a version (administrators, after the password).</summary>
    [HttpPost, PageHandler("HardDelete"), LegacyPage("/Packages/Manage")]
    public async Task<IActionResult> HardDelete(string version, string? password, CancellationToken cancellationToken)
        => Present(await Workflow.HardDeleteAsync(version, password, cancellationToken));

    /// <summary>Permanently deletes every version and the owners (administrators, after the password).</summary>
    [HttpPost, PageHandler("HardDeleteAll"), LegacyPage("/Packages/Manage")]
    public async Task<IActionResult> HardDeleteAll(string? password, CancellationToken cancellationToken)
        => Present(await Workflow.HardDeleteAllAsync(password, cancellationToken));

    /// <summary>Deprecates a version.</summary>
    [HttpPost, PageHandler("Deprecate"), LegacyPage("/Packages/Manage")]
    public async Task<IActionResult> Deprecate(
        string version,
        List<string>? reasons,
        string message,
        string alternatePackageId,
        string? alternateVersionRange,
        CancellationToken cancellationToken)
        => Present(await Workflow.DeprecateAsync(version, reasons, message, alternatePackageId, alternateVersionRange, cancellationToken));

    /// <summary>Removes a version's deprecation.</summary>
    [HttpPost, PageHandler("Undeprecate"), LegacyPage("/Packages/Manage")]
    public async Task<IActionResult> Undeprecate(string version, CancellationToken cancellationToken)
        => Present(await Workflow.UndeprecateAsync(version, cancellationToken));

    /// <summary>Adds a vulnerability to a version.</summary>
    [HttpPost, PageHandler("AddVulnerability"), LegacyPage("/Packages/Manage")]
    public async Task<IActionResult> AddVulnerability(
        string version,
        string? advisoryUrl,
        int severity,
        string? versionRange,
        CancellationToken cancellationToken)
        => Present(await Workflow.AddVulnerabilityAsync(version, advisoryUrl, severity, versionRange, cancellationToken));

    /// <summary>Removes a vulnerability from a version.</summary>
    [HttpPost, PageHandler("RemoveVulnerability"), LegacyPage("/Packages/Manage")]
    public async Task<IActionResult> RemoveVulnerability(string version, int key, CancellationToken cancellationToken)
        => Present(await Workflow.RemoveVulnerabilityAsync(version, key, cancellationToken));

    /// <summary>Marks or unmarks the ID as verified (administrators).</summary>
    [HttpPost, PageHandler("SetVerified"), LegacyPage("/Packages/Manage")]
    public async Task<IActionResult> SetVerified(bool verified, CancellationToken cancellationToken)
        => Present(await Workflow.SetVerifiedAsync(verified, cancellationToken));

    /// <summary>Deletes the PDBs recorded for a version.</summary>
    [HttpPost, PageHandler("DeleteSymbols"), LegacyPage("/Packages/Manage")]
    public async Task<IActionResult> DeleteSymbols(string version, CancellationToken cancellationToken)
        => Present(await Workflow.DeleteSymbolsAsync(version, cancellationToken));

    /// <summary>Resolves an abuse report.</summary>
    [HttpPost, PageHandler("ResolveReport"), LegacyPage("/Packages/Manage")]
    public async Task<IActionResult> ResolveReport(int key, CancellationToken cancellationToken)
        => Present(await Workflow.ResolveReportAsync(key, cancellationToken));

    /// <summary>
    /// A POST that names no handler (or one this screen does not have), such as a stray <c>&lt;form&gt;</c> without
    /// <c>?handler=</c>: the antiforgery check has already run, and there is nothing to do, so the answer is 404. (A Razor Page
    /// with only named POST handlers behaved alike for the caller; without this action routing would answer 404 before the
    /// antiforgery filter and an unprotected POST would not be refused with 400.) It carries no <see cref="LegacyPageAttribute"/>:
    /// it is not one of the page's handlers, so it stays out of the route contract.
    /// </summary>
    [HttpPost, PageHandler]
    public IActionResult NoHandler() => NotFound();

    private IActionResult Present(IActionResult result)
        => PackageControllerSupport.Resolve(this, result, ViewName, Workflow.Model);
}
