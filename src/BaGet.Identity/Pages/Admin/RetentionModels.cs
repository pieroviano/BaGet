using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.Workflows;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Identity.Pages.Admin;

/// <summary>
/// The page model of the retention policies page when it was a Razor Page: lists the policies and creates one (asks for the
/// administrator's password, audited with the access audit).
/// </summary>
/// <remarks>
/// Kept as a shim (no breaking change of the public API); <c>AdminRetentionController</c> serves <c>/admin/retention</c>. Both call
/// <c>RetentionWorkflow</c>.
/// </remarks>
[Obsolete("The retention screens are served by AdminRetentionController; this page model has no page any more and will be removed in the next major version.")]
public class RetentionModel : RetentionPageModel
{
    private RetentionWorkflow? _workflow;

    private RetentionWorkflow Workflow => _workflow ??= new RetentionWorkflow(HttpContext, Url);

    /// <summary>The new policy.</summary>
    [BindProperty]
    public RetentionPolicyForm Form { get => Workflow.Policies.Form; set => Workflow.Policies.Form = value; }

    /// <summary>Every policy.</summary>
    public IReadOnlyList<RetentionPolicyView> Policies => Workflow.Policies.Policies;

    /// <summary>Lists the policies.</summary>
    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await Workflow.GetPoliciesAsync(cancellationToken);
        await LoadChoicesAsync(cancellationToken);
        Message = Workflow.Policies.Message;
        Error = Workflow.Policies.Error;
    }

    /// <summary>Creates a policy and opens it.</summary>
    public async Task<IActionResult> OnPostAsync(string? password, CancellationToken cancellationToken)
    {
        var result = await Workflow.CreateAsync(password, cancellationToken);
        await LoadChoicesAsync(cancellationToken);
        Message = Workflow.Policies.Message;
        Error = Workflow.Policies.Error;

        return result ?? Page();
    }
}

/// <summary>
/// The page model of one retention policy when it was a Razor Page: its rules (changeable), its last run, and the actions on it
/// (dry run, run now, delete).
/// </summary>
/// <remarks>
/// Kept as a shim (no breaking change of the public API); <c>AdminRetentionController</c> serves <c>/admin/retention/{key}</c>. Both
/// call <c>RetentionWorkflow</c>.
/// </remarks>
[Obsolete("The retention screens are served by AdminRetentionController; this page model has no page any more and will be removed in the next major version.")]
public class RetentionPolicyModel : RetentionPageModel
{
    private RetentionWorkflow? _workflow;

    private RetentionWorkflow Workflow => _workflow ??= new RetentionWorkflow(HttpContext, Url);

    /// <summary>The policy's key (route).</summary>
    [BindProperty(SupportsGet = true)]
    public int Key { get => Workflow.Policy.Key; set => Workflow.Policy.Key = value; }

    /// <summary>The rules, as edited.</summary>
    [BindProperty]
    public RetentionPolicyForm Form { get => Workflow.Policy.Form; set => Workflow.Policy.Form = value; }

    /// <summary>The policy with the names of what it applies to.</summary>
    public RetentionPolicyView? View => Workflow.Policy.View;

    /// <summary>The last run since the server started (a dry run's report included), or null.</summary>
    public RetentionJobStatus? Status => Workflow.Policy.Status;

    /// <summary>Whether a run is in progress (the page refreshes itself).</summary>
    public bool Running => Workflow.Policy.Running;

    /// <summary>Shows the policy.</summary>
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
        => await Finish(await Workflow.GetPolicyAsync(cancellationToken), cancellationToken);

    /// <summary>Saves the rules.</summary>
    public async Task<IActionResult> OnPostSaveAsync(string? password, CancellationToken cancellationToken)
        => await Finish(await Workflow.SaveAsync(password, cancellationToken), cancellationToken);

    /// <summary>Starts a dry run: the report of what a run would delete.</summary>
    public async Task<IActionResult> OnPostDryRunAsync(CancellationToken cancellationToken)
        => await Finish(await Workflow.DryRunAsync(cancellationToken), cancellationToken);

    /// <summary>Starts a run that deletes.</summary>
    public async Task<IActionResult> OnPostRunAsync(string? password, CancellationToken cancellationToken)
        => await Finish(await Workflow.RunAsync(password, cancellationToken), cancellationToken);

    /// <summary>Deletes the policy (what it deleted stays deleted).</summary>
    public async Task<IActionResult> OnPostDeleteAsync(string? password, CancellationToken cancellationToken)
        => await Finish(await Workflow.DeleteAsync(password, cancellationToken), cancellationToken);

    private async Task<IActionResult> Finish(IActionResult? result, CancellationToken cancellationToken)
    {
        await LoadChoicesAsync(cancellationToken);
        Message = Workflow.Policy.Message;
        Error = Workflow.Policy.Error;

        return result ?? Page();
    }
}

/// <summary>
/// The page model of the legal holds page when it was a Razor Page: artifacts (or single versions) that no retention policy deletes.
/// Placing a hold asks for nothing; lifting one asks for the administrator's password.
/// </summary>
/// <remarks>
/// Kept as a shim (no breaking change of the public API); <c>AdminRetentionController</c> serves <c>/admin/retention/holds</c>. Both
/// call <c>RetentionWorkflow</c>.
/// </remarks>
[Obsolete("The retention screens are served by AdminRetentionController; this page model has no page any more and will be removed in the next major version.")]
public class RetentionHoldsModel : RetentionPageModel
{
    private RetentionWorkflow? _workflow;

    private RetentionWorkflow Workflow => _workflow ??= new RetentionWorkflow(HttpContext, Url);

    /// <summary>Only the holds of this repository (query).</summary>
    [BindProperty(SupportsGet = true)]
    public string? Repository { get => Workflow.Holds.Repository; set => Workflow.Holds.Repository = value; }

    /// <summary>The repository of the new hold.</summary>
    [BindProperty]
    public string? HoldRepository { get => Workflow.Holds.HoldRepository; set => Workflow.Holds.HoldRepository = value; }

    /// <summary>The artifact name of the new hold.</summary>
    [BindProperty]
    public string? HoldName { get => Workflow.Holds.HoldName; set => Workflow.Holds.HoldName = value; }

    /// <summary>The version of the new hold; empty for every version.</summary>
    [BindProperty]
    public string? HoldVersion { get => Workflow.Holds.HoldVersion; set => Workflow.Holds.HoldVersion = value; }

    /// <summary>Why (a case number, a ticket).</summary>
    [BindProperty]
    public string? Reason { get => Workflow.Holds.Reason; set => Workflow.Holds.Reason = value; }

    /// <summary>The holds.</summary>
    public IReadOnlyList<RetentionHoldView> Holds => Workflow.Holds.Holds;

    /// <summary>Lists the holds.</summary>
    public async Task OnGetAsync(CancellationToken cancellationToken)
        => await Finish(await Workflow.GetHoldsAsync(cancellationToken), cancellationToken);

    /// <summary>Places a hold.</summary>
    public async Task<IActionResult> OnPostAddAsync(CancellationToken cancellationToken)
        => await Finish(await Workflow.AddHoldAsync(cancellationToken), cancellationToken);

    /// <summary>Lifts a hold.</summary>
    public async Task<IActionResult> OnPostRemoveAsync(int key, string? password, CancellationToken cancellationToken)
        => await Finish(await Workflow.RemoveHoldAsync(key, password, cancellationToken), cancellationToken);

    private async Task<IActionResult> Finish(IActionResult? result, CancellationToken cancellationToken)
    {
        await LoadChoicesAsync(cancellationToken);
        Message = Workflow.Holds.Message;
        Error = Workflow.Holds.Error;

        return result ?? Page();
    }
}
