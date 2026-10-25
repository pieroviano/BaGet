using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace BaGet.Identity.Workflows;

/// <summary>
/// Deletes a repository with no packages, and its history rows. The default repository cannot be deleted. A repository that still
/// holds packages is refused (delete the packages first), and so is one that a virtual repository lists as a member (remove it there
/// first). Refused with 503 while the <see cref="MaintenanceGate"/> is closed. Used by <c>AdminRepositoriesController</c>.
/// </summary>
internal sealed class RepositoryDeleteWorkflow : AdminWorkflow
{
    public RepositoryDeleteWorkflow(HttpContext httpContext, IUrlHelper url) : base(httpContext, url)
    {
    }

    /// <summary>The state the screen renders; the caller sets <c>Name</c> first.</summary>
    public RepositoryDeleteViewModel Model { get; } = new();

    /// <summary>The message to flash to the repositories screen after a deletion, or null.</summary>
    public string? RedirectMessage { get; private set; }

    /// <summary>Shows what the deletion does.</summary>
    public async Task<IActionResult?> GetAsync(CancellationToken cancellationToken)
    {
        Model.Repository = await Services.GetRequiredService<IRepositoryAdministration>().FindAsync(Model.Name, cancellationToken);
        if (Model.Repository == null) return new NotFoundResult();

        Model.ReferencedBy = await Services.GetRequiredService<IVirtualRepositoryAdministration>().ReferencedByAsync(Model.Repository.Name, cancellationToken);

        return null;
    }

    /// <summary>Deletes the repository, then returns to the list.</summary>
    public async Task<IActionResult?> PostAsync(CancellationToken cancellationToken)
    {
        ThrowIfClosed();

        var administration = Services.GetRequiredService<IRepositoryAdministration>();
        Model.Repository = await administration.FindAsync(Model.Name, cancellationToken);
        if (Model.Repository == null) return new NotFoundResult();

        try
        {
            await administration.DeleteAsync(Model.Name!, cancellationToken);
        }
        catch (KeyNotFoundException)
        {
            return new NotFoundResult();
        }
        catch (System.InvalidOperationException e) when (e is RepositoryProtectedException or RepositoryNotEmptyException or RepositoryReferencedException)
        {
            Model.Error = e.Message;

            return null;
        }

        RedirectMessage = $"Repository {Model.Name} was deleted.";

        return RedirectToScreen("/Admin/Repositories");
    }
}
