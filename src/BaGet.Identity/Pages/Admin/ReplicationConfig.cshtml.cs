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
/// One replication: its settings (changeable), the connection test, <b>Run now</b>, <b>Resume</b> (a new run that continues what an
/// interrupted or failed one left: what the target holds is skipped), <b>Stop</b>, the log of its runs (status, counts, the first
/// failures and conflicts) and the delete. Saving, testing, running, resuming and deleting ask for the administrator's password; stopping
/// does not. Runs happen in the background and the page refreshes itself while one is going. Administrators only.
/// </summary>
public class ReplicationConfigModel : ReplicationPageModel
{
    /// <summary>The configuration's key (route).</summary>
    [BindProperty(SupportsGet = true)]
    public int Key { get; set; }

    /// <summary>The settings, as edited.</summary>
    [BindProperty]
    public ReplicationForm Form { get; set; } = new();

    /// <summary>The configuration with the names of what it uses.</summary>
    public ReplicationConfigView? View { get; private set; }

    /// <summary>The log, newest first.</summary>
    public IReadOnlyList<ReplicationRun> Runs { get; private set; } = Array.Empty<ReplicationRun>();

    /// <summary>What this process knows of the last job (a refusal that wrote no log entry shows here).</summary>
    public ReplicationJobStatus? Job { get; private set; }

    /// <summary>The result of the last connection test on this request, or null.</summary>
    public ReplicationTestResult? Test { get; private set; }

    /// <summary>How many items the local repository holds, or null when it could not be listed.</summary>
    public int? LocalItems { get; private set; }

    /// <summary>Whether a run is going here (the page refreshes itself).</summary>
    public bool Running => Job?.Running == true;

    /// <summary>The newest run that ended without finishing everything, which <b>Resume</b> continues; null when there is none.</summary>
    public ReplicationRun? Resumable
    {
        get
        {
            var last = Runs.FirstOrDefault();

            return last != null && last.Status is ReplicationRunStatus.Failed or ReplicationRunStatus.Interrupted ? last : null;
        }
    }

    /// <summary>Shows the configuration.</summary>
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!await LoadAsync(cancellationToken)) return NotFound();

        Form = ReplicationForm.From(View!);

        return Page();
    }

    /// <summary>Saves the settings.</summary>
    public Task<IActionResult> OnPostSaveAsync(string? password, CancellationToken cancellationToken)
        => ActAsync(password, confirm: true, async () =>
        {
            var config = await Replication.SaveAsync(Form.ToInput(Key), Actor, cancellationToken);

            return $"Replication {config.Name} was saved.";
        }, () => RedirectToPage(new { key = Key }));

    /// <summary>Tests the connection with the typed settings; saves nothing.</summary>
    public async Task<IActionResult> OnPostTestAsync(string? password, CancellationToken cancellationToken)
    {
        if (!await LoadAsync(cancellationToken)) return NotFound();

        var current = await UserManager.GetUserAsync(User);
        if (current == null) return Challenge();

        if (!await IsConfirmedAsync(current, password))
        {
            Error = "The confirmation is incorrect, so nothing was tested.";

            return Page();
        }

        Test = await Replication.TestAsync(Form.ToInput(Key), cancellationToken);

        return Page();
    }

    /// <summary>Starts a run.</summary>
    public async Task<IActionResult> OnPostRunAsync(string? password, CancellationToken cancellationToken)
    {
        if (await Replication.GetAsync(Key, cancellationToken) == null) return NotFound();

        return await ActAsync(password, confirm: true, () => Start("manual", null), () => RedirectToPage(new { key = Key }));
    }

    /// <summary>Starts a run that continues the last interrupted or failed one.</summary>
    public async Task<IActionResult> OnPostResumeAsync(string? password, CancellationToken cancellationToken)
    {
        if (await Replication.GetAsync(Key, cancellationToken) == null) return NotFound();

        var runs = await Replication.ListRunsAsync(Key, 1, cancellationToken);
        var last = runs.FirstOrDefault();
        var resume = last != null && last.Status is ReplicationRunStatus.Failed or ReplicationRunStatus.Interrupted ? last.Key : (int?)null;

        return await ActAsync(password, confirm: true, () => Start("resume", resume), () => RedirectToPage(new { key = Key }));
    }

    private Task<string> Start(string trigger, int? resumedFrom)
    {
        if (!Jobs.TryStart(Key, trigger, Actor, resumedFrom, out var refusal)) throw new ReplicationException(refusal ?? "The run was not started.");

        return Task.FromResult("The replication started; the log below shows its progress.");
    }

    /// <summary>Asks the running replication to stop; it ends as interrupted and can be resumed.</summary>
    public async Task<IActionResult> OnPostStopAsync(CancellationToken cancellationToken)
    {
        if (await Replication.GetAsync(Key, cancellationToken) == null) return NotFound();

        if (Jobs.Cancel(Key)) Message = "The replication was asked to stop; it ends as interrupted.";
        else Error = "No run of this replication is going on this server.";

        return RedirectToPage(new { key = Key });
    }

    /// <summary>Deletes the configuration with its log.</summary>
    public async Task<IActionResult> OnPostDeleteAsync(string? password, CancellationToken cancellationToken)
    {
        if (await Replication.GetAsync(Key, cancellationToken) == null) return NotFound();

        return await ActAsync(password, confirm: true, async () =>
        {
            await Replication.DeleteAsync(Key, Actor, cancellationToken);

            return "The replication was deleted. What it copied stays where it is.";
        }, () => RedirectToPage("/Admin/Replication"));
    }

    private async Task<bool> LoadAsync(CancellationToken cancellationToken)
    {
        View = await Replication.GetAsync(Key, cancellationToken);
        if (View == null) return false;

        Runs = await Replication.ListRunsAsync(Key, 25, cancellationToken);
        Job = Jobs.Get(Key);
        await LoadChoicesAsync(cancellationToken);
        if (View.Repository != null)
        {
            try
            {
                LocalItems = (await HttpContext.RequestServices.GetRequiredService<IReplicationInventory>().GetAsync(View.Repository, summary: true, cancellationToken))?.Count;
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                LocalItems = null;
            }
        }

        return true;
    }
}
