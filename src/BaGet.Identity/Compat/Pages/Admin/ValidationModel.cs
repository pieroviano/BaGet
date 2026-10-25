using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.Workflows;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Identity.Pages.Admin;

/// <summary>
/// The page model of the validation queue page when it was a Razor Page: submissions still pending and packages quarantined because
/// a validator (or indexing) failed, to retry, release or reject.
/// </summary>
/// <remarks>
/// Kept as a shim (no breaking change of the public API); <c>AdminOperationsController</c> serves <c>/admin/validation</c>. Both
/// call <c>OperationsWorkflow</c>.
/// </remarks>
[Obsolete("The validation queue screen is served by AdminOperationsController; this page model has no page any more and will be removed in the next major version.")]
public class ValidationModel : AdminPageModel
{
    private OperationsWorkflow? _workflow;

    private OperationsWorkflow Workflow => _workflow ??= new OperationsWorkflow(HttpContext, Url);

    /// <summary>Whether validation can be administered here.</summary>
    public bool IsSupported => Workflow.Validation.IsSupported;

    /// <summary>The pending and quarantined submissions.</summary>
    public IReadOnlyList<ValidationSubmissionSummary> Submissions => Workflow.Validation.Submissions;

    /// <summary>Whether the signed in administrator has a password to confirm with.</summary>
    public bool HasPassword => Workflow.Validation.HasPassword;

    /// <summary>For an account without a password: it signed in again with its provider a moment ago.</summary>
    public bool Reauthenticated => Workflow.Validation.Reauthenticated;

    /// <summary>Whether an external provider is available to sign in again with.</summary>
    public bool CanReauthenticate => Accounts.OpenIdConnect.IsConfigured;

    /// <summary>Outcome of the last action.</summary>
    [TempData]
    public string? Message { get => Workflow.Validation.Message; set => Workflow.Validation.Message = value; }

    /// <summary>Why the last action was refused.</summary>
    [TempData]
    public string? Error { get => Workflow.Validation.Error; set => Workflow.Validation.Error = value; }

    /// <summary>Lists the submissions.</summary>
    public async Task OnGetAsync(CancellationToken cancellationToken) => await Workflow.GetValidationAsync(cancellationToken);

    /// <summary>Validates the submission again.</summary>
    public async Task<IActionResult> OnPostRetryAsync(string? submissionId, CancellationToken cancellationToken)
        => await Workflow.RetryAsync(submissionId, cancellationToken) ?? Page();

    /// <summary>Indexes the quarantined package although validation failed.</summary>
    public async Task<IActionResult> OnPostReleaseAsync(string? submissionId, string? password, CancellationToken cancellationToken)
        => await Workflow.ReleaseAsync(submissionId, password, cancellationToken) ?? Page();

    /// <summary>Deletes the quarantined package.</summary>
    public async Task<IActionResult> OnPostRejectAsync(string? submissionId, string? password, CancellationToken cancellationToken)
        => await Workflow.RejectAsync(submissionId, password, cancellationToken) ?? Page();
}
