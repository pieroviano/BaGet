using System;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BaGet.Web;

/// <summary>The page model of the report-abuse page when it was a Razor Page.</summary>
/// <remarks>
/// Kept as a shim so that code compiled against it still loads and runs (no breaking change of the public API); there is no page
/// behind it any more: <see cref="ReportAbuseController"/> serves it. Both share <see cref="ReportAbuseWorkflow"/>.
/// </remarks>
[Obsolete("The report-abuse page is served by ReportAbuseController; this page model has no page any more and will be removed in the next major version.")]
public class ReportAbuseModel : PageModel
{
    private readonly ReportAbuseWorkflow _workflow;

    /// <summary>Creates the page model.</summary>
    public ReportAbuseModel(IContext context, SystemTime time)
    {
        _workflow = new ReportAbuseWorkflow(context, time);
    }

    /// <summary>The package ID.</summary>
    [BindProperty(SupportsGet = true)]
    public string? Id { get; set; }

    /// <summary>The package version.</summary>
    [BindProperty(SupportsGet = true)]
    public string? Version { get; set; }

    /// <summary>One of <see cref="AbuseReport.Reasons"/>.</summary>
    [BindProperty]
    [Required]
    public string? Reason { get; set; }

    /// <summary>What the reporter saw.</summary>
    [BindProperty]
    [Required]
    [StringLength(AbstractContext<DbContext>.DefaultMaxStringLength)]
    public string? Details { get; set; }

    /// <summary>The address of the reporter, optional.</summary>
    [BindProperty]
    [EmailAddress]
    [StringLength(AbstractContext<DbContext>.MaxEmailLength)]
    [Display(Name = "Your email (optional)")]
    public string? ReporterEmail { get; set; }

    /// <summary>True once the report was recorded.</summary>
    public bool Submitted { get; private set; }

    /// <summary>Reports are kept in the database, which Azure Table storage cannot do.</summary>
    public bool CanReport => _workflow.CanReport;

    /// <summary>Shows the form.</summary>
    public void OnGet()
    {
    }

    /// <summary>Records the report.</summary>
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var model = new ReportAbuseViewModel
        {
            Id = Id,
            Version = Version,
            Reason = Reason,
            Details = Details,
            ReporterEmail = ReporterEmail,
        };

        switch (await _workflow.SubmitAsync(model, ModelState, cancellationToken))
        {
            case ReportAbuseOutcome.NotFound:
                return NotFound();
            case ReportAbuseOutcome.Submitted:
                Submitted = true;
                return Page();
            default:
                return Page();
        }
    }
}
