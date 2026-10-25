using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.Mvc;
using BaGet.Identity.Pages.Admin;
using BaGet.Identity.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace BaGet.Identity.Workflows;

/// <summary>
/// The replication screens: the replications and the form that creates one, and one replication (save, test the connection, run,
/// resume, stop, delete, its log). Everything but stopping asks for the administrator's password. Shared by
/// <c>AdminReplicationController</c>.
/// </summary>
internal sealed class ReplicationWorkflow : PolicyWorkflow
{
    public ReplicationWorkflow(HttpContext httpContext, IUrlHelper url) : base(httpContext, url)
    {
    }

    /// <summary>The replications screen.</summary>
    public ReplicationViewModel Replications { get; } = new();

    /// <summary>One replication screen; the caller sets <c>Key</c> first.</summary>
    public ReplicationConfigViewModel Config { get; } = new();

    private IReplicationAdministration Replication => Services.GetRequiredService<IReplicationAdministration>();

    private ReplicationJobs Jobs => Services.GetRequiredService<ReplicationJobs>();

    // ------------------------------------------------------------------ replications

    /// <summary>Lists the replications.</summary>
    public async Task<IActionResult?> GetReplicationsAsync(CancellationToken cancellationToken)
    {
        await LoadReplicationsAsync(cancellationToken);

        return await ShowAsync(Replications);
    }

    /// <summary>Creates a replication and opens it.</summary>
    public async Task<IActionResult?> CreateAsync(string? password, CancellationToken cancellationToken)
    {
        var current = await UserManager.GetUserAsync(User);
        if (current == null) return new ChallengeResult();

        if (!await IsConfirmedAsync(current, password))
        {
            Replications.Error = NotConfirmed;
            await LoadReplicationsAsync(cancellationToken);

            return await ShowAsync(Replications);
        }

        try
        {
            var config = await Replication.SaveAsync(Replications.Form.ToInput(0), Actor, cancellationToken);
            Replications.Message = $"Replication {config.Name} was created.";

            return RedirectToScreen("/Admin/ReplicationConfig", new { key = config.Key });
        }
        catch (AccessModelException e)
        {
            Replications.Error = e.Message;
            await LoadReplicationsAsync(cancellationToken);

            return await ShowAsync(Replications);
        }
    }

    private async Task LoadReplicationsAsync(CancellationToken cancellationToken)
    {
        Replications.Replications = await Replication.ListAsync(cancellationToken);
        Replications.Repositories = await AccessSupport.ReplicationRepositoriesAsync(Services, cancellationToken);
    }

    // ------------------------------------------------------------------ one replication

    /// <summary>Shows the configuration.</summary>
    public async Task<IActionResult?> GetConfigAsync(CancellationToken cancellationToken)
    {
        if (!await LoadConfigAsync(cancellationToken)) return NotFound();

        Config.Form = ReplicationForm.From(Config.View!);

        return await ShowAsync(Config);
    }

    /// <summary>Saves the settings.</summary>
    public Task<IActionResult> SaveAsync(string? password, CancellationToken cancellationToken)
        => ActAsync(Config, password, confirm: true, async () =>
        {
            var config = await Replication.SaveAsync(Config.Form.ToInput(Config.Key), Actor, cancellationToken);

            return $"Replication {config.Name} was saved.";
        }, BackToConfig);

    /// <summary>Tests the connection with the typed settings; saves nothing.</summary>
    public async Task<IActionResult?> TestAsync(string? password, CancellationToken cancellationToken)
    {
        if (!await LoadConfigAsync(cancellationToken)) return NotFound();

        var current = await UserManager.GetUserAsync(User);
        if (current == null) return new ChallengeResult();

        if (!await IsConfirmedAsync(current, password))
        {
            Config.Error = "The confirmation is incorrect, so nothing was tested.";

            return await ShowAsync(Config);
        }

        Config.Test = await Replication.TestAsync(Config.Form.ToInput(Config.Key), cancellationToken);

        return await ShowAsync(Config);
    }

    /// <summary>Starts a run.</summary>
    public async Task<IActionResult> RunAsync(string? password, CancellationToken cancellationToken)
    {
        var view = await Replication.GetAsync(Config.Key, cancellationToken);
        if (view == null) return NotFound();

        return await ActAsync(Config, password, confirm: true, () => Start(view.Config, "manual", null), BackToConfig);
    }

    /// <summary>Starts a run that continues the last interrupted or failed one.</summary>
    public async Task<IActionResult> ResumeAsync(string? password, CancellationToken cancellationToken)
    {
        var view = await Replication.GetAsync(Config.Key, cancellationToken);
        if (view == null) return NotFound();

        var runs = await Replication.ListRunsAsync(Config.Key, 1, cancellationToken);
        var last = runs.FirstOrDefault();
        var resume = last != null && last.Status is ReplicationRunStatus.Failed or ReplicationRunStatus.Interrupted ? last.Key : (int?)null;

        return await ActAsync(Config, password, confirm: true, () => Start(view.Config, "resume", resume), BackToConfig);
    }

    private Task<string> Start(ReplicationConfig config, string trigger, int? resumedFrom)
    {
        if (!config.Enabled) throw new ReplicationException($"The replication '{config.Name}' is disabled: enable it first.");
        if (!Jobs.TryStart(Config.Key, trigger, Actor, resumedFrom, out var refusal)) throw new ReplicationException(refusal ?? "The run was not started.");

        return Task.FromResult("The replication started; the log below shows its progress.");
    }

    /// <summary>Asks the running replication to stop; it ends as interrupted and can be resumed.</summary>
    public async Task<IActionResult> StopAsync(CancellationToken cancellationToken)
    {
        if (await Replication.GetAsync(Config.Key, cancellationToken) == null) return NotFound();

        if (Jobs.Cancel(Config.Key)) Config.Message = "The replication was asked to stop; it ends as interrupted.";
        else Config.Error = "No run of this replication is going on this server.";

        return BackToConfig();
    }

    /// <summary>Deletes the configuration with its log.</summary>
    public async Task<IActionResult> DeleteAsync(string? password, CancellationToken cancellationToken)
    {
        if (await Replication.GetAsync(Config.Key, cancellationToken) == null) return NotFound();

        return await ActAsync(Config, password, confirm: true, async () =>
        {
            await Replication.DeleteAsync(Config.Key, Actor, cancellationToken);

            return "The replication was deleted. What it copied stays where it is.";
        }, () => RedirectToScreen("/Admin/Replication"));
    }

    private IActionResult BackToConfig() => RedirectToScreen("/Admin/ReplicationConfig", new { key = Config.Key });

    private async Task<bool> LoadConfigAsync(CancellationToken cancellationToken)
    {
        Config.View = await Replication.GetAsync(Config.Key, cancellationToken);
        if (Config.View == null) return false;

        Config.Runs = await Replication.ListRunsAsync(Config.Key, 25, cancellationToken);
        Config.Job = Jobs.Get(Config.Key);
        Config.Repositories = await AccessSupport.ReplicationRepositoriesAsync(Services, cancellationToken);
        if (Config.View.Repository != null)
        {
            try
            {
                Config.LocalItems = (await Services.GetRequiredService<IReplicationInventory>().GetAsync(Config.View.Repository, summary: true, cancellationToken))?.Count;
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                Config.LocalItems = null;
            }
        }

        return true;
    }
}
