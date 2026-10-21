using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace BaGet.Identity.Pages.Admin;

/// <summary>
/// Lists the repositories with their feed addresses and creates new ones: local, remote (an upstream cache, see
/// <see cref="RemoteRepositorySettings"/>) or virtual (an ordered group of members, filled in on its edit page); the name
/// is validated by <see cref="RepositoryNames"/>. Creating is refused with 503 while the <see cref="MaintenanceGate"/> is
/// closed. Creating a remote repository, which can carry credentials, asks for the administrator's password again.
/// </summary>
public class RepositoriesModel : RepositoryPageModel
{
    /// <summary>The name for the repository to create.</summary>
    [BindProperty]
    public string? Name { get; set; }

    /// <summary>The upstream settings for the remote repository to create.</summary>
    [BindProperty]
    public RemoteFormModel Remote { get; set; } = new();

    /// <summary>The repositories.</summary>
    public IReadOnlyList<RepositorySummary> Repositories { get; private set; } = Array.Empty<RepositorySummary>();

    /// <summary>Whether the database can hold more than the default repository.</summary>
    public bool CanCreate => HttpContext.RequestServices.GetRequiredService<IRepositoryService>().SupportsMultipleRepositories;

    /// <summary>Outcome of the last action.</summary>
    [TempData]
    public string? Message { get; set; }

    /// <summary>Whether uploads to <paramref name="repository"/> are possible at all (a remote one takes none; a virtual one needs a deploy repository).</summary>
    public bool TakesUploads(RepositorySummary repository) => repository.Kind switch
    {
        RepositoryKind.Remote => false,
        RepositoryKind.Virtual => _deployable.Contains(repository.Name),
        _ => true,
    };

    private HashSet<string> _deployable = new();

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

    /// <summary>Creates a virtual NuGet repository with no members; they are added on its page.</summary>
    public async Task<IActionResult> OnPostVirtualAsync(CancellationToken cancellationToken)
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
            await HttpContext.RequestServices.GetRequiredService<IVirtualRepositoryAdministration>()
                .CreateAsync(name!, new VirtualRepositorySettings(), cancellationToken, User.Identity?.Name);
        }
        catch (Exception e) when (e is NotSupportedException or RepositoryExistsException or ArgumentException)
        {
            Error = e.Message;
            await LoadAsync(cancellationToken);

            return Page();
        }

        TempData["Message"] = $"Virtual repository {name} was created. Add its members below.";

        return RedirectToPage("/Admin/RepositoryEdit", new { name });
    }

    /// <summary>Creates a remote NuGet repository from the posted upstream settings.</summary>
    public async Task<IActionResult> OnPostRemoteAsync(string? password, CancellationToken cancellationToken)
    {
        HttpContext.RequestServices.GetService<MaintenanceGate>()?.ThrowIfClosed();

        if (!await ConfirmedAsync(password))
        {
            Error = NotConfirmed;
            await LoadAsync(cancellationToken);

            return Page();
        }

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
                .CreateAsync(name!, RepositoryFormat.NuGet, RepositoryKind.Remote, Remote.ToSettings(), cancellationToken);
        }
        catch (Exception e) when (e is NotSupportedException or RepositoryExistsException or ArgumentException or InvalidOperationException)
        {
            Error = e.Message;
            await LoadAsync(cancellationToken);

            return Page();
        }

        Message = $"Remote repository {name} was created.";

        return RedirectToPage("/Admin/RepositoryEdit", new { name });
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        await LoadConfirmationAsync();
        Repositories = await HttpContext.RequestServices.GetRequiredService<IRepositoryAdministration>().ListAsync(cancellationToken);

        var virtuals = HttpContext.RequestServices.GetRequiredService<IVirtualRepositoryAdministration>();
        _deployable = new HashSet<string>(StringComparer.Ordinal);
        foreach (var repository in Repositories.Where(r => r.Kind == RepositoryKind.Virtual))
        {
            if ((await virtuals.FindAsync(repository.Name, cancellationToken))?.DeployTarget != null) _deployable.Add(repository.Name);
        }
    }
}
