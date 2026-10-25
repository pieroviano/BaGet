using System;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BaGet.Web;

/// <summary>The page model of the statistics page when it was a Razor Page.</summary>
/// <remarks>
/// Kept as a shim so that code compiled against it still loads and runs (no breaking change of the public API); there is no page
/// behind it any more: <see cref="StatisticsController"/> serves <c>/stats</c>. Both share <see cref="StatisticsWorkflow"/>.
/// </remarks>
[Obsolete("The statistics page is served by StatisticsController; this page model has no page any more and will be removed in the next major version.")]
public class StatisticsModel : PageModel
{
    private readonly StatisticsWorkflow _workflow;

    /// <summary>Creates the page model.</summary>
    public StatisticsModel(StatisticsService statistics)
    {
        _workflow = new StatisticsWorkflow(statistics ?? throw new ArgumentNullException(nameof(statistics)));
    }

    /// <summary>The feed's statistics; null when the database cannot report them.</summary>
    public FeedStatistics? Statistics { get; private set; }

    /// <summary>Loads the statistics.</summary>
    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Statistics = await _workflow.GetAsync(cancellationToken);
    }
}
