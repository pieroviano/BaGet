using System.Threading;
using System.Threading.Tasks;
using BaGet.Identity.Mvc;
using BaGet.Identity.Workflows;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Identity.Controllers;

/// <summary>
/// The administrator's operational screens: maintenance operations (<c>/admin/operations</c>), the validation queue
/// (<c>/admin/validation</c>), abuse reports (<c>/admin/abuse</c>), the cluster status (<c>/admin/cluster</c>), the package formats
/// (<c>/admin/formats</c>) and the notice shown where there is no access model (<c>/admin/access-unavailable</c>). They replace the
/// Razor Pages of the same names (same URLs, handlers and <c>asp-page</c> names); the logic is in
/// <see cref="OperationsWorkflow"/>. Every write is refused with 503 while the read-only gate is closed.
/// </summary>
public sealed class AdminOperationsController : AdminControllerBase
{
    // ---------------------------------------------------------------- Operations

    /// <summary>Lists the operations and their status.</summary>
    [Route("admin/operations"), HttpGet, HttpHead, PageHandler, LegacyPage("/Admin/Operations")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var workflow = new OperationsWorkflow(HttpContext, Url);
        await workflow.GetOperationsAsync(cancellationToken);
        Flash(workflow.Operations);

        return View("Index", workflow.Operations);
    }

    /// <summary>Starts an operation on a repository.</summary>
    [Route("admin/operations"), HttpPost, PageHandler, LegacyPage("/Admin/Operations")]
    public async Task<IActionResult> Start(string? operation, string? repository, string? password, CancellationToken cancellationToken)
    {
        var workflow = new OperationsWorkflow(HttpContext, Url);
        var result = await workflow.StartAsync(operation, repository, password, cancellationToken);
        Flash(workflow.Operations);

        return result ?? View("Index", workflow.Operations);
    }

    // ---------------------------------------------------------------- Validation

    /// <summary>Lists the pending and quarantined submissions.</summary>
    [Route("admin/validation"), HttpGet, HttpHead, PageHandler, LegacyPage("/Admin/Validation")]
    public async Task<IActionResult> Validation(CancellationToken cancellationToken)
    {
        var workflow = new OperationsWorkflow(HttpContext, Url);
        await workflow.GetValidationAsync(cancellationToken);
        Flash(workflow.Validation);

        return View("Validation", workflow.Validation);
    }

    /// <summary>Validates the submission again.</summary>
    [Route("admin/validation"), HttpPost, PageHandler("Retry"), LegacyPage("/Admin/Validation")]
    public async Task<IActionResult> Retry(string? submissionId, CancellationToken cancellationToken)
    {
        var workflow = new OperationsWorkflow(HttpContext, Url);

        return AfterValidation(workflow, await workflow.RetryAsync(submissionId, cancellationToken));
    }

    /// <summary>Indexes the quarantined package although validation failed.</summary>
    [Route("admin/validation"), HttpPost, PageHandler("Release"), LegacyPage("/Admin/Validation")]
    public async Task<IActionResult> Release(string? submissionId, string? password, CancellationToken cancellationToken)
    {
        var workflow = new OperationsWorkflow(HttpContext, Url);

        return AfterValidation(workflow, await workflow.ReleaseAsync(submissionId, password, cancellationToken));
    }

    /// <summary>Deletes the quarantined package.</summary>
    [Route("admin/validation"), HttpPost, PageHandler("Reject"), LegacyPage("/Admin/Validation")]
    public async Task<IActionResult> Reject(string? submissionId, string? password, CancellationToken cancellationToken)
    {
        var workflow = new OperationsWorkflow(HttpContext, Url);

        return AfterValidation(workflow, await workflow.RejectAsync(submissionId, password, cancellationToken));
    }

    private IActionResult AfterValidation(OperationsWorkflow workflow, IActionResult? result)
    {
        Flash(workflow.Validation);

        return result ?? View("Validation", workflow.Validation);
    }

    // ---------------------------------------------------------------- Abuse

    /// <summary>Lists the abuse reports (the unresolved ones unless <c>all</c>).</summary>
    [Route("admin/abuse"), HttpGet, HttpHead, PageHandler, LegacyPage("/Admin/Abuse")]
    public async Task<IActionResult> Abuse(bool all, CancellationToken cancellationToken)
    {
        var workflow = new OperationsWorkflow(HttpContext, Url);
        workflow.Abuse.All = all;
        await workflow.GetAbuseAsync(cancellationToken);
        Flash(workflow.Abuse);

        return View("Abuse", workflow.Abuse);
    }

    /// <summary>Marks a report resolved.</summary>
    [Route("admin/abuse"), HttpPost, PageHandler("Resolve"), LegacyPage("/Admin/Abuse")]
    public async Task<IActionResult> Resolve(int key, bool all, CancellationToken cancellationToken)
    {
        var workflow = new OperationsWorkflow(HttpContext, Url);
        workflow.Abuse.All = all;

        return AfterAbuse(workflow, await workflow.ResolveAsync(key, cancellationToken));
    }

    /// <summary>Reopens a resolved report.</summary>
    [Route("admin/abuse"), HttpPost, PageHandler("Reopen"), LegacyPage("/Admin/Abuse")]
    public async Task<IActionResult> Reopen(int key, bool all, CancellationToken cancellationToken)
    {
        var workflow = new OperationsWorkflow(HttpContext, Url);
        workflow.Abuse.All = all;

        return AfterAbuse(workflow, await workflow.ReopenAsync(key, cancellationToken));
    }

    private IActionResult AfterAbuse(OperationsWorkflow workflow, IActionResult? result)
    {
        Flash(workflow.Abuse);

        return result ?? View("Abuse", workflow.Abuse);
    }

    // ---------------------------------------------------------------- Cluster, Formats, AccessUnavailable

    /// <summary>The cluster status; <c>?refresh=true</c> runs the health checks again.</summary>
    [Route("admin/cluster"), HttpGet, HttpHead, PageHandler, LegacyPage("/Admin/Cluster")]
    public async Task<IActionResult> Cluster(bool refresh, CancellationToken cancellationToken)
    {
        var workflow = new OperationsWorkflow(HttpContext, Url);
        await workflow.GetClusterAsync(refresh, cancellationToken);

        return View("Cluster", workflow.Cluster);
    }

    /// <summary>The package formats this server can host.</summary>
    [Route("admin/formats"), HttpGet, HttpHead, PageHandler, LegacyPage("/Admin/Formats")]
    public async Task<IActionResult> Formats(CancellationToken cancellationToken)
    {
        var workflow = new OperationsWorkflow(HttpContext, Url);
        await workflow.GetFormatsAsync(cancellationToken);

        return View("Formats", workflow.Formats);
    }

    /// <summary>
    /// Where the access screens send an administrator when the configured database has no access model (Azure Table): says so and
    /// explains what applies instead. The page had no handler method at all, hence no <see cref="PageHandlerAttribute"/>.
    /// </summary>
    [Route("admin/access-unavailable"), HttpGet, HttpHead, LegacyPage("/Admin/AccessUnavailable")]
    public IActionResult AccessUnavailable() => View("AccessUnavailable");
}
