using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace BaGet.Identity.Pages.Admin;

/// <summary>
/// Deletes a repository with no packages, and its history rows. The default repository cannot be deleted. A repository
/// that still holds packages is refused (delete the packages first), and so is one that a virtual repository lists as a
/// member (remove it there first). Refused with 503 while the
/// <see cref="MaintenanceGate"/> is closed.
/// </summary>
public class RepositoryDeleteModel : AdminPageModel
{
    /// <summary>The repository's name (route).</summary>
    [BindProperty(SupportsGet = true)]
    public string? Name { get; set; }

    /// <summary>The repository.</summary>
    public RepositorySummary? Repository { get; private set; }

    /// <summary>The virtual repositories that list this repository as a member (the deletion is refused while there are any).</summary>
    public IReadOnlyList<string> ReferencedBy { get; private set; } = new List<string>();

    /// <summary>Why the deletion was refused.</summary>
    public string? Error { get; private set; }

    /// <summary>Shows what the deletion does.</summary>
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        Repository = await HttpContext.RequestServices.GetRequiredService<IRepositoryAdministration>().FindAsync(Name, cancellationToken);
        if (Repository == null) return NotFound();

        ReferencedBy = await HttpContext.RequestServices.GetRequiredService<IVirtualRepositoryAdministration>().ReferencedByAsync(Repository.Name, cancellationToken);

        return Page();
    }

    /// <summary>Deletes the repository, then returns to the list.</summary>
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        HttpContext.RequestServices.GetService<MaintenanceGate>()?.ThrowIfClosed();

        var administration = HttpContext.RequestServices.GetRequiredService<IRepositoryAdministration>();
        Repository = await administration.FindAsync(Name, cancellationToken);
        if (Repository == null) return NotFound();

        try
        {
            await administration.DeleteAsync(Name!, cancellationToken);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (System.InvalidOperationException e) when (e is RepositoryProtectedException or RepositoryNotEmptyException or RepositoryReferencedException)
        {
            Error = e.Message;

            return Page();
        }

        TempData["Message"] = $"Repository {Name} was deleted.";

        return RedirectToPage("/Admin/Repositories");
    }
}
