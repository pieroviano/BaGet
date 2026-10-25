using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.Mvc;
using BaGet.Identity.Workflows;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace BaGet.Identity.Controllers;

/// <summary>
/// The delete tools of a Docker image (<c>/docker-manage/{**image}</c>, also at <c>/r/{repo}/…</c>): delete a tag, a manifest, the
/// unused layers or the whole image. It replaces the <c>/Packages/DockerManage</c> Razor Page (same URLs, same <c>?handler=</c>
/// forms, same gates); the logic is <see cref="DockerManageWorkflow"/>, shared with the obsolete <c>DockerManageModel</c>. The
/// image-name / repository-kind / Delete-permission check runs in <see cref="OnActionExecutionAsync"/>, before the global
/// repository filter, as the page model's did.
/// </summary>
[Route("docker-manage/{**image}")]
[RepositoryPage(Format = RepositoryFormat.Docker, NotOnVirtual = true)]
public sealed class DockerManageController : AccountControllerBase
{
    private const string ViewName = "Index";

    private DockerManageWorkflow? _workflow;

    /// <inheritdoc/>
    protected override bool RequiresSignIn => true;

    private DockerManageWorkflow Workflow => _workflow!;

    /// <inheritdoc/>
    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        OnActionExecuting(context);
        if (context.Result != null) return;

        _workflow = new DockerManageWorkflow(HttpContext, PackageControllerSupport.Route(context, "image"));
        var refusal = await _workflow.EnterAsync();
        if (refusal != null)
        {
            context.Result = Present(refusal);
            return;
        }

        OnActionExecuted(await next());
    }

    /// <summary>The screen.</summary>
    [HttpGet, HttpHead, PageHandler, LegacyPage("/Packages/DockerManage")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
        => Present(await Workflow.GetAsync(cancellationToken));

    /// <summary>Deletes a tag, after the password.</summary>
    [HttpPost, PageHandler("DeleteTag"), LegacyPage("/Packages/DockerManage")]
    public async Task<IActionResult> DeleteTag(string? tag, string? password, CancellationToken cancellationToken)
        => Present(await Workflow.DeleteTagAsync(tag, password, cancellationToken));

    /// <summary>Deletes a manifest with its tags, after the password.</summary>
    [HttpPost, PageHandler("DeleteManifest"), LegacyPage("/Packages/DockerManage")]
    public async Task<IActionResult> DeleteManifest(string? digest, string? password, CancellationToken cancellationToken)
        => Present(await Workflow.DeleteManifestAsync(digest, password, cancellationToken));

    /// <summary>Removes the layers no manifest uses, after the password.</summary>
    [HttpPost, PageHandler("Prune"), LegacyPage("/Packages/DockerManage")]
    public async Task<IActionResult> Prune(string? password, CancellationToken cancellationToken)
        => Present(await Workflow.PruneAsync(password, cancellationToken));

    /// <summary>Deletes the whole image, after the password.</summary>
    [HttpPost, PageHandler("DeleteImage"), LegacyPage("/Packages/DockerManage")]
    public async Task<IActionResult> DeleteImage(string? password, CancellationToken cancellationToken)
        => Present(await Workflow.DeleteImageAsync(password, cancellationToken));

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
