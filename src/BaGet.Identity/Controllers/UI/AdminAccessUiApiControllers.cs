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
/// The JSON twin of the groups screens (<c>/api/ui/admin/groups</c>): the list with the new-group form and one group (rename, members, delete).
/// <see cref="AdminGroupsController"/> calls it in process. Server administrators only (a session, or an <c>admin:api</c> key); every change that
/// touches membership or deletes asks the administrator's password in the body, and every write is refused with 503 while the server is read-only.
/// </summary>
[Route("api/ui/admin/groups")]
public sealed class AdminGroupsUiApiController : AccessUiApiControllerBase
{
    private AdminGroupsUseCase Groups => UseCase<AdminGroupsUseCase>();

    /// <summary>Lists the groups.</summary>
    [HttpGet(""), UiApi("/Admin/Groups")]
    public async Task<ActionResult<ScreenResult<GroupsViewModel>>> List(CancellationToken cancellationToken = default)
        => Respond(await Groups.ListAsync(await CallerAsync(cancellationToken), cancellationToken));

    /// <summary>Creates a group and points at it.</summary>
    [HttpPost(""), UiApi("/Admin/Groups")]
    public async Task<ActionResult<ScreenResult<GroupsViewModel>>> Create([FromBody] GroupCreation? form, CancellationToken cancellationToken = default)
        => Respond(await Groups.CreateAsync(await CallerAsync(cancellationToken), form ?? new GroupCreation(null, null), cancellationToken));

    /// <summary>Shows one group, with the member search for <paramref name="q"/>.</summary>
    [HttpGet("{name}"), UiApi("/Admin/GroupDetail")]
    public async Task<ActionResult<ScreenResult<GroupDetailViewModel>>> Get(string name, string? q, CancellationToken cancellationToken = default)
        => Respond(await Groups.GetAsync(await CallerAsync(cancellationToken), name, q, cancellationToken));

    /// <summary>Renames the group.</summary>
    [HttpPost("{name}/rename"), UiApi("/Admin/GroupDetail", Handler = "Rename")]
    public async Task<ActionResult<ScreenResult<GroupDetailViewModel>>> Rename(string name, [FromBody] GroupRename? form, CancellationToken cancellationToken = default)
        => Respond(await Groups.RenameAsync(await CallerAsync(cancellationToken), name, form ?? new GroupRename(null), cancellationToken));

    /// <summary>Adds a user to the group.</summary>
    [HttpPost("{name}/add-member"), UiApi("/Admin/GroupDetail", Handler = "AddMember")]
    public async Task<ActionResult<ScreenResult<GroupDetailViewModel>>> AddMember(string name, [FromBody] GroupMemberChange? form, CancellationToken cancellationToken = default)
        => Respond(await Groups.AddMemberAsync(await CallerAsync(cancellationToken), name, form ?? new GroupMemberChange(null, null), cancellationToken));

    /// <summary>Removes a user from the group.</summary>
    [HttpPost("{name}/remove-member"), UiApi("/Admin/GroupDetail", Handler = "RemoveMember")]
    public async Task<ActionResult<ScreenResult<GroupDetailViewModel>>> RemoveMember(string name, [FromBody] GroupMemberChange? form, CancellationToken cancellationToken = default)
        => Respond(await Groups.RemoveMemberAsync(await CallerAsync(cancellationToken), name, form ?? new GroupMemberChange(null, null), cancellationToken));

    /// <summary>Deletes the group with the grants and project roles that name it.</summary>
    [HttpPost("{name}/delete"), UiApi("/Admin/GroupDetail", Handler = "Delete")]
    public async Task<ActionResult<ScreenResult<GroupDetailViewModel>>> Delete(string name, [FromBody] PasswordForm? form, CancellationToken cancellationToken = default)
        => Respond(await Groups.DeleteAsync(await CallerAsync(cancellationToken), name, form ?? new PasswordForm(null), cancellationToken));
}

/// <summary>
/// The JSON twin of the permission target screens (<c>/api/ui/admin/permissions</c>): the list with the new-target form (server administrators only)
/// and one target (patterns, preview, grants, delete), which also admits the administrators of the target's project for a project target (the target
/// is the <c>name</c> route value). <see cref="AdminPermissionsController"/> calls it in process. Saving patterns, changing grants and deleting ask for
/// the password in the body; every write is refused with 503 while the server is read-only.
/// </summary>
[Route("api/ui/admin/permissions")]
public sealed class AdminPermissionsUiApiController : AccessUiApiControllerBase
{
    private const string DetailScreen = "/Admin/PermissionDetail";

    private AdminPermissionsUseCase Permissions => UseCase<AdminPermissionsUseCase>();

    /// <inheritdoc/>
    protected override async Task<bool> AuthorizeAsync(CancellationToken cancellationToken)
    {
        // Only the detail screen is widened to a project's administrators (the target is the name route value).
        if (ScreenName() != DetailScreen) return false;

        var services = HttpContext.RequestServices;

        return await AccessAuthorization.TargetAsync(
            services.GetRequiredService<IAccessAdministration>(),
            false,
            services.GetRequiredService<UserManager<BaGetUser>>().GetUserId(User),
            RouteData.Values["name"]?.ToString(),
            cancellationToken);
    }

    /// <summary>Lists the permission targets.</summary>
    [HttpGet(""), UiApi("/Admin/Permissions")]
    public async Task<ActionResult<ScreenResult<PermissionsViewModel>>> List(CancellationToken cancellationToken = default)
        => Respond(await Permissions.ListAsync(await CallerAsync(cancellationToken), cancellationToken));

    /// <summary>Creates a target and points at it.</summary>
    [HttpPost(""), UiApi("/Admin/Permissions")]
    public async Task<ActionResult<ScreenResult<PermissionsViewModel>>> Create([FromBody] PermissionTargetForm? form, CancellationToken cancellationToken = default)
        => Respond(await Permissions.CreateAsync(await CallerAsync(cancellationToken), form ?? new PermissionTargetForm(), cancellationToken));

    /// <summary>Previews the form patterns without saving anything.</summary>
    [HttpPost("preview"), UiApi("/Admin/Permissions", Handler = "Preview")]
    public async Task<ActionResult<ScreenResult<PermissionsViewModel>>> Preview([FromBody] PermissionTargetForm? form, CancellationToken cancellationToken = default)
        => Respond(await Permissions.PreviewAsync(await CallerAsync(cancellationToken), form ?? new PermissionTargetForm(), cancellationToken));

    /// <summary>Re-creates the built-in groups and targets that are missing (the upgrade defaults).</summary>
    [HttpPost("restore-built-ins"), UiApi("/Admin/Permissions", Handler = "RestoreBuiltIns")]
    public async Task<ActionResult<ScreenResult<PermissionsViewModel>>> RestoreBuiltIns([FromBody] PasswordForm? form, CancellationToken cancellationToken = default)
        => Respond(await Permissions.RestoreBuiltInsAsync(await CallerAsync(cancellationToken), form ?? new PasswordForm(null), cancellationToken));

    /// <summary>Shows one target. <paramref name="principalType"/> and <paramref name="principalName"/> pre-fill the grant form.</summary>
    [HttpGet("{name}"), UiApi(DetailScreen)]
    public async Task<ActionResult<ScreenResult<PermissionDetailViewModel>>> Get(string name, string? testPackage, PrincipalType? principalType, string? principalName, CancellationToken cancellationToken = default)
        => Respond(await Permissions.GetAsync(await CallerAsync(cancellationToken), name, testPackage, principalType, principalName, cancellationToken));

    /// <summary>Previews the form patterns (and the test package) without saving anything.</summary>
    [HttpPost("{name}/preview"), UiApi(DetailScreen, Handler = "Preview")]
    public async Task<ActionResult<ScreenResult<PermissionDetailViewModel>>> PreviewDetail(string name, [FromBody] PermissionTargetForm? form, CancellationToken cancellationToken = default)
        => Respond(await Permissions.PreviewDetailAsync(await CallerAsync(cancellationToken), name, form ?? new PermissionTargetForm(), cancellationToken));

    /// <summary>Replaces the target's patterns.</summary>
    [HttpPost("{name}/save"), UiApi(DetailScreen, Handler = "Save")]
    public async Task<ActionResult<ScreenResult<PermissionDetailViewModel>>> Save(string name, [FromBody] PermissionTargetForm? form, CancellationToken cancellationToken = default)
        => Respond(await Permissions.SaveAsync(await CallerAsync(cancellationToken), name, form ?? new PermissionTargetForm(), cancellationToken));

    /// <summary>Gives a principal the ticked actions through the target, replacing an earlier grant to it.</summary>
    [HttpPost("{name}/set-grant"), UiApi(DetailScreen, Handler = "SetGrant")]
    public async Task<ActionResult<ScreenResult<PermissionDetailViewModel>>> SetGrant(string name, [FromBody] GrantForm? form, CancellationToken cancellationToken = default)
        => Respond(await Permissions.SetGrantAsync(await CallerAsync(cancellationToken), name, form ?? new GrantForm(null, null, null, null), cancellationToken));

    /// <summary>Removes a principal's grant.</summary>
    [HttpPost("{name}/remove-grant"), UiApi(DetailScreen, Handler = "RemoveGrant")]
    public async Task<ActionResult<ScreenResult<PermissionDetailViewModel>>> RemoveGrant(string name, [FromBody] PrincipalRemoval? form, CancellationToken cancellationToken = default)
        => Respond(await Permissions.RemoveGrantAsync(await CallerAsync(cancellationToken), name, form ?? new PrincipalRemoval(PrincipalType.Group, null, null), cancellationToken));

    /// <summary>Deletes the target and its grants.</summary>
    [HttpPost("{name}/delete"), UiApi(DetailScreen, Handler = "Delete")]
    public async Task<ActionResult<ScreenResult<PermissionDetailViewModel>>> Delete(string name, [FromBody] PasswordForm? form, CancellationToken cancellationToken = default)
        => Respond(await Permissions.DeleteAsync(await CallerAsync(cancellationToken), name, form ?? new PasswordForm(null), cancellationToken));
}

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

/// <summary>
/// The JSON twin of the access audit log (<c>/api/ui/admin/audit</c>): one entry per change to groups, memberships, permission targets, grants,
/// projects, the repositories' project and anonymous-read switch, newest first, filtered by <c>actor</c>, <c>subject</c>, <c>from</c> and <c>to</c>.
/// Read only; server administrators only. <see cref="AdminAuditController"/> calls it in process.
/// </summary>
[Route("api/ui/admin/audit")]
public sealed class AdminAuditUiApiController : AccessUiApiControllerBase
{
    /// <summary>Lists the entries that match the filter.</summary>
    [HttpGet(""), UiApi("/Admin/Audit")]
    public async Task<ActionResult<ScreenResult<AuditViewModel>>> Get(string? actor, string? subject, string? from, string? to, CancellationToken cancellationToken = default)
        => Respond(await UseCase<AdminAuditUseCase>().GetAsync(new AuditQuery(actor, subject, from, to), cancellationToken));
}
