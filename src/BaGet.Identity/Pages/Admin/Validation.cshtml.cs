using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace BaGet.Identity.Pages.Admin;

/// <summary>
/// The validation queue across every repository: submissions still pending and packages quarantined because a
/// validator (or indexing) failed. An administrator can retry validation, release a quarantined package (it is
/// indexed anyway, which is logged and audited) or reject it (staged file and rows deleted). Release and reject
/// need the administrator's password (or a recent provider sign-in); every write is refused with 503 while the
/// <see cref="MaintenanceGate"/> is closed.
/// </summary>
public class ValidationModel : AdminPageModel
{
    private IValidationAdministration? Administration
        => HttpContext.RequestServices.GetService<IValidationAdministration>();

    /// <summary>Whether validation can be administered here.</summary>
    public bool IsSupported => Administration?.IsSupported == true;

    /// <summary>The pending and quarantined submissions.</summary>
    public IReadOnlyList<ValidationSubmissionSummary> Submissions { get; private set; } = Array.Empty<ValidationSubmissionSummary>();

    /// <summary>Whether the signed in administrator has a password to confirm with.</summary>
    public bool HasPassword { get; private set; } = true;

    /// <summary>For an account without a password: it signed in again with its provider a moment ago.</summary>
    public bool Reauthenticated { get; private set; }

    /// <summary>Whether an external provider is available to sign in again with.</summary>
    public bool CanReauthenticate => Accounts.OpenIdConnect.IsConfigured;

    /// <summary>Outcome of the last action.</summary>
    [TempData]
    public string? Message { get; set; }

    /// <summary>Why the last action was refused.</summary>
    [TempData]
    public string? Error { get; set; }

    /// <summary>Lists the submissions.</summary>
    public async Task OnGetAsync(CancellationToken cancellationToken) => await LoadAsync(cancellationToken);

    /// <summary>Validates the submission again.</summary>
    public Task<IActionResult> OnPostRetryAsync(string? submissionId, CancellationToken cancellationToken)
        => ActAsync(submissionId, null, confirm: false, (a, id, user) => a.RetryAsync(id, user, cancellationToken), "queued for validation again");

    /// <summary>Indexes the quarantined package although validation failed.</summary>
    public Task<IActionResult> OnPostReleaseAsync(string? submissionId, string? password, CancellationToken cancellationToken)
        => ActAsync(submissionId, password, confirm: true, (a, id, user) => a.ReleaseAsync(id, user, cancellationToken), "released and indexed");

    /// <summary>Deletes the quarantined package.</summary>
    public Task<IActionResult> OnPostRejectAsync(string? submissionId, string? password, CancellationToken cancellationToken)
        => ActAsync(submissionId, password, confirm: true, (a, id, user) => a.RejectAsync(id, user, cancellationToken), "rejected and deleted");

    private async Task<IActionResult> ActAsync(
        string? submissionId,
        string? password,
        bool confirm,
        Func<IValidationAdministration, string, string?, Task<string?>> action,
        string done)
    {
        var administration = Administration;
        if (administration == null || !administration.IsSupported || string.IsNullOrEmpty(submissionId)) return NotFound();

        var user = await UserManager.GetUserAsync(User);
        if (user == null) return Challenge();

        if (confirm && !await IsConfirmedAsync(user, password))
        {
            Error = "The confirmation is incorrect, so nothing was changed.";
            return RedirectToPage();
        }

        var refusal = await action(administration, submissionId, user.UserName);
        if (refusal == null)
        {
            Message = $"Submission {submissionId} was {done}.";
        }
        else
        {
            Error = refusal;
        }

        return RedirectToPage();
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        var administration = Administration;
        if (administration?.IsSupported == true)
        {
            Submissions = await administration.ListAsync(cancellationToken);
        }

        var current = await UserManager.GetUserAsync(User);
        HasPassword = current == null || await UserManager.HasPasswordAsync(current);
        Reauthenticated = !HasPassword && await HasReauthenticatedRecentlyAsync();
    }
}
