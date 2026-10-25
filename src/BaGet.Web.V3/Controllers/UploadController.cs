using System.Threading;
using System.Threading.Tasks;
using BaGet.Identity.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace BaGet.Web;

/// <summary>
/// The NuGet upload page (<c>/upload</c>, <c>/r/{repo}/upload</c>): uploading through the browser in two steps, the first stages
/// the file and shows what it contains (<c>?handler=Stage</c>), the second publishes it (<c>?handler=Submit</c>) or discards it
/// (<c>?handler=Cancel</c>). It replaces the Razor Page <c>Upload</c>: same URLs, forms and <c>asp-page</c> name (<see cref="LegacyPageAttribute"/>). The repository rule is
/// <see cref="RepositoryPageAttribute.Upload"/>: the caller needs the right to publish, and a virtual repository continues in its
/// deploy repository. View: <c>Views/Upload/Index.cshtml</c>.
/// </summary>
/// <remarks>
/// Deliberately not an account controller: those answer 404 while accounts are disabled, and this page must still work there -
/// with a pasted API key, exactly as the push endpoint requires one. Every step is authenticated (session, or API key) by the
/// workflow; the antiforgery token is validated by <see cref="UiControllerBase"/>.
/// </remarks>
[Route("upload")]
[LegacyPage("/Upload")]
[RepositoryPage(Upload = true)]
public sealed class UploadController : UiControllerBase
{
    private UploadWorkflow Workflow => HttpContext.RequestServices.GetRequiredService<UploadWorkflow>();

    /// <summary>Step one: the form.</summary>
    [HttpGet, HttpHead, PageHandler]
    public IActionResult Index(string? id) => View("Index", Workflow.Show(User, id));

    /// <summary>Stages the chosen file and shows what it contains.</summary>
    [HttpPost, PageHandler("Stage")]
    public async Task<IActionResult> Stage(string? id, IFormFile? package, string? apiKey, CancellationToken cancellationToken)
        => Render(await Workflow.StageAsync(User, HttpContext.RequestServices, id, package, apiKey, cancellationToken));

    /// <summary>Publishes the staged upload.</summary>
    [HttpPost, PageHandler("Submit")]
    public async Task<IActionResult> Submit(string? id, string token, string? apiKey, bool acceptTerms, CancellationToken cancellationToken)
        => Render(await Workflow.SubmitAsync(User, HttpContext.RequestServices, id, token, apiKey, acceptTerms, cancellationToken));

    /// <summary>Discards the staged upload.</summary>
    [HttpPost, PageHandler("Cancel")]
    public async Task<IActionResult> Cancel(string? id, string token, string? apiKey, CancellationToken cancellationToken)
        => Render(await Workflow.CancelAsync(User, HttpContext.RequestServices, id, token, apiKey, cancellationToken));

    private IActionResult Render(UploadViewModel model)
    {
        Response.StatusCode = model.StatusCode;

        return View("Index", model);
    }
}
