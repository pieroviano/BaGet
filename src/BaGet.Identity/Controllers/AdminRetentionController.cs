using System.Threading;
using System.Threading.Tasks;
using BaGet.Identity.Mvc;
using BaGet.Identity.Pages.Admin;
using BaGet.Identity.Workflows;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Identity.Controllers;

/// <summary>
/// The retention screens: the policies (<c>/admin/retention</c>), one policy (<c>/admin/retention/{key}</c>) and the legal holds
/// (<c>/admin/retention/holds</c>). They replace the Razor Pages of the same names (same URLs, handlers and <c>asp-page</c> names);
/// the logic is in <see cref="RetentionWorkflow"/>. Administrators only (404 for anyone else), every POST is refused with 503 while
/// the read-only gate is closed.
/// </summary>
public sealed class AdminRetentionController : RetentionControllerBase
{
    // ---------------------------------------------------------------- policies

    /// <summary>Lists the policies.</summary>
    [Route("admin/retention"), HttpGet, HttpHead, PageHandler, LegacyPage("/Admin/Retention")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var workflow = new RetentionWorkflow(HttpContext, Url);

        return ShowPolicies(workflow, await workflow.GetPoliciesAsync(cancellationToken));
    }

    /// <summary>Creates a policy and opens it.</summary>
    [Route("admin/retention"), HttpPost, PageHandler, LegacyPage("/Admin/Retention")]
    public async Task<IActionResult> Create([Bind(Prefix = "Form")] RetentionPolicyForm? form, string? password, CancellationToken cancellationToken)
    {
        var workflow = new RetentionWorkflow(HttpContext, Url);
        workflow.Policies.Form = form ?? new RetentionPolicyForm();

        return ShowPolicies(workflow, await workflow.CreateAsync(password, cancellationToken));
    }

    // ---------------------------------------------------------------- one policy

    /// <summary>Shows the policy.</summary>
    [Route("admin/retention/{key:int}"), HttpGet, HttpHead, PageHandler, LegacyPage("/Admin/RetentionPolicy")]
    public async Task<IActionResult> Policy(int key, CancellationToken cancellationToken)
    {
        var workflow = StartPolicy(key, null);

        return ShowPolicy(workflow, await workflow.GetPolicyAsync(cancellationToken));
    }

    /// <summary>Saves the rules.</summary>
    [Route("admin/retention/{key:int}"), HttpPost, PageHandler("Save"), LegacyPage("/Admin/RetentionPolicy")]
    public async Task<IActionResult> Save(int key, [Bind(Prefix = "Form")] RetentionPolicyForm? form, string? password, CancellationToken cancellationToken)
    {
        var workflow = StartPolicy(key, form);

        return ShowPolicy(workflow, await workflow.SaveAsync(password, cancellationToken));
    }

    /// <summary>Starts a dry run: the report of what a run would delete.</summary>
    [Route("admin/retention/{key:int}"), HttpPost, PageHandler("DryRun"), LegacyPage("/Admin/RetentionPolicy")]
    public async Task<IActionResult> DryRun(int key, CancellationToken cancellationToken)
    {
        var workflow = StartPolicy(key, null);

        return ShowPolicy(workflow, await workflow.DryRunAsync(cancellationToken));
    }

    /// <summary>Starts a run that deletes.</summary>
    [Route("admin/retention/{key:int}"), HttpPost, PageHandler("Run"), LegacyPage("/Admin/RetentionPolicy")]
    public async Task<IActionResult> Run(int key, string? password, CancellationToken cancellationToken)
    {
        var workflow = StartPolicy(key, null);

        return ShowPolicy(workflow, await workflow.RunAsync(password, cancellationToken));
    }

    /// <summary>Deletes the policy (what it deleted stays deleted).</summary>
    [Route("admin/retention/{key:int}"), HttpPost, PageHandler("Delete"), LegacyPage("/Admin/RetentionPolicy")]
    public async Task<IActionResult> Delete(int key, string? password, CancellationToken cancellationToken)
    {
        var workflow = StartPolicy(key, null);

        return ShowPolicy(workflow, await workflow.DeleteAsync(password, cancellationToken));
    }

    /// <summary>A form posted to the screen's own URL, with no handler: the screen is rendered (the page had no default handler).</summary>
    [Route("admin/retention/{key:int}"), HttpPost, PageHandler, ImplicitPageHandler, LegacyPage("/Admin/RetentionPolicy")]
    public async Task<IActionResult> PolicyPost(int key, CancellationToken cancellationToken)
    {
        var workflow = StartPolicy(key, null);

        return ShowPolicy(workflow, await workflow.GetPolicyAsync(cancellationToken));
    }

    // ---------------------------------------------------------------- holds

    /// <summary>Lists the holds.</summary>
    [Route("admin/retention/holds"), HttpGet, HttpHead, PageHandler, LegacyPage("/Admin/RetentionHolds")]
    public async Task<IActionResult> Holds([FromQuery] string? repository, CancellationToken cancellationToken)
    {
        var workflow = StartHolds(repository);

        return ShowHolds(workflow, await workflow.GetHoldsAsync(cancellationToken));
    }

    /// <summary>Places a hold.</summary>
    [Route("admin/retention/holds"), HttpPost, PageHandler("Add"), LegacyPage("/Admin/RetentionHolds")]
    public async Task<IActionResult> AddHold(
        [FromQuery] string? repository, string? holdRepository, string? holdName, string? holdVersion, string? reason, CancellationToken cancellationToken)
    {
        var workflow = StartHolds(repository);
        workflow.Holds.HoldRepository = holdRepository;
        workflow.Holds.HoldName = holdName;
        workflow.Holds.HoldVersion = holdVersion;
        workflow.Holds.Reason = reason;

        return ShowHolds(workflow, await workflow.AddHoldAsync(cancellationToken));
    }

    /// <summary>Lifts a hold.</summary>
    [Route("admin/retention/holds"), HttpPost, PageHandler("Remove"), LegacyPage("/Admin/RetentionHolds")]
    public async Task<IActionResult> RemoveHold([FromQuery] string? repository, int key, string? password, CancellationToken cancellationToken)
    {
        var workflow = StartHolds(repository);

        return ShowHolds(workflow, await workflow.RemoveHoldAsync(key, password, cancellationToken));
    }

    /// <summary>A form posted to the screen's own URL, with no handler: the screen is rendered (the page had no default handler).</summary>
    [Route("admin/retention/holds"), HttpPost, PageHandler, ImplicitPageHandler, LegacyPage("/Admin/RetentionHolds")]
    public async Task<IActionResult> HoldsPost([FromQuery] string? repository, CancellationToken cancellationToken)
    {
        var workflow = StartHolds(repository);

        return ShowHolds(workflow, await workflow.GetHoldsAsync(cancellationToken));
    }

    // ---------------------------------------------------------------- helpers

    private RetentionWorkflow StartPolicy(int key, RetentionPolicyForm? form)
    {
        var workflow = new RetentionWorkflow(HttpContext, Url);
        workflow.Policy.Key = key;
        if (form != null) workflow.Policy.Form = form;

        return workflow;
    }

    private RetentionWorkflow StartHolds(string? repository)
    {
        var workflow = new RetentionWorkflow(HttpContext, Url);
        workflow.Holds.Repository = repository;

        return workflow;
    }

    private IActionResult ShowPolicies(RetentionWorkflow workflow, IActionResult? result)
    {
        Flash(workflow.Policies);

        return result ?? View("Index", workflow.Policies);
    }

    private IActionResult ShowPolicy(RetentionWorkflow workflow, IActionResult? result)
    {
        Flash(workflow.Policy);

        return result ?? View("Policy", workflow.Policy);
    }

    private IActionResult ShowHolds(RetentionWorkflow workflow, IActionResult? result)
    {
        Flash(workflow.Holds);

        return result ?? View("Holds", workflow.Holds);
    }
}
