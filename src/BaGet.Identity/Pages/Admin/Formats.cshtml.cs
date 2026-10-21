using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.Extensions.DependencyInjection;

namespace BaGet.Identity.Pages.Admin;

/// <summary>
/// Lists the package formats this server can host (the registered <see cref="IFormatModule"/>s): where each is served, which
/// kinds of repository can be created, which storage folders it owns, which capabilities it implements and how many
/// repositories use it. Read-only, administrators only; creating a repository of a format is on the Repositories page.
/// </summary>
public class FormatsModel : AdminPageModel
{
    /// <summary>One format with what it offers.</summary>
    public sealed record Row(IFormatModule Module, int Repositories, IReadOnlyList<string> Capabilities);

    /// <summary>The formats.</summary>
    public IReadOnlyList<Row> Rows { get; private set; } = Array.Empty<Row>();

    /// <summary>Lists the formats.</summary>
    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var services = HttpContext.RequestServices;
        var repositories = await services.GetRequiredService<IRepositoryAdministration>().ListAsync(cancellationToken);

        Rows = services.GetRequiredService<FormatRegistry>().Modules
            .Select(module => new Row(
                module,
                repositories.Count(r => r.Format == module.Format),
                Capabilities(services, module.Format)))
            .ToList();
    }

    // What the module's services answer to: a capability a phase has not implemented yet is simply not listed.
    private static IReadOnlyList<string> Capabilities(IServiceProvider services, RepositoryFormat format)
    {
        var list = new List<string>();
        if (services.GetFormatService<IFormatIndexer>(format) != null) list.Add("Deploy (indexing)");
        if (services.GetFormatService<IFormatSearchProvider>(format) != null) list.Add("Search");
        if (services.GetFormatService<IFormatRetentionHooks>(format) != null) list.Add("Retention");
        if (services.GetFormatService<IFormatScanner>(format) != null) list.Add("Vulnerability scanning");

        return list;
    }
}
