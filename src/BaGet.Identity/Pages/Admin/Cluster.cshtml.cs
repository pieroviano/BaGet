using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.Extensions.DependencyInjection;

namespace BaGet.Identity.Pages.Admin;

/// <summary>
/// The cluster status for administrators (<c>Ha:Enabled</c>): the nodes that sent a heartbeat recently with their readiness, the job
/// leases held (which node runs what), the shared read-only state, and this node's own health checks (database, storage, the shared
/// store). A single node lists itself and says clustering is off. Read-only; the high-availability settings are on the Server settings
/// page and need a restart.
/// </summary>
public class ClusterModel : AdminPageModel
{
    /// <summary>The status read for the page.</summary>
    public ClusterStatus Status { get; private set; } = null!;

    /// <summary>Reads the status; <c>?refresh=true</c> runs the health checks again instead of using the last answer.</summary>
    public async Task OnGetAsync(bool refresh, CancellationToken cancellationToken)
    {
        var services = HttpContext.RequestServices;
        if (refresh) await services.GetRequiredService<ReadinessService>().CheckAsync(force: true, cancellationToken);

        Status = await services.GetRequiredService<IClusterStatusService>().GetAsync(cancellationToken);
    }
}
