using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.Workflows;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Identity.Pages.Admin;

/// <summary>
/// The page model of the replications page when it was a Razor Page: lists the replications and creates one (asks for the
/// administrator's password, audited with the access audit).
/// </summary>
/// <remarks>
/// Kept as a shim (no breaking change of the public API); <c>AdminReplicationController</c> serves <c>/admin/replication</c>. Both
/// call <c>ReplicationWorkflow</c>.
/// </remarks>
[Obsolete("The replication screens are served by AdminReplicationController; this page model has no page any more and will be removed in the next major version.")]
public class ReplicationModel : ReplicationPageModel
{
    private ReplicationWorkflow? _workflow;

    private ReplicationWorkflow Workflow => _workflow ??= new ReplicationWorkflow(HttpContext, Url);

    /// <summary>The new replication.</summary>
    [BindProperty]
    public ReplicationForm Form { get => Workflow.Replications.Form; set => Workflow.Replications.Form = value; }

    /// <summary>Every replication.</summary>
    public IReadOnlyList<ReplicationConfigView> Replications => Workflow.Replications.Replications;

    /// <summary>Lists the replications.</summary>
    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await Workflow.GetReplicationsAsync(cancellationToken);
        await LoadChoicesAsync(cancellationToken);
    }

    /// <summary>Creates a replication and opens it.</summary>
    public async Task<IActionResult> OnPostAsync(string? password, CancellationToken cancellationToken)
    {
        var result = await Workflow.CreateAsync(password, cancellationToken);
        await LoadChoicesAsync(cancellationToken);
        Message = Workflow.Replications.Message;
        Error = Workflow.Replications.Error;

        return result ?? Page();
    }
}

/// <summary>
/// The page model of one replication when it was a Razor Page: its settings (changeable), the connection test, run, resume, stop,
/// the log of its runs and the delete.
/// </summary>
/// <remarks>
/// Kept as a shim (no breaking change of the public API); <c>AdminReplicationController</c> serves
/// <c>/admin/replication/{key}</c>. Both call <c>ReplicationWorkflow</c>.
/// </remarks>
[Obsolete("The replication screens are served by AdminReplicationController; this page model has no page any more and will be removed in the next major version.")]
public class ReplicationConfigModel : ReplicationPageModel
{
    private ReplicationWorkflow? _workflow;

    private ReplicationWorkflow Workflow => _workflow ??= new ReplicationWorkflow(HttpContext, Url);

    /// <summary>The configuration's key (route).</summary>
    [BindProperty(SupportsGet = true)]
    public int Key { get => Workflow.Config.Key; set => Workflow.Config.Key = value; }

    /// <summary>The settings, as edited.</summary>
    [BindProperty]
    public ReplicationForm Form { get => Workflow.Config.Form; set => Workflow.Config.Form = value; }

    /// <summary>The configuration with the names of what it uses.</summary>
    public ReplicationConfigView? View => Workflow.Config.View;

    /// <summary>The log, newest first.</summary>
    public IReadOnlyList<ReplicationRun> Runs => Workflow.Config.Runs;

    /// <summary>What this process knows of the last job (a refusal that wrote no log entry shows here).</summary>
    public ReplicationJobStatus? Job => Workflow.Config.Job;

    /// <summary>The result of the last connection test on this request, or null.</summary>
    public ReplicationTestResult? Test => Workflow.Config.Test;

    /// <summary>How many items the local repository holds, or null when it could not be listed.</summary>
    public int? LocalItems => Workflow.Config.LocalItems;

    /// <summary>Whether a run is going here (the page refreshes itself).</summary>
    public bool Running => Workflow.Config.Running;

    /// <summary>The newest run that ended without finishing everything, which <b>Resume</b> continues; null when there is none.</summary>
    public ReplicationRun? Resumable => Workflow.Config.Resumable;

    /// <summary>Shows the configuration.</summary>
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
        => await Finish(await Workflow.GetConfigAsync(cancellationToken), cancellationToken);

    /// <summary>Saves the settings.</summary>
    public async Task<IActionResult> OnPostSaveAsync(string? password, CancellationToken cancellationToken)
        => await Finish(await Workflow.SaveAsync(password, cancellationToken), cancellationToken);

    /// <summary>Tests the connection with the typed settings; saves nothing.</summary>
    public async Task<IActionResult> OnPostTestAsync(string? password, CancellationToken cancellationToken)
        => await Finish(await Workflow.TestAsync(password, cancellationToken), cancellationToken);

    /// <summary>Starts a run.</summary>
    public async Task<IActionResult> OnPostRunAsync(string? password, CancellationToken cancellationToken)
        => await Finish(await Workflow.RunAsync(password, cancellationToken), cancellationToken);

    /// <summary>Starts a run that continues the last interrupted or failed one.</summary>
    public async Task<IActionResult> OnPostResumeAsync(string? password, CancellationToken cancellationToken)
        => await Finish(await Workflow.ResumeAsync(password, cancellationToken), cancellationToken);

    /// <summary>Asks the running replication to stop; it ends as interrupted and can be resumed.</summary>
    public async Task<IActionResult> OnPostStopAsync(CancellationToken cancellationToken)
        => await Finish(await Workflow.StopAsync(cancellationToken), cancellationToken);

    /// <summary>Deletes the configuration with its log.</summary>
    public async Task<IActionResult> OnPostDeleteAsync(string? password, CancellationToken cancellationToken)
        => await Finish(await Workflow.DeleteAsync(password, cancellationToken), cancellationToken);

    private async Task<IActionResult> Finish(IActionResult? result, CancellationToken cancellationToken)
    {
        await LoadChoicesAsync(cancellationToken);
        Message = Workflow.Config.Message;
        Error = Workflow.Config.Error;

        return result ?? Page();
    }
}
