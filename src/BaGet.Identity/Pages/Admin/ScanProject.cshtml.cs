using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace BaGet.Identity.Pages.Admin;

/// <summary>
/// The scanning policy of a project: it applies to every repository of the project that has no policy of its own. Saving asks
/// the administrator's password again, like the repository's policy; administrators only.
/// </summary>
public class ScanProjectModel : AccessPageModel
{
    /// <summary>The project's name (route).</summary>
    [BindProperty(SupportsGet = true)]
    public string? Name { get; set; }

    /// <summary>The project.</summary>
    public ProjectDetails? Project { get; private set; }

    /// <summary>The policy stored for the project, or null.</summary>
    public ScanPolicy? Own { get; private set; }

    /// <summary>The fields of the policy editor.</summary>
    public ScanPolicyFormView PolicyForm => new(ScanPolicyFields.From(Own), Confirmation("scan-project-password"), "scan-project");

    /// <summary>Shows the project's policy.</summary>
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!await LoadAsync(cancellationToken)) return NotFound();

        return Page();
    }

    /// <summary>Saves the project's policy; empty values remove it.</summary>
    public async Task<IActionResult> OnPostPolicyAsync(string? warn, string? blockDownload, string? blockDeploy, bool blockUnscanned, string? password, CancellationToken cancellationToken)
    {
        var project = await AccessAdmin.GetProjectAsync(Name ?? string.Empty, cancellationToken);
        if (project == null) return NotFound();

        return await ActAsync(password, confirm: true, async () =>
        {
            var values = new ScanPolicyFields { Warn = warn, BlockDownload = blockDownload, BlockDeploy = blockDeploy, BlockUnscanned = blockUnscanned }.ToValues();
            await HttpContext.RequestServices.GetRequiredService<IScanPolicyService>()
                .SaveAsync(ScanPolicyScope.Project, project.Project.Key, values, Actor, cancellationToken);

            return values.IsEmpty ? $"The scanning policy of {project.Project.Name} was removed." : $"The scanning policy of {project.Project.Name} was saved.";
        }, () => RedirectToPage("/Admin/ScanProject", new { name = Name }));
    }

    private async Task<bool> LoadAsync(CancellationToken cancellationToken)
    {
        Project = await AccessAdmin.GetProjectAsync(Name ?? string.Empty, cancellationToken);
        if (Project == null) return false;

        Own = await HttpContext.RequestServices.GetRequiredService<IScanPolicyService>().GetAsync(ScanPolicyScope.Project, Project.Project.Key, cancellationToken);

        return true;
    }
}
