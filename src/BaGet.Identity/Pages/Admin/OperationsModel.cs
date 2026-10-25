using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.Workflows;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Identity.Pages.Admin;

/// <summary>
/// The page model of the operations page when it was a Razor Page: starts the maintenance operations for a chosen repository as
/// background jobs and shows the last status of each.
/// </summary>
/// <remarks>
/// Kept as a shim (no breaking change of the public API); <c>AdminOperationsController</c> serves <c>/admin/operations</c>. Both
/// call <c>OperationsWorkflow</c>.
/// </remarks>
[Obsolete("The operations screen is served by AdminOperationsController; this page model has no page any more and will be removed in the next major version.")]
public class OperationsModel : AdminPageModel
{
    /// <summary>One row of the page: an operation on a repository.</summary>
    public sealed record Row(IAdminOperation Operation, RepositoryInfo Repository, AdminJobStatus? Status);

    private OperationsWorkflow? _workflow;

    private OperationsWorkflow Workflow => _workflow ??= new OperationsWorkflow(HttpContext, Url);

    /// <summary>The operations available on this server, for every repository.</summary>
    public IReadOnlyList<Row> Rows => Workflow.Operations.Rows.Select(r => new Row(r.Operation, r.Repository, r.Status)).ToList();

    /// <summary>Whether any job is running (the page refreshes itself).</summary>
    public bool AnyRunning => Workflow.Operations.AnyRunning;

    /// <summary>Outcome of the last action.</summary>
    [TempData]
    public string? Message { get => Workflow.Operations.Message; set => Workflow.Operations.Message = value; }

    /// <summary>Why the last action was refused.</summary>
    [TempData]
    public string? Error { get => Workflow.Operations.Error; set => Workflow.Operations.Error = value; }

    /// <summary>Whether the signed-in user has a password to confirm with.</summary>
    public bool HasPassword => Workflow.Operations.HasPassword;

    /// <summary>For an account without a password: it signed in again with its provider a moment ago.</summary>
    public bool Reauthenticated => Workflow.Operations.Reauthenticated;

    /// <summary>The model of the password confirmation partial, with <paramref name="id"/> for the input.</summary>
    public ConfirmPasswordView Confirmation(string id)
        => new ConfirmPasswordView(id, HasPassword, Reauthenticated, Accounts.OpenIdConnect.IsConfigured, Request.Path + Request.QueryString);

    /// <summary>Lists the operations and their status.</summary>
    public async Task OnGetAsync(CancellationToken cancellationToken) => await Workflow.GetOperationsAsync(cancellationToken);

    /// <summary>Starts <paramref name="operation"/> on <paramref name="repository"/>.</summary>
    public async Task<IActionResult> OnPostAsync(string? operation, string? repository, string? password, CancellationToken cancellationToken)
        => await Workflow.StartAsync(operation, repository, password, cancellationToken) ?? Page();
}
