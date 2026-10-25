using System;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Identity.Application;
using BaGet.Identity.Mvc;
using BaGet.Web.Application;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Web;

/// <summary>
/// The JSON twin of the statistics screen: <c>GET /api/ui/stats</c>, and <c>GET /api/ui/r/{repo}/stats</c> for another repository (404 in a
/// virtual one, like the page). <see cref="StatisticsController"/> calls it in process and renders the view.
/// </summary>
[Route("api/ui")]
[RepositoryPage(NotOnVirtual = true)]
public sealed class StatisticsUiApiController : UiApiControllerBase
{
    private readonly StatisticsUseCase _statistics;

    /// <summary>Creates the controller.</summary>
    public StatisticsUiApiController(StatisticsUseCase statistics)
    {
        _statistics = statistics ?? throw new ArgumentNullException(nameof(statistics));
    }

    /// <summary>The feed's statistics.</summary>
    [HttpGet("stats"), UiApi("/Statistics")]
    public async Task<ActionResult<ScreenResult<StatisticsViewModel>>> Get(CancellationToken cancellationToken)
        => Respond(await _statistics.GetAsync(cancellationToken));
}
