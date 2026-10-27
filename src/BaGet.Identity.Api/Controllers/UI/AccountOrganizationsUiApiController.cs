using System.Threading;
using System.Threading.Tasks;
using BaGet.Identity.Application.Screens;
using BaGet.Identity.Controllers.Base;
using Microsoft.AspNetCore.Mvc;
using BaGet.Identity.Application.Models.Organizations;
using BaGet.Identity.Application.UseCases.Account;
using BaGet.Identity.Application.UseCases.Admin;
using BaGet.Identity.Controllers.FrontEnd;
using BaGet.Identity.Mvc.UI;

namespace BaGet.Identity.Controllers.UI;

/// <summary>
/// The JSON twin of the signed-in user's organizations (<c>/api/ui/account/organizations</c>): <see cref="AccountOrganizationsController"/> calls it
/// in process. Screen only: over HTTP it answers 403 to every caller.
/// </summary>
[Route("api/ui/account/organizations")]
public sealed class AccountOrganizationsUiApiController : AccountUiApiControllerBase
{
    /// <inheritdoc/>
    protected override bool RequiresSignIn => true;

    /// <summary>The organizations of the user.</summary>
    [HttpGet, UiApi(typeof(AccountOrganizationsController), nameof(AccountOrganizationsController.Organizations))]
    public async Task<ActionResult<ScreenResult<OrganizationsViewModel>>> Organizations(CancellationToken cancellationToken)
        => Respond(await UseCase<AccountOverviewUseCase>().ShowOrganizationsAsync(User, cancellationToken));

    /// <summary>Create an organization.</summary>
    [HttpPost("create"), UiApi(typeof(AccountOrganizationsController), nameof(AccountOrganizationsController.Organizations), Handler = "Create")]
    public async Task<ActionResult<ScreenResult<OrganizationsViewModel>>> OrganizationsCreate([FromBody] CreateOrganizationForm? form, CancellationToken cancellationToken)
        => Respond(await UseCase<AccountOverviewUseCase>().CreateOrganizationAsync(User, form, cancellationToken));

    /// <summary>Add a member (organization administrators only; 403 otherwise).</summary>
    [HttpPost("add-member"), UiApi(typeof(AccountOrganizationsController), nameof(AccountOrganizationsController.Organizations), Handler = "AddMember")]
    public async Task<ActionResult<ScreenResult<OrganizationsViewModel>>> OrganizationsAddMember([FromBody] AddOrganizationMemberForm? form, CancellationToken cancellationToken)
        => Respond(await UseCase<AccountOverviewUseCase>().AddMemberAsync(User, form, cancellationToken));

    /// <summary>Remove a member (an administrator removes anyone; any member may leave; 403 otherwise).</summary>
    [HttpPost("remove-member"), UiApi(typeof(AccountOrganizationsController), nameof(AccountOrganizationsController.Organizations), Handler = "RemoveMember")]
    public async Task<ActionResult<ScreenResult<OrganizationsViewModel>>> OrganizationsRemoveMember([FromBody] RemoveOrganizationMemberForm? form, CancellationToken cancellationToken)
        => Respond(await UseCase<AccountOverviewUseCase>().RemoveMemberAsync(User, form, cancellationToken));
}
