using System;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Identity.Application.Screens;
using BaGet.Identity.Controllers.Base;
using Microsoft.AspNetCore.Mvc;
using BaGet.Identity.Mvc.Attributes;
using BaGet.Identity.Mvc.UI;
using BaGet.Web.Application.Models.Browse;
using BaGet.Web.Application.Screens;
using BaGet.Web.Application.UseCases;
using BaGet.Web.V3.Controllers.FrontEnd;

namespace BaGet.Web.V3.Controllers.UI;

/// <summary>
/// The JSON twin of the report-abuse screen: <c>GET</c> and <c>POST /api/ui/packages/{id}/{version}/report-abuse</c> (and under <c>/api/ui/r/{repo}/</c>; 404 in a
/// virtual repository). The POST takes the form as JSON (<c>reason</c>, <c>details</c>, <c>reporterEmail</c>) and answers 400 with the reasons when it is refused.
/// <see cref="ReportAbuseController"/> calls it in process.
/// </summary>
[Route("api/ui")]
[RepositoryPage(NotOnVirtual = true)]
public sealed class ReportAbuseUiApiController : UiApiControllerBase
{
    private readonly ReportAbuseUseCase _reports;

    /// <summary>Creates the controller.</summary>
    public ReportAbuseUiApiController(ReportAbuseUseCase reports)
    {
        _reports = reports ?? throw new ArgumentNullException(nameof(reports));
    }

    /// <summary>The empty form.</summary>
    [HttpGet("packages/{id}/{version}/report-abuse"), UiApi(typeof(ReportAbuseController), nameof(ReportAbuseController.Show))]
    public Task<ActionResult<ScreenResult<ReportAbuseScreen>>> Get(string? id, string? version)
        => Task.FromResult(Respond(_reports.Get(id, version)));

    /// <summary>Records the report.</summary>
    [HttpPost("packages/{id}/{version}/report-abuse"), UiApi(typeof(ReportAbuseController), nameof(ReportAbuseController.Show))]
    public async Task<ActionResult<ScreenResult<ReportAbuseScreen>>> Submit(string? id, string? version, [FromBody] ReportAbuseForm form, CancellationToken cancellationToken)
        => Respond(await _reports.SubmitAsync(id, version, form, cancellationToken));
}
