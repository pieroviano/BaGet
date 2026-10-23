using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Identity.Pages.Admin;

/// <summary>
/// The retention policies: which versions of a repository (or of every repository of a project) are deleted and when, listed with
/// their last run and the form that creates one. A policy is opened (<see cref="RetentionPolicyModel"/>) to change it, dry-run it,
/// run it now or delete it. Creating one asks for the administrator's password, since an enabled policy deletes data, and is
/// audited with the access audit. Administrators only.
/// </summary>
public class RetentionModel : RetentionPageModel
{
    /// <summary>The new policy.</summary>
    [BindProperty]
    public RetentionPolicyForm Form { get; set; } = new();

    /// <summary>Every policy.</summary>
    public IReadOnlyList<RetentionPolicyView> Policies { get; private set; } = Array.Empty<RetentionPolicyView>();

    /// <summary>Lists the policies.</summary>
    public Task OnGetAsync(CancellationToken cancellationToken) => LoadAsync(cancellationToken);

    /// <summary>Creates a policy and opens it.</summary>
    public async Task<IActionResult> OnPostAsync(string? password, CancellationToken cancellationToken)
    {
        var current = await UserManager.GetUserAsync(User);
        if (current == null) return Challenge();

        if (!await IsConfirmedAsync(current, password))
        {
            Error = "The confirmation is incorrect, so nothing was changed.";
            await LoadAsync(cancellationToken);

            return Page();
        }

        try
        {
            var policy = await Retention.SavePolicyAsync(Form.ToInput(0), Actor, cancellationToken);
            Message = $"Policy {policy.Name} was created.";

            return RedirectToPage("/Admin/RetentionPolicy", new { key = policy.Key });
        }
        catch (AccessModelException e)
        {
            Error = e.Message;
            await LoadAsync(cancellationToken);

            return Page();
        }
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        Policies = await Retention.ListPoliciesAsync(cancellationToken);
        await LoadChoicesAsync(cancellationToken);
    }
}
