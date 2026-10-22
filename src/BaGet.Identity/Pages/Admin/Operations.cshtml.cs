using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace BaGet.Identity.Pages.Admin;

/// <summary>
/// Starts the maintenance operations (catalog rebuild, downloads import) for a chosen repository as background
/// jobs and shows the last status of each. One run at a time per operation and repository. Starting is refused with
/// 503 while the <see cref="MaintenanceGate"/> is closed, and recorded with <see cref="IAdminAudit"/>.
/// </summary>
public class OperationsModel : AdminPageModel
{
    /// <summary>One row of the page: an operation on a repository.</summary>
    public sealed record Row(IAdminOperation Operation, RepositoryInfo Repository, AdminJobStatus? Status);

    /// <summary>The operations available on this server, for every repository.</summary>
    public IReadOnlyList<Row> Rows { get; private set; } = Array.Empty<Row>();

    /// <summary>Whether any job is running (the page refreshes itself).</summary>
    public bool AnyRunning => Rows.Any(r => r.Status?.State == AdminJobState.Running);

    /// <summary>Outcome of the last action.</summary>
    [TempData]
    public string? Message { get; set; }

    /// <summary>Why the last action was refused.</summary>
    [TempData]
    public string? Error { get; set; }

    /// <summary>Lists the operations and their status.</summary>
    public async Task OnGetAsync(CancellationToken cancellationToken) => await LoadAsync(cancellationToken);

    /// <summary>Starts <paramref name="operation"/> on <paramref name="repository"/>.</summary>
    public async Task<IActionResult> OnPostAsync(string? operation, string? repository, CancellationToken cancellationToken)
    {
        var services = HttpContext.RequestServices;
        var jobs = services.GetRequiredService<AdminJobService>();
        var info = await services.GetRequiredService<IRepositoryService>().FindAsync(repository, cancellationToken);
        var definition = jobs.Operations.FirstOrDefault(o => o.Name == operation);
        if (info == null || definition == null || !definition.IsAvailable(services) || !definition.AppliesTo(info)) return NotFound();

        var user = await UserManager.GetUserAsync(User);
        if (!jobs.TryStart(definition.Name, info, user?.UserName, out var refusal))
        {
            Error = refusal;
        }
        else
        {
            Message = $"{definition.Title} started on {info.Name}.";
        }

        return RedirectToPage();
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        var services = HttpContext.RequestServices;
        var jobs = services.GetRequiredService<AdminJobService>();
        var repositories = await services.GetRequiredService<IRepositoryService>().ListAsync(cancellationToken);

        Rows = jobs.Operations
            .Where(o => o.IsAvailable(services))
            .SelectMany(o => repositories.Where(o.AppliesTo).Select(r => new Row(o, r, jobs.Get(o.Name, r.Name))))
            .ToList();
    }
}
