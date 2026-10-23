using System;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Identity.Pages.Admin;

/// <summary>
/// One retention policy: its rules (changeable), its last run, and the actions on it. <b>Dry run</b> reports what a run would delete
/// and changes nothing (it uses the code a run uses, so the report is the run's plan); <b>Run now</b> deletes it; both run in the
/// background and the page shows the report when it is ready. Saving, running and deleting ask for the administrator's password;
/// the dry run does not. A run is refused with 503 while the server is read-only. Administrators only.
/// </summary>
public class RetentionPolicyModel : RetentionPageModel
{
    /// <summary>The policy's key (route).</summary>
    [BindProperty(SupportsGet = true)]
    public int Key { get; set; }

    /// <summary>The rules, as edited.</summary>
    [BindProperty]
    public RetentionPolicyForm Form { get; set; } = new();

    /// <summary>The policy with the names of what it applies to.</summary>
    public RetentionPolicyView? View { get; private set; }

    /// <summary>The last run since the server started (a dry run's report included), or null.</summary>
    public RetentionJobStatus? Status { get; private set; }

    /// <summary>Whether a run is in progress (the page refreshes itself).</summary>
    public bool Running => Status?.State == RetentionJobState.Running;

    /// <summary>Shows the policy.</summary>
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!await LoadAsync(cancellationToken)) return NotFound();

        Form = RetentionPolicyForm.From(View!);

        return Page();
    }

    /// <summary>Saves the rules.</summary>
    public Task<IActionResult> OnPostSaveAsync(string? password, CancellationToken cancellationToken)
        => ActAsync(password, confirm: true, async () =>
        {
            var policy = await Retention.SavePolicyAsync(Form.ToInput(Key), Actor, cancellationToken);

            return $"Policy {policy.Name} was saved.";
        }, () => RedirectToPage(new { key = Key }));

    /// <summary>Starts a dry run: the report of what a run would delete.</summary>
    public async Task<IActionResult> OnPostDryRunAsync(CancellationToken cancellationToken)
    {
        if (await Retention.GetPolicyAsync(Key, cancellationToken) == null) return NotFound();

        if (!Jobs.TryStart(Key, dryRun: true, Actor, out var refusal)) Error = refusal;
        else Message = "The dry run started; nothing is deleted.";

        return RedirectToPage(new { key = Key });
    }

    /// <summary>Starts a run that deletes.</summary>
    public async Task<IActionResult> OnPostRunAsync(string? password, CancellationToken cancellationToken)
    {
        var view = await Retention.GetPolicyAsync(Key, cancellationToken);
        if (view == null) return NotFound();

        return await ActAsync(password, confirm: true, () =>
        {
            if (!view.Policy.Enabled) throw new RetentionException("The policy is disabled: enable it first (a dry run works either way).");
            if (!Jobs.TryStart(Key, dryRun: false, Actor, out var refusal)) throw new RetentionException(refusal ?? "The run was not started.");

            return Task.FromResult($"{view.Policy.Name} started; versions it selects are deleted.");
        }, () => RedirectToPage(new { key = Key }));
    }

    /// <summary>Deletes the policy (what it deleted stays deleted).</summary>
    public async Task<IActionResult> OnPostDeleteAsync(string? password, CancellationToken cancellationToken)
    {
        if (await Retention.GetPolicyAsync(Key, cancellationToken) == null) return NotFound();

        return await ActAsync(password, confirm: true, async () =>
        {
            await Retention.DeletePolicyAsync(Key, Actor, cancellationToken);

            return "The policy was deleted.";
        }, () => RedirectToPage("/Admin/Retention"));
    }

    private async Task<bool> LoadAsync(CancellationToken cancellationToken)
    {
        View = await Retention.GetPolicyAsync(Key, cancellationToken);
        if (View == null) return false;

        Status = Jobs.Get(Key);
        await LoadChoicesAsync(cancellationToken);

        return true;
    }
}
