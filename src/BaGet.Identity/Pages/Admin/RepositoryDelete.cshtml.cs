using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace BaGet.Identity.Pages.Admin;

/// <summary>
/// Deletes an empty repository. The default repository cannot be deleted. A repository that still holds packages is
/// refused (removing its content is not available yet: delete the packages first). Refused with 503 while the
/// <see cref="MaintenanceGate"/> is closed.
/// </summary>
public class RepositoryDeleteModel : AdminPageModel
{
    /// <summary>The repository's name (route).</summary>
    [BindProperty(SupportsGet = true)]
    public string? Name { get; set; }

    /// <summary>The repository.</summary>
    public RepositorySummary? Repository { get; private set; }

    /// <summary>Why the deletion was refused.</summary>
    public string? Error { get; private set; }

    /// <summary>Shows what the deletion does.</summary>
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        Repository = await HttpContext.RequestServices.GetRequiredService<IRepositoryAdministration>().FindAsync(Name, cancellationToken);

        return Repository == null ? NotFound() : Page();
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
        catch (System.InvalidOperationException e) when (e is RepositoryProtectedException or RepositoryNotEmptyException)
        {
            Error = e.Message;

            return Page();
        }

        TempData["Message"] = $"Repository {Name} was deleted.";

        return RedirectToPage("/Admin/Repositories");
    }
}
