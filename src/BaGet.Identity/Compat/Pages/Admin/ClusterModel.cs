using System;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.Workflows;

namespace BaGet.Identity.Pages.Admin;

/// <summary>
/// The page model of the cluster page when it was a Razor Page: the cluster status for administrators (<c>Ha:Enabled</c>), read-only.
/// </summary>
/// <remarks>
/// Kept as a shim (no breaking change of the public API); <c>AdminOperationsController</c> serves <c>/admin/cluster</c>. Both call
/// <c>OperationsWorkflow</c>.
/// </remarks>
[Obsolete("The cluster screen is served by AdminOperationsController; this page model has no page any more and will be removed in the next major version.")]
public class ClusterModel : AdminPageModel
{
    private OperationsWorkflow? _workflow;

    private OperationsWorkflow Workflow => _workflow ??= new OperationsWorkflow(HttpContext, Url);

    /// <summary>The status read for the page.</summary>
    public ClusterStatus Status => Workflow.Cluster.Status;

    /// <summary>Reads the status; <c>?refresh=true</c> runs the health checks again instead of using the last answer.</summary>
    public async Task OnGetAsync(bool refresh, CancellationToken cancellationToken)
        => await Workflow.GetClusterAsync(refresh, cancellationToken);
}
