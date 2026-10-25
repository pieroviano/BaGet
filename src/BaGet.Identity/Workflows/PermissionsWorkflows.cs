using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.Mvc;
using BaGet.Identity.Pages.Admin;
using BaGet.Identity.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Identity.Workflows;

/// <summary>
/// The permission targets list (rules saying which repositories and package IDs their grants apply to) and the new-target form. The
/// patterns can be previewed before saving. Administrators only. Used by <c>AdminPermissionsController</c>.
/// </summary>
internal sealed class PermissionsWorkflow : AccessWorkflow
{
    public PermissionsWorkflow(HttpContext httpContext, IUrlHelper url) : base(httpContext, url)
    {
    }

    /// <summary>The screen; the caller sets the form fields first.</summary>
    public PermissionsViewModel Model { get; } = new();

    /// <summary>Lists the targets.</summary>
    public async Task ListAsync(CancellationToken cancellationToken)
    {
        Model.IsServerAdministrator = IsServerAdministrator;
        await LoadConfirmationAsync(Model);
        Model.Targets = await AccessAdmin.ListTargetsAsync(cancellationToken);
        Model.Projects = await AccessAdmin.ListProjectsAsync(cancellationToken);
    }

    /// <summary>Creates a target and opens it; a refusal renders the list with the reason.</summary>
    public async Task<IActionResult?> CreateAsync(CancellationToken cancellationToken)
    {
        try
        {
            int? projectKey = null;
            if (!string.IsNullOrWhiteSpace(Model.Project))
            {
                var project = await AccessAdmin.GetProjectAsync(Model.Project.Trim(), cancellationToken)
                              ?? throw new AccessModelException($"There is no project named '{Model.Project}'.");
                projectKey = project.Project.Key;
            }

            var target = await AccessAdmin.CreateTargetAsync(
                Model.Name ?? string.Empty,
                AccessPatterns.Split(Model.RepositoryPatterns),
                AccessPatterns.Split(Model.IncludePatterns),
                AccessPatterns.Split(Model.ExcludePatterns),
                projectKey,
                Actor,
                cancellationToken);
            Model.Message = $"Permission target {target.Name} was created. Add grants to it.";

            return RedirectToScreen("/Admin/PermissionDetail", new { name = target.Name });
        }
        catch (AccessModelException e)
        {
            Model.Error = e.Message;
            await ListAsync(cancellationToken);

            return null;
        }
    }

    /// <summary>Previews the form patterns without saving anything.</summary>
    public async Task PreviewPatternsAsync(CancellationToken cancellationToken)
    {
        await ListAsync(cancellationToken);
        Model.Preview = await PreviewAsync(
            AccessPatterns.Split(Model.RepositoryPatterns), AccessPatterns.Split(Model.IncludePatterns), AccessPatterns.Split(Model.ExcludePatterns), Model.TestPackage, cancellationToken);
    }

    /// <summary>Re-creates the built-in groups and targets that are missing (the upgrade defaults).</summary>
    public Task<IActionResult> RestoreBuiltInsAsync(string? password, CancellationToken cancellationToken)
        => ActAsync(Model, password, confirm: true, async () =>
        {
            await AccessAdmin.RestoreBuiltInsAsync(Actor, cancellationToken);
            return "The built-in groups and permission targets were restored where they were missing.";
        }, () => RedirectToScreen("/Admin/Permissions"));
}

/// <summary>
/// One permission target: its repository and package patterns (with a preview of the repositories they match and a test box for
/// package IDs), and the grants: which users, groups or organizations hold which of Read, Annotate, Deploy, Delete and Manage
/// through it. Editing patterns, setting or removing a grant and deleting the target ask for the password again. Shared by
/// <c>AdminPermissionsController</c>.
/// </summary>
internal sealed class PermissionDetailWorkflow : AccessWorkflow
{
    public PermissionDetailWorkflow(HttpContext httpContext, IUrlHelper url) : base(httpContext, url)
    {
    }

    /// <summary>The screen; the caller sets the route, query and form values first.</summary>
    public PermissionDetailViewModel Model { get; } = new();

    /// <summary>Shows the target; 404 when there is none.</summary>
    public async Task<IActionResult?> GetAsync(CancellationToken cancellationToken)
    {
        if (!await LoadAsync(cancellationToken)) return new NotFoundResult();

        var target = Model.Target!;
        Model.RepositoryPatterns = AccessSupport.Lines(target.Target.RepositoryPatterns);
        Model.IncludePatterns = AccessSupport.Lines(target.Target.IncludePatterns);
        Model.ExcludePatterns = AccessSupport.Lines(target.Target.ExcludePatterns);
        Model.Preview = await RestrictToProjectAsync(await PreviewAsync(
            target.Target.RepositoryPatterns.ToArray(), target.Target.IncludePatterns.ToArray(), target.Target.ExcludePatterns.ToArray(), Model.TestPackage, cancellationToken), cancellationToken);

        if (!string.IsNullOrWhiteSpace(Model.PrincipalName))
        {
            var id = await AccessAdmin.ResolvePrincipalAsync(Model.PrincipalType, Model.PrincipalName, cancellationToken);
            var existing = target.Grants.FirstOrDefault(g => g.PrincipalType == Model.PrincipalType && g.PrincipalId == id);
            Model.FormActions = existing?.Actions ?? AccessAction.None;
        }

        return null;
    }

    /// <summary>Previews the form patterns (and the test package) without saving anything.</summary>
    public async Task<IActionResult?> PreviewPatternsAsync(CancellationToken cancellationToken)
    {
        if (!await LoadAsync(cancellationToken)) return new NotFoundResult();

        Model.Preview = await RestrictToProjectAsync(await PreviewAsync(
            AccessPatterns.Split(Model.RepositoryPatterns), AccessPatterns.Split(Model.IncludePatterns), AccessPatterns.Split(Model.ExcludePatterns), Model.TestPackage, cancellationToken), cancellationToken);

        return null;
    }

    /// <summary>Replaces the target patterns.</summary>
    public Task<IActionResult> SaveAsync(string? password, CancellationToken cancellationToken)
        => ActAsync(Model, password, confirm: true, async () =>
        {
            await AccessAdmin.UpdateTargetAsync(
                Model.Name ?? string.Empty,
                AccessPatterns.Split(Model.RepositoryPatterns),
                AccessPatterns.Split(Model.IncludePatterns),
                AccessPatterns.Split(Model.ExcludePatterns),
                Actor,
                cancellationToken);
            return "The patterns were saved.";
        }, Back);

    /// <summary>Gives a principal the ticked actions through the target, replacing an earlier grant to it.</summary>
    public Task<IActionResult> SetGrantAsync(string[]? actions, string? password, CancellationToken cancellationToken)
        => ActAsync(Model, password, confirm: true, async () =>
        {
            var id = await AccessAdmin.ResolvePrincipalAsync(Model.PrincipalType, Model.PrincipalName ?? string.Empty, cancellationToken)
                     ?? throw new AccessModelException($"There is no {Model.PrincipalType.ToString().ToLowerInvariant()} named '{Model.PrincipalName}'.");
            var granted = AccessSupport.ParseActions(actions);
            await AccessAdmin.SetGrantAsync(Model.Name ?? string.Empty, Model.PrincipalType, id, granted, Actor, cancellationToken);

            return granted == AccessAction.None
                ? $"{Model.PrincipalName} holds nothing through this target any more."
                : $"{Model.PrincipalName} now holds {AccessSupport.Words(granted)} through this target.";
        }, Back);

    /// <summary>Removes a principal grant.</summary>
    public Task<IActionResult> RemoveGrantAsync(PrincipalType type, string? principalId, string? password, CancellationToken cancellationToken)
        => ActAsync(Model, password, confirm: true, async () =>
        {
            await AccessAdmin.SetGrantAsync(Model.Name ?? string.Empty, type, principalId ?? string.Empty, AccessAction.None, Actor, cancellationToken);
            return "The grant was removed.";
        }, Back);

    /// <summary>Deletes the target and its grants.</summary>
    public async Task<IActionResult> DeleteAsync(string? password, CancellationToken cancellationToken)
    {
        var deleted = false;
        var project = (await AccessAdmin.GetTargetAsync(Model.Name ?? string.Empty, cancellationToken))?.Target.ProjectName;
        var result = await ActAsync(Model, password, confirm: true, async () =>
        {
            await AccessAdmin.DeleteTargetAsync(Model.Name ?? string.Empty, Actor, cancellationToken);
            deleted = true;
            return $"Permission target {Model.Name} was deleted.";
        }, Back);

        if (!deleted) return result;

        return IsServerAdministrator || project == null
            ? RedirectToScreen("/Admin/Permissions")
            : RedirectToScreen("/Admin/ProjectDetail", new { name = project });
    }

    // A project target only ever applies to the project's repositories, so that is all the preview may show (and all a
    // project administrator may learn about).
    private async Task<PatternPreview> RestrictToProjectAsync(PatternPreview preview, CancellationToken cancellationToken)
    {
        var projectName = Model.Target?.Target.ProjectName;
        if (projectName == null) return preview;

        var project = await AccessAdmin.GetProjectAsync(projectName, cancellationToken);
        var own = project?.Repositories ?? Array.Empty<string>();

        return preview with { Repositories = preview.Repositories.Where(r => own.Contains(r, StringComparer.Ordinal)).ToList() };
    }

    private IActionResult Back() => RedirectToScreen("/Admin/PermissionDetail", new { name = Model.Name });

    private async Task<bool> LoadAsync(CancellationToken cancellationToken)
    {
        Model.IsServerAdministrator = IsServerAdministrator;
        await LoadConfirmationAsync(Model);
        Model.Target = await AccessAdmin.GetTargetAsync(Model.Name ?? string.Empty, cancellationToken);

        return Model.Target != null;
    }
}
