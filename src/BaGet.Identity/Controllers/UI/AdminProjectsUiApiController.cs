using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.Application;
using BaGet.Identity.Application.Models;
using BaGet.Identity.Application.Screens;
using BaGet.Identity.Application.UseCases;
using BaGet.Identity.Controllers.Base;
using BaGet.Identity.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace BaGet.Identity.Controllers.UI;

/// <summary>
/// The JSON twin of the project screens (<c>/api/ui/admin/projects</c>): the list with the new-project form and one project (repositories, members,
/// the project's permission targets, delete). Server administrators, and a project's administrators for the projects they administer (the project is the
/// <c>name</c> route value; only server administrators create and delete projects and assign repositories). <see cref="AdminProjectsController"/> calls
/// it in process. Member changes and deletions ask for the password in the body; every write is refused with 503 while the server is read-only.
/// </summary>
[Route("api/ui/admin/projects")]
public sealed class AdminProjectsUiApiController : AccessUiApiControllerBase
{
    private const string DetailScreen = "/Admin/ProjectDetail";

    private AdminProjectsUseCase Projects => UseCase<AdminProjectsUseCase>();

    /// <inheritdoc/>
    protected override async Task<bool> AuthorizeAsync(CancellationToken cancellationToken)
    {
        var services = HttpContext.RequestServices;
        var access = services.GetRequiredService<IAccessAdministration>();
        var userId = services.GetRequiredService<UserManager<BaGetUser>>().GetUserId(User);

        return ScreenName() == DetailScreen
            ? await AccessAuthorization.ProjectAsync(access, false, userId, RouteData.Values["name"]?.ToString(), cancellationToken)
            : await AccessAuthorization.ProjectsAsync(access, false, userId, cancellationToken);
    }

    /// <summary>Lists the projects the caller may open.</summary>
    [HttpGet(""), UiApi("/Admin/Projects")]
    public async Task<ActionResult<ScreenResult<ProjectsViewModel>>> List(CancellationToken cancellationToken = default)
        => Respond(await Projects.ListAsync(await CallerAsync(cancellationToken), cancellationToken));

    /// <summary>Creates a project (server administrators only) and points at it.</summary>
    [HttpPost(""), UiApi("/Admin/Projects")]
    public async Task<ActionResult<ScreenResult<ProjectsViewModel>>> Create([FromBody] ProjectCreation? form, CancellationToken cancellationToken = default)
        => Respond(await Projects.CreateAsync(await CallerAsync(cancellationToken), form ?? new ProjectCreation(null, null), cancellationToken));

    /// <summary>Shows one project.</summary>
    [HttpGet("{name}"), UiApi(DetailScreen)]
    public async Task<ActionResult<ScreenResult<ProjectDetailViewModel>>> Get(string name, CancellationToken cancellationToken = default)
        => Respond(await Projects.GetAsync(await CallerAsync(cancellationToken), name, cancellationToken));

    /// <summary>Gives a user or group a role in the project, replacing an earlier one.</summary>
    [HttpPost("{name}/set-member"), UiApi(DetailScreen, Handler = "SetMember")]
    public async Task<ActionResult<ScreenResult<ProjectDetailViewModel>>> SetMember(string name, [FromBody] ProjectMemberForm? form, CancellationToken cancellationToken = default)
        => Respond(await Projects.SetMemberAsync(await CallerAsync(cancellationToken), name, form ?? new ProjectMemberForm(PrincipalType.User, null, ProjectRole.Viewer, null), cancellationToken));

    /// <summary>Takes a user or group out of the project.</summary>
    [HttpPost("{name}/remove-member"), UiApi(DetailScreen, Handler = "RemoveMember")]
    public async Task<ActionResult<ScreenResult<ProjectDetailViewModel>>> RemoveMember(string name, [FromBody] PrincipalRemoval? form, CancellationToken cancellationToken = default)
        => Respond(await Projects.RemoveMemberAsync(await CallerAsync(cancellationToken), name, form ?? new PrincipalRemoval(PrincipalType.User, null, null), cancellationToken));

    /// <summary>Puts a repository in the project (server administrators only); it leaves any project it was in.</summary>
    [HttpPost("{name}/assign-repository"), UiApi(DetailScreen, Handler = "AssignRepository")]
    public async Task<ActionResult<ScreenResult<ProjectDetailViewModel>>> AssignRepository(string name, [FromBody] ProjectRepositoryForm? form, CancellationToken cancellationToken = default)
        => Respond(await Projects.AssignRepositoryAsync(await CallerAsync(cancellationToken), name, form ?? new ProjectRepositoryForm(null, null), cancellationToken));

    /// <summary>Takes a repository out of the project (server administrators only).</summary>
    [HttpPost("{name}/unassign-repository"), UiApi(DetailScreen, Handler = "UnassignRepository")]
    public async Task<ActionResult<ScreenResult<ProjectDetailViewModel>>> UnassignRepository(string name, [FromBody] ProjectRepositoryForm? form, CancellationToken cancellationToken = default)
        => Respond(await Projects.UnassignRepositoryAsync(await CallerAsync(cancellationToken), name, form ?? new ProjectRepositoryForm(null, null), cancellationToken));

    /// <summary>Creates a permission target that applies only to the project's repositories.</summary>
    [HttpPost("{name}/create-target"), UiApi(DetailScreen, Handler = "CreateTarget")]
    public async Task<ActionResult<ScreenResult<ProjectDetailViewModel>>> CreateTarget(string name, [FromBody] ProjectTargetForm? form, CancellationToken cancellationToken = default)
        => Respond(await Projects.CreateTargetAsync(await CallerAsync(cancellationToken), name, form ?? new ProjectTargetForm(null, null, null, null, null), cancellationToken));

    /// <summary>Previews the new target's patterns without saving anything.</summary>
    [HttpPost("{name}/preview"), UiApi(DetailScreen, Handler = "Preview")]
    public async Task<ActionResult<ScreenResult<ProjectDetailViewModel>>> Preview(string name, [FromBody] ProjectTargetForm? form, CancellationToken cancellationToken = default)
        => Respond(await Projects.PreviewAsync(await CallerAsync(cancellationToken), name, form ?? new ProjectTargetForm(null, null, null, null, null), cancellationToken));

    /// <summary>Deletes the project (server administrators only) with its members and targets; the repositories stay.</summary>
    [HttpPost("{name}/delete"), UiApi(DetailScreen, Handler = "Delete")]
    public async Task<ActionResult<ScreenResult<ProjectDetailViewModel>>> Delete(string name, [FromBody] PasswordForm? form, CancellationToken cancellationToken = default)
        => Respond(await Projects.DeleteAsync(await CallerAsync(cancellationToken), name, form ?? new PasswordForm(null), cancellationToken));
}
