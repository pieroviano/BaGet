using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.Mvc;
using BaGet.Identity.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace BaGet.Identity.Controllers;

/// <summary>
/// The delete tools of a Docker image (<c>/docker-manage/{**image}</c>, also at <c>/r/{repo}/…</c>): delete a tag, a manifest, the
/// unused layers or the whole image. It replaces the <c>/Packages/DockerManage</c> Razor Page (same URLs, same <c>?handler=</c>
/// forms, same gates); the work is done by <see cref="DockerManageUiApiController"/> (the JSON twin, over <c>DockerManageUseCase</c>),
/// which this controller calls in process and renders. The image-name / repository-kind / Delete-permission check runs in
/// <see cref="OnActionExecutionAsync"/>, before the global repository filter.
/// </summary>
[Route("docker-manage/{**image}")]
[RepositoryPage(Format = RepositoryFormat.Docker, NotOnVirtual = true)]
public sealed class DockerManageController : AccountControllerBase
{
    private const string ViewName = "Index";

    /// <inheritdoc/>
    protected override bool RequiresSignIn => true;

    private string? Image => RouteData.Values.TryGetValue("image", out var image) ? image as string : null;

    /// <inheritdoc/>
    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        OnActionExecuting(context);
        if (context.Result != null) return;

        var refusal = await UiApiEntry.InvokeAsync<DockerManageUiApiController, DockerManageViewModel>(
            this,
            api => api.EnterAsync(PackageControllerSupport.Route(context, "image"), HttpMethods.IsPost(Request.Method), context.HttpContext.RequestAborted));
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
        => Present(await Api<DockerManageUiApiController, DockerManageViewModel>(api => api.Get(Image, cancellationToken)));

    /// <summary>Deletes a tag, after the password.</summary>
    [HttpPost, PageHandler("DeleteTag"), LegacyPage("/Packages/DockerManage")]
    public async Task<IActionResult> DeleteTag(string? tag, string? password, CancellationToken cancellationToken)
        => Present(await Api<DockerManageUiApiController, DockerManageViewModel>(api => api.DeleteTag(Image, new DockerManageForm { Tag = tag, Password = password }, cancellationToken)));

    /// <summary>Deletes a manifest with its tags, after the password.</summary>
    [HttpPost, PageHandler("DeleteManifest"), LegacyPage("/Packages/DockerManage")]
    public async Task<IActionResult> DeleteManifest(string? digest, string? password, CancellationToken cancellationToken)
        => Present(await Api<DockerManageUiApiController, DockerManageViewModel>(api => api.DeleteManifest(Image, new DockerManageForm { Digest = digest, Password = password }, cancellationToken)));

    /// <summary>Removes the layers no manifest uses, after the password.</summary>
    [HttpPost, PageHandler("Prune"), LegacyPage("/Packages/DockerManage")]
    public async Task<IActionResult> Prune(string? password, CancellationToken cancellationToken)
        => Present(await Api<DockerManageUiApiController, DockerManageViewModel>(api => api.Prune(Image, new DockerManageForm { Password = password }, cancellationToken)));

    /// <summary>Deletes the whole image, after the password.</summary>
    [HttpPost, PageHandler("DeleteImage"), LegacyPage("/Packages/DockerManage")]
    public async Task<IActionResult> DeleteImage(string? password, CancellationToken cancellationToken)
        => Present(await Api<DockerManageUiApiController, DockerManageViewModel>(api => api.DeleteImage(Image, new DockerManageForm { Password = password }, cancellationToken)));

    /// <summary>
    /// A POST that names no handler (or one this screen does not have), such as a stray <c>&lt;form&gt;</c> without
    /// <c>?handler=</c>: the antiforgery check has already run, and there is nothing to do, so the answer is 404. (A Razor Page
    /// with only named POST handlers behaved alike for the caller; without this action routing would answer 404 before the
    /// antiforgery filter and an unprotected POST would not be refused with 400.) It carries no <see cref="LegacyPageAttribute"/>:
    /// it is not one of the page's handlers, so it stays out of the route contract.
    /// </summary>
    [HttpPost, PageHandler]
    public IActionResult NoHandler() => NotFound();

    private IActionResult Present(UiApiResponse<DockerManageViewModel> response) => Render(response, ViewName);
}
