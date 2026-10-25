using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.Workflows;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Identity.Pages.Admin;

/// <summary>What the pattern fields partial (<c>_PatternFields</c>) shows.</summary>
/// <param name="Repositories">The repository patterns, one per line.</param>
/// <param name="Include">The include patterns, one per line.</param>
/// <param name="Exclude">The exclude patterns, one per line.</param>
/// <param name="TestPackage">The test box package ID.</param>
/// <param name="Preview">What the patterns select, or null before the first preview.</param>
public sealed record PatternFieldsView(string? Repositories, string? Include, string? Exclude, string? TestPackage, PatternPreview? Preview);

/// <summary>
/// The page model of the permission targets page when it was a Razor Page: lists the permission targets (rules saying which
/// repositories and package IDs their grants apply to) and creates one. The patterns can be previewed before saving. Administrators
/// only.
/// </summary>
/// <remarks>
/// Kept as a shim (no breaking change of the public API); <c>AdminPermissionsController</c> serves <c>/admin/permissions</c>. Both
/// call <c>PermissionsWorkflow</c>.
/// </remarks>
[Obsolete("The permission targets screen is served by AdminPermissionsController; this page model has no page any more and will be removed in the next major version.")]
public class PermissionsModel : AccessPageModel
{
    private PermissionsWorkflow? _workflow;

    private PermissionsWorkflow Workflow
    {
        get
        {
            var workflow = _workflow ??= new PermissionsWorkflow(HttpContext, Url);
            workflow.IsServerAdministrator = IsServerAdministrator;
            workflow.CurrentUserId = CurrentUserId;

            return workflow;
        }
    }

    /// <summary>The new target name.</summary>
    [BindProperty]
    public string? Name { get => Workflow.Model.Name; set => Workflow.Model.Name = value; }

    /// <summary>Repository names or <c>*</c> globs, one per line.</summary>
    [BindProperty]
    public string? RepositoryPatterns { get => Workflow.Model.RepositoryPatterns; set => Workflow.Model.RepositoryPatterns = value; }

    /// <summary>Package ID globs the target applies to, one per line; none means all.</summary>
    [BindProperty]
    public string? IncludePatterns { get => Workflow.Model.IncludePatterns; set => Workflow.Model.IncludePatterns = value; }

    /// <summary>Package ID globs the target leaves out, one per line.</summary>
    [BindProperty]
    public string? ExcludePatterns { get => Workflow.Model.ExcludePatterns; set => Workflow.Model.ExcludePatterns = value; }

    /// <summary>The project that administers the target, if any.</summary>
    [BindProperty]
    public string? Project { get => Workflow.Model.Project; set => Workflow.Model.Project = value; }

    /// <summary>A package ID to test the patterns with.</summary>
    [BindProperty]
    public string? TestPackage { get => Workflow.Model.TestPackage; set => Workflow.Model.TestPackage = value; }

    /// <summary>The targets.</summary>
    public IReadOnlyList<TargetSummary> Targets => Workflow.Model.Targets;

    /// <summary>The projects a target can belong to.</summary>
    public IReadOnlyList<ProjectSummary> Projects => Workflow.Model.Projects;

    /// <summary>The preview of the form patterns.</summary>
    public PatternPreview? Preview => Workflow.Model.Preview;

    /// <summary>The model of the pattern fields partial.</summary>
    public PatternFieldsView Fields => Workflow.Model.Fields;

    /// <summary>Shows the targets.</summary>
    public async Task OnGetAsync(CancellationToken cancellationToken) => await Workflow.ListAsync(cancellationToken);

    /// <summary>Creates a target and opens it.</summary>
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var result = await Workflow.CreateAsync(cancellationToken);
        AccessWorkflow.CopyOutcome(this, Workflow.Model);

        return result ?? Page();
    }

    /// <summary>Previews the form patterns without saving anything.</summary>
    public async Task<IActionResult> OnPostPreviewAsync(CancellationToken cancellationToken)
    {
        await Workflow.PreviewPatternsAsync(cancellationToken);

        return Page();
    }

    /// <summary>Re-creates the built-in groups and targets that are missing (the upgrade defaults).</summary>
    public async Task<IActionResult> OnPostRestoreBuiltInsAsync(string? password, CancellationToken cancellationToken)
    {
        var result = await Workflow.RestoreBuiltInsAsync(password, cancellationToken);
        AccessWorkflow.CopyOutcome(this, Workflow.Model);

        return result;
    }
}
