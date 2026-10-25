using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.Workflows;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Identity.Pages.Admin;

/// <summary>A package ID as the administrator pages list it.</summary>
/// <param name="Package">The ID summarised across its versions.</param>
/// <param name="Owners">The names of its owners; empty for an ownerless ID.</param>
/// <param name="RepositoryName">The repository the ID is in; ownership and versions are per repository.</param>
public record AdminPackageRow(
    Account.PackagesModel.OwnedPackage Package,
    IReadOnlyList<string> Owners,
    string RepositoryName = Repository.DefaultName);

/// <summary>
/// The page model of the user packages page when it was a Razor Page: the package IDs one user owns, directly or through an
/// organization, with a way into each one's management page.
/// </summary>
/// <remarks>
/// Kept as a shim (no breaking change of the public API); <c>AdminUsersController.UserPackages</c> serves
/// <c>/admin/users/{name}/packages</c>. Both call <c>AdminPackagesWorkflow</c>.
/// </remarks>
[Obsolete("The user packages screen is served by AdminUsersController; this page model has no page any more and will be removed in the next major version.")]
public class UserPackagesModel : AdminPageModel
{
    private AdminPackagesWorkflow? _workflow;

    private AdminPackagesWorkflow Workflow => _workflow ??= new AdminPackagesWorkflow(HttpContext, Url);

    /// <summary>The user name, from the route.</summary>
    [BindProperty(SupportsGet = true)]
    public string? Name { get => Workflow.OfUser.Name; set => Workflow.OfUser.Name = value; }

    /// <summary>The user's packages.</summary>
    public IReadOnlyList<AdminPackageRow> Packages => Workflow.OfUser.Packages;

    /// <summary>Lists the user's packages; 404 for an unknown user.</summary>
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
        => await Workflow.GetUserAsync(cancellationToken) ?? Page();
}
