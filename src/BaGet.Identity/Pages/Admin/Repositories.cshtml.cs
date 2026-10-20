using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace BaGet.Identity.Pages.Admin;

/// <summary>
/// Lists the repositories with their feed addresses and creates new ones. Only local NuGet repositories can be
/// created today; the name is validated by <see cref="RepositoryNames"/>. Creating is refused with 503 while the
/// <see cref="MaintenanceGate"/> is closed.
/// </summary>
public class RepositoriesModel : AdminPageModel
{
    /// <summary>The name for the repository to create.</summary>
    [BindProperty]
    public string? Name { get; set; }

    /// <summary>The repositories.</summary>
    public IReadOnlyList<RepositorySummary> Repositories { get; private set; } = Array.Empty<RepositorySummary>();

    /// <summary>Whether the database can hold more than the default repository.</summary>
    public bool CanCreate => HttpContext.RequestServices.GetRequiredService<IRepositoryService>().SupportsMultipleRepositories;

    /// <summary>Outcome of the last action.</summary>
    [TempData]
    public string? Message { get; set; }

    /// <summary>Why the last action was refused.</summary>
    public string? Error { get; private set; }

    /// <summary>The feed address of <paramref name="repository"/>.</summary>
    public string FeedUrl(RepositorySummary repository) => RepositoryFeedUrl.For(HttpContext, repository.Name);

    /// <summary>Lists the repositories.</summary>
    public async Task OnGetAsync(CancellationToken cancellationToken) => await LoadAsync(cancellationToken);

    /// <summary>Creates a local NuGet repository.</summary>
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        HttpContext.RequestServices.GetService<MaintenanceGate>()?.ThrowIfClosed();

        var name = Name?.Trim();
        if (!RepositoryNames.IsValid(name, out var invalid))
        {
            Error = invalid;
            await LoadAsync(cancellationToken);

            return Page();
        }

        try
        {
            await HttpContext.RequestServices.GetRequiredService<IRepositoryService>()
                .CreateAsync(name!, RepositoryFormat.NuGet, RepositoryKind.Local, cancellationToken);
        }
        catch (Exception e) when (e is NotSupportedException or RepositoryExistsException)
        {
            Error = e.Message;
            await LoadAsync(cancellationToken);

            return Page();
        }

        Message = $"Repository {name} was created.";

        return RedirectToPage();
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        Repositories = await HttpContext.RequestServices.GetRequiredService<IRepositoryAdministration>().ListAsync(cancellationToken);
    }
}
