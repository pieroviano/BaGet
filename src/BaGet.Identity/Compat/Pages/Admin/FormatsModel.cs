using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.Workflows;

namespace BaGet.Identity.Pages.Admin;

/// <summary>
/// The page model of the formats page when it was a Razor Page: the package formats this server can host (the registered
/// <see cref="IFormatModule"/>s), with where each is served, which repositories can be created and which capabilities it has.
/// </summary>
/// <remarks>
/// Kept as a shim (no breaking change of the public API); <c>AdminOperationsController</c> serves <c>/admin/formats</c>. Both call
/// <c>OperationsWorkflow</c>.
/// </remarks>
[Obsolete("The formats screen is served by AdminOperationsController; this page model has no page any more and will be removed in the next major version.")]
public class FormatsModel : AdminPageModel
{
    /// <summary>One format with what it offers.</summary>
    public sealed record Row(IFormatModule Module, int Repositories, IReadOnlyList<string> Capabilities);

    private OperationsWorkflow? _workflow;

    private OperationsWorkflow Workflow => _workflow ??= new OperationsWorkflow(HttpContext, Url);

    /// <summary>The formats.</summary>
    public IReadOnlyList<Row> Rows => Workflow.Formats.Rows.Select(r => new Row(r.Module, r.Repositories, r.Capabilities)).ToList();

    /// <summary>Lists the formats.</summary>
    public async Task OnGetAsync(CancellationToken cancellationToken) => await Workflow.GetFormatsAsync(cancellationToken);
}
