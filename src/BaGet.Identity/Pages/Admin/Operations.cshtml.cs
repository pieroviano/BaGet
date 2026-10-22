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

    /// <summary>Whether the signed-in user has a password to confirm with.</summary>
    public bool HasPassword { get; private set; } = true;

    /// <summary>For an account without a password: it signed in again with its provider a moment ago.</summary>
    public bool Reauthenticated { get; private set; }

    /// <summary>The model of the password confirmation partial, with <paramref name="id"/> for the input.</summary>
    public ConfirmPasswordView Confirmation(string id)
        => new ConfirmPasswordView(id, HasPassword, Reauthenticated, Accounts.OpenIdConnect.IsConfigured, Request.Path + Request.QueryString);

    /// <summary>Lists the operations and their status.</summary>
    public async Task OnGetAsync(CancellationToken cancellationToken) => await LoadAsync(cancellationToken);

    /// <summary>The message shown when the confirmation fails; nothing was started.</summary>
    private const string NotConfirmed = "The confirmation is incorrect, so nothing was started.";

    /// <summary>Starts <paramref name="operation"/> on <paramref name="repository"/>.</summary>
    public async Task<IActionResult> OnPostAsync(string? operation, string? repository, string? password, CancellationToken cancellationToken)
    {
        var services = HttpContext.RequestServices;
        var jobs = services.GetRequiredService<AdminJobService>();
        var info = await services.GetRequiredService<IRepositoryService>().FindAsync(repository, cancellationToken);
        var definition = jobs.Operations.FirstOrDefault(o => o.Name == operation);
        if (info == null || definition == null || !definition.IsAvailable(services) || !definition.AppliesTo(info)) return NotFound();

        var user = await UserManager.GetUserAsync(User);

        // An operation that deletes data asks for the password again, like the other destructive pages.
        if (definition.RequiresConfirmation && !await IsConfirmedAsync(user, password))
        {
            Error = NotConfirmed;

            return RedirectToPage();
        }

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
        var current = await UserManager.GetUserAsync(User);
        HasPassword = current == null || await UserManager.HasPasswordAsync(current);
        Reauthenticated = !HasPassword && await HasReauthenticatedRecentlyAsync();

        var services = HttpContext.RequestServices;
        var jobs = services.GetRequiredService<AdminJobService>();
        var repositories = await services.GetRequiredService<IRepositoryService>().ListAsync(cancellationToken);

        Rows = jobs.Operations
            .Where(o => o.IsAvailable(services))
            .SelectMany(o => repositories.Where(o.AppliesTo).Select(r => new Row(o, r, jobs.Get(o.Name, r.Name))))
            .ToList();
    }
}
