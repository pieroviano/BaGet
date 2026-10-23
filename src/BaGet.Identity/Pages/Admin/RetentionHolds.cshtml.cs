using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Identity.Pages.Admin;

/// <summary>
/// The legal holds: artifacts (or single versions) that no retention policy deletes. A hold names a repository, an artifact in the
/// form its format uses (package id, npm name, Maven <c>group:artifact</c>, Docker image) and optionally one version (a Docker tag
/// or digest); with no version it covers every version. Placing a hold is protective and asks for nothing; lifting one asks for
/// the administrator's password. Both are audited with the access audit. Administrators only.
/// </summary>
public class RetentionHoldsModel : RetentionPageModel
{
    /// <summary>Only the holds of this repository (query).</summary>
    [BindProperty(SupportsGet = true)]
    public string? Repository { get; set; }

    /// <summary>The repository of the new hold.</summary>
    [BindProperty]
    public string? HoldRepository { get; set; }

    /// <summary>The artifact name of the new hold.</summary>
    [BindProperty]
    public string? HoldName { get; set; }

    /// <summary>The version of the new hold; empty for every version.</summary>
    [BindProperty]
    public string? HoldVersion { get; set; }

    /// <summary>Why (a case number, a ticket).</summary>
    [BindProperty]
    public string? Reason { get; set; }

    /// <summary>The holds.</summary>
    public IReadOnlyList<RetentionHoldView> Holds { get; private set; } = Array.Empty<RetentionHoldView>();

    /// <summary>Lists the holds.</summary>
    public Task OnGetAsync(CancellationToken cancellationToken) => LoadAsync(cancellationToken);

    /// <summary>Places a hold.</summary>
    public async Task<IActionResult> OnPostAddAsync(CancellationToken cancellationToken)
    {
        try
        {
            var hold = await Retention.AddHoldAsync(HoldRepository ?? string.Empty, HoldName ?? string.Empty, HoldVersion, Reason, Actor, cancellationToken);
            Message = $"{hold.Name}{(string.IsNullOrEmpty(hold.Version) ? string.Empty : " " + hold.Version)} is on legal hold.";

            return RedirectToPage(new { repository = Repository });
        }
        catch (AccessModelException e)
        {
            Error = e.Message;
            await LoadAsync(cancellationToken);

            return Page();
        }
    }

    /// <summary>Lifts a hold.</summary>
    public Task<IActionResult> OnPostRemoveAsync(int key, string? password, CancellationToken cancellationToken)
        => ActAsync(password, confirm: true, async () =>
        {
            return await Retention.RemoveHoldAsync(key, Actor, cancellationToken) ? "The hold was lifted." : "That hold no longer exists.";
        }, () => RedirectToPage(new { repository = Repository }));

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        Holds = await Retention.ListHoldsAsync(Repository, cancellationToken);
        await LoadChoicesAsync(cancellationToken);
    }
}
