using System;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Identity.Mvc;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Web;

/// <summary>
/// The feed's statistics (<c>/stats</c>, and <c>/r/{repo}/stats</c>; 404 in a virtual repository). Replaces the <c>/Statistics</c>
/// Razor Page (<see cref="StatisticsModel"/> is now an obsolete shim).
/// </summary>
[RepositoryPage(NotOnVirtual = true)]
public sealed class StatisticsController : UiControllerBase
{
    private readonly StatisticsWorkflow _workflow;

    /// <summary>Creates the controller.</summary>
    public StatisticsController(StatisticsWorkflow workflow)
    {
        _workflow = workflow ?? throw new ArgumentNullException(nameof(workflow));
    }

    /// <summary>The statistics page.</summary>
    [Route("stats"), HttpGet, HttpHead, PageHandler, LegacyPage("/Statistics")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
        => View("Index", new StatisticsViewModel(await _workflow.GetAsync(cancellationToken)));
}
