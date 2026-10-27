using System;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Identity.Application.Screens;
using BaGet.Identity.Controllers.Base;
using Microsoft.AspNetCore.Mvc;
using BaGet.Identity.Mvc.UI;
using BaGet.Web.Application.UseCases;
using BaGet.Web.V3.Controllers.FrontEnd;

namespace BaGet.Web.V3.Controllers.UI;

/// <summary>
/// The JSON twin of the legal screens: <c>GET /api/ui/terms</c> and <c>GET /api/ui/privacy</c> answer the operator's Markdown
/// (<c>Site:TermsOfUsePath</c>, <c>Site:PrivacyPolicyPath</c>) rendered to safe HTML, or 404 while a file is not configured. Anonymous.
/// <see cref="LegalController"/> calls it in process and renders the view.
/// </summary>
[Route("api/ui")]
public sealed class LegalUiApiController : UiApiControllerBase
{
    private readonly LegalUseCase _legal;

    /// <summary>Creates the controller.</summary>
    public LegalUiApiController(LegalUseCase legal)
    {
        _legal = legal ?? throw new ArgumentNullException(nameof(legal));
    }

    /// <summary>The Privacy Policy.</summary>
    [HttpGet("privacy"), UiApi("/Privacy")]
    public async Task<ActionResult<ScreenResult<LegalScreen>>> Privacy(CancellationToken cancellationToken)
        => Respond(await _legal.PrivacyAsync(cancellationToken));

    /// <summary>The Terms of Use.</summary>
    [HttpGet("terms"), UiApi("/Terms")]
    public async Task<ActionResult<ScreenResult<LegalScreen>>> Terms(CancellationToken cancellationToken)
        => Respond(await _legal.TermsAsync(cancellationToken));
}
