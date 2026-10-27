using System.Threading;
using System.Threading.Tasks;
using BaGet.Identity.Controllers.Base;
using BaGet.Web.V3.Controllers.UI;
using Microsoft.AspNetCore.Mvc;
using BaGet.Identity.Mvc.Attributes;
using BaGet.Web.Application.Screens;
using BaGet.Web.V3.ViewModels;

namespace BaGet.Web.V3.Controllers.FrontEnd;

/// <summary>
/// The page of a package (<c>/packages/{id}/{version?}</c>, and under <c>/r/{repo}/</c>): the requested version, else the newest
/// listed one. Replaces the <c>/Package</c> Razor Page. The access rule on the
/// package (Read) is the repository check of <see cref="RepositoryPageAttribute"/>. Renders what <see cref="PackageUiApiController"/> answers.
/// </summary>
[RepositoryPage]
public sealed class PackageController : UiControllerBase
{
    /// <summary>Shows the package.</summary>
    [Route("packages/{id}/{version?}"), HttpGet, HttpHead, PageHandler]
    public async Task<IActionResult> Show(string id, string? version, CancellationToken cancellationToken)
        => Render(await Api<PackageUiApiController, PackageScreen>(api => api.Get(id, version, cancellationToken)), "Show", PackageViewModel.From);
}
