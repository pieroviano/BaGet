using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Identity.Workflows;

namespace BaGet.Identity.Pages.Admin;

/// <summary>
/// The page model of the organizations page when it was a Razor Page: lists every organization with its members and the package IDs
/// it owns, each linking to its page. Organizations are created by users (<c>/account/organizations</c>).
/// </summary>
/// <remarks>
/// Kept as a shim (no breaking change of the public API); <c>AdminOrganizationsController</c> serves <c>/admin/organizations</c>.
/// Both call <c>OrganizationsWorkflow</c>.
/// </remarks>
[Obsolete("The organizations screen is served by AdminOrganizationsController; this page model has no page any more and will be removed in the next major version.")]
public class OrganizationsModel : AdminPageModel
{
    private OrganizationsWorkflow? _workflow;

    private OrganizationsWorkflow Workflow => _workflow ??= new OrganizationsWorkflow(HttpContext, Url);

    /// <summary>The organizations, by name.</summary>
    public IReadOnlyList<OrganizationSummary> Organizations => Workflow.List.Organizations;

    /// <summary>Lists the organizations.</summary>
    public async Task OnGetAsync(CancellationToken cancellationToken) => await Workflow.ListAsync(cancellationToken);
}
