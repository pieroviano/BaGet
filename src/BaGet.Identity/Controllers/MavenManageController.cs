using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.Mvc;
using BaGet.Identity.Workflows;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace BaGet.Identity.Controllers;

/// <summary>
/// The owners' screen for a Maven artifact (<c>/maven-manage/{group}/{artifact}</c>, also at <c>/r/{repo}/…</c>): owners, versions,
/// the files of the chosen version, deletion and snapshot clean-up. It replaces the <c>/Packages/MavenManage</c> Razor Page (same
/// URLs, same <c>?handler=</c> forms, same gates); the logic is <see cref="MavenManageWorkflow"/>. The coordinates / repository-kind / ownership check runs in <see cref="OnActionExecutionAsync"/>,
/// before the global repository filter.
/// </summary>
[Route("maven-manage/{group}/{artifact}")]
[RepositoryPage(Format = RepositoryFormat.Maven, NotOnVirtual = true, NotOnRemote = true)]
public sealed class MavenManageController : AccountControllerBase
{
    private const string ViewName = "Index";

    private MavenManageWorkflow? _workflow;

    /// <inheritdoc/>
    protected override bool RequiresSignIn => true;

    private MavenManageWorkflow Workflow => _workflow!;

    /// <inheritdoc/>
    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        OnActionExecuting(context);
        if (context.Result != null) return;

        _workflow = new MavenManageWorkflow(
            HttpContext,
            PackageControllerSupport.Route(context, "group"),
            PackageControllerSupport.Route(context, "artifact"),
            PackageControllerSupport.FormOrQuery(context, "version"));
        var refusal = await _workflow.EnterAsync(PackageControllerSupport.HandlerName(context));
        if (refusal != null)
        {
            context.Result = Present(refusal);
            return;
        }

        OnActionExecuted(await next());
    }

    /// <summary>The screen.</summary>
    [HttpGet, HttpHead, PageHandler, LegacyPage("/Packages/MavenManage")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
        => Present(await Workflow.GetAsync(cancellationToken));

    /// <summary>Adds an owner.</summary>
    [HttpPost, PageHandler("AddOwner"), LegacyPage("/Packages/MavenManage")]
    public async Task<IActionResult> AddOwner(string ownerName, CancellationToken cancellationToken)
        => Present(await Workflow.AddOwnerAsync(ownerName, cancellationToken));

    /// <summary>Removes an owner.</summary>
    [HttpPost, PageHandler("RemoveOwner"), LegacyPage("/Packages/MavenManage")]
    public async Task<IActionResult> RemoveOwner(string ownerName, CancellationToken cancellationToken)
        => Present(await Workflow.RemoveOwnerAsync(ownerName, cancellationToken));

    /// <summary>Deletes one file of the chosen version, after the password.</summary>
    [HttpPost, PageHandler("DeleteFile"), LegacyPage("/Packages/MavenManage")]
    public async Task<IActionResult> DeleteFile(string? fileName, string? password, CancellationToken cancellationToken)
        => Present(await Workflow.DeleteFileAsync(fileName, password, cancellationToken));

    /// <summary>Deletes one version, after the password.</summary>
    [HttpPost, PageHandler("DeleteVersion"), LegacyPage("/Packages/MavenManage")]
    public async Task<IActionResult> DeleteVersion(string? version, string? password, CancellationToken cancellationToken)
        => Present(await Workflow.DeleteVersionAsync(version, password, cancellationToken));

    /// <summary>Deletes the whole artifact, after the password.</summary>
    [HttpPost, PageHandler("DeleteArtifact"), LegacyPage("/Packages/MavenManage")]
    public async Task<IActionResult> DeleteArtifact(string? password, CancellationToken cancellationToken)
        => Present(await Workflow.DeleteArtifactAsync(password, cancellationToken));

    /// <summary>Deletes old snapshot builds of a version, after the password.</summary>
    [HttpPost, PageHandler("Cleanup"), LegacyPage("/Packages/MavenManage")]
    public async Task<IActionResult> Cleanup(string? version, string? keep, string? password, CancellationToken cancellationToken)
        => Present(await Workflow.CleanupAsync(version, keep, password, cancellationToken));

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
