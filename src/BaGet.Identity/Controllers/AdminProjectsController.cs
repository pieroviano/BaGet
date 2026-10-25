using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.Mvc;
using BaGet.Identity.Workflows;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Identity.Controllers;

/// <summary>
/// The project screens: the list with the new-project form (<c>/admin/projects</c>) and one project (<c>/admin/projects/{name}</c>:
/// repositories, members, the project's permission targets, delete). They replace the Razor Pages <c>/Admin/Projects</c> and
/// <c>/Admin/ProjectDetail</c> (same URLs, handlers and <c>asp-page</c> names); the logic is in <see cref="ProjectsWorkflow"/> and
/// <see cref="ProjectDetailWorkflow"/>. Server administrators, and the administrators of a project for the projects they administer
/// (the project is the <c>name</c> route value; only server administrators create and delete projects and assign repositories).
/// Member changes and deletions ask for the password again, and every POST is refused with 503 while the read-only gate is closed
/// (<see cref="AccessControllerBase"/>).
/// </summary>
public sealed class AdminProjectsController : AccessControllerBase
{
    private const string DetailPage = "/Admin/ProjectDetail";

    /// <inheritdoc />
    protected override async Task<bool> AuthorizeAsync(CancellationToken cancellationToken)
    {
        var isDetail = ControllerContext.ActionDescriptor.EndpointMetadata.OfType<LegacyPageAttribute>().Any(a => a.PageName == DetailPage);

        return isDetail
            ? await AccessAuthorization.ProjectAsync(AccessAdmin, IsServerAdministrator, CurrentUserId, RouteData.Values["name"]?.ToString(), cancellationToken)
            : await AccessAuthorization.ProjectsAsync(AccessAdmin, IsServerAdministrator, CurrentUserId, cancellationToken);
    }

    /// <summary>Lists the projects the signed-in user may open.</summary>
    [Route("admin/projects"), HttpGet, HttpHead, PageHandler, LegacyPage("/Admin/Projects")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var workflow = StartList(null, null);
        await workflow.ListAsync(cancellationToken);

        return ShowList(workflow, null);
    }

    /// <summary>Creates a project (administrators only) and opens it.</summary>
    [Route("admin/projects"), HttpPost, PageHandler, LegacyPage("/Admin/Projects")]
    public async Task<IActionResult> Create(string? name, string? displayName, CancellationToken cancellationToken)
    {
        var workflow = StartList(name, displayName);

        return ShowList(workflow, await workflow.CreateAsync(cancellationToken));
    }

    /// <summary>Shows one project.</summary>
    [Route("admin/projects/{name}"), HttpGet, HttpHead, PageHandler, LegacyPage(DetailPage)]
    public async Task<IActionResult> Detail(string name, CancellationToken cancellationToken)
    {
        var workflow = StartDetail(name);

        return ShowDetail(workflow, await workflow.GetAsync(cancellationToken));
    }

    /// <summary>Gives a user or group a role in the project, replacing an earlier one.</summary>
    [Route("admin/projects/{name}"), HttpPost, PageHandler("SetMember"), LegacyPage(DetailPage)]
    public async Task<IActionResult> SetMember(string name, PrincipalType type, string? principalName, ProjectRole role, string? password, CancellationToken cancellationToken)
    {
        var workflow = StartDetail(name);

        return ShowDetail(workflow, await workflow.SetMemberAsync(type, principalName, role, password, cancellationToken));
    }

    /// <summary>Takes a user or group out of the project.</summary>
    [Route("admin/projects/{name}"), HttpPost, PageHandler("RemoveMember"), LegacyPage(DetailPage)]
    public async Task<IActionResult> RemoveMember(string name, PrincipalType type, string? principalId, string? password, CancellationToken cancellationToken)
    {
        var workflow = StartDetail(name);

        return ShowDetail(workflow, await workflow.RemoveMemberAsync(type, principalId, password, cancellationToken));
    }

    /// <summary>Puts a repository in the project (server administrators only); it leaves any project it was in.</summary>
    [Route("admin/projects/{name}"), HttpPost, PageHandler("AssignRepository"), LegacyPage(DetailPage)]
    public async Task<IActionResult> AssignRepository(string name, string? repository, string? password, CancellationToken cancellationToken)
    {
        var workflow = StartDetail(name);

        return ShowDetail(workflow, await workflow.AssignRepositoryAsync(repository, password, cancellationToken));
    }

    /// <summary>Takes a repository out of the project (server administrators only).</summary>
    [Route("admin/projects/{name}"), HttpPost, PageHandler("UnassignRepository"), LegacyPage(DetailPage)]
    public async Task<IActionResult> UnassignRepository(string name, string? repository, string? password, CancellationToken cancellationToken)
    {
        var workflow = StartDetail(name);

        return ShowDetail(workflow, await workflow.UnassignRepositoryAsync(repository, password, cancellationToken));
    }

    /// <summary>Creates a permission target that applies only to the project's repositories.</summary>
    [Route("admin/projects/{name}"), HttpPost, PageHandler("CreateTarget"), LegacyPage(DetailPage)]
    public async Task<IActionResult> CreateTarget(string name, string? targetName, string? repositoryPatterns, string? includePatterns, string? excludePatterns, string? testPackage, CancellationToken cancellationToken)
    {
        var workflow = StartDetail(name, targetName, repositoryPatterns, includePatterns, excludePatterns, testPackage);

        return ShowDetail(workflow, await workflow.CreateTargetAsync(cancellationToken));
    }

    /// <summary>Previews the new target patterns without saving anything.</summary>
    [Route("admin/projects/{name}"), HttpPost, PageHandler("Preview"), LegacyPage(DetailPage)]
    public async Task<IActionResult> Preview(string name, string? targetName, string? repositoryPatterns, string? includePatterns, string? excludePatterns, string? testPackage, CancellationToken cancellationToken)
    {
        var workflow = StartDetail(name, targetName, repositoryPatterns, includePatterns, excludePatterns, testPackage);

        return ShowDetail(workflow, await workflow.PreviewPatternsAsync(cancellationToken));
    }

    /// <summary>Deletes the project (server administrators only) with its members and targets; repositories stay.</summary>
    [Route("admin/projects/{name}"), HttpPost, PageHandler("Delete"), LegacyPage(DetailPage)]
    public async Task<IActionResult> Delete(string name, string? password, CancellationToken cancellationToken)
    {
        var workflow = StartDetail(name);

        return ShowDetail(workflow, await workflow.DeleteAsync(password, cancellationToken));
    }

    /// <summary>A form posted to the project's own URL with no handler: the screen is rendered (the page had no default handler).</summary>
    [Route("admin/projects/{name}"), HttpPost, PageHandler, ImplicitPageHandler, LegacyPage(DetailPage)]
    public async Task<IActionResult> DetailPost(string name, CancellationToken cancellationToken)
    {
        var workflow = StartDetail(name);

        return ShowDetail(workflow, await workflow.GetAsync(cancellationToken));
    }

    private ProjectsWorkflow StartList(string? name, string? displayName)
    {
        var workflow = new ProjectsWorkflow(HttpContext, Url) { IsServerAdministrator = IsServerAdministrator, CurrentUserId = CurrentUserId };
        workflow.Model.Name = name;
        workflow.Model.DisplayName = displayName;

        return workflow;
    }

    private ProjectDetailWorkflow StartDetail(string? name, string? targetName = null, string? repositoryPatterns = null, string? includePatterns = null, string? excludePatterns = null, string? testPackage = null)
    {
        var workflow = new ProjectDetailWorkflow(HttpContext, Url) { IsServerAdministrator = IsServerAdministrator, CurrentUserId = CurrentUserId };
        var model = workflow.Model;
        model.Name = name;
        model.TargetName = targetName;
        model.RepositoryPatterns = repositoryPatterns;
        model.IncludePatterns = includePatterns;
        model.ExcludePatterns = excludePatterns;
        model.TestPackage = testPackage;

        return workflow;
    }

    private IActionResult ShowList(ProjectsWorkflow workflow, IActionResult? result)
    {
        Flash(workflow.Model);

        return result ?? View("Index", workflow.Model);
    }

    private IActionResult ShowDetail(ProjectDetailWorkflow workflow, IActionResult? result)
    {
        Flash(workflow.Model);

        return result ?? View("Detail", workflow.Model);
    }
}
