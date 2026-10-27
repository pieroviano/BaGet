using System.Threading;
using System.Threading.Tasks;
using BaGet.Identity.Application.Screens;
using BaGet.Identity.Controllers.Base;
using Microsoft.AspNetCore.Mvc;
using BaGet.Identity.Application.Models.Account;
using BaGet.Identity.Application.UseCases.Account;
using BaGet.Identity.Controllers.FrontEnd;
using BaGet.Identity.Mvc.Attributes;
using BaGet.Identity.Mvc.UI;

namespace BaGet.Identity.Controllers.UI;

/// <summary>
/// The JSON twin of a user's or an organization's public page (<c>/api/ui/profiles/{name}</c>, and <c>/api/ui/r/{repo}/profiles/{name}</c>):
/// <see cref="ProfileController"/> calls it in process. The page is public data (names, owned package IDs with their downloads, an avatar address),
/// so unlike the other account screens an API key may read it too.
/// </summary>
[Route("api/ui/profiles")]
[RepositoryPage]
public sealed class ProfileUiApiController : AccountUiApiControllerBase
{
    /// <inheritdoc/>
    protected override bool CookieOnly => false;

    /// <summary>The profile named <paramref name="name"/>; 404 when neither a user nor an organization has it.</summary>
    [HttpGet("{name}"), UiApi(typeof(ProfileController), nameof(ProfileController.Profile))]
    public async Task<ActionResult<ScreenResult<ProfileViewModel>>> Profile(string? name, CancellationToken cancellationToken)
        => Respond(await UseCase<ProfileUseCase>().ShowAsync(User, name, cancellationToken));
}
