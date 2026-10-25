using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.Workflows;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace BaGet.Identity.Pages.Admin;

/// <summary>
/// The page model of the repositories page when it was a Razor Page: lists the repositories with their feed addresses and creates
/// new ones: local, remote (an upstream cache, see <see cref="RemoteRepositorySettings"/>) or virtual (an ordered group of members,
/// filled in on its edit page), of any format whose module offers the kind; the name is validated by <see cref="RepositoryNames"/>.
/// Creating is refused with 503 while the <see cref="MaintenanceGate"/> is closed. Creating a remote repository, which can carry
/// credentials, asks for the administrator's password again.
/// </summary>
/// <remarks>
/// Kept as a shim (no breaking change of the public API); <c>AdminRepositoriesController</c> serves <c>/admin/repositories</c>.
/// Both call <c>RepositoriesWorkflow</c>.
/// </remarks>
[Obsolete("The repositories screen is served by AdminRepositoriesController; this page model has no page any more and will be removed in the next major version.")]
public class RepositoriesModel : RepositoryPageModel
{
    private RepositoriesWorkflow? _workflow;

    private RepositoriesWorkflow Workflow => _workflow ??= new RepositoriesWorkflow(HttpContext, Url);

    /// <summary>The name for the repository to create.</summary>
    [BindProperty]
    public string? Name { get => Workflow.Model.Name; set => Workflow.Model.Name = value; }

    /// <summary>The upstream settings for the remote repository to create.</summary>
    [BindProperty]
    public RemoteFormModel Remote { get => Workflow.Model.Remote; set => Workflow.Model.Remote = value; }

    /// <summary>The repositories.</summary>
    public IReadOnlyList<RepositorySummary> Repositories => Workflow.Model.Repositories;

    /// <summary>Whether the database can hold more than the default repository.</summary>
    public bool CanCreate => HttpContext.RequestServices.GetRequiredService<IRepositoryService>().SupportsMultipleRepositories;

    /// <summary>Outcome of the last action.</summary>
    [TempData]
    public string? Message { get => Workflow.Model.Message; set => Workflow.Model.Message = value; }

    /// <summary>Whether uploads to <paramref name="repository"/> are possible at all (a remote one takes none; a virtual one needs a deploy repository).</summary>
    public bool TakesUploads(RepositorySummary repository) => Workflow.Model.TakesUploads(repository);

    /// <summary>The feed address of <paramref name="repository"/>, by its format's <see cref="IFormatModule.FeedPath"/>.</summary>
    public string FeedUrl(RepositorySummary repository) => Workflow.Model.FeedUrl(repository);

    /// <summary>The links its format contributes for <paramref name="repository"/> (browse, upload...), without the upload link where it takes none.</summary>
    public IReadOnlyList<FormatLink> LinksOf(RepositorySummary repository) => Workflow.Model.LinksOf(repository);

    /// <summary>Where <paramref name="link"/> leads for <paramref name="repository"/>.</summary>
    public string LinkUrl(RepositorySummary repository, FormatLink link) => Workflow.Model.LinkUrl(repository, link);

    /// <summary>Every format this server knows, for the list filter.</summary>
    public IReadOnlyList<IFormatModule> AllFormats => Workflow.Model.AllFormats;

    /// <summary>The formats a remote repository can be created for (their module lists <see cref="RepositoryKind.Remote"/>).</summary>
    public IReadOnlyList<IFormatModule> RemoteFormats => Workflow.Model.RemoteFormats;

    /// <summary>The formats a virtual repository can be created for (<see cref="IFormatModule.SupportsVirtual"/>).</summary>
    public IReadOnlyList<IFormatModule> VirtualFormats => Workflow.Model.VirtualFormats;

    /// <summary>The formats other than NuGet that can be created here (their module asks for a local repository), with their form fields.</summary>
    public IReadOnlyList<IFormatModule> OtherFormats => Workflow.Model.OtherFormats;

    /// <summary>The format to keep selected after a refused creation.</summary>
    public string? Format => Workflow.Model.Format;

    /// <summary>The format the list is narrowed to (<c>?format=</c>, a format name such as <c>Npm</c>), or null.</summary>
    public string? FilterFormat => Workflow.Model.FilterFormat;

    /// <summary>The kind the list is narrowed to (<c>?kind=</c>: Local, Remote or Virtual), or null.</summary>
    public string? FilterKind => Workflow.Model.FilterKind;

    /// <summary>The name fragment the list is narrowed to (<c>?q=</c>), or null.</summary>
    public string? FilterName => Workflow.Model.FilterName;

    /// <summary>Lists the repositories, narrowed by <c>format</c>, <c>kind</c> and <c>q</c>.</summary>
    public async Task OnGetAsync(CancellationToken cancellationToken) => await Workflow.GetAsync(cancellationToken);

    /// <summary>Creates a local NuGet repository.</summary>
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
        => await FinishAsync(await Workflow.PostAsync(cancellationToken));

    /// <summary>Creates a local repository of the format named by <paramref name="format"/> from the posted fields.</summary>
    public async Task<IActionResult> OnPostFormatAsync(string? format, bool anonymousRead = true, CancellationToken cancellationToken = default)
        => await FinishAsync(await Workflow.PostFormatAsync(format, anonymousRead, cancellationToken));

    /// <summary>Creates a virtual repository (NuGet unless <paramref name="format"/> says otherwise) with no members; they are added on its page.</summary>
    public async Task<IActionResult> OnPostVirtualAsync(string? format = null, bool anonymousRead = true, CancellationToken cancellationToken = default)
        => await FinishAsync(await Workflow.PostVirtualAsync(format, anonymousRead, cancellationToken));

    /// <summary>Creates a remote repository (NuGet unless <paramref name="format"/> says otherwise) from the posted upstream settings.</summary>
    public async Task<IActionResult> OnPostRemoteAsync(string? password, string? anonymousRead, string? format = null, CancellationToken cancellationToken = default)
        => await FinishAsync(await Workflow.PostRemoteAsync(password, anonymousRead, format, cancellationToken));

    // What the base class knows about the outcome: the reason of a refusal, and the confirmation state the partials need.
    private async Task<IActionResult> FinishAsync(IActionResult? result)
    {
        Error = Workflow.Model.Error;
        if (result != null) return result;

        await LoadConfirmationAsync();

        return Page();
    }
}
