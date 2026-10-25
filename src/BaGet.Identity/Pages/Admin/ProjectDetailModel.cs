using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.Workflows;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Identity.Pages.Admin;

/// <summary>
/// The page model of the project page when it was a Razor Page: its repositories (assigned by server administrators), its members
/// with their roles, and the permission targets that apply only to its repositories. Server administrators and the project own
/// administrators manage members and targets; only server administrators assign repositories and delete the project. Member
/// changes and deletions ask for the password again.
/// </summary>
/// <remarks>
/// Kept as a shim (no breaking change of the public API); <c>AdminProjectsController</c> serves <c>/admin/projects/{name}</c>. Both
/// call <c>ProjectDetailWorkflow</c>.
/// </remarks>
[Obsolete("The project screen is served by AdminProjectsController; this page model has no page any more and will be removed in the next major version.")]
public class ProjectDetailModel : AccessPageModel
{
    private ProjectDetailWorkflow? _workflow;

    private ProjectDetailWorkflow Workflow
    {
        get
        {
            var workflow = _workflow ??= new ProjectDetailWorkflow(HttpContext, Url);
            workflow.IsServerAdministrator = IsServerAdministrator;
            workflow.CurrentUserId = CurrentUserId;

            return workflow;
        }
    }

    /// <summary>The project name (route).</summary>
    [BindProperty(SupportsGet = true)]
    public string? Name { get => Workflow.Model.Name; set => Workflow.Model.Name = value; }

    /// <summary>The project.</summary>
    public ProjectDetails? Project => Workflow.Model.Project;

    /// <summary>The NuGet repositories not yet in this project, for the assignment picker.</summary>
    public IReadOnlyList<string> Assignable => Workflow.Model.Assignable;

    /// <summary>The new target name.</summary>
    [BindProperty]
    public string? TargetName { get => Workflow.Model.TargetName; set => Workflow.Model.TargetName = value; }

    /// <summary>Repository globs of the new target, one per line (they only ever reach the project repositories).</summary>
    [BindProperty]
    public string? RepositoryPatterns { get => Workflow.Model.RepositoryPatterns; set => Workflow.Model.RepositoryPatterns = value; }

    /// <summary>Include globs of the new target.</summary>
    [BindProperty]
    public string? IncludePatterns { get => Workflow.Model.IncludePatterns; set => Workflow.Model.IncludePatterns = value; }

    /// <summary>Exclude globs of the new target.</summary>
    [BindProperty]
    public string? ExcludePatterns { get => Workflow.Model.ExcludePatterns; set => Workflow.Model.ExcludePatterns = value; }

    /// <summary>A package ID to test the patterns with.</summary>
    [BindProperty]
    public string? TestPackage { get => Workflow.Model.TestPackage; set => Workflow.Model.TestPackage = value; }

    /// <summary>What the new target patterns select.</summary>
    public PatternPreview? Preview => Workflow.Model.Preview;

    /// <summary>The model of the pattern fields partial.</summary>
    public PatternFieldsView Fields => Workflow.Model.Fields;

    /// <inheritdoc />
    protected override Task<bool> AuthorizeAsync(CancellationToken cancellationToken)
        => AccessAuthorization.ProjectAsync(AccessAdmin, IsServerAdministrator, CurrentUserId, RouteData.Values["name"]?.ToString(), cancellationToken);

    /// <summary>Shows the project.</summary>
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
        => await Workflow.GetAsync(cancellationToken) ?? Page();

    /// <summary>Gives a user or group a role in the project, replacing an earlier one.</summary>
    public async Task<IActionResult> OnPostSetMemberAsync(PrincipalType type, string? principalName, ProjectRole role, string? password, CancellationToken cancellationToken)
        => Outcome(await Workflow.SetMemberAsync(type, principalName, role, password, cancellationToken));

    /// <summary>Takes a user or group out of the project.</summary>
    public async Task<IActionResult> OnPostRemoveMemberAsync(PrincipalType type, string? principalId, string? password, CancellationToken cancellationToken)
        => Outcome(await Workflow.RemoveMemberAsync(type, principalId, password, cancellationToken));

    /// <summary>Puts a repository in the project (server administrators only); it leaves any project it was in.</summary>
    public async Task<IActionResult> OnPostAssignRepositoryAsync(string? repository, string? password, CancellationToken cancellationToken)
        => Outcome(await Workflow.AssignRepositoryAsync(repository, password, cancellationToken));

    /// <summary>Takes a repository out of the project (server administrators only).</summary>
    public async Task<IActionResult> OnPostUnassignRepositoryAsync(string? repository, string? password, CancellationToken cancellationToken)
        => Outcome(await Workflow.UnassignRepositoryAsync(repository, password, cancellationToken));

    /// <summary>Creates a permission target that applies only to the project repositories.</summary>
    public async Task<IActionResult> OnPostCreateTargetAsync(CancellationToken cancellationToken)
    {
        var result = await Workflow.CreateTargetAsync(cancellationToken);
        AccessWorkflow.CopyOutcome(this, Workflow.Model);

        return result ?? Page();
    }

    /// <summary>Previews the new target patterns without saving anything.</summary>
    public async Task<IActionResult> OnPostPreviewAsync(CancellationToken cancellationToken)
        => await Workflow.PreviewPatternsAsync(cancellationToken) ?? Page();

    /// <summary>Deletes the project (server administrators only) with its members and targets; repositories stay.</summary>
    public async Task<IActionResult> OnPostDeleteAsync(string? password, CancellationToken cancellationToken)
        => Outcome(await Workflow.DeleteAsync(password, cancellationToken));

    private IActionResult Outcome(IActionResult result)
    {
        AccessWorkflow.CopyOutcome(this, Workflow.Model);

        return result;
    }
}
