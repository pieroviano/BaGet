using System.Threading;
using System.Threading.Tasks;
using BaGet.Identity.Mvc;
using BaGet.Identity.Pages.Admin;
using BaGet.Identity.ViewModels;
using BaGet.Identity.Workflows;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Identity.Controllers;

/// <summary>
/// The administrator's repository screens: the list with the create forms (<c>/admin/repositories</c>), one repository
/// (<c>/admin/repositories/{name}</c>: anonymous read, upstream, cache, members, Maven settings) and its deletion
/// (<c>/admin/repositories/{name}/delete</c>). They replace the Razor Pages <c>/Admin/Repositories</c>,
/// <c>/Admin/RepositoryEdit</c> and <c>/Admin/RepositoryDelete</c> (same URLs, same <c>?handler=</c> values, same
/// <c>asp-page</c> names through <see cref="LegacyPageAttribute"/>); the logic is in <see cref="RepositoriesWorkflow"/>,
/// <see cref="RepositoryEditWorkflow"/> and <see cref="RepositoryDeleteWorkflow"/>.
/// Administrators only; every write is refused with 503 while the read-only gate is closed, and the changes that hand out
/// credentials or delete data ask for the administrator's password again, as the pages did.
/// </summary>
public sealed class AdminRepositoriesController : RepositoryControllerBase
{
    // ---------------------------------------------------------------- Repositories

    /// <summary>Lists the repositories (narrowed by <c>format</c>, <c>kind</c>, <c>q</c>) and shows the create forms.</summary>
    [Route("admin/repositories"), HttpGet, HttpHead, PageHandler, LegacyPage("/Admin/Repositories")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var workflow = StartList(null, null);
        await workflow.GetAsync(cancellationToken);

        return ShowList(workflow, null);
    }

    /// <summary>Creates a local NuGet repository.</summary>
    [Route("admin/repositories"), HttpPost, PageHandler, LegacyPage("/Admin/Repositories")]
    public async Task<IActionResult> Create(string? name, RemoteFormModel? remote, CancellationToken cancellationToken)
    {
        var workflow = StartList(name, remote);

        return ShowList(workflow, await workflow.PostAsync(cancellationToken));
    }

    /// <summary>Creates a local repository of a format other than NuGet from the posted <c>field:*</c> values.</summary>
    [Route("admin/repositories"), HttpPost, PageHandler("Format"), LegacyPage("/Admin/Repositories")]
    public async Task<IActionResult> CreateFormat(string? name, RemoteFormModel? remote, string? format, bool anonymousRead = true, CancellationToken cancellationToken = default)
    {
        var workflow = StartList(name, remote);

        return ShowList(workflow, await workflow.PostFormatAsync(format, anonymousRead, cancellationToken));
    }

    /// <summary>Creates a virtual repository with no members.</summary>
    [Route("admin/repositories"), HttpPost, PageHandler("Virtual"), LegacyPage("/Admin/Repositories")]
    public async Task<IActionResult> CreateVirtual(string? name, RemoteFormModel? remote, string? format = null, bool anonymousRead = true, CancellationToken cancellationToken = default)
    {
        var workflow = StartList(name, remote);

        return ShowList(workflow, await workflow.PostVirtualAsync(format, anonymousRead, cancellationToken));
    }

    /// <summary>Creates a remote repository from the posted upstream settings, after the administrator's password.</summary>
    [Route("admin/repositories"), HttpPost, PageHandler("Remote"), LegacyPage("/Admin/Repositories")]
    public async Task<IActionResult> CreateRemote(string? name, RemoteFormModel? remote, string? password, string? anonymousRead, string? format = null, CancellationToken cancellationToken = default)
    {
        var workflow = StartList(name, remote);

        return ShowList(workflow, await workflow.PostRemoteAsync(password, anonymousRead, format, cancellationToken));
    }

    private RepositoriesWorkflow StartList(string? name, RemoteFormModel? remote)
    {
        var workflow = new RepositoriesWorkflow(HttpContext, Url);
        workflow.Model.Name = name;
        if (remote != null) workflow.Model.Remote = remote;

        return workflow;
    }

    private IActionResult ShowList(RepositoriesWorkflow workflow, IActionResult? result)
    {
        Flash(workflow.Model);

        return result ?? View("Index", workflow.Model);
    }

    // ---------------------------------------------------------------- RepositoryEdit

    /// <summary>Shows one repository.</summary>
    [Route("admin/repositories/{name}"), HttpGet, HttpHead, PageHandler, LegacyPage("/Admin/RepositoryEdit")]
    public async Task<IActionResult> Edit(string name, CancellationToken cancellationToken)
    {
        var workflow = StartEdit(name, null);

        return ShowEdit(workflow, await workflow.GetAsync(cancellationToken));
    }

    /// <summary>Saves the repository.</summary>
    [Route("admin/repositories/{name}"), HttpPost, PageHandler, LegacyPage("/Admin/RepositoryEdit")]
    public async Task<IActionResult> Save(string name, RepositoryEditForm? form, string? password, CancellationToken cancellationToken)
    {
        var workflow = StartEdit(name, form);

        return ShowEdit(workflow, await workflow.PostAsync(password, cancellationToken));
    }

    /// <summary>Saves the policy of a local Maven repository.</summary>
    [Route("admin/repositories/{name}"), HttpPost, PageHandler("MavenSettings"), LegacyPage("/Admin/RepositoryEdit")]
    public async Task<IActionResult> MavenSettings(string name, RepositoryEditForm? form, string? policy, string? redeploy, string? snapshotBuilds, CancellationToken cancellationToken)
    {
        var workflow = StartEdit(name, form);

        return ShowEdit(workflow, await workflow.PostMavenSettingsAsync(policy, redeploy, snapshotBuilds, cancellationToken));
    }

    /// <summary>Asks a remote repository's upstream with the unsaved values and saves nothing.</summary>
    [Route("admin/repositories/{name}"), HttpPost, PageHandler("Test"), LegacyPage("/Admin/RepositoryEdit")]
    public async Task<IActionResult> Test(string name, RepositoryEditForm? form, string? password, CancellationToken cancellationToken)
    {
        var workflow = StartEdit(name, form);

        return ShowEdit(workflow, await workflow.PostTestAsync(password, cancellationToken));
    }

    /// <summary>Deletes everything a remote repository cached.</summary>
    [Route("admin/repositories/{name}"), HttpPost, PageHandler("Purge"), LegacyPage("/Admin/RepositoryEdit")]
    public async Task<IActionResult> Purge(string name, RepositoryEditForm? form, string? password, CancellationToken cancellationToken)
    {
        var workflow = StartEdit(name, form);

        return ShowEdit(workflow, await workflow.PostPurgeAsync(password, cancellationToken));
    }

    /// <summary>Deletes one cached package of a remote repository.</summary>
    [Route("admin/repositories/{name}"), HttpPost, PageHandler("PurgePackage"), LegacyPage("/Admin/RepositoryEdit")]
    public async Task<IActionResult> PurgePackage(string name, RepositoryEditForm? form, string? password, CancellationToken cancellationToken)
    {
        var workflow = StartEdit(name, form);

        return ShowEdit(workflow, await workflow.PostPurgePackageAsync(password, cancellationToken));
    }

    /// <summary>Adds a member at the end of a virtual repository.</summary>
    [Route("admin/repositories/{name}"), HttpPost, PageHandler("AddMember"), LegacyPage("/Admin/RepositoryEdit")]
    public async Task<IActionResult> AddMember(string name, RepositoryEditForm? form, string? member, CancellationToken cancellationToken)
    {
        var workflow = StartEdit(name, form);

        return ShowEdit(workflow, await workflow.PostAddMemberAsync(member, cancellationToken));
    }

    /// <summary>Removes a member of a virtual repository.</summary>
    [Route("admin/repositories/{name}"), HttpPost, PageHandler("RemoveMember"), LegacyPage("/Admin/RepositoryEdit")]
    public async Task<IActionResult> RemoveMember(string name, RepositoryEditForm? form, string? member, CancellationToken cancellationToken)
    {
        var workflow = StartEdit(name, form);

        return ShowEdit(workflow, await workflow.PostRemoveMemberAsync(member, cancellationToken));
    }

    /// <summary>Moves a member of a virtual repository up or down.</summary>
    [Route("admin/repositories/{name}"), HttpPost, PageHandler("MoveMember"), LegacyPage("/Admin/RepositoryEdit")]
    public async Task<IActionResult> MoveMember(string name, RepositoryEditForm? form, string? member, string? direction, CancellationToken cancellationToken)
    {
        var workflow = StartEdit(name, form);

        return ShowEdit(workflow, await workflow.PostMoveMemberAsync(member, direction, cancellationToken));
    }

    /// <summary>Sets the member of a virtual repository that receives uploads.</summary>
    [Route("admin/repositories/{name}"), HttpPost, PageHandler("Deploy"), LegacyPage("/Admin/RepositoryEdit")]
    public async Task<IActionResult> Deploy(string name, RepositoryEditForm? form, string? deploy, CancellationToken cancellationToken)
    {
        var workflow = StartEdit(name, form);

        return ShowEdit(workflow, await workflow.PostDeployAsync(deploy, cancellationToken));
    }

    /// <summary>Shows where a package version would come from in a virtual repository.</summary>
    [Route("admin/repositories/{name}"), HttpPost, PageHandler("Preview"), LegacyPage("/Admin/RepositoryEdit")]
    public async Task<IActionResult> Preview(string name, RepositoryEditForm? form, CancellationToken cancellationToken)
    {
        var workflow = StartEdit(name, form);

        return ShowEdit(workflow, await workflow.PostPreviewAsync(cancellationToken));
    }

    private RepositoryEditWorkflow StartEdit(string? name, RepositoryEditForm? form)
    {
        var workflow = new RepositoryEditWorkflow(HttpContext, Url);
        workflow.Model.Name = name;
        if (form != null)
        {
            workflow.Model.AnonymousRead = form.AnonymousRead;
            workflow.Model.Remote = form.Remote;
            workflow.Model.PreviewPackageId = form.PreviewPackageId;
            workflow.Model.PreviewVersion = form.PreviewVersion;
            workflow.Model.PackageId = form.PackageId;
            workflow.Model.PackageVersion = form.PackageVersion;
        }

        return workflow;
    }

    private IActionResult ShowEdit(RepositoryEditWorkflow workflow, IActionResult? result)
    {
        Flash(workflow.Model);

        return result ?? View("Edit", workflow.Model);
    }

    // ---------------------------------------------------------------- RepositoryDelete

    /// <summary>Shows what deleting the repository does.</summary>
    [Route("admin/repositories/{name}/delete"), HttpGet, HttpHead, PageHandler, LegacyPage("/Admin/RepositoryDelete")]
    public async Task<IActionResult> DeleteRepository(string name, CancellationToken cancellationToken)
    {
        var workflow = new RepositoryDeleteWorkflow(HttpContext, Url);
        workflow.Model.Name = name;

        return ShowDeletion(workflow, await workflow.GetAsync(cancellationToken));
    }

    /// <summary>Deletes the repository, then returns to the list.</summary>
    [Route("admin/repositories/{name}/delete"), HttpPost, PageHandler, LegacyPage("/Admin/RepositoryDelete")]
    public async Task<IActionResult> DeleteRepositoryConfirmed(string name, CancellationToken cancellationToken)
    {
        var workflow = new RepositoryDeleteWorkflow(HttpContext, Url);
        workflow.Model.Name = name;

        return ShowDeletion(workflow, await workflow.PostAsync(cancellationToken));
    }

    private IActionResult ShowDeletion(RepositoryDeleteWorkflow workflow, IActionResult? result)
    {
        if (workflow.RedirectMessage != null) TempData["Message"] = workflow.RedirectMessage;

        return result ?? View("Delete", workflow.Model);
    }
}
