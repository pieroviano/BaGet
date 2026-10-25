using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.Workflows;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Identity.Pages.Admin;

/// <summary>What the password confirmation partial needs.</summary>
/// <param name="Id">The input's id.</param>
/// <param name="HasPassword">Whether the administrator has a password to type.</param>
/// <param name="Reauthenticated">For an account without a password: it signed in again a moment ago.</param>
/// <param name="CanReauthenticate">Whether a provider is available to sign in again with.</param>
/// <param name="ReturnPage">The page to come back to after signing in again.</param>
public record ConfirmPasswordView(string Id, bool HasPassword, bool Reauthenticated, bool CanReauthenticate, string ReturnPage);

/// <summary>A member as the organization page lists them.</summary>
/// <param name="UserName">The user name.</param>
/// <param name="IsAdmin">Whether the member administers the organization.</param>
public record OrganizationMemberRow(string? UserName, bool IsAdmin);

/// <summary>
/// The page model of the organization page when it was a Razor Page: rename it, add, promote and remove members, delete it.
/// </summary>
/// <remarks>
/// Kept as a shim (no breaking change of the public API); <c>AdminOrganizationsController</c> serves
/// <c>/admin/organizations/{name}</c>. Both call <c>OrganizationsWorkflow</c>.
/// </remarks>
[Obsolete("The organization screen is served by AdminOrganizationsController; this page model has no page any more and will be removed in the next major version.")]
public class OrganizationDetailModel : AdminPageModel
{
    private OrganizationsWorkflow? _workflow;

    private OrganizationsWorkflow Workflow => _workflow ??= new OrganizationsWorkflow(HttpContext, Url);

    /// <summary>The organization's name (route).</summary>
    [BindProperty(SupportsGet = true)]
    public string? Name { get => Workflow.Model.Name; set => Workflow.Model.Name = value; }

    /// <summary>The organization.</summary>
    public Organization? Organization => Workflow.Model.Organization;

    /// <summary>Its members, by name.</summary>
    public IReadOnlyList<OrganizationMemberRow> Members => Workflow.Model.Members;

    /// <summary>How many package IDs it owns, in every repository.</summary>
    public int OwnedPackageIds => Workflow.Model.OwnedPackageIds;

    /// <summary>Whether the signed in administrator has a password to confirm with.</summary>
    public bool HasPassword => Workflow.Model.HasPassword;

    /// <summary>For an account without a password: it signed in again with its provider a moment ago.</summary>
    public bool Reauthenticated => Workflow.Model.Reauthenticated;

    /// <summary>Whether an external provider is available to sign in again with.</summary>
    public bool CanReauthenticate => Accounts.OpenIdConnect.IsConfigured;

    /// <summary>The model of the password confirmation partial, with <paramref name="id"/> for the input.</summary>
    public ConfirmPasswordView Confirmation(string id)
        => new ConfirmPasswordView(id, HasPassword, Reauthenticated, CanReauthenticate, "/Admin/OrganizationDetail");

    /// <summary>Outcome of the last action.</summary>
    [TempData]
    public string? Message { get => Workflow.Model.Message; set => Workflow.Model.Message = value; }

    /// <summary>Why the last action was refused.</summary>
    [TempData]
    public string? Error { get => Workflow.Model.Error; set => Workflow.Model.Error = value; }

    /// <summary>Shows the organization.</summary>
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
        => await Workflow.GetAsync(cancellationToken) ?? Page();

    /// <summary>Renames the organization.</summary>
    public async Task<IActionResult> OnPostRenameAsync(string? newName, string? password, CancellationToken cancellationToken)
        => await Workflow.RenameAsync(newName, password, cancellationToken) ?? Page();

    /// <summary>Deletes the organization.</summary>
    public async Task<IActionResult> OnPostDeleteAsync(string? password, CancellationToken cancellationToken)
        => await Workflow.DeleteAsync(password, cancellationToken) ?? Page();

    /// <summary>Adds a user to the organization.</summary>
    public async Task<IActionResult> OnPostAddMemberAsync(string? userName, bool isAdmin, string? password, CancellationToken cancellationToken)
        => await Workflow.AddMemberAsync(userName, isAdmin, password, cancellationToken) ?? Page();

    /// <summary>Makes a member an administrator of the organization, or not.</summary>
    public async Task<IActionResult> OnPostSetAdminAsync(string? userName, bool isAdmin, string? password, CancellationToken cancellationToken)
        => await Workflow.SetAdminAsync(userName, isAdmin, password, cancellationToken) ?? Page();

    /// <summary>Removes a member from the organization.</summary>
    public async Task<IActionResult> OnPostRemoveMemberAsync(string? userName, string? password, CancellationToken cancellationToken)
        => await Workflow.RemoveMemberAsync(userName, password, cancellationToken) ?? Page();
}
