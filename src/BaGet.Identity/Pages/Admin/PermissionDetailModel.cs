using System;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.Workflows;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Identity.Pages.Admin;

/// <summary>
/// The page model of the permission target page when it was a Razor Page: its repository and package patterns (with a preview and a
/// test box for package IDs), and the grants: which users, groups or organizations hold which of Read, Annotate, Deploy, Delete and
/// Manage through it. Editing patterns, setting or removing a grant and deleting the target ask for the password again.
/// Administrators, and the administrators of the target project for a project target.
/// </summary>
/// <remarks>
/// Kept as a shim (no breaking change of the public API); <c>AdminPermissionsController</c> serves
/// <c>/admin/permissions/{name}</c>. Both call <c>PermissionDetailWorkflow</c>.
/// </remarks>
[Obsolete("The permission target screen is served by AdminPermissionsController; this page model has no page any more and will be removed in the next major version.")]
public class PermissionDetailModel : AccessPageModel
{
    private PermissionDetailWorkflow? _workflow;

    private PermissionDetailWorkflow Workflow
    {
        get
        {
            var workflow = _workflow ??= new PermissionDetailWorkflow(HttpContext, Url);
            workflow.IsServerAdministrator = IsServerAdministrator;
            workflow.CurrentUserId = CurrentUserId;

            return workflow;
        }
    }

    /// <summary>The target name (route).</summary>
    [BindProperty(SupportsGet = true)]
    public string? Name { get => Workflow.Model.Name; set => Workflow.Model.Name = value; }

    /// <summary>Repository names or globs, one per line.</summary>
    [BindProperty]
    public string? RepositoryPatterns { get => Workflow.Model.RepositoryPatterns; set => Workflow.Model.RepositoryPatterns = value; }

    /// <summary>Package ID globs the target applies to, one per line.</summary>
    [BindProperty]
    public string? IncludePatterns { get => Workflow.Model.IncludePatterns; set => Workflow.Model.IncludePatterns = value; }

    /// <summary>Package ID globs the target leaves out, one per line.</summary>
    [BindProperty]
    public string? ExcludePatterns { get => Workflow.Model.ExcludePatterns; set => Workflow.Model.ExcludePatterns = value; }

    /// <summary>A package ID to test the patterns with.</summary>
    [BindProperty(SupportsGet = true)]
    public string? TestPackage { get => Workflow.Model.TestPackage; set => Workflow.Model.TestPackage = value; }

    /// <summary>The kind of principal in the grant form.</summary>
    [BindProperty(SupportsGet = true)]
    public PrincipalType PrincipalType { get => Workflow.Model.PrincipalType; set => Workflow.Model.PrincipalType = value; }

    /// <summary>The user, group or organization name in the grant form.</summary>
    [BindProperty(SupportsGet = true)]
    public string? PrincipalName { get => Workflow.Model.PrincipalName; set => Workflow.Model.PrincipalName = value; }

    /// <summary>The target.</summary>
    public TargetDetails? Target => Workflow.Model.Target;

    /// <summary>What the patterns select.</summary>
    public PatternPreview? Preview => Workflow.Model.Preview;

    /// <summary>The actions ticked in the grant form.</summary>
    public AccessAction FormActions => Workflow.Model.FormActions;

    /// <summary>The model of the pattern fields partial.</summary>
    public PatternFieldsView Fields => Workflow.Model.Fields;

    /// <inheritdoc />
    protected override Task<bool> AuthorizeAsync(CancellationToken cancellationToken)
        => AccessAuthorization.TargetAsync(AccessAdmin, IsServerAdministrator, CurrentUserId, RouteData.Values["name"]?.ToString(), cancellationToken);

    /// <summary>Shows the target.</summary>
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
        => await Workflow.GetAsync(cancellationToken) ?? Page();

    /// <summary>Previews the form patterns (and the test package) without saving anything.</summary>
    public async Task<IActionResult> OnPostPreviewAsync(CancellationToken cancellationToken)
        => await Workflow.PreviewPatternsAsync(cancellationToken) ?? Page();

    /// <summary>Replaces the target patterns.</summary>
    public async Task<IActionResult> OnPostSaveAsync(string? password, CancellationToken cancellationToken)
        => Outcome(await Workflow.SaveAsync(password, cancellationToken));

    /// <summary>Gives a principal the ticked actions through the target, replacing an earlier grant to it.</summary>
    public async Task<IActionResult> OnPostSetGrantAsync(string[]? actions, string? password, CancellationToken cancellationToken)
        => Outcome(await Workflow.SetGrantAsync(actions, password, cancellationToken));

    /// <summary>Removes a principal grant.</summary>
    public async Task<IActionResult> OnPostRemoveGrantAsync(PrincipalType type, string? principalId, string? password, CancellationToken cancellationToken)
        => Outcome(await Workflow.RemoveGrantAsync(type, principalId, password, cancellationToken));

    /// <summary>Deletes the target and its grants.</summary>
    public async Task<IActionResult> OnPostDeleteAsync(string? password, CancellationToken cancellationToken)
        => Outcome(await Workflow.DeleteAsync(password, cancellationToken));

    private IActionResult Outcome(IActionResult result)
    {
        AccessWorkflow.CopyOutcome(this, Workflow.Model);

        return result;
    }
}
