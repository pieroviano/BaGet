using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Identity.Pages.Admin;

/// <summary>
/// The replications: a local repository copied to or from a repository of another BaGet server (push or pull, on deploy and on a
/// schedule), listed with their last result and the form that creates one. A replication is opened (<see cref="ReplicationConfigModel"/>)
/// to change it, test the connection, run it, resume an interrupted run, read its log or delete it. Creating one asks for the
/// administrator's password and is audited with the access audit. The peer's API key is stored protected and never shown again.
/// Administrators only.
/// </summary>
public class ReplicationModel : ReplicationPageModel
{
    /// <summary>The new replication.</summary>
    [BindProperty]
    public ReplicationForm Form { get; set; } = new();

    /// <summary>Every replication.</summary>
    public IReadOnlyList<ReplicationConfigView> Replications { get; private set; } = Array.Empty<ReplicationConfigView>();

    /// <summary>Lists the replications.</summary>
    public Task OnGetAsync(CancellationToken cancellationToken) => LoadAsync(cancellationToken);

    /// <summary>Creates a replication and opens it.</summary>
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
            var config = await Replication.SaveAsync(Form.ToInput(0), Actor, cancellationToken);
            Message = $"Replication {config.Name} was created.";

            return RedirectToPage("/Admin/ReplicationConfig", new { key = config.Key });
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
        Replications = await Replication.ListAsync(cancellationToken);
        await LoadChoicesAsync(cancellationToken);
    }
}
