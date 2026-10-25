using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.Mvc;
using BaGet.Identity.Workflows;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace BaGet.Identity.Controllers;

/// <summary>
/// The owners' screen for an npm package (<c>/npm-manage/{*id}</c>, also at <c>/r/{repo}/…</c>): owners, dist-tags, deprecation and
/// unpublishing. It replaces the <c>/Packages/NpmManage</c> Razor Page (same URLs, same <c>?handler=</c> forms, same gates); the
/// logic is <see cref="NpmManageWorkflow"/>. The name / repository-kind / ownership
/// check runs in <see cref="OnActionExecutionAsync"/>, before the global repository filter.
/// </summary>
[Route("npm-manage/{*id}")]
[RepositoryPage(Format = RepositoryFormat.Npm, NotOnVirtual = true, NotOnRemote = true)]
public sealed class NpmManageController : AccountControllerBase
{
    private const string ViewName = "Index";

    private NpmManageWorkflow? _workflow;

    /// <inheritdoc/>
    protected override bool RequiresSignIn => true;

    private NpmManageWorkflow Workflow => _workflow!;

    /// <inheritdoc/>
    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        OnActionExecuting(context);
        if (context.Result != null) return;

        _workflow = new NpmManageWorkflow(HttpContext, PackageControllerSupport.Route(context, "id"));
        var refusal = await _workflow.EnterAsync(PackageControllerSupport.HandlerName(context));
        if (refusal != null)
        {
            context.Result = Present(refusal);
            return;
        }

        OnActionExecuted(await next());
    }

    /// <summary>The screen.</summary>
    [HttpGet, HttpHead, PageHandler, LegacyPage("/Packages/NpmManage")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
        => Present(await Workflow.GetAsync(cancellationToken));

    /// <summary>Adds an owner.</summary>
    [HttpPost, PageHandler("AddOwner"), LegacyPage("/Packages/NpmManage")]
    public async Task<IActionResult> AddOwner(string ownerName, CancellationToken cancellationToken)
        => Present(await Workflow.AddOwnerAsync(ownerName, cancellationToken));

    /// <summary>Removes an owner.</summary>
    [HttpPost, PageHandler("RemoveOwner"), LegacyPage("/Packages/NpmManage")]
    public async Task<IActionResult> RemoveOwner(string ownerName, CancellationToken cancellationToken)
        => Present(await Workflow.RemoveOwnerAsync(ownerName, cancellationToken));

    /// <summary>Points a dist-tag at a version.</summary>
    [HttpPost, PageHandler("SetTag"), LegacyPage("/Packages/NpmManage")]
    public async Task<IActionResult> SetTag(string tag, string version, CancellationToken cancellationToken)
        => Present(await Workflow.SetTagAsync(tag, version, cancellationToken));

    /// <summary>Removes a dist-tag.</summary>
    [HttpPost, PageHandler("RemoveTag"), LegacyPage("/Packages/NpmManage")]
    public async Task<IActionResult> RemoveTag(string tag, CancellationToken cancellationToken)
        => Present(await Workflow.RemoveTagAsync(tag, cancellationToken));

    /// <summary>Deprecates a version.</summary>
    [HttpPost, PageHandler("Deprecate"), LegacyPage("/Packages/NpmManage")]
    public async Task<IActionResult> Deprecate(string version, string? message, CancellationToken cancellationToken)
        => Present(await Workflow.DeprecateAsync(version, message, cancellationToken));

    /// <summary>Removes a version's deprecation.</summary>
    [HttpPost, PageHandler("Undeprecate"), LegacyPage("/Packages/NpmManage")]
    public async Task<IActionResult> Undeprecate(string version, CancellationToken cancellationToken)
        => Present(await Workflow.UndeprecateAsync(version, cancellationToken));

    /// <summary>Unpublishes one version, after the password is confirmed.</summary>
    [HttpPost, PageHandler("Unpublish"), LegacyPage("/Packages/NpmManage")]
    public async Task<IActionResult> Unpublish(string version, string? password, CancellationToken cancellationToken)
        => Present(await Workflow.UnpublishAsync(version, password, cancellationToken));

    /// <summary>Unpublishes every version, after the password is confirmed.</summary>
    [HttpPost, PageHandler("UnpublishAll"), LegacyPage("/Packages/NpmManage")]
    public async Task<IActionResult> UnpublishAll(string? password, CancellationToken cancellationToken)
        => Present(await Workflow.UnpublishAllAsync(password, cancellationToken));

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
