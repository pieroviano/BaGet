using System.Threading;
using System.Threading.Tasks;
using BaGet.Identity.Mvc;
using BaGet.Identity.ViewModels;
using BaGet.Identity.Workflows;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Identity.Controllers;

/// <summary>
/// The signed-in user's organizations: create one, and manage the members of those the user administers. It replaces the Razor
/// Page <c>Account/Organizations</c>: same URL, same <c>?handler=</c> forms.
/// </summary>
public sealed class AccountOrganizationsController : AccountControllerBase
{
    /// <inheritdoc/>
    protected override bool RequiresSignIn => true;

    /// <summary>The organizations of the user.</summary>
    [Route("account/organizations"), HttpGet, HttpHead, PageHandler, LegacyPage("/Account/Organizations")]
    public async Task<IActionResult> Organizations(CancellationToken cancellationToken)
    {
        var model = new OrganizationsViewModel();

        return Respond(await new OrganizationsWorkflow(Screen).ShowAsync(model, cancellationToken), "Organizations", model);
    }

    /// <summary>Create an organization.</summary>
    [Route("account/organizations"), HttpPost, PageHandler("Create"), LegacyPage("/Account/Organizations")]
    public async Task<IActionResult> OrganizationsCreate(string name, CancellationToken cancellationToken)
    {
        var model = new OrganizationsViewModel();

        return Respond(await new OrganizationsWorkflow(Screen).CreateAsync(model, name, cancellationToken), "Organizations", model);
    }

    /// <summary>Add a member (organization administrators only).</summary>
    [Route("account/organizations"), HttpPost, PageHandler("AddMember"), LegacyPage("/Account/Organizations")]
    public async Task<IActionResult> OrganizationsAddMember(string organization, string userName, bool isAdmin, CancellationToken cancellationToken)
    {
        var model = new OrganizationsViewModel();

        return Respond(await new OrganizationsWorkflow(Screen).AddMemberAsync(model, organization, userName, isAdmin, cancellationToken), "Organizations", model);
    }

    /// <summary>Remove a member (an administrator removes anyone; any member may leave).</summary>
    [Route("account/organizations"), HttpPost, PageHandler("RemoveMember"), LegacyPage("/Account/Organizations")]
    public async Task<IActionResult> OrganizationsRemoveMember(string organization, string? userName, CancellationToken cancellationToken)
    {
        var model = new OrganizationsViewModel();

        return Respond(await new OrganizationsWorkflow(Screen).RemoveMemberAsync(model, organization, userName, cancellationToken), "Organizations", model);
    }
}

/// <summary>
/// The signed-in user's own packages and permissions: "My packages", "My access" and the redirect to the user's profile. It
/// replaces the Razor Pages <c>Account/Packages</c>, <c>Account/Access</c> and <c>Account/Profile</c>: same URLs. Every action needs a signed-in user.
/// </summary>
public sealed class AccountPackagesController : AccountControllerBase
{
    /// <inheritdoc/>
    protected override bool RequiresSignIn => true;

    /// <summary>The package IDs the user owns, per repository.</summary>
    [Route("account/packages"), HttpGet, HttpHead, PageHandler, LegacyPage("/Account/Packages")]
    public async Task<IActionResult> Packages(CancellationToken cancellationToken)
    {
        var model = new AccountPackagesViewModel();

        return Respond(await new AccountPackagesWorkflow(Screen).ShowAsync(model, cancellationToken), "Packages", model);
    }

    /// <summary>What the user may do in each repository.</summary>
    [Route("account/access"), HttpGet, HttpHead, PageHandler, LegacyPage("/Account/Access")]
    public async Task<IActionResult> Access(string? package, CancellationToken cancellationToken)
    {
        var model = new AccountAccessViewModel { Package = package };

        return Respond(await new AccountAccessWorkflow(Screen).ShowAsync(model, cancellationToken), "Access", model);
    }

    /// <summary>The signed-in user's own profile: forwards to <c>/profiles/{name}</c>.</summary>
    [Route("account/profile"), HttpGet, HttpHead, PageHandler, LegacyPage("/Account/Profile")]
    public IActionResult MyProfile() => Respond(new ProfileWorkflow(Screen).Mine(), "MyProfile");
}

/// <summary>
/// A user's or an organization's public page: the packages it owns (<c>/profiles/{name}</c>, and under
/// <c>/r/{repo}/</c> too). It replaces the Razor Page <c>Profiles/Profile</c>.
/// </summary>
[RepositoryPage]
public sealed class ProfileController : AccountControllerBase
{
    /// <summary>The profile named <paramref name="name"/>; 404 when neither a user nor an organization has it.</summary>
    [Route("profiles/{name}"), HttpGet, HttpHead, PageHandler, LegacyPage("/Profiles/Profile")]
    public async Task<IActionResult> Profile(string? name, CancellationToken cancellationToken)
    {
        var model = new ProfileViewModel { Name = name };

        return Respond(await new ProfileWorkflow(Screen).ShowAsync(model, cancellationToken), "Profile", model);
    }
}
