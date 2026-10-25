using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Identity.ViewModels;
using BaGet.Identity.Workflows;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Identity.Pages.Account;

/// <summary>
/// The page model of the organizations page when it was a Razor Page: the signed in user's organizations, create one, and manage
/// the members of those the user administers.
/// </summary>
/// <remarks>
/// Kept as a shim (no breaking change of the public API); <c>AccountOrganizationsController</c> serves <c>/account/organizations</c> and
/// delegates to the same <c>OrganizationsWorkflow</c>.
/// </remarks>
[Obsolete("The organizations screen is served by AccountOrganizationsController; this page model has no page any more and will be removed in the next major version.")]
public class OrganizationsModel : AccountPageModel
{
    private readonly OrganizationsViewModel _state = new OrganizationsViewModel();

    protected override bool RequiresSignIn => true;

    public IReadOnlyList<OrganizationView> Organizations
        => _state.Organizations.Select(o => new OrganizationView { Name = o.Name, IsAdmin = o.IsAdmin, Members = o.Members }).ToList();

    public string? Message => _state.Message;

    public async Task OnGetAsync(CancellationToken cancellationToken)
        => await new OrganizationsWorkflow(Screen).ShowAsync(_state, cancellationToken);

    public async Task<IActionResult> OnPostCreateAsync(string name, CancellationToken cancellationToken)
        => OutcomeResult(await new OrganizationsWorkflow(Screen).CreateAsync(_state, name, cancellationToken));

    public async Task<IActionResult> OnPostAddMemberAsync(string organization, string userName, bool isAdmin, CancellationToken cancellationToken)
        => OutcomeResult(await new OrganizationsWorkflow(Screen).AddMemberAsync(_state, organization, userName, isAdmin, cancellationToken));

    public async Task<IActionResult> OnPostRemoveMemberAsync(string organization, string? userName, CancellationToken cancellationToken)
        => OutcomeResult(await new OrganizationsWorkflow(Screen).RemoveMemberAsync(_state, organization, userName, cancellationToken));

    public class OrganizationView
    {
        public string? Name { get; set; }

        public bool IsAdmin { get; set; }

        public IReadOnlyList<(string UserName, bool IsAdmin)>? Members { get; set; }
    }
}
