using System;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Identity.Mvc;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Web;

/// <summary>
/// Report abuse (<c>/packages/{id}/{version}/report-abuse</c>, and under <c>/r/{repo}/</c>; 404 in a virtual repository). Replaces the
/// <c>/ReportAbuse</c> Razor Page (<see cref="ReportAbuseModel"/> is now an obsolete shim). The form is posted with antiforgery
/// validation (<see cref="UiControllerBase"/>).
/// </summary>
[RepositoryPage(NotOnVirtual = true)]
public sealed class ReportAbuseController : UiControllerBase
{
    private readonly ReportAbuseWorkflow _workflow;

    /// <summary>Creates the controller.</summary>
    public ReportAbuseController(ReportAbuseWorkflow workflow)
    {
        _workflow = workflow ?? throw new ArgumentNullException(nameof(workflow));
    }

    /// <summary>The empty form.</summary>
    [Route("packages/{id}/{version}/report-abuse"), HttpGet, HttpHead, PageHandler, LegacyPage("/ReportAbuse")]
    public IActionResult Show(string? id, string? version)
        => View("Show", new ReportAbuseViewModel { Id = id, Version = version, CanReport = _workflow.CanReport });

    /// <summary>Records the report.</summary>
    [Route("packages/{id}/{version}/report-abuse"), HttpPost, PageHandler, LegacyPage("/ReportAbuse")]
    public async Task<IActionResult> Submit(ReportAbuseViewModel model, CancellationToken cancellationToken)
    {
        model.CanReport = _workflow.CanReport;
        switch (await _workflow.SubmitAsync(model, ModelState, cancellationToken))
        {
            case ReportAbuseOutcome.NotFound:
                return NotFound();
            case ReportAbuseOutcome.Submitted:
                model.Submitted = true;
                return View("Show", model);
            default:
                return View("Show", model);
        }
    }
}
