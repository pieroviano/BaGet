using System.Threading;
using System.Threading.Tasks;
using BaGet.Identity.Application;
using BaGet.Identity.Mvc;
using BaGet.Identity.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Identity.Controllers;

/// <summary>
/// The JSON twin of the retention screens (<c>/api/ui/admin/retention</c>): the policies, one policy (save, dry run, run now, delete) and the legal
/// holds. <see cref="AdminRetentionController"/> calls it in process. Administrators only; creating, saving, running and deleting a policy and
/// lifting a hold ask the administrator's password in the body; every write is refused with 503 while the server is read-only.
/// </summary>
[Route("api/ui/admin/retention")]
public sealed class RetentionUiApiController : AccessUiApiControllerBase
{
    private RetentionUseCase Retention => UseCase<RetentionUseCase>();

    private string ReturnPage => Request.Path + Request.QueryString;

    // ---------------------------------------------------------------- policies

    /// <summary>Lists the policies and the form that creates one.</summary>
    [HttpGet(""), UiApi("/Admin/Retention")]
    public async Task<ActionResult<ScreenResult<RetentionViewModel>>> GetPolicies(CancellationToken cancellationToken = default)
        => Respond(await Retention.GetPoliciesAsync(User, ReturnPage, cancellationToken));

    /// <summary>Creates a policy and opens it.</summary>
    [HttpPost(""), UiApi("/Admin/Retention")]
    public async Task<ActionResult<ScreenResult<RetentionViewModel>>> Create([FromBody] RetentionPolicyBody? body, CancellationToken cancellationToken = default)
        => Respond(await Retention.CreateAsync(User, body, ReturnPage, cancellationToken));

    // ---------------------------------------------------------------- one policy

    /// <summary>Shows one policy, its last run and report.</summary>
    [HttpGet("{key:int}"), UiApi("/Admin/RetentionPolicy")]
    public async Task<ActionResult<ScreenResult<RetentionPolicyViewModel>>> GetPolicy(int key, CancellationToken cancellationToken = default)
        => Respond(await Retention.GetPolicyAsync(User, key, ReturnPage, cancellationToken));

    /// <summary>Saves the rules.</summary>
    [HttpPost("{key:int}/save"), UiApi("/Admin/RetentionPolicy", Handler = "Save")]
    public async Task<ActionResult<ScreenResult<RetentionPolicyViewModel>>> Save(int key, [FromBody] RetentionPolicyBody? body, CancellationToken cancellationToken = default)
        => Respond(await Retention.SaveAsync(User, key, body, cancellationToken));

    /// <summary>Starts a dry run: the report of what a run would delete.</summary>
    [HttpPost("{key:int}/dry-run"), UiApi("/Admin/RetentionPolicy", Handler = "DryRun")]
    public async Task<ActionResult<ScreenResult<RetentionPolicyViewModel>>> DryRun(int key, CancellationToken cancellationToken = default)
        => Respond(await Retention.DryRunAsync(User, key, cancellationToken));

    /// <summary>Starts a run that deletes.</summary>
    [HttpPost("{key:int}/run"), UiApi("/Admin/RetentionPolicy", Handler = "Run")]
    public async Task<ActionResult<ScreenResult<RetentionPolicyViewModel>>> Run(int key, [FromBody] PasswordForm? body, CancellationToken cancellationToken = default)
        => Respond(await Retention.RunAsync(User, key, body?.Password, cancellationToken));

    /// <summary>Deletes the policy (what it deleted stays deleted).</summary>
    [HttpPost("{key:int}/delete"), UiApi("/Admin/RetentionPolicy", Handler = "Delete")]
    public async Task<ActionResult<ScreenResult<RetentionPolicyViewModel>>> Delete(int key, [FromBody] PasswordForm? body, CancellationToken cancellationToken = default)
        => Respond(await Retention.DeleteAsync(User, key, body?.Password, cancellationToken));

    // ---------------------------------------------------------------- holds

    /// <summary>Lists the legal holds (of <c>?repository=</c> only, when given).</summary>
    [HttpGet("holds"), UiApi("/Admin/RetentionHolds")]
    public async Task<ActionResult<ScreenResult<RetentionHoldsViewModel>>> GetHolds([FromQuery] string? repository, CancellationToken cancellationToken = default)
        => Respond(await Retention.GetHoldsAsync(User, repository, ReturnPage, cancellationToken));

    /// <summary>Places a hold.</summary>
    [HttpPost("holds/add"), UiApi("/Admin/RetentionHolds", Handler = "Add")]
    public async Task<ActionResult<ScreenResult<RetentionHoldsViewModel>>> AddHold(
        [FromQuery] string? repository, [FromBody] RetentionHoldForm? form, CancellationToken cancellationToken = default)
        => Respond(await Retention.AddHoldAsync(User, repository, form, ReturnPage, cancellationToken));

    /// <summary>Lifts a hold.</summary>
    [HttpPost("holds/remove"), UiApi("/Admin/RetentionHolds", Handler = "Remove")]
    public async Task<ActionResult<ScreenResult<RetentionHoldsViewModel>>> RemoveHold(
        [FromQuery] string? repository, [FromBody] RetentionHoldRemoval? removal, CancellationToken cancellationToken = default)
        => Respond(await Retention.RemoveHoldAsync(User, repository, removal, cancellationToken));
}
