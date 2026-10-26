using System.Threading.Tasks;
using BaGet.Identity.Mvc;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Web;

/// <summary>
/// The administration REST API (<c>/api/v1</c>) for people (<c>/admin/api</c>): where it is, how to authenticate (the key of an
/// administrator with the <c>admin:api</c> scope, or the global key), the password confirmation header, and every operation with the
/// page that offers the same operation. Read-only, administrators only. Replaces the <c>/Admin/Api</c> Razor Page; renders what
/// <see cref="ApiCatalogUiApiController"/> answers.
/// </summary>
public sealed class ApiCatalogController : AdminControllerBase
{
    /// <summary>Lists the operations.</summary>
    [Route("admin/api"), HttpGet, HttpHead, PageHandler, LegacyPage("/Admin/Api")]
    public async Task<IActionResult> Index()
        => Render(await Api<ApiCatalogUiApiController, ApiCatalogViewModel>(api => api.Get()), "Index");
}
