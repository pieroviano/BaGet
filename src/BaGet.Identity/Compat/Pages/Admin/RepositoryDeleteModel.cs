using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.Workflows;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Identity.Pages.Admin;

/// <summary>
/// The page model of the delete-repository page when it was a Razor Page: deletes a repository with no packages, and its history
/// rows. The default repository cannot be deleted. A repository that still holds packages is refused (delete the packages first),
/// and so is one that a virtual repository lists as a member (remove it there first). Refused with 503 while the
/// <see cref="MaintenanceGate"/> is closed.
/// </summary>
/// <remarks>
/// Kept as a shim (no breaking change of the public API); <c>AdminRepositoriesController</c> serves
/// <c>/admin/repositories/{name}/delete</c>. Both call <c>RepositoryDeleteWorkflow</c>.
/// </remarks>
[Obsolete("The delete-repository screen is served by AdminRepositoriesController; this page model has no page any more and will be removed in the next major version.")]
public class RepositoryDeleteModel : AdminPageModel
{
    private RepositoryDeleteWorkflow? _workflow;

    private RepositoryDeleteWorkflow Workflow => _workflow ??= new RepositoryDeleteWorkflow(HttpContext, Url);

    /// <summary>The repository's name (route).</summary>
    [BindProperty(SupportsGet = true)]
    public string? Name { get => Workflow.Model.Name; set => Workflow.Model.Name = value; }

    /// <summary>The repository.</summary>
    public RepositorySummary? Repository => Workflow.Model.Repository;

    /// <summary>The virtual repositories that list this repository as a member (the deletion is refused while there are any).</summary>
    public IReadOnlyList<string> ReferencedBy => Workflow.Model.ReferencedBy;

    /// <summary>Why the deletion was refused.</summary>
    public string? Error => Workflow.Model.Error;

    /// <summary>Shows what the deletion does.</summary>
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
        => await Workflow.GetAsync(cancellationToken) ?? Page();

    /// <summary>Deletes the repository, then returns to the list.</summary>
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var result = await Workflow.PostAsync(cancellationToken);
        if (Workflow.RedirectMessage != null) TempData["Message"] = Workflow.RedirectMessage;

        return result ?? Page();
    }
}
