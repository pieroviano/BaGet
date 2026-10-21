using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace BaGet.Identity.Pages.Admin;

/// <summary>
/// Shows a repository's feed address and the access model's view of it (who effectively holds each action) and edits what can
/// change: whether anonymous requests may read it. The name never
/// changes (it is in URLs and storage paths), and neither does the format or kind. Saving is refused with 503 while the
/// <see cref="MaintenanceGate"/> is closed.
/// </summary>
public class RepositoryEditModel : AdminPageModel
{
    /// <summary>The repository's name (route).</summary>
    [BindProperty(SupportsGet = true)]
    public string? Name { get; set; }

    /// <summary>Whether requests without credentials may read the repository.</summary>
    [BindProperty]
    public bool AnonymousRead { get; set; }

    /// <summary>The repository.</summary>
    public RepositorySummary? Repository { get; private set; }

    /// <summary>
    /// Who effectively holds Read, Annotate, Deploy, Delete and Manage in the repository, from the access model; null for
    /// a repository that is not a NuGet one.
    /// </summary>
    public RepositoryAccessSummary? Access { get; private set; }

    /// <summary>The repository's feed address.</summary>
    public string FeedUrl => RepositoryFeedUrl.For(HttpContext, Repository?.Name ?? string.Empty);

    /// <summary>Shows the repository.</summary>
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        Repository = await HttpContext.RequestServices.GetRequiredService<IRepositoryAdministration>().FindAsync(Name, cancellationToken);
        if (Repository == null) return NotFound();

        AnonymousRead = Repository.AnonymousRead;

        var services = HttpContext.RequestServices;
        var info = (await services.GetRequiredService<IRepositoryService>().ListAsync(cancellationToken))
            .FirstOrDefault(r => r.Name == Repository.Name);
        if (info != null && info.Format == RepositoryFormat.NuGet)
        {
            Access = await services.GetRequiredService<IAccessEvaluator>().DescribeAsync(info, cancellationToken);
        }

        return Page();
    }

    /// <summary>Saves the repository.</summary>
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        HttpContext.RequestServices.GetService<MaintenanceGate>()?.ThrowIfClosed();

        var administration = HttpContext.RequestServices.GetRequiredService<IRepositoryAdministration>();
        if (await administration.FindAsync(Name, cancellationToken) == null) return NotFound();

        await administration.SetAnonymousReadAsync(Name!, AnonymousRead, cancellationToken, User.Identity?.Name);
        TempData["Message"] = $"Repository {Name} was saved.";

        return RedirectToPage("/Admin/Repositories");
    }
}
