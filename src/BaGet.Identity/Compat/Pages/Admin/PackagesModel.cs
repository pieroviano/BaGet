using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Identity.ViewModels;
using BaGet.Identity.Workflows;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Identity.Pages.Admin;

/// <summary>
/// The page model of the ownerless packages page when it was a Razor Page: the package IDs that have no owner, in every
/// repository. Only administrators and the global API key may change them.
/// </summary>
/// <remarks>
/// Kept as a shim (no breaking change of the public API); <c>AdminPackagesController</c> serves <c>/admin/packages</c>. Both call
/// <c>AdminPackagesWorkflow</c>.
/// </remarks>
[Obsolete("The ownerless packages screen is served by AdminPackagesController; this page model has no page any more and will be removed in the next major version.")]
public class PackagesModel : AdminPageModel
{
    /// <summary>How many IDs a page lists.</summary>
    public const int PageSize = AdminPackagesViewModel.PageSize;

    private AdminPackagesWorkflow? _workflow;

    private AdminPackagesWorkflow Workflow => _workflow ??= new AdminPackagesWorkflow(HttpContext, Url);

    /// <summary>One-based page number. Not "page": Razor Pages reserves that route value for the page path.</summary>
    [BindProperty(SupportsGet = true, Name = "p")]
    public int PageNumber { get => Workflow.Ownerless.PageNumber; set => Workflow.Ownerless.PageNumber = value; }

    /// <summary>The ownerless IDs on this page.</summary>
    public IReadOnlyList<AdminPackageRow> Packages => Workflow.Ownerless.Packages;

    /// <summary>Whether a Repository column is worth showing: some listed ID is outside the default repository.</summary>
    public bool ShowRepository => Workflow.Ownerless.ShowRepository;

    /// <summary>The number of pages.</summary>
    public int PageCount => Workflow.Ownerless.PageCount;

    /// <summary>Lists the ownerless IDs of every repository.</summary>
    public async Task OnGetAsync(CancellationToken cancellationToken) => await Workflow.GetOwnerlessAsync(cancellationToken);
}
