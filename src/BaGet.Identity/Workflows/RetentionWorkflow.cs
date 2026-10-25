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
/// The retention screens: the policies and the form that creates one, one policy (save, dry run, run now, delete) and the legal
/// holds. Creating, saving, running and deleting a policy and lifting a hold ask for the administrator's password; a dry run and
/// placing a hold do not. Shared by <c>AdminRetentionController</c> and the obsolete <c>RetentionModel</c>,
/// <c>RetentionPolicyModel</c> and <c>RetentionHoldsModel</c>.
/// </summary>
internal sealed class RetentionWorkflow : PolicyWorkflow
{
    public RetentionWorkflow(HttpContext httpContext, IUrlHelper url) : base(httpContext, url)
    {
    }

    /// <summary>The policies screen.</summary>
    public RetentionViewModel Policies { get; } = new();

    /// <summary>One policy screen; the caller sets <c>Key</c> first.</summary>
    public RetentionPolicyViewModel Policy { get; } = new();

    /// <summary>The holds screen; the caller sets <c>Repository</c> first.</summary>
    public RetentionHoldsViewModel Holds { get; } = new();

    private IRetentionAdministration Retention => Services.GetRequiredService<IRetentionAdministration>();

    private RetentionJobs Jobs => Services.GetRequiredService<RetentionJobs>();

    // ------------------------------------------------------------------ policies

    /// <summary>Lists the policies.</summary>
    public async Task<IActionResult?> GetPoliciesAsync(CancellationToken cancellationToken)
    {
        await LoadPoliciesAsync(cancellationToken);

        return await ShowAsync(Policies);
    }

    /// <summary>Creates a policy and opens it.</summary>
    public async Task<IActionResult?> CreateAsync(string? password, CancellationToken cancellationToken)
    {
        var current = await UserManager.GetUserAsync(User);
        if (current == null) return new ChallengeResult();

        if (!await IsConfirmedAsync(current, password))
        {
            Policies.Error = NotConfirmed;
            await LoadPoliciesAsync(cancellationToken);

            return await ShowAsync(Policies);
        }

        try
        {
            var policy = await Retention.SavePolicyAsync(Policies.Form.ToInput(0), Actor, cancellationToken);
            Policies.Message = $"Policy {policy.Name} was created.";

            return RedirectToScreen("/Admin/RetentionPolicy", new { key = policy.Key });
        }
        catch (AccessModelException e)
        {
            Policies.Error = e.Message;
            await LoadPoliciesAsync(cancellationToken);

            return await ShowAsync(Policies);
        }
    }

    private async Task LoadPoliciesAsync(CancellationToken cancellationToken)
    {
        Policies.Policies = await Retention.ListPoliciesAsync(cancellationToken);
        Policies.Repositories = await AccessSupport.RetentionRepositoriesAsync(Services, cancellationToken);
        Policies.ProjectNames = await AccessSupport.ProjectNamesAsync(Services, cancellationToken);
    }

    // ------------------------------------------------------------------ one policy

    /// <summary>Shows the policy.</summary>
    public async Task<IActionResult?> GetPolicyAsync(CancellationToken cancellationToken)
    {
        if (!await LoadPolicyAsync(cancellationToken)) return NotFound();

        Policy.Form = RetentionPolicyForm.From(Policy.View!);

        return await ShowAsync(Policy);
    }

    /// <summary>Saves the rules.</summary>
    public Task<IActionResult> SaveAsync(string? password, CancellationToken cancellationToken)
        => ActAsync(Policy, password, confirm: true, async () =>
        {
            var policy = await Retention.SavePolicyAsync(Policy.Form.ToInput(Policy.Key), Actor, cancellationToken);

            return $"Policy {policy.Name} was saved.";
        }, BackToPolicy);

    /// <summary>Starts a dry run: the report of what a run would delete.</summary>
    public async Task<IActionResult> DryRunAsync(CancellationToken cancellationToken)
    {
        if (await Retention.GetPolicyAsync(Policy.Key, cancellationToken) == null) return NotFound();

        if (!Jobs.TryStart(Policy.Key, dryRun: true, Actor, out var refusal)) Policy.Error = refusal;
        else Policy.Message = "The dry run started; nothing is deleted.";

        return BackToPolicy();
    }

    /// <summary>Starts a run that deletes.</summary>
    public async Task<IActionResult> RunAsync(string? password, CancellationToken cancellationToken)
    {
        var view = await Retention.GetPolicyAsync(Policy.Key, cancellationToken);
        if (view == null) return NotFound();

        return await ActAsync(Policy, password, confirm: true, () =>
        {
            if (!view.Policy.Enabled) throw new RetentionException("The policy is disabled: enable it first (a dry run works either way).");
            if (!Jobs.TryStart(Policy.Key, dryRun: false, Actor, out var refusal)) throw new RetentionException(refusal ?? "The run was not started.");

            return Task.FromResult($"{view.Policy.Name} started; versions it selects are deleted.");
        }, BackToPolicy);
    }

    /// <summary>Deletes the policy (what it deleted stays deleted).</summary>
    public async Task<IActionResult> DeleteAsync(string? password, CancellationToken cancellationToken)
    {
        if (await Retention.GetPolicyAsync(Policy.Key, cancellationToken) == null) return NotFound();

        return await ActAsync(Policy, password, confirm: true, async () =>
        {
            await Retention.DeletePolicyAsync(Policy.Key, Actor, cancellationToken);

            return "The policy was deleted.";
        }, () => RedirectToScreen("/Admin/Retention"));
    }

    private IActionResult BackToPolicy() => RedirectToScreen("/Admin/RetentionPolicy", new { key = Policy.Key });

    private async Task<bool> LoadPolicyAsync(CancellationToken cancellationToken)
    {
        Policy.View = await Retention.GetPolicyAsync(Policy.Key, cancellationToken);
        if (Policy.View == null) return false;

        Policy.Status = Jobs.Get(Policy.Key);
        Policy.Repositories = await AccessSupport.RetentionRepositoriesAsync(Services, cancellationToken);
        Policy.ProjectNames = await AccessSupport.ProjectNamesAsync(Services, cancellationToken);

        return true;
    }

    // ------------------------------------------------------------------ holds

    /// <summary>Lists the holds.</summary>
    public async Task<IActionResult?> GetHoldsAsync(CancellationToken cancellationToken)
    {
        await LoadHoldsAsync(cancellationToken);

        return await ShowAsync(Holds);
    }

    /// <summary>Places a hold.</summary>
    public async Task<IActionResult?> AddHoldAsync(CancellationToken cancellationToken)
    {
        try
        {
            var hold = await Retention.AddHoldAsync(Holds.HoldRepository ?? string.Empty, Holds.HoldName ?? string.Empty, Holds.HoldVersion, Holds.Reason, Actor, cancellationToken);
            Holds.Message = $"{hold.Name}{(string.IsNullOrEmpty(hold.Version) ? string.Empty : " " + hold.Version)} is on legal hold.";

            return RedirectToScreen("/Admin/RetentionHolds", new { repository = Holds.Repository });
        }
        catch (AccessModelException e)
        {
            Holds.Error = e.Message;
            await LoadHoldsAsync(cancellationToken);

            return await ShowAsync(Holds);
        }
    }

    /// <summary>Lifts a hold.</summary>
    public Task<IActionResult> RemoveHoldAsync(int key, string? password, CancellationToken cancellationToken)
        => ActAsync(Holds, password, confirm: true, async () =>
        {
            return await Retention.RemoveHoldAsync(key, Actor, cancellationToken) ? "The hold was lifted." : "That hold no longer exists.";
        }, () => RedirectToScreen("/Admin/RetentionHolds", new { repository = Holds.Repository }));

    private async Task LoadHoldsAsync(CancellationToken cancellationToken)
    {
        Holds.Holds = await Retention.ListHoldsAsync(Holds.Repository, cancellationToken);
        Holds.Repositories = await AccessSupport.RetentionRepositoriesAsync(Services, cancellationToken);
    }
}
