using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace BaGet.Identity.Pages.Account;

/// <summary>
/// The signed-in user's own effective permissions: for each repository where they hold any action, which of Read,
/// Annotate, Deploy, Delete and Manage, and the permission targets or project roles that grant them (optionally for one
/// package ID, to see how include/exclude patterns apply). Repositories where the user holds nothing are not listed, so
/// the page discloses nothing about private repositories. Package ownership is a second requirement for changing
/// existing package IDs and is not shown here.
/// </summary>
public class AccessModel : AccountPageModel
{
    /// <inheritdoc />
    protected override bool RequiresSignIn => true;

    /// <summary>A package ID to evaluate the grants for; empty means the repository as a whole.</summary>
    [BindProperty(SupportsGet = true)]
    public string? Package { get; set; }

    /// <summary>The repositories where the user holds some action.</summary>
    public IReadOnlyList<RepositoryAccessRow> Rows { get; private set; } = Array.Empty<RepositoryAccessRow>();

    /// <summary>The projects the user administers.</summary>
    public IReadOnlyList<string> AdministeredProjects { get; private set; } = Array.Empty<string>();

    /// <summary>Whether the configured database has an access model at all.</summary>
    public bool ModelAvailable { get; private set; } = true;

    /// <summary>Shows the user's access.</summary>
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var services = HttpContext.RequestServices;
        var principal = await services.GetRequiredService<ISessionPrincipalFactory>().CreateAsync(User);
        if (principal == null) return Challenge();

        ModelAvailable = services.GetRequiredService<IContext>() is not NullContext;
        var evaluator = services.GetRequiredService<IAccessEvaluator>();
        var package = string.IsNullOrWhiteSpace(Package) ? null : Package.Trim();

        var rows = new List<RepositoryAccessRow>();
        foreach (var repository in await services.GetRequiredService<IRepositoryService>().ListAsync(cancellationToken))
        {
            if (repository.Format != RepositoryFormat.NuGet) continue;

            var explanation = await evaluator.ExplainAsync(principal, repository, package, cancellationToken);
            if (explanation.Actions != AccessAction.None) rows.Add(new RepositoryAccessRow(repository, explanation));
        }

        Rows = rows
            .OrderBy(r => r.Repository.IsDefault ? 0 : 1)
            .ThenBy(r => r.Repository.Name, StringComparer.Ordinal)
            .ToList();

        if (ModelAvailable && principal.UserId != null)
        {
            AdministeredProjects = (await services.GetRequiredService<IAccessAdministration>().ProjectRolesOfUserAsync(principal.UserId, cancellationToken))
                .Where(r => r.Role == ProjectRole.Admin)
                .Select(r => r.Project)
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        return Page();
    }
}
