using System.Threading;
using System.Threading.Tasks;
using BaGet.Identity.Controllers.Base;
using BaGet.Web.V3.Controllers.UI;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;
using BaGet.Identity.Mvc.Attributes;
using BaGet.Web.Application.UseCases;
using BaGet.Web.V3.ViewModels;

namespace BaGet.Web.V3.Controllers.FrontEnd;

/// <summary>
/// The Terms of Use (<c>/terms</c>) and Privacy Policy (<c>/privacy</c>) pages: the operator-supplied Markdown files
/// (<c>Site:TermsOfUsePath</c>, <c>Site:PrivacyPolicyPath</c>), 404 while a file is not configured or does not exist. Same URLs, same
/// views in <c>Views/Legal/</c>. Renders what <see cref="LegalUiApiController"/> answers.
/// </summary>
public sealed class LegalController : UiControllerBase
{
    /// <summary>The Privacy Policy.</summary>
    [Route("privacy"), HttpGet, HttpHead, PageHandler]
    public async Task<IActionResult> Privacy(CancellationToken cancellationToken)
        => Render(await Api<LegalUiApiController, LegalScreen>(api => api.Privacy(cancellationToken)), "Privacy", ToViewModel);

    /// <summary>The Terms of Use.</summary>
    [Route("terms"), HttpGet, HttpHead, PageHandler]
    public async Task<IActionResult> Terms(CancellationToken cancellationToken)
        => Render(await Api<LegalUiApiController, LegalScreen>(api => api.Terms(cancellationToken)), "Terms", ToViewModel);

    // The view keeps its model (hosts override the view); the twin's DTO carries the rendered HTML as text.
    private static object? ToViewModel(LegalScreen screen) => new LegalViewModel(screen.Title, new HtmlString(screen.ContentHtml));
}
