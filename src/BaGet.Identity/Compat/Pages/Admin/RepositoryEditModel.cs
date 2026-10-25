using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.Workflows;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Identity.Pages.Admin;

/// <summary>
/// The page model of the repository page when it was a Razor Page: a repository's feed address, the access model's view of it, and
/// what can change: whether anonymous requests may read it; a remote repository's upstream, connection test and cache purge; a
/// virtual repository's members, deploy repository and preview; a local Maven repository's settings. Saving and purging are refused
/// with 503 while the <see cref="MaintenanceGate"/> is closed; the changes that hand out credentials or delete data ask for the
/// administrator's password again.
/// </summary>
/// <remarks>
/// Kept as a shim (no breaking change of the public API); <c>AdminRepositoriesController</c> serves
/// <c>/admin/repositories/{name}</c>. Both call <c>RepositoryEditWorkflow</c>.
/// </remarks>
[Obsolete("The repository screen is served by AdminRepositoriesController; this page model has no page any more and will be removed in the next major version.")]
public class RepositoryEditModel : RepositoryPageModel
{
    private RepositoryEditWorkflow? _workflow;

    private RepositoryEditWorkflow Workflow => _workflow ??= new RepositoryEditWorkflow(HttpContext, Url);

    /// <summary>The repository's name (route).</summary>
    [BindProperty(SupportsGet = true)]
    public string? Name { get => Workflow.Model.Name; set => Workflow.Model.Name = value; }

    /// <summary>Whether requests without credentials may read the repository.</summary>
    [BindProperty]
    public bool AnonymousRead { get => Workflow.Model.AnonymousRead; set => Workflow.Model.AnonymousRead = value; }

    /// <summary>The repository.</summary>
    public RepositorySummary? Repository => Workflow.Model.Repository;

    /// <summary>The upstream fields of a remote repository.</summary>
    [BindProperty]
    public RemoteFormModel Remote { get => Workflow.Model.Remote; set => Workflow.Model.Remote = value; }

    /// <summary>The remote repository's upstream, null for the other kinds.</summary>
    public RemoteRepositoryDetail? RemoteDetail => Workflow.Model.RemoteDetail;

    /// <summary>The settings of a local Maven repository (policy, redeploy, snapshot builds kept), null for the other repositories.</summary>
    public MavenRepositorySettings? MavenSettings => Workflow.Model.MavenSettings;

    /// <summary>What a remote repository stores and remembers.</summary>
    public RemoteCacheOverview? Cache => Workflow.Model.Cache;

    /// <summary>The virtual repository's members and deploy repository, null for the other kinds.</summary>
    public VirtualRepositoryDetail? VirtualDetail => Workflow.Model.VirtualDetail;

    /// <summary>The repositories that can still be added to a virtual repository.</summary>
    public IReadOnlyList<RepositoryInfo> MemberCandidates => Workflow.Model.MemberCandidates;

    /// <summary>The virtual repositories that list this repository as a member.</summary>
    public IReadOnlyList<string> ReferencedBy => Workflow.Model.ReferencedBy;

    /// <summary>The package id to resolve in the preview.</summary>
    [BindProperty]
    public string? PreviewPackageId { get => Workflow.Model.PreviewPackageId; set => Workflow.Model.PreviewPackageId = value; }

    /// <summary>The package version to resolve in the preview.</summary>
    [BindProperty]
    public string? PreviewVersion { get => Workflow.Model.PreviewVersion; set => Workflow.Model.PreviewVersion = value; }

    /// <summary>Where the previewed package version would come from.</summary>
    public ResolutionPreview? Preview => Workflow.Model.Preview;

    /// <summary>The outcome of "Test connection".</summary>
    public RemoteConnectionResult? TestResult => Workflow.Model.TestResult;

    /// <summary>The outcome of the last action.</summary>
    [TempData]
    public string? Message { get => Workflow.Model.Message; set => Workflow.Model.Message = value; }

    /// <summary>The package id to purge from the cache.</summary>
    [BindProperty]
    public string? PackageId { get => Workflow.Model.PackageId; set => Workflow.Model.PackageId = value; }

    /// <summary>The version to purge (blank = every version).</summary>
    [BindProperty]
    public string? PackageVersion { get => Workflow.Model.PackageVersion; set => Workflow.Model.PackageVersion = value; }

    /// <summary>
    /// Who effectively holds Read, Annotate, Deploy, Delete and Manage in the repository, from the access model; null for
    /// the default repository when the database has no access model.
    /// </summary>
    public RepositoryAccessSummary? Access => Workflow.Model.Access;

    /// <summary>The page of the repository's format that lists its packages (<c>/Index</c> for NuGet, <c>/NpmPackages</c> for npm).</summary>
    public string BrowsePage => Workflow.Model.BrowsePage;

    /// <summary>The name of what a package is called in this repository's format (shown in the labels).</summary>
    public string PackageWord => Workflow.Model.PackageWord;

    /// <summary>What the "version" of a package is called in this repository's format (a Docker image has a tag or a digest).</summary>
    public string VersionWord => Workflow.Model.VersionWord;

    /// <summary>The label of the field that names a package (a Docker image is an image name).</summary>
    public string PackageLabel => Workflow.Model.PackageLabel;

    /// <summary>The repository's feed address.</summary>
    public string FeedUrl => Workflow.Model.FeedUrl;

    /// <summary>For the default repository: the remote repository it fetches through, when <c>Mirror:Enabled</c>.</summary>
    public RemoteRepositoryDetail? MirrorRepository => Workflow.Model.MirrorRepository;

    /// <summary>Whether <c>Mirror:Enabled</c> makes the default repository fetch through <see cref="MirrorRepository"/>.</summary>
    public bool MirrorActive => Workflow.Model.MirrorActive;

    /// <summary>Shows the repository.</summary>
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
        => await FinishAsync(await Workflow.GetAsync(cancellationToken));

    /// <summary>Saves the repository.</summary>
    public async Task<IActionResult> OnPostAsync(string? password, CancellationToken cancellationToken)
        => await FinishAsync(await Workflow.PostAsync(password, cancellationToken));

    /// <summary>Saves the policy of a local Maven repository.</summary>
    public async Task<IActionResult> OnPostMavenSettingsAsync(string? policy, string? redeploy, string? snapshotBuilds, CancellationToken cancellationToken)
        => await FinishAsync(await Workflow.PostMavenSettingsAsync(policy, redeploy, snapshotBuilds, cancellationToken));

    /// <summary>Asks the upstream with the posted, unsaved values (a blank password means the stored one) and saves nothing.</summary>
    public async Task<IActionResult> OnPostTestAsync(string? password, CancellationToken cancellationToken)
        => await FinishAsync(await Workflow.PostTestAsync(password, cancellationToken));

    /// <summary>Deletes every package the remote repository stored and forgets what it remembered of its upstream.</summary>
    public async Task<IActionResult> OnPostPurgeAsync(string? password, CancellationToken cancellationToken)
        => await FinishAsync(await Workflow.PostPurgeAsync(password, cancellationToken));

    /// <summary>Deletes one cached package (every version, or the one given) and forgets what was remembered about it.</summary>
    public async Task<IActionResult> OnPostPurgePackageAsync(string? password, CancellationToken cancellationToken)
        => await FinishAsync(await Workflow.PostPurgePackageAsync(password, cancellationToken));

    /// <summary>Adds a repository at the end of the virtual repository's members.</summary>
    public async Task<IActionResult> OnPostAddMemberAsync(string? member, CancellationToken cancellationToken)
        => await FinishAsync(await Workflow.PostAddMemberAsync(member, cancellationToken));

    /// <summary>Removes a member (and the deploy repository too when it was that member).</summary>
    public async Task<IActionResult> OnPostRemoveMemberAsync(string? member, CancellationToken cancellationToken)
        => await FinishAsync(await Workflow.PostRemoveMemberAsync(member, cancellationToken));

    /// <summary>Moves a member one place earlier (<c>up</c>) or later (<c>down</c>) in the resolution order.</summary>
    public async Task<IActionResult> OnPostMoveMemberAsync(string? member, string? direction, CancellationToken cancellationToken)
        => await FinishAsync(await Workflow.PostMoveMemberAsync(member, direction, cancellationToken));

    /// <summary>Sets the member that receives uploads (blank: the virtual repository takes none).</summary>
    public async Task<IActionResult> OnPostDeployAsync(string? deploy, CancellationToken cancellationToken)
        => await FinishAsync(await Workflow.PostDeployAsync(deploy, cancellationToken));

    /// <summary>Shows where the package version typed would come from, member by member (nothing is fetched or stored).</summary>
    public async Task<IActionResult> OnPostPreviewAsync(CancellationToken cancellationToken)
        => await FinishAsync(await Workflow.PostPreviewAsync(cancellationToken));

    // What the base class knows about the outcome: the reason of a refusal, and the confirmation state the partials need.
    private async Task<IActionResult> FinishAsync(IActionResult? result)
    {
        Error = Workflow.Model.Error;
        if (result != null) return result;

        await LoadConfirmationAsync();

        return Page();
    }
}
