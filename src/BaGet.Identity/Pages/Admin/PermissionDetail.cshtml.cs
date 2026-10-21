using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Identity.Pages.Admin;

/// <summary>
/// One permission target: its repository and package patterns (with a preview of the repositories they match and a
/// test box for package IDs), and the grants: which users, groups or organizations hold which of Read, Annotate,
/// Deploy, Delete and Manage through it. Editing patterns, setting or removing a grant and deleting the target ask for
/// the password again. Administrators, and the administrators of the target's project for a project target.
/// </summary>
public class PermissionDetailModel : AccessPageModel
{
    /// <summary>The target's name (route).</summary>
    [BindProperty(SupportsGet = true)]
    public string? Name { get; set; }

    /// <summary>Repository names or globs, one per line.</summary>
    [BindProperty]
    public string? RepositoryPatterns { get; set; }

    /// <summary>Package ID globs the target applies to, one per line.</summary>
    [BindProperty]
    public string? IncludePatterns { get; set; }

    /// <summary>Package ID globs the target leaves out, one per line.</summary>
    [BindProperty]
    public string? ExcludePatterns { get; set; }

    /// <summary>A package ID to test the patterns with.</summary>
    [BindProperty(SupportsGet = true)]
    public string? TestPackage { get; set; }

    /// <summary>The kind of principal in the grant form.</summary>
    [BindProperty(SupportsGet = true)]
    public PrincipalType PrincipalType { get; set; } = PrincipalType.Group;

    /// <summary>The user, group or organization name in the grant form.</summary>
    [BindProperty(SupportsGet = true)]
    public string? PrincipalName { get; set; }

    /// <summary>The target.</summary>
    public TargetDetails? Target { get; private set; }

    /// <summary>What the patterns select.</summary>
    public PatternPreview? Preview { get; private set; }

    /// <summary>The actions ticked in the grant form.</summary>
    public AccessAction FormActions { get; private set; }

    /// <summary>The model of the pattern fields partial.</summary>
    public PatternFieldsView Fields => new(RepositoryPatterns, IncludePatterns, ExcludePatterns, TestPackage, Preview);

    /// <inheritdoc />
    protected override async Task<bool> AuthorizeAsync(CancellationToken cancellationToken)
    {
        if (IsServerAdministrator) return true;

        var name = RouteData.Values["name"]?.ToString();
        if (string.IsNullOrEmpty(name) || CurrentUserId == null) return false;

        var target = await AccessAdmin.GetTargetAsync(name, cancellationToken);

        return target?.Target.ProjectName != null && await AccessAdmin.IsProjectAdminAsync(CurrentUserId, target.Target.ProjectName, cancellationToken);
    }

    /// <summary>Shows the target.</summary>
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!await LoadAsync(cancellationToken)) return NotFound();

        RepositoryPatterns = AccessPageModel.Lines(Target!.Target.RepositoryPatterns);
        IncludePatterns = AccessPageModel.Lines(Target.Target.IncludePatterns);
        ExcludePatterns = AccessPageModel.Lines(Target.Target.ExcludePatterns);
        Preview = await RestrictToProjectAsync(await PreviewAsync(
            Target.Target.RepositoryPatterns.ToArray(), Target.Target.IncludePatterns.ToArray(), Target.Target.ExcludePatterns.ToArray(), TestPackage, cancellationToken), cancellationToken);

        if (!string.IsNullOrWhiteSpace(PrincipalName))
        {
            var id = await AccessAdmin.ResolvePrincipalAsync(PrincipalType, PrincipalName, cancellationToken);
            var existing = Target.Grants.FirstOrDefault(g => g.PrincipalType == PrincipalType && g.PrincipalId == id);
            FormActions = existing?.Actions ?? AccessAction.None;
        }

        return Page();
    }

    /// <summary>Previews the form's patterns (and the test package) without saving anything.</summary>
    public async Task<IActionResult> OnPostPreviewAsync(CancellationToken cancellationToken)
    {
        if (!await LoadAsync(cancellationToken)) return NotFound();

        Preview = await RestrictToProjectAsync(await PreviewAsync(
            AccessPatterns.Split(RepositoryPatterns), AccessPatterns.Split(IncludePatterns), AccessPatterns.Split(ExcludePatterns), TestPackage, cancellationToken), cancellationToken);

        return Page();
    }

    /// <summary>Replaces the target's patterns.</summary>
    public Task<IActionResult> OnPostSaveAsync(string? password, CancellationToken cancellationToken)
        => ActAsync(password, confirm: true, async () =>
        {
            await AccessAdmin.UpdateTargetAsync(
                Name ?? string.Empty,
                AccessPatterns.Split(RepositoryPatterns),
                AccessPatterns.Split(IncludePatterns),
                AccessPatterns.Split(ExcludePatterns),
                Actor,
                cancellationToken);
            return "The patterns were saved.";
        }, Back);

    /// <summary>Gives a principal the ticked actions through the target, replacing an earlier grant to it.</summary>
    public Task<IActionResult> OnPostSetGrantAsync(string[]? actions, string? password, CancellationToken cancellationToken)
        => ActAsync(password, confirm: true, async () =>
        {
            var id = await AccessAdmin.ResolvePrincipalAsync(PrincipalType, PrincipalName ?? string.Empty, cancellationToken)
                     ?? throw new AccessModelException($"There is no {PrincipalType.ToString().ToLowerInvariant()} named '{PrincipalName}'.");
            var granted = ParseActions(actions);
            await AccessAdmin.SetGrantAsync(Name ?? string.Empty, PrincipalType, id, granted, Actor, cancellationToken);

            return granted == AccessAction.None
                ? $"{PrincipalName} holds nothing through this target any more."
                : $"{PrincipalName} now holds {Words(granted)} through this target.";
        }, Back);

    /// <summary>Removes a principal's grant.</summary>
    public Task<IActionResult> OnPostRemoveGrantAsync(PrincipalType type, string? principalId, string? password, CancellationToken cancellationToken)
        => ActAsync(password, confirm: true, async () =>
        {
            await AccessAdmin.SetGrantAsync(Name ?? string.Empty, type, principalId ?? string.Empty, AccessAction.None, Actor, cancellationToken);
            return "The grant was removed.";
        }, Back);

    /// <summary>Deletes the target and its grants.</summary>
    public async Task<IActionResult> OnPostDeleteAsync(string? password, CancellationToken cancellationToken)
    {
        var deleted = false;
        var project = (await AccessAdmin.GetTargetAsync(Name ?? string.Empty, cancellationToken))?.Target.ProjectName;
        var result = await ActAsync(password, confirm: true, async () =>
        {
            await AccessAdmin.DeleteTargetAsync(Name ?? string.Empty, Actor, cancellationToken);
            deleted = true;
            return $"Permission target {Name} was deleted.";
        }, Back);

        if (!deleted) return result;

        return IsServerAdministrator || project == null
            ? RedirectToPage("/Admin/Permissions")
            : RedirectToPage("/Admin/ProjectDetail", new { name = project });
    }

    // A project target only ever applies to the project's repositories, so that is all the preview may show (and all a
    // project administrator may learn about).
    private async Task<PatternPreview> RestrictToProjectAsync(PatternPreview preview, CancellationToken cancellationToken)
    {
        var projectName = Target?.Target.ProjectName;
        if (projectName == null) return preview;

        var project = await AccessAdmin.GetProjectAsync(projectName, cancellationToken);
        var own = project?.Repositories ?? Array.Empty<string>();

        return preview with { Repositories = preview.Repositories.Where(r => own.Contains(r, StringComparer.Ordinal)).ToList() };
    }

    private IActionResult Back() => RedirectToPage("/Admin/PermissionDetail", new { name = Name });

    private async Task<bool> LoadAsync(CancellationToken cancellationToken)
    {
        Target = await AccessAdmin.GetTargetAsync(Name ?? string.Empty, cancellationToken);

        return Target != null;
    }
}
