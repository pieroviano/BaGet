using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace BaGet.Identity.Pages.Admin;

/// <summary>
/// Abuse reports of every repository: the unresolved ones by default (<c>?all=true</c> for all), each with its
/// repository, resolvable or reopened by an administrator. Resolving is refused with 503 while the
/// <see cref="MaintenanceGate"/> is closed.
/// </summary>
public class AbuseModel : AdminPageModel
{
    private IAbuseReportAdministration Reports
        => HttpContext.RequestServices.GetRequiredService<IAbuseReportAdministration>();

    /// <summary>Show the resolved reports too.</summary>
    [BindProperty(SupportsGet = true)]
    public bool All { get; set; }

    /// <summary>Whether reports are stored with the configured database.</summary>
    public bool IsSupported => Reports.IsSupported;

    /// <summary>The reports, newest first.</summary>
    public IReadOnlyList<AbuseReportSummary> Items { get; private set; } = Array.Empty<AbuseReportSummary>();

    /// <summary>Outcome of the last action.</summary>
    [TempData]
    public string? Message { get; set; }

    /// <summary>Lists the reports.</summary>
    public async Task OnGetAsync(CancellationToken cancellationToken)
        => Items = await Reports.ListAsync(unresolvedOnly: !All, cancellationToken);

    /// <summary>Marks a report resolved.</summary>
    public Task<IActionResult> OnPostResolveAsync(int key, CancellationToken cancellationToken) => SetAsync(key, true, cancellationToken);

    /// <summary>Reopens a resolved report.</summary>
    public Task<IActionResult> OnPostReopenAsync(int key, CancellationToken cancellationToken) => SetAsync(key, false, cancellationToken);

    private async Task<IActionResult> SetAsync(int key, bool resolved, CancellationToken cancellationToken)
    {
        if (!await Reports.SetResolvedAsync(key, resolved, cancellationToken)) return NotFound();

        var user = await UserManager.GetUserAsync(User);
        HttpContext.RequestServices.GetRequiredService<IAdminAudit>()
            .Record(user?.UserName, resolved ? "abuse.resolve" : "abuse.reopen", $"report {key}");
        Message = resolved ? $"Report {key} was resolved." : $"Report {key} was reopened.";

        return RedirectToPage(new { all = All ? "true" : null });
    }
}
