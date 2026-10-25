using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.Workflows;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Identity.Pages.Admin;

/// <summary>
/// The page model of the projects page when it was a Razor Page: lists the projects (groups of repositories with their own
/// administrators) and creates one. Administrators see every project and create new ones; a project administrators see the
/// projects they administer.
/// </summary>
/// <remarks>
/// Kept as a shim (no breaking change of the public API); <c>AdminProjectsController</c> serves <c>/admin/projects</c>. Both call
/// <c>ProjectsWorkflow</c>.
/// </remarks>
[Obsolete("The projects screen is served by AdminProjectsController; this page model has no page any more and will be removed in the next major version.")]
public class ProjectsModel : AccessPageModel
{
    private ProjectsWorkflow? _workflow;

    private ProjectsWorkflow Workflow
    {
        get
        {
            var workflow = _workflow ??= new ProjectsWorkflow(HttpContext, Url);
            workflow.IsServerAdministrator = IsServerAdministrator;
            workflow.CurrentUserId = CurrentUserId;

            return workflow;
        }
    }

    /// <summary>The new project name.</summary>
    [BindProperty]
    public string? Name { get => Workflow.Model.Name; set => Workflow.Model.Name = value; }

    /// <summary>The new project display name.</summary>
    [BindProperty]
    public string? DisplayName { get => Workflow.Model.DisplayName; set => Workflow.Model.DisplayName = value; }

    /// <summary>The projects the signed-in user may open.</summary>
    public IReadOnlyList<ProjectSummary> Projects => Workflow.Model.Projects;

    /// <inheritdoc />
    protected override Task<bool> AuthorizeAsync(CancellationToken cancellationToken)
        => AccessAuthorization.ProjectsAsync(AccessAdmin, IsServerAdministrator, CurrentUserId, cancellationToken);

    /// <summary>Shows the projects.</summary>
    public async Task OnGetAsync(CancellationToken cancellationToken) => await Workflow.ListAsync(cancellationToken);

    /// <summary>Creates a project (administrators only) and opens it.</summary>
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var result = await Workflow.CreateAsync(cancellationToken);
        AccessWorkflow.CopyOutcome(this, Workflow.Model);

        return result ?? Page();
    }
}
