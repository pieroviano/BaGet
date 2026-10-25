using System;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Identity.Mvc;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Web;

/// <summary>
/// The feed's statistics (<c>/stats</c>, and <c>/r/{repo}/stats</c>; 404 in a virtual repository). Replaces the <c>/Statistics</c>
/// Razor Page.
/// </summary>
[RepositoryPage(NotOnVirtual = true)]
public sealed class StatisticsController : UiControllerBase
{
    /// <summary>The statistics page.</summary>
    [Route("stats"), HttpGet, HttpHead, PageHandler, LegacyPage("/Statistics")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
        => Render(await Api<StatisticsUiApiController, StatisticsViewModel>(api => api.Get(cancellationToken)), "Index");
}
