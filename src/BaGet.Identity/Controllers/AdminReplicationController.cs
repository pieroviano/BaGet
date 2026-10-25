using System.Threading;
using System.Threading.Tasks;
using BaGet.Identity.Mvc;
using BaGet.Identity.Pages.Admin;
using BaGet.Identity.Workflows;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Identity.Controllers;

/// <summary>
/// The replication screens: the replications (<c>/admin/replication</c>) and one replication (<c>/admin/replication/{key}</c>).
/// They replace the Razor Pages of the same names (same URLs, handlers and <c>asp-page</c> names); the logic is in
/// <see cref="ReplicationWorkflow"/>. Administrators only (404 for anyone else), every POST is refused with 503 while the read-only
/// gate is closed. The peer API key is write only: it is never filled back into a form.
/// </summary>
public sealed class AdminReplicationController : ReplicationControllerBase
{
    // ---------------------------------------------------------------- replications

    /// <summary>Lists the replications.</summary>
    [Route("admin/replication"), HttpGet, HttpHead, PageHandler, LegacyPage("/Admin/Replication")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var workflow = new ReplicationWorkflow(HttpContext, Url);

        return ShowReplications(workflow, await workflow.GetReplicationsAsync(cancellationToken));
    }

    /// <summary>Creates a replication and opens it.</summary>
    [Route("admin/replication"), HttpPost, PageHandler, LegacyPage("/Admin/Replication")]
    public async Task<IActionResult> Create([Bind(Prefix = "Form")] ReplicationForm? form, string? password, CancellationToken cancellationToken)
    {
        var workflow = new ReplicationWorkflow(HttpContext, Url);
        workflow.Replications.Form = form ?? new ReplicationForm();

        return ShowReplications(workflow, await workflow.CreateAsync(password, cancellationToken));
    }

    // ---------------------------------------------------------------- one replication

    /// <summary>Shows the replication.</summary>
    [Route("admin/replication/{key:int}"), HttpGet, HttpHead, PageHandler, LegacyPage("/Admin/ReplicationConfig")]
    public async Task<IActionResult> Config(int key, CancellationToken cancellationToken)
    {
        var workflow = StartConfig(key, null);

        return ShowConfig(workflow, await workflow.GetConfigAsync(cancellationToken));
    }

    /// <summary>Saves the settings.</summary>
    [Route("admin/replication/{key:int}"), HttpPost, PageHandler("Save"), LegacyPage("/Admin/ReplicationConfig")]
    public async Task<IActionResult> Save(int key, [Bind(Prefix = "Form")] ReplicationForm? form, string? password, CancellationToken cancellationToken)
    {
        var workflow = StartConfig(key, form);

        return ShowConfig(workflow, await workflow.SaveAsync(password, cancellationToken));
    }

    /// <summary>Tests the connection with the typed settings; saves nothing.</summary>
    [Route("admin/replication/{key:int}"), HttpPost, PageHandler("Test"), LegacyPage("/Admin/ReplicationConfig")]
    public async Task<IActionResult> Test(int key, [Bind(Prefix = "Form")] ReplicationForm? form, string? password, CancellationToken cancellationToken)
    {
        var workflow = StartConfig(key, form);

        return ShowConfig(workflow, await workflow.TestAsync(password, cancellationToken));
    }

    /// <summary>Starts a run.</summary>
    [Route("admin/replication/{key:int}"), HttpPost, PageHandler("Run"), LegacyPage("/Admin/ReplicationConfig")]
    public async Task<IActionResult> Run(int key, string? password, CancellationToken cancellationToken)
    {
        var workflow = StartConfig(key, null);

        return ShowConfig(workflow, await workflow.RunAsync(password, cancellationToken));
    }

    /// <summary>Starts a run that continues the last interrupted or failed one.</summary>
    [Route("admin/replication/{key:int}"), HttpPost, PageHandler("Resume"), LegacyPage("/Admin/ReplicationConfig")]
    public async Task<IActionResult> Resume(int key, string? password, CancellationToken cancellationToken)
    {
        var workflow = StartConfig(key, null);

        return ShowConfig(workflow, await workflow.ResumeAsync(password, cancellationToken));
    }

    /// <summary>Asks the running replication to stop; it ends as interrupted and can be resumed.</summary>
    [Route("admin/replication/{key:int}"), HttpPost, PageHandler("Stop"), LegacyPage("/Admin/ReplicationConfig")]
    public async Task<IActionResult> Stop(int key, CancellationToken cancellationToken)
    {
        var workflow = StartConfig(key, null);

        return ShowConfig(workflow, await workflow.StopAsync(cancellationToken));
    }

    /// <summary>Deletes the configuration with its log.</summary>
    [Route("admin/replication/{key:int}"), HttpPost, PageHandler("Delete"), LegacyPage("/Admin/ReplicationConfig")]
    public async Task<IActionResult> Delete(int key, string? password, CancellationToken cancellationToken)
    {
        var workflow = StartConfig(key, null);

        return ShowConfig(workflow, await workflow.DeleteAsync(password, cancellationToken));
    }

    /// <summary>A form posted to the screen's own URL, with no handler: the screen is rendered (the page had no default handler).</summary>
    [Route("admin/replication/{key:int}"), HttpPost, PageHandler, ImplicitPageHandler, LegacyPage("/Admin/ReplicationConfig")]
    public async Task<IActionResult> ConfigPost(int key, CancellationToken cancellationToken)
    {
        var workflow = StartConfig(key, null);

        return ShowConfig(workflow, await workflow.GetConfigAsync(cancellationToken));
    }

    // ---------------------------------------------------------------- helpers

    private ReplicationWorkflow StartConfig(int key, ReplicationForm? form)
    {
        var workflow = new ReplicationWorkflow(HttpContext, Url);
        workflow.Config.Key = key;
        if (form != null) workflow.Config.Form = form;

        return workflow;
    }

    private IActionResult ShowReplications(ReplicationWorkflow workflow, IActionResult? result)
    {
        Flash(workflow.Replications);

        return result ?? View("Index", workflow.Replications);
    }

    private IActionResult ShowConfig(ReplicationWorkflow workflow, IActionResult? result)
    {
        Flash(workflow.Config);

        return result ?? View("Config", workflow.Config);
    }
}
