using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.Workflows;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Identity.Pages.Admin;

/// <summary>
/// The page model of the abuse reports page when it was a Razor Page: the reports of every repository, the unresolved ones by
/// default (<c>?all=true</c> for all), each resolvable or reopened by an administrator.
/// </summary>
/// <remarks>
/// Kept as a shim (no breaking change of the public API); <c>AdminOperationsController</c> serves <c>/admin/abuse</c>. Both call
/// <c>OperationsWorkflow</c>.
/// </remarks>
[Obsolete("The abuse reports screen is served by AdminOperationsController; this page model has no page any more and will be removed in the next major version.")]
public class AbuseModel : AdminPageModel
{
    private OperationsWorkflow? _workflow;

    private OperationsWorkflow Workflow => _workflow ??= new OperationsWorkflow(HttpContext, Url);

    /// <summary>Show the resolved reports too.</summary>
    [BindProperty(SupportsGet = true)]
    public bool All { get => Workflow.Abuse.All; set => Workflow.Abuse.All = value; }

    /// <summary>Whether reports are stored with the configured database.</summary>
    public bool IsSupported => Workflow.Abuse.IsSupported;

    /// <summary>The reports, newest first.</summary>
    public IReadOnlyList<AbuseReportSummary> Items => Workflow.Abuse.Items;

    /// <summary>Outcome of the last action.</summary>
    [TempData]
    public string? Message { get => Workflow.Abuse.Message; set => Workflow.Abuse.Message = value; }

    /// <summary>Lists the reports.</summary>
    public async Task OnGetAsync(CancellationToken cancellationToken) => await Workflow.GetAbuseAsync(cancellationToken);

    /// <summary>Marks a report resolved.</summary>
    public async Task<IActionResult> OnPostResolveAsync(int key, CancellationToken cancellationToken)
        => await Workflow.ResolveAsync(key, cancellationToken) ?? Page();

    /// <summary>Reopens a resolved report.</summary>
    public async Task<IActionResult> OnPostReopenAsync(int key, CancellationToken cancellationToken)
        => await Workflow.ReopenAsync(key, cancellationToken) ?? Page();
}
