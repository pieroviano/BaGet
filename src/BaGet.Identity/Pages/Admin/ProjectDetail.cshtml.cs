using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace BaGet.Identity.Pages.Admin;

/// <summary>
/// One project: its repositories (assigned by server administrators), its members with their roles, and the permission
/// targets that apply only to its repositories. Server administrators and the project's own administrators manage
/// members and targets; only server administrators assign repositories and delete the project. Member changes and
/// deletions ask for the password again.
/// </summary>
public class ProjectDetailModel : AccessPageModel
{
    /// <summary>The project's name (route).</summary>
    [BindProperty(SupportsGet = true)]
    public string? Name { get; set; }

    /// <summary>The project.</summary>
    public ProjectDetails? Project { get; private set; }

    /// <summary>The NuGet repositories not yet in this project, for the assignment picker.</summary>
    public IReadOnlyList<string> Assignable { get; private set; } = Array.Empty<string>();

    /// <summary>The new target's name.</summary>
    [BindProperty]
    public string? TargetName { get; set; }

    /// <summary>Repository globs of the new target, one per line (they only ever reach the project's repositories).</summary>
    [BindProperty]
    public string? RepositoryPatterns { get; set; }

    /// <summary>Include globs of the new target.</summary>
    [BindProperty]
    public string? IncludePatterns { get; set; }

    /// <summary>Exclude globs of the new target.</summary>
    [BindProperty]
    public string? ExcludePatterns { get; set; }

    /// <summary>A package ID to test the patterns with.</summary>
    [BindProperty]
    public string? TestPackage { get; set; }

    /// <summary>What the new target's patterns select.</summary>
    public PatternPreview? Preview { get; private set; }

    /// <summary>The model of the pattern fields partial.</summary>
    public PatternFieldsView Fields => new(RepositoryPatterns, IncludePatterns, ExcludePatterns, TestPackage, Preview);

    /// <inheritdoc />
    protected override async Task<bool> AuthorizeAsync(CancellationToken cancellationToken)
    {
        if (IsServerAdministrator) return true;

        var name = RouteData.Values["name"]?.ToString();

        return !string.IsNullOrEmpty(name) && CurrentUserId != null && await AccessAdmin.IsProjectAdminAsync(CurrentUserId, name, cancellationToken);
    }

    /// <summary>Shows the project.</summary>
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!await LoadAsync(cancellationToken)) return NotFound();

        return Page();
    }

    /// <summary>Gives a user or group a role in the project, replacing an earlier one.</summary>
    public Task<IActionResult> OnPostSetMemberAsync(PrincipalType type, string? principalName, ProjectRole role, string? password, CancellationToken cancellationToken)
        => ActAsync(password, confirm: true, async () =>
        {
            if (type == PrincipalType.Organization) throw new AccessModelException("A project member is a user or a group.");

            var id = await AccessAdmin.ResolvePrincipalAsync(type, principalName ?? string.Empty, cancellationToken)
                     ?? throw new AccessModelException($"There is no {type.ToString().ToLowerInvariant()} named '{principalName}'.");
            await AccessAdmin.SetProjectMemberAsync(Name ?? string.Empty, type, id, role, Actor, cancellationToken);

            return $"{principalName} is now {role} of {Name}.";
        }, Back);

    /// <summary>Takes a user or group out of the project.</summary>
    public Task<IActionResult> OnPostRemoveMemberAsync(PrincipalType type, string? principalId, string? password, CancellationToken cancellationToken)
        => ActAsync(password, confirm: true, async () =>
        {
            await AccessAdmin.RemoveProjectMemberAsync(Name ?? string.Empty, type, principalId ?? string.Empty, Actor, cancellationToken);
            return "The member was removed.";
        }, Back);

    /// <summary>Puts a repository in the project (server administrators only); it leaves any project it was in.</summary>
    public Task<IActionResult> OnPostAssignRepositoryAsync(string? repository, string? password, CancellationToken cancellationToken)
        => ActAsync(password, confirm: true, async () =>
        {
            if (!IsServerAdministrator) throw new AccessModelException("Only server administrators assign repositories to projects.");

            await AccessAdmin.SetRepositoryProjectAsync(repository ?? string.Empty, Name, Actor, cancellationToken);
            return $"Repository {repository} is now in {Name}.";
        }, Back);

    /// <summary>Takes a repository out of the project (server administrators only).</summary>
    public Task<IActionResult> OnPostUnassignRepositoryAsync(string? repository, string? password, CancellationToken cancellationToken)
        => ActAsync(password, confirm: true, async () =>
        {
            if (!IsServerAdministrator) throw new AccessModelException("Only server administrators take repositories out of projects.");

            await AccessAdmin.SetRepositoryProjectAsync(repository ?? string.Empty, null, Actor, cancellationToken);
            return $"Repository {repository} is no longer in {Name}.";
        }, Back);

    /// <summary>Creates a permission target that applies only to the project's repositories.</summary>
    public async Task<IActionResult> OnPostCreateTargetAsync(CancellationToken cancellationToken)
    {
        if (!await LoadAsync(cancellationToken)) return NotFound();

        try
        {
            var target = await AccessAdmin.CreateTargetAsync(
                TargetName ?? string.Empty,
                AccessPatterns.Split(RepositoryPatterns),
                AccessPatterns.Split(IncludePatterns),
                AccessPatterns.Split(ExcludePatterns),
                Project!.Project.Key,
                Actor,
                cancellationToken);
            Message = $"Permission target {target.Name} was created. Add grants to it.";

            return RedirectToPage("/Admin/PermissionDetail", new { name = target.Name });
        }
        catch (AccessModelException e)
        {
            Error = e.Message;

            return Page();
        }
    }

    /// <summary>Previews the new target's patterns without saving anything.</summary>
    public async Task<IActionResult> OnPostPreviewAsync(CancellationToken cancellationToken)
    {
        if (!await LoadAsync(cancellationToken)) return NotFound();

        // Only the project's own repositories can be reached, whatever the patterns say.
        var preview = await PreviewAsync(
            AccessPatterns.Split(RepositoryPatterns), AccessPatterns.Split(IncludePatterns), AccessPatterns.Split(ExcludePatterns), TestPackage, cancellationToken);
        Preview = preview with { Repositories = preview.Repositories.Where(r => Project!.Repositories.Contains(r, StringComparer.Ordinal)).ToList() };

        return Page();
    }

    /// <summary>Deletes the project (server administrators only) with its members and targets; repositories stay.</summary>
    public async Task<IActionResult> OnPostDeleteAsync(string? password, CancellationToken cancellationToken)
    {
        var deleted = false;
        var result = await ActAsync(password, confirm: true, async () =>
        {
            if (!IsServerAdministrator) throw new AccessModelException("Only server administrators delete projects.");

            await AccessAdmin.DeleteProjectAsync(Name ?? string.Empty, Actor, cancellationToken);
            deleted = true;
            return $"Project {Name} was deleted.";
        }, Back);

        return deleted ? RedirectToPage("/Admin/Projects") : result;
    }

    private IActionResult Back() => RedirectToPage("/Admin/ProjectDetail", new { name = Name });

    private async Task<bool> LoadAsync(CancellationToken cancellationToken)
    {
        Project = await AccessAdmin.GetProjectAsync(Name ?? string.Empty, cancellationToken);
        if (Project == null) return false;

        var all = await HttpContext.RequestServices.GetRequiredService<IRepositoryService>().ListAsync(cancellationToken);
        Assignable = all
            .Where(r => !Project.Repositories.Contains(r.Name, StringComparer.Ordinal))
            .Select(r => r.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        return true;
    }
}
