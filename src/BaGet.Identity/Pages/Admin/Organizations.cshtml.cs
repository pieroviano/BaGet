using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace BaGet.Identity.Pages.Admin;

/// <summary>
/// Lists every organization with its members and the package IDs it owns, each linking to its page
/// (<see cref="OrganizationDetailModel"/>). Organizations are created by users (<c>/account/organizations</c>).
/// </summary>
public class OrganizationsModel : AdminPageModel
{
    /// <summary>The organizations, by name.</summary>
    public IReadOnlyList<OrganizationSummary> Organizations { get; private set; } = Array.Empty<OrganizationSummary>();

    /// <summary>Lists the organizations.</summary>
    public async Task OnGetAsync(CancellationToken cancellationToken)
        => Organizations = await Ownership.GetAllOrganizationsAsync(cancellationToken);
}
