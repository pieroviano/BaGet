using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace BaGet.Identity.Workflows;

/// <summary>
/// The projects list (groups of repositories with their own administrators) and the new-project form. Administrators see every
/// project and create new ones; a project's administrators see the projects they administer. Shared by
/// <c>AdminProjectsController</c>.
/// </summary>
internal sealed class ProjectsWorkflow : AccessWorkflow
{
    public ProjectsWorkflow(HttpContext httpContext, IUrlHelper url) : base(httpContext, url)
    {
    }

    /// <summary>The screen; the caller sets <c>Name</c> and <c>DisplayName</c> from the form first.</summary>
    public ProjectsViewModel Model { get; } = new();

    /// <summary>Lists the projects the signed-in user may open.</summary>
    public async Task ListAsync(CancellationToken cancellationToken)
    {
        Model.IsServerAdministrator = IsServerAdministrator;
        var all = await AccessAdmin.ListProjectsAsync(cancellationToken);
        if (IsServerAdministrator)
        {
            Model.Projects = all;
            return;
        }

        var mine = new HashSet<string>(await AccessAuthorization.AdministeredAsync(AccessAdmin, CurrentUserId, cancellationToken), StringComparer.Ordinal);
        Model.Projects = all.Where(p => mine.Contains(p.Name)).ToList();
    }

    /// <summary>Creates a project (administrators only) and opens it.</summary>
    public async Task<IActionResult?> CreateAsync(CancellationToken cancellationToken)
    {
        if (!IsServerAdministrator) return new NotFoundResult();

        try
        {
            var project = await AccessAdmin.CreateProjectAsync(
                Model.Name ?? string.Empty,
                string.IsNullOrWhiteSpace(Model.DisplayName) ? null : Model.DisplayName.Trim(),
                Actor,
                cancellationToken);
            Model.Message = $"Project {project.Name} was created.";

            return RedirectToScreen("/Admin/ProjectDetail", new { name = project.Name });
        }
        catch (AccessModelException e)
        {
            Model.Error = e.Message;
            await ListAsync(cancellationToken);

            return null;
        }
    }
}

/// <summary>
/// One project: its repositories (assigned by server administrators), its members with their roles, and the permission targets that
/// apply only to its repositories. Server administrators and the project's own administrators manage members and targets; only
/// server administrators assign repositories and delete the project. Member changes and deletions ask for the password again.
/// Used by <c>AdminProjectsController</c>.
/// </summary>
internal sealed class ProjectDetailWorkflow : AccessWorkflow
{
    public ProjectDetailWorkflow(HttpContext httpContext, IUrlHelper url) : base(httpContext, url)
    {
    }

    /// <summary>The screen; the caller sets the route and form values first.</summary>
    public ProjectDetailViewModel Model { get; } = new();

    /// <summary>Shows the project; 404 when there is none.</summary>
    public async Task<IActionResult?> GetAsync(CancellationToken cancellationToken)
        => await LoadAsync(cancellationToken) ? null : new NotFoundResult();

    /// <summary>Gives a user or group a role in the project, replacing an earlier one.</summary>
    public Task<IActionResult> SetMemberAsync(PrincipalType type, string? principalName, ProjectRole role, string? password, CancellationToken cancellationToken)
        => ActAsync(Model, password, confirm: true, async () =>
        {
            if (type == PrincipalType.Organization) throw new AccessModelException("A project member is a user or a group.");

            var id = await AccessAdmin.ResolvePrincipalAsync(type, principalName ?? string.Empty, cancellationToken)
                     ?? throw new AccessModelException($"There is no {type.ToString().ToLowerInvariant()} named '{principalName}'.");
            await AccessAdmin.SetProjectMemberAsync(Model.Name ?? string.Empty, type, id, role, Actor, cancellationToken);

            return $"{principalName} is now {role} of {Model.Name}.";
        }, Back);

    /// <summary>Takes a user or group out of the project.</summary>
    public Task<IActionResult> RemoveMemberAsync(PrincipalType type, string? principalId, string? password, CancellationToken cancellationToken)
        => ActAsync(Model, password, confirm: true, async () =>
        {
            await AccessAdmin.RemoveProjectMemberAsync(Model.Name ?? string.Empty, type, principalId ?? string.Empty, Actor, cancellationToken);
            return "The member was removed.";
        }, Back);

    /// <summary>Puts a repository in the project (server administrators only); it leaves any project it was in.</summary>
    public Task<IActionResult> AssignRepositoryAsync(string? repository, string? password, CancellationToken cancellationToken)
        => ActAsync(Model, password, confirm: true, async () =>
        {
            if (!IsServerAdministrator) throw new AccessModelException("Only server administrators assign repositories to projects.");

            await AccessAdmin.SetRepositoryProjectAsync(repository ?? string.Empty, Model.Name, Actor, cancellationToken);
            return $"Repository {repository} is now in {Model.Name}.";
        }, Back);

    /// <summary>Takes a repository out of the project (server administrators only).</summary>
    public Task<IActionResult> UnassignRepositoryAsync(string? repository, string? password, CancellationToken cancellationToken)
        => ActAsync(Model, password, confirm: true, async () =>
        {
            if (!IsServerAdministrator) throw new AccessModelException("Only server administrators take repositories out of projects.");

            await AccessAdmin.SetRepositoryProjectAsync(repository ?? string.Empty, null, Actor, cancellationToken);
            return $"Repository {repository} is no longer in {Model.Name}.";
        }, Back);

    /// <summary>Creates a permission target that applies only to the project's repositories.</summary>
    public async Task<IActionResult?> CreateTargetAsync(CancellationToken cancellationToken)
    {
        if (!await LoadAsync(cancellationToken)) return new NotFoundResult();

        try
        {
            var target = await AccessAdmin.CreateTargetAsync(
                Model.TargetName ?? string.Empty,
                AccessPatterns.Split(Model.RepositoryPatterns),
                AccessPatterns.Split(Model.IncludePatterns),
                AccessPatterns.Split(Model.ExcludePatterns),
                Model.Project!.Project.Key,
                Actor,
                cancellationToken);
            Model.Message = $"Permission target {target.Name} was created. Add grants to it.";

            return RedirectToScreen("/Admin/PermissionDetail", new { name = target.Name });
        }
        catch (AccessModelException e)
        {
            Model.Error = e.Message;

            return null;
        }
    }

    /// <summary>Previews the new target patterns without saving anything.</summary>
    public async Task<IActionResult?> PreviewPatternsAsync(CancellationToken cancellationToken)
    {
        if (!await LoadAsync(cancellationToken)) return new NotFoundResult();

        // Only the project's own repositories can be reached, whatever the patterns say.
        var preview = await PreviewAsync(
            AccessPatterns.Split(Model.RepositoryPatterns), AccessPatterns.Split(Model.IncludePatterns), AccessPatterns.Split(Model.ExcludePatterns), Model.TestPackage, cancellationToken);
        Model.Preview = preview with { Repositories = preview.Repositories.Where(r => Model.Project!.Repositories.Contains(r, StringComparer.Ordinal)).ToList() };

        return null;
    }

    /// <summary>Deletes the project (server administrators only) with its members and targets; repositories stay.</summary>
    public async Task<IActionResult> DeleteAsync(string? password, CancellationToken cancellationToken)
    {
        var deleted = false;
        var result = await ActAsync(Model, password, confirm: true, async () =>
        {
            if (!IsServerAdministrator) throw new AccessModelException("Only server administrators delete projects.");

            await AccessAdmin.DeleteProjectAsync(Model.Name ?? string.Empty, Actor, cancellationToken);
            deleted = true;
            return $"Project {Model.Name} was deleted.";
        }, Back);

        return deleted ? RedirectToScreen("/Admin/Projects") : result;
    }

    private IActionResult Back() => RedirectToScreen("/Admin/ProjectDetail", new { name = Model.Name });

    private async Task<bool> LoadAsync(CancellationToken cancellationToken)
    {
        Model.IsServerAdministrator = IsServerAdministrator;
        await LoadConfirmationAsync(Model);

        Model.Project = await AccessAdmin.GetProjectAsync(Model.Name ?? string.Empty, cancellationToken);
        if (Model.Project == null) return false;

        var all = await Services.GetRequiredService<IRepositoryService>().ListAsync(cancellationToken);
        Model.Assignable = all
            .Where(r => !Model.Project.Repositories.Contains(r.Name, StringComparer.Ordinal))
            .Select(r => r.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        return true;
    }
}

/// <summary>
/// The access audit log: one entry per change to groups, memberships, permission targets, grants, projects, the repositories' project
/// and anonymous-read switch, newest first, filtered by who made the change, what it was done to and the dates (UTC,
/// <c>yyyy-MM-dd</c>, both inclusive). Read only. Used by <c>AdminAuditController</c>.
/// </summary>
internal sealed class AuditWorkflow : AccessWorkflow
{
    /// <summary>How many entries the screen lists at most.</summary>
    public const int PageSize = 200;

    public AuditWorkflow(HttpContext httpContext, IUrlHelper url) : base(httpContext, url)
    {
    }

    /// <summary>The screen; the caller sets the filter fields from the query first.</summary>
    public AuditViewModel Model { get; } = new();

    /// <summary>Lists the matching entries.</summary>
    public async Task GetAsync(CancellationToken cancellationToken)
    {
        var from = ParseDay(Model.From, "From", 0);
        var to = ParseDay(Model.To, "To", 1);

        Model.Entries = await AccessAdmin.AuditAsync(new AuditFilter(Model.ActorFilter, Model.Subject, from, to), PageSize, cancellationToken);
    }

    private DateTime? ParseDay(string? text, string field, int addDays)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        if (DateTime.TryParseExact(text.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var day))
        {
            return day.AddDays(addDays);
        }

        Model.FilterError = $"{field} is not a date (use yyyy-MM-dd).";
        return null;
    }
}
