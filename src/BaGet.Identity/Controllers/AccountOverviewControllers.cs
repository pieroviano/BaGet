using System.Threading;
using System.Threading.Tasks;
using BaGet.Identity.Application;
using BaGet.Identity.Application.Models;
using BaGet.Identity.Application.Screens;
using BaGet.Identity.Application.UseCases;
using BaGet.Identity.Controllers.Base;
using BaGet.Identity.Controllers.UI;
using BaGet.Identity.Mvc;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Identity.Controllers;

/// <summary>
/// The signed-in user's organizations: create one, and manage the members of those the user administers. It replaces the Razor
/// Page <c>Account/Organizations</c>: same URL, same <c>?handler=</c> forms. The work is done by <see cref="AccountOrganizationsUiApiController"/>.
/// </summary>
public sealed class AccountOrganizationsController : AccountControllerBase
{
    /// <inheritdoc/>
    protected override bool RequiresSignIn => true;

    /// <summary>The organizations of the user.</summary>
    [Route("account/organizations"), HttpGet, HttpHead, PageHandler, LegacyPage("/Account/Organizations")]
    public async Task<IActionResult> Organizations(CancellationToken cancellationToken)
        => Render(await Api<AccountOrganizationsUiApiController, OrganizationsViewModel>(api => api.Organizations(cancellationToken)), "Organizations");

    /// <summary>Create an organization.</summary>
    [Route("account/organizations"), HttpPost, PageHandler("Create"), LegacyPage("/Account/Organizations")]
    public async Task<IActionResult> OrganizationsCreate(string name, CancellationToken cancellationToken)
        => Render(
            await Api<AccountOrganizationsUiApiController, OrganizationsViewModel>(api => api.OrganizationsCreate(new CreateOrganizationForm { Name = name }, cancellationToken)),
            "Organizations");

    /// <summary>Add a member (organization administrators only).</summary>
    [Route("account/organizations"), HttpPost, PageHandler("AddMember"), LegacyPage("/Account/Organizations")]
    public async Task<IActionResult> OrganizationsAddMember(string organization, string userName, bool isAdmin, CancellationToken cancellationToken)
        => Render(
            await Api<AccountOrganizationsUiApiController, OrganizationsViewModel>(api => api.OrganizationsAddMember(
                new AddOrganizationMemberForm { Organization = organization, UserName = userName, IsAdmin = isAdmin },
                cancellationToken)),
            "Organizations");

    /// <summary>Remove a member (an administrator removes anyone; any member may leave).</summary>
    [Route("account/organizations"), HttpPost, PageHandler("RemoveMember"), LegacyPage("/Account/Organizations")]
    public async Task<IActionResult> OrganizationsRemoveMember(string organization, string? userName, CancellationToken cancellationToken)
        => Render(
            await Api<AccountOrganizationsUiApiController, OrganizationsViewModel>(api => api.OrganizationsRemoveMember(
                new RemoveOrganizationMemberForm { Organization = organization, UserName = userName },
                cancellationToken)),
            "Organizations");
}

/// <summary>
/// The signed-in user's own packages and permissions: "My packages", "My access" and the redirect to the user's profile. It
/// replaces the Razor Pages <c>Account/Packages</c>, <c>Account/Access</c> and <c>Account/Profile</c>: same URLs. Every action needs a signed-in user.
/// The work is done by <see cref="AccountPackagesUiApiController"/>.
/// </summary>
public sealed class AccountPackagesController : AccountControllerBase
{
    /// <inheritdoc/>
    protected override bool RequiresSignIn => true;

    /// <summary>The package IDs the user owns, per repository.</summary>
    [Route("account/packages"), HttpGet, HttpHead, PageHandler, LegacyPage("/Account/Packages")]
    public async Task<IActionResult> Packages(CancellationToken cancellationToken)
        => Render(await Api<AccountPackagesUiApiController, AccountPackagesViewModel>(api => api.Packages(cancellationToken)), "Packages");

    /// <summary>What the user may do in each repository.</summary>
    [Route("account/access"), HttpGet, HttpHead, PageHandler, LegacyPage("/Account/Access")]
    public async Task<IActionResult> Access(string? package, CancellationToken cancellationToken)
        => Render(await Api<AccountPackagesUiApiController, AccountAccessViewModel>(api => api.Access(package, cancellationToken)), "Access");

    /// <summary>The signed-in user's own profile: forwards to <c>/profiles/{name}</c>.</summary>
    [Route("account/profile"), HttpGet, HttpHead, PageHandler, LegacyPage("/Account/Profile")]
    public async Task<IActionResult> MyProfile()
        => Render(await Api<AccountPackagesUiApiController, EmptyScreen>(api => api.MyProfile()), "MyProfile", _ => null);
}

/// <summary>
/// A user's or an organization's public page: the packages it owns (<c>/profiles/{name}</c>, and under
/// <c>/r/{repo}/</c> too). It replaces the Razor Page <c>Profiles/Profile</c>. The work is done by <see cref="ProfileUiApiController"/>.
/// </summary>
[RepositoryPage]
public sealed class ProfileController : AccountControllerBase
{
    /// <summary>The profile named <paramref name="name"/>; 404 when neither a user nor an organization has it.</summary>
    [Route("profiles/{name}"), HttpGet, HttpHead, PageHandler, LegacyPage("/Profiles/Profile")]
    public async Task<IActionResult> Profile(string? name, CancellationToken cancellationToken)
        => Render(await Api<ProfileUiApiController, ProfileViewModel>(api => api.Profile(name, cancellationToken)), "Profile");
}
