using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Identity.Pages.Admin;

/// <summary>
/// Lists the projects (groups of repositories with their own administrators) and creates one. Administrators see
/// every project and create new ones; a project's administrators see the projects they administer.
/// </summary>
public class ProjectsModel : AccessPageModel
{
    /// <summary>The new project's name.</summary>
    [BindProperty]
    public string? Name { get; set; }

    /// <summary>The new project's display name.</summary>
    [BindProperty]
    public string? DisplayName { get; set; }

    /// <summary>The projects the signed-in user may open.</summary>
    public IReadOnlyList<ProjectSummary> Projects { get; private set; } = Array.Empty<ProjectSummary>();

    /// <inheritdoc />
    protected override async Task<bool> AuthorizeAsync(CancellationToken cancellationToken)
        => IsServerAdministrator || await AdministeredAsync(cancellationToken) is { Count: > 0 };

    /// <summary>Shows the projects.</summary>
    public Task OnGetAsync(CancellationToken cancellationToken) => LoadAsync(cancellationToken);

    /// <summary>Creates a project (administrators only) and opens it.</summary>
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!IsServerAdministrator) return NotFound();

        try
        {
            var project = await AccessAdmin.CreateProjectAsync(Name ?? string.Empty, string.IsNullOrWhiteSpace(DisplayName) ? null : DisplayName.Trim(), Actor, cancellationToken);
            Message = $"Project {project.Name} was created.";

            return RedirectToPage("/Admin/ProjectDetail", new { name = project.Name });
        }
        catch (AccessModelException e)
        {
            Error = e.Message;
            await LoadAsync(cancellationToken);

            return Page();
        }
    }

    private async Task<IReadOnlyList<string>> AdministeredAsync(CancellationToken cancellationToken)
        => CurrentUserId == null
            ? Array.Empty<string>()
            : (await AccessAdmin.ProjectRolesOfUserAsync(CurrentUserId, cancellationToken))
                .Where(r => r.Role == ProjectRole.Admin).Select(r => r.Project).Distinct(StringComparer.Ordinal).ToList();

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        var all = await AccessAdmin.ListProjectsAsync(cancellationToken);
        if (IsServerAdministrator)
        {
            Projects = all;
            return;
        }

        var mine = new HashSet<string>(await AdministeredAsync(cancellationToken), StringComparer.Ordinal);
        Projects = all.Where(p => mine.Contains(p.Name)).ToList();
    }
}
