using System.Threading;
using System.Threading.Tasks;
using BaGet.Identity.Application;
using BaGet.Identity.Application.Models;
using BaGet.Identity.Application.Screens;
using BaGet.Identity.Application.UseCases;
using BaGet.Identity.Controllers.Base;
using BaGet.Identity.Mvc;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Identity.Controllers.UI;

/// <summary>
/// The JSON twin of "My packages", "My access" and the "my profile" redirect (<c>/api/ui/account/packages</c>, <c>access</c>, <c>profile</c>):
/// <see cref="AccountPackagesController"/> calls it in process. Screen only: over HTTP it answers 403 to every caller.
/// </summary>
[Route("api/ui/account")]
public sealed class AccountPackagesUiApiController : AccountUiApiControllerBase
{
    /// <inheritdoc/>
    protected override bool RequiresSignIn => true;

    /// <summary>The package IDs the user owns, per repository.</summary>
    [HttpGet("packages"), UiApi("/Account/Packages")]
    public async Task<ActionResult<ScreenResult<AccountPackagesViewModel>>> Packages(CancellationToken cancellationToken)
        => Respond(await UseCase<AccountOverviewUseCase>().ShowPackagesAsync(User, cancellationToken));

    /// <summary>What the user may do in each repository (optionally for one package ID).</summary>
    [HttpGet("access"), UiApi("/Account/Access")]
    public async Task<ActionResult<ScreenResult<AccountAccessViewModel>>> Access(string? package, CancellationToken cancellationToken)
        => Respond(await UseCase<AccountOverviewUseCase>().ShowAccessAsync(User, package, cancellationToken));

    /// <summary>The user's own profile: continues at <c>next.localUrl</c> (<c>~/profiles/{name}</c>).</summary>
    [HttpGet("profile"), UiApi("/Account/Profile")]
    public ActionResult<ScreenResult<EmptyScreen>> MyProfile()
        => Respond(UseCase<AccountOverviewUseCase>().ShowMyProfile(User));
}
