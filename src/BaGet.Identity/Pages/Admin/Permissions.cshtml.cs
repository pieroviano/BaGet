using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Identity.Pages.Admin;

/// <summary>What the pattern fields partial (<c>_PatternFields</c>) shows.</summary>
/// <param name="Repositories">The repository patterns, one per line.</param>
/// <param name="Include">The include patterns, one per line.</param>
/// <param name="Exclude">The exclude patterns, one per line.</param>
/// <param name="TestPackage">The test box's package ID.</param>
/// <param name="Preview">What the patterns select, or null before the first preview.</param>
public sealed record PatternFieldsView(string? Repositories, string? Include, string? Exclude, string? TestPackage, PatternPreview? Preview);

/// <summary>
/// Lists the permission targets (rules saying which repositories and package IDs their grants apply to) and creates
/// one. The patterns can be previewed before saving: the repositories they match, and whether a package ID typed in
/// the test box is included. Administrators only; a project's administrators create targets from the project page.
/// </summary>
public class PermissionsModel : AccessPageModel
{
    /// <summary>The new target's name.</summary>
    [BindProperty]
    public string? Name { get; set; }

    /// <summary>Repository names or <c>*</c> globs, one per line.</summary>
    [BindProperty]
    public string? RepositoryPatterns { get; set; }

    /// <summary>Package ID globs the target applies to, one per line; none means all.</summary>
    [BindProperty]
    public string? IncludePatterns { get; set; }

    /// <summary>Package ID globs the target leaves out, one per line.</summary>
    [BindProperty]
    public string? ExcludePatterns { get; set; }

    /// <summary>The project that administers the target, if any.</summary>
    [BindProperty]
    public string? Project { get; set; }

    /// <summary>A package ID to test the patterns with.</summary>
    [BindProperty]
    public string? TestPackage { get; set; }

    /// <summary>The targets.</summary>
    public IReadOnlyList<TargetSummary> Targets { get; private set; } = Array.Empty<TargetSummary>();

    /// <summary>The projects a target can belong to.</summary>
    public IReadOnlyList<ProjectSummary> Projects { get; private set; } = Array.Empty<ProjectSummary>();

    /// <summary>The preview of the form's patterns.</summary>
    public PatternPreview? Preview { get; private set; }

    /// <summary>The model of the pattern fields partial.</summary>
    public PatternFieldsView Fields => new(RepositoryPatterns, IncludePatterns, ExcludePatterns, TestPackage, Preview);

    /// <summary>Shows the targets.</summary>
    public Task OnGetAsync(CancellationToken cancellationToken) => LoadAsync(cancellationToken);

    /// <summary>Creates a target and opens it.</summary>
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        try
        {
            int? projectKey = null;
            if (!string.IsNullOrWhiteSpace(Project))
            {
                var project = await AccessAdmin.GetProjectAsync(Project.Trim(), cancellationToken)
                              ?? throw new AccessModelException($"There is no project named '{Project}'.");
                projectKey = project.Project.Key;
            }

            var target = await AccessAdmin.CreateTargetAsync(
                Name ?? string.Empty,
                AccessPatterns.Split(RepositoryPatterns),
                AccessPatterns.Split(IncludePatterns),
                AccessPatterns.Split(ExcludePatterns),
                projectKey,
                Actor,
                cancellationToken);
            Message = $"Permission target {target.Name} was created. Add grants to it.";

            return RedirectToPage("/Admin/PermissionDetail", new { name = target.Name });
        }
        catch (AccessModelException e)
        {
            Error = e.Message;
            await LoadAsync(cancellationToken);

            return Page();
        }
    }

    /// <summary>Previews the form's patterns without saving anything.</summary>
    public async Task<IActionResult> OnPostPreviewAsync(CancellationToken cancellationToken)
    {
        await LoadAsync(cancellationToken);
        Preview = await PreviewAsync(
            AccessPatterns.Split(RepositoryPatterns), AccessPatterns.Split(IncludePatterns), AccessPatterns.Split(ExcludePatterns), TestPackage, cancellationToken);

        return Page();
    }

    /// <summary>Re-creates the built-in groups and targets that are missing (the upgrade defaults).</summary>
    public Task<IActionResult> OnPostRestoreBuiltInsAsync(string? password, CancellationToken cancellationToken)
        => ActAsync(password, confirm: true, async () =>
        {
            await AccessAdmin.RestoreBuiltInsAsync(Actor, cancellationToken);
            return "The built-in groups and permission targets were restored where they were missing.";
        }, () => RedirectToPage("/Admin/Permissions"));

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        Targets = await AccessAdmin.ListTargetsAsync(cancellationToken);
        Projects = await AccessAdmin.ListProjectsAsync(cancellationToken);
    }
}
