using System.Threading;
using System.Threading.Tasks;
using BaGet.Identity.Application.Screens;
using BaGet.Identity.Controllers.Base;
using Microsoft.AspNetCore.Mvc;
using BaGet.Identity.Application.Models.Packages;
using BaGet.Identity.Application.UseCases.Packages;
using BaGet.Identity.Controllers.FrontEnd;
using BaGet.Identity.Mvc.Attributes;
using BaGet.Identity.Mvc.UI;

namespace BaGet.Identity.Controllers.UI;

/// <summary>
/// The JSON twin of the ownerless packages screen (<c>GET /api/ui/admin/packages</c>, and <c>/api/ui/r/{repo}/admin/packages</c> like the page): the package
/// IDs that have no owner, in every repository. <see cref="AdminPackagesController"/> calls it in process. (The packages of one user are
/// <see cref="AdminUsersUiApiController.GetPackages"/>.)
/// </summary>
[Route("api/ui/admin/packages")]
public sealed class AdminPackagesUiApiController : AdminUiApiControllerBase
{
    /// <summary>Lists the ownerless IDs of every repository, <paramref name="pageNumber"/> being one-based.</summary>
    [HttpGet(""), UiApi("/Admin/Packages"), RepositoryPage]
    public async Task<ActionResult<ScreenResult<AdminPackagesViewModel>>> List([FromQuery(Name = "p")] int pageNumber = 1, CancellationToken cancellationToken = default)
        => Respond(await UseCase<AdminPackagesUseCase>().GetOwnerlessAsync(pageNumber, cancellationToken));
}
