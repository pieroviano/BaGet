using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Web.Api;

/// <summary>Creates a group.</summary>
public sealed class CreateGroupRequest
{
    /// <summary>The group's name.</summary>
    public string? Name { get; set; }

    /// <summary>What the group is for.</summary>
    public string? Description { get; set; }
}

/// <summary>Renames a group.</summary>
public sealed class RenameGroupRequest
{
    /// <summary>The new name (the built-in groups keep theirs).</summary>
    public string? NewName { get; set; }
}

/// <summary>Creates a project.</summary>
public sealed class CreateProjectRequest
{
    /// <summary>The project key (lowercase letters, digits and hyphens).</summary>
    public string? Name { get; set; }

    /// <summary>A name for people.</summary>
    public string? DisplayName { get; set; }
}

/// <summary>Gives a user or group a role in a project.</summary>
public sealed class ProjectMemberRequest
{
    /// <summary><c>User</c> or <c>Group</c>.</summary>
    public string? Type { get; set; }

    /// <summary>The user's or group's name.</summary>
    public string? Principal { get; set; }

    /// <summary>The role: <c>Admin</c>, <c>Developer</c>, <c>Viewer</c>…</summary>
    public string? Role { get; set; }
}

/// <summary>The patterns of a permission target.</summary>
public sealed class PermissionTargetRequest
{
    /// <summary>The target's name (on create).</summary>
    public string? Name { get; set; }

    /// <summary>Repository names or <c>*</c> globs.</summary>
    public List<string> RepositoryPatterns { get; set; } = new();

    /// <summary>Package id globs; empty = every package.</summary>
    public List<string> IncludePatterns { get; set; } = new();

    /// <summary>Package id globs to leave out.</summary>
    public List<string> ExcludePatterns { get; set; } = new();

    /// <summary>The administering project's name (on create).</summary>
    public string? Project { get; set; }
}

/// <summary>Asks what a set of patterns selects.</summary>
public sealed class PatternPreviewRequest
{
    /// <summary>Repository names or globs.</summary>
    public List<string> RepositoryPatterns { get; set; } = new();

    /// <summary>Package id globs.</summary>
    public List<string> IncludePatterns { get; set; } = new();

    /// <summary>Package id globs left out.</summary>
    public List<string> ExcludePatterns { get; set; } = new();

    /// <summary>A package id to test against the include and exclude patterns.</summary>
    public string? TestPackage { get; set; }
}

/// <summary>What a set of patterns selects.</summary>
public sealed record PatternPreviewDto(IReadOnlyList<string> Repositories, string? TestPackage, PatternTest? Test);

/// <summary>Gives a principal actions through a target.</summary>
public sealed class GrantRequest
{
    /// <summary><c>User</c>, <c>Group</c> or <c>Organization</c>.</summary>
    public string? Type { get; set; }

    /// <summary>The principal's name.</summary>
    public string? Principal { get; set; }

    /// <summary>Some of <c>Read</c>, <c>Annotate</c>, <c>Deploy</c>, <c>Delete</c>, <c>Manage</c>; none removes the grant.</summary>
    public List<string> Actions { get; set; } = new();
}

/// <summary>A change that needs the relational access model; the Azure Table database has none.</summary>
public abstract class AccessApiController : AdminApiController
{
    /// <summary>The access model's administration.</summary>
    protected IAccessAdministration AccessAdmin => Service<IAccessAdministration>();

    /// <summary>422 where the database has no access model, else null.</summary>
    protected IActionResult? Unavailable()
        => Service<IContext>() is NullContext
            ? ApiProblems.Create(422, "not-supported", "Not supported here", "This database has no access model (Azure Table): everyone reads and any authenticated caller writes.")
            : null;

    /// <summary>The principal type named by <paramref name="text"/>, or null with the problem to send.</summary>
    protected bool TryPrincipalType(string? text, out PrincipalType type, out IActionResult? problem)
    {
        problem = null;
        if (Enum.TryParse(text, ignoreCase: true, out type) && Enum.IsDefined(type)) return true;

        problem = ApiProblems.BadRequest($"Unknown principal type '{text}'. Use User, Group or Organization.");

        return false;
    }

    /// <summary>The id of the named principal, or throws the refusal the pages show.</summary>
    protected async Task<string> ResolveAsync(PrincipalType type, string? name)
        => await AccessAdmin.ResolvePrincipalAsync(type, name ?? string.Empty, Cancellation)
           ?? throw new AccessModelException($"There is no {type.ToString().ToLowerInvariant()} named '{name}'.");
}

/// <summary>
/// Projects (groups of repositories with their own administrators): the Projects and ProjectDetail pages' operations. Server
/// administrators only, as the API as a whole is (the pages also admit a project's own administrators; the API does not delegate).
/// </summary>
[Route(ApiCatalog.RootTemplate + "/projects")]
public class ProjectsController : AccessApiController
{
    /// <summary>Lists projects.</summary>
    [HttpGet]
    [ApiOperation("List projects with their repository and member counts", "/Admin/Projects", Response = typeof(ProjectSummary), PagedList = true)]
    public async Task<IActionResult> List(int? page, int? pageSize)
        => Unavailable() ?? RespondPage(await AccessAdmin.ListProjectsAsync(Cancellation), page, pageSize);

    /// <summary>One project.</summary>
    [HttpGet("{name}")]
    [ApiOperation("Show a project with its repositories, members and permission targets", "/Admin/ProjectDetail", Response = typeof(ProjectDetails))]
    public async Task<IActionResult> Get(string name)
    {
        if (Unavailable() is { } unavailable) return unavailable;

        return await AccessAdmin.GetProjectAsync(name, Cancellation) is { } project ? Respond(project) : ApiProblems.NotFound($"project '{name}'");
    }

    /// <summary>Creates a project.</summary>
    [HttpPost]
    [ApiOperation("Create a project", "/Admin/Projects", Response = typeof(ProjectSummary), Status = 201)]
    public async Task<IActionResult> Create([FromBody] CreateProjectRequest request)
    {
        if (Unavailable() is { } unavailable) return unavailable;
        ThrowIfClosed();

        var project = await AccessAdmin.CreateProjectAsync(request.Name ?? string.Empty, string.IsNullOrWhiteSpace(request.DisplayName) ? null : request.DisplayName.Trim(), Actor, Cancellation);
        var created = (await AccessAdmin.ListProjectsAsync(Cancellation)).First(p => p.Key == project.Key);

        return Respond(created, 201, $"{ApiCatalog.Root}/projects/{Uri.EscapeDataString(project.Name!)}");
    }

    /// <summary>Deletes a project; its repositories stay.</summary>
    [HttpDelete("{name}")]
    [ApiOperation("Delete a project with its members and targets (its repositories stay)", "/Admin/ProjectDetail", Confirm = true, Status = 204)]
    public async Task<IActionResult> Delete(string name)
    {
        if (Unavailable() is { } unavailable) return unavailable;
        ThrowIfClosed();
        if (await AccessAdmin.GetProjectAsync(name, Cancellation) == null) return ApiProblems.NotFound($"project '{name}'");
        if (await RequireConfirmationAsync() is { } refusal) return refusal;

        await AccessAdmin.DeleteProjectAsync(name, Actor, Cancellation);

        return Done();
    }

    /// <summary>Gives a user or group a role.</summary>
    [HttpPut("{name}/members")]
    [ApiOperation("Give a user or group a role in a project", "/Admin/ProjectDetail", Confirm = true, Status = 204)]
    public async Task<IActionResult> SetMember(string name, [FromBody] ProjectMemberRequest request)
    {
        if (Unavailable() is { } unavailable) return unavailable;
        ThrowIfClosed();
        if (await AccessAdmin.GetProjectAsync(name, Cancellation) == null) return ApiProblems.NotFound($"project '{name}'");
        if (!TryPrincipalType(request.Type, out var type, out var problem)) return problem!;
        if (type == PrincipalType.Organization) return ApiProblems.BadRequest("A project member is a user or a group.");
        if (!Enum.TryParse<ProjectRole>(request.Role, ignoreCase: true, out var role) || !Enum.IsDefined(role))
        {
            return ApiProblems.BadRequest($"Unknown role '{request.Role}'. Use {string.Join(", ", Enum.GetNames(typeof(ProjectRole)))}.");
        }

        if (await RequireConfirmationAsync() is { } refusal) return refusal;

        await AccessAdmin.SetProjectMemberAsync(name, type, await ResolveAsync(type, request.Principal), role, Actor, Cancellation);

        return Done();
    }

    /// <summary>Takes a user or group out of a project.</summary>
    [HttpDelete("{name}/members/{type}/{principal}")]
    [ApiOperation("Remove a user or group from a project", "/Admin/ProjectDetail", Confirm = true, Status = 204)]
    public async Task<IActionResult> RemoveMember(string name, string type, string principal)
    {
        if (Unavailable() is { } unavailable) return unavailable;
        ThrowIfClosed();
        if (await AccessAdmin.GetProjectAsync(name, Cancellation) == null) return ApiProblems.NotFound($"project '{name}'");
        if (!TryPrincipalType(type, out var parsed, out var problem)) return problem!;
        if (await RequireConfirmationAsync() is { } refusal) return refusal;

        await AccessAdmin.RemoveProjectMemberAsync(name, parsed, await ResolveAsync(parsed, principal), Actor, Cancellation);

        return Done();
    }

    /// <summary>Puts a repository in the project.</summary>
    [HttpPut("{name}/repositories/{repository}")]
    [ApiOperation("Put a repository in a project (it leaves any project it was in)", "/Admin/ProjectDetail", Confirm = true, Status = 204)]
    public async Task<IActionResult> AssignRepository(string name, string repository)
    {
        if (Unavailable() is { } unavailable) return unavailable;
        ThrowIfClosed();
        if (await AccessAdmin.GetProjectAsync(name, Cancellation) == null) return ApiProblems.NotFound($"project '{name}'");
        if (await RequireConfirmationAsync() is { } refusal) return refusal;

        await AccessAdmin.SetRepositoryProjectAsync(repository, name, Actor, Cancellation);

        return Done();
    }

    /// <summary>Takes a repository out of the project.</summary>
    [HttpDelete("{name}/repositories/{repository}")]
    [ApiOperation("Take a repository out of a project", "/Admin/ProjectDetail", Confirm = true, Status = 204)]
    public async Task<IActionResult> UnassignRepository(string name, string repository)
    {
        if (Unavailable() is { } unavailable) return unavailable;
        ThrowIfClosed();
        var project = await AccessAdmin.GetProjectAsync(name, Cancellation);
        if (project == null) return ApiProblems.NotFound($"project '{name}'");
        if (!project.Repositories.Contains(repository, StringComparer.Ordinal)) return ApiProblems.NotFound($"repository '{repository}' in project '{name}'");
        if (await RequireConfirmationAsync() is { } refusal) return refusal;

        await AccessAdmin.SetRepositoryProjectAsync(repository, null, Actor, Cancellation);

        return Done();
    }
}

/// <summary>Groups of users: the Groups and GroupDetail pages' operations.</summary>
[Route(ApiCatalog.RootTemplate + "/groups")]
public class GroupsController : AccessApiController
{
    /// <summary>Lists groups.</summary>
    [HttpGet]
    [ApiOperation("List groups with their member counts", "/Admin/Groups", Response = typeof(GroupSummary), PagedList = true)]
    public async Task<IActionResult> List(int? page, int? pageSize)
        => Unavailable() ?? RespondPage(await AccessAdmin.ListGroupsAsync(Cancellation), page, pageSize);

    /// <summary>One group.</summary>
    [HttpGet("{name}")]
    [ApiOperation("Show a group with its members and the grants naming it", "/Admin/GroupDetail", Response = typeof(GroupDetails))]
    public async Task<IActionResult> Get(string name)
    {
        if (Unavailable() is { } unavailable) return unavailable;

        return await AccessAdmin.GetGroupAsync(name, Cancellation) is { } group ? Respond(group) : ApiProblems.NotFound($"group '{name}'");
    }

    /// <summary>Creates a group.</summary>
    [HttpPost]
    [ApiOperation("Create a group", "/Admin/Groups", Response = typeof(GroupSummary), Status = 201)]
    public async Task<IActionResult> Create([FromBody] CreateGroupRequest request)
    {
        if (Unavailable() is { } unavailable) return unavailable;
        ThrowIfClosed();

        var group = await AccessAdmin.CreateGroupAsync(request.Name ?? string.Empty, request.Description, Actor, Cancellation);

        return Respond((await AccessAdmin.GetGroupAsync(group.Name!, Cancellation))!.Group, 201, $"{ApiCatalog.Root}/groups/{Uri.EscapeDataString(group.Name!)}");
    }

    /// <summary>Renames a group.</summary>
    [HttpPatch("{name}")]
    [ApiOperation("Rename a group", "/Admin/GroupDetail", Response = typeof(GroupSummary))]
    public async Task<IActionResult> Rename(string name, [FromBody] RenameGroupRequest request)
    {
        if (Unavailable() is { } unavailable) return unavailable;
        ThrowIfClosed();
        if (await AccessAdmin.GetGroupAsync(name, Cancellation) == null) return ApiProblems.NotFound($"group '{name}'");

        await AccessAdmin.RenameGroupAsync(name, request.NewName ?? string.Empty, Actor, Cancellation);

        return Respond((await AccessAdmin.GetGroupAsync(request.NewName!.Trim(), Cancellation))?.Group);
    }

    /// <summary>Deletes a group.</summary>
    [HttpDelete("{name}")]
    [ApiOperation("Delete a group with the grants and project roles that name it", "/Admin/GroupDetail", Confirm = true, Status = 204)]
    public async Task<IActionResult> Delete(string name)
    {
        if (Unavailable() is { } unavailable) return unavailable;
        ThrowIfClosed();
        if (await AccessAdmin.GetGroupAsync(name, Cancellation) == null) return ApiProblems.NotFound($"group '{name}'");
        if (await RequireConfirmationAsync() is { } refusal) return refusal;

        await AccessAdmin.DeleteGroupAsync(name, Actor, Cancellation);

        return Done();
    }

    /// <summary>Adds a user to a group.</summary>
    [HttpPut("{name}/members/{user}")]
    [ApiOperation("Add a user to a group", "/Admin/GroupDetail", Confirm = true, Status = 204)]
    public async Task<IActionResult> AddMember(string name, string user)
    {
        if (Unavailable() is { } unavailable) return unavailable;
        ThrowIfClosed();
        if (await AccessAdmin.GetGroupAsync(name, Cancellation) == null) return ApiProblems.NotFound($"group '{name}'");
        if (await AccessAdmin.ResolvePrincipalAsync(PrincipalType.User, user, Cancellation) is not { } userId) return ApiProblems.NotFound($"user '{user}'");
        if (await RequireConfirmationAsync() is { } refusal) return refusal;

        await AccessAdmin.AddGroupMemberAsync(name, userId, Actor, Cancellation);

        return Done();
    }

    /// <summary>Removes a user from a group.</summary>
    [HttpDelete("{name}/members/{user}")]
    [ApiOperation("Remove a user from a group", "/Admin/GroupDetail", Confirm = true, Status = 204)]
    public async Task<IActionResult> RemoveMember(string name, string user)
    {
        if (Unavailable() is { } unavailable) return unavailable;
        ThrowIfClosed();
        if (await AccessAdmin.GetGroupAsync(name, Cancellation) == null) return ApiProblems.NotFound($"group '{name}'");
        if (await AccessAdmin.ResolvePrincipalAsync(PrincipalType.User, user, Cancellation) is not { } userId) return ApiProblems.NotFound($"user '{user}'");
        if (await RequireConfirmationAsync() is { } refusal) return refusal;

        await AccessAdmin.RemoveGroupMemberAsync(name, userId, Actor, Cancellation);

        return Done();
    }
}

/// <summary>Permission targets (repository and package patterns with the actions principals hold): the Permissions and PermissionDetail pages' operations.</summary>
[Route(ApiCatalog.RootTemplate + "/permission-targets")]
public class PermissionTargetsController : AccessApiController
{
    /// <summary>Lists targets.</summary>
    [HttpGet]
    [ApiOperation("List permission targets", "/Admin/Permissions", Response = typeof(TargetSummary), PagedList = true)]
    public async Task<IActionResult> List(int? page, int? pageSize)
        => Unavailable() ?? RespondPage(await AccessAdmin.ListTargetsAsync(Cancellation), page, pageSize);

    /// <summary>One target.</summary>
    [HttpGet("{name}")]
    [ApiOperation("Show a permission target with its grants", "/Admin/PermissionDetail", Response = typeof(TargetDetails))]
    public async Task<IActionResult> Get(string name)
    {
        if (Unavailable() is { } unavailable) return unavailable;

        return await AccessAdmin.GetTargetAsync(name, Cancellation) is { } target ? Respond(target) : ApiProblems.NotFound($"permission target '{name}'");
    }

    /// <summary>Creates a target without grants.</summary>
    [HttpPost]
    [ApiOperation("Create a permission target (add grants afterwards)", "/Admin/Permissions", Response = typeof(TargetSummary), Status = 201)]
    public async Task<IActionResult> Create([FromBody] PermissionTargetRequest request)
    {
        if (Unavailable() is { } unavailable) return unavailable;
        ThrowIfClosed();

        int? projectKey = null;
        if (!string.IsNullOrWhiteSpace(request.Project))
        {
            var project = await AccessAdmin.GetProjectAsync(request.Project.Trim(), Cancellation)
                          ?? throw new AccessModelException($"There is no project named '{request.Project}'.");
            projectKey = project.Project.Key;
        }

        var target = await AccessAdmin.CreateTargetAsync(request.Name ?? string.Empty, request.RepositoryPatterns, request.IncludePatterns, request.ExcludePatterns, projectKey, Actor, Cancellation);

        return Respond((await AccessAdmin.GetTargetAsync(target.Name!, Cancellation))!.Target, 201, $"{ApiCatalog.Root}/permission-targets/{Uri.EscapeDataString(target.Name!)}");
    }

    /// <summary>Replaces a target's patterns.</summary>
    [HttpPut("{name}")]
    [ApiOperation("Replace a permission target's repository and package patterns", "/Admin/PermissionDetail", Confirm = true, Response = typeof(TargetSummary))]
    public async Task<IActionResult> Update(string name, [FromBody] PermissionTargetRequest request)
    {
        if (Unavailable() is { } unavailable) return unavailable;
        ThrowIfClosed();
        if (await AccessAdmin.GetTargetAsync(name, Cancellation) == null) return ApiProblems.NotFound($"permission target '{name}'");
        if (await RequireConfirmationAsync() is { } refusal) return refusal;

        await AccessAdmin.UpdateTargetAsync(name, request.RepositoryPatterns, request.IncludePatterns, request.ExcludePatterns, Actor, Cancellation);

        return Respond((await AccessAdmin.GetTargetAsync(name, Cancellation))!.Target);
    }

    /// <summary>Deletes a target and its grants.</summary>
    [HttpDelete("{name}")]
    [ApiOperation("Delete a permission target and its grants", "/Admin/PermissionDetail", Confirm = true, Status = 204)]
    public async Task<IActionResult> Delete(string name)
    {
        if (Unavailable() is { } unavailable) return unavailable;
        ThrowIfClosed();
        if (await AccessAdmin.GetTargetAsync(name, Cancellation) == null) return ApiProblems.NotFound($"permission target '{name}'");
        if (await RequireConfirmationAsync() is { } refusal) return refusal;

        await AccessAdmin.DeleteTargetAsync(name, Actor, Cancellation);

        return Done();
    }

    /// <summary>Sets a principal's actions through a target.</summary>
    [HttpPut("{name}/grants")]
    [ApiOperation("Give a user, group or organization actions through a permission target (none removes the grant)", "/Admin/PermissionDetail", Confirm = true, Response = typeof(TargetDetails))]
    public async Task<IActionResult> SetGrant(string name, [FromBody] GrantRequest request)
    {
        if (Unavailable() is { } unavailable) return unavailable;
        ThrowIfClosed();
        if (await AccessAdmin.GetTargetAsync(name, Cancellation) == null) return ApiProblems.NotFound($"permission target '{name}'");
        if (!TryPrincipalType(request.Type, out var type, out var problem)) return problem!;

        var actions = AccessAction.None;
        foreach (var text in request.Actions ?? new List<string>())
        {
            if (!Enum.TryParse<AccessAction>(text, ignoreCase: true, out var action) || action == AccessAction.All || action == AccessAction.None || !Enum.IsDefined(action))
            {
                return ApiProblems.BadRequest($"Unknown action '{text}'. Use Read, Annotate, Deploy, Delete or Manage.");
            }

            actions |= action;
        }

        if (await RequireConfirmationAsync() is { } refusal) return refusal;

        await AccessAdmin.SetGrantAsync(name, type, await ResolveAsync(type, request.Principal), actions, Actor, Cancellation);

        return Respond(await AccessAdmin.GetTargetAsync(name, Cancellation));
    }

    /// <summary>Previews what patterns select.</summary>
    [HttpPost("preview")]
    [ApiOperation("Preview which repositories (and whether a package) a set of patterns selects; saves nothing", "/Admin/Permissions", Response = typeof(PatternPreviewDto))]
    public async Task<IActionResult> Preview([FromBody] PatternPreviewRequest request)
    {
        if (Unavailable() is { } unavailable) return unavailable;

        var all = await Service<IRepositoryService>().ListAsync(Cancellation);
        var names = all.Where(r => AccessPatterns.IsMatch(request.RepositoryPatterns, r.Name)).Select(r => r.Name).OrderBy(n => n, StringComparer.Ordinal).ToList();
        var package = request.TestPackage?.Trim();

        return Respond(new PatternPreviewDto(
            names,
            string.IsNullOrEmpty(package) ? null : package,
            string.IsNullOrEmpty(package) ? null : AccessPatterns.Test(request.IncludePatterns, request.ExcludePatterns, package)));
    }

    /// <summary>Re-creates the built-in groups and targets that are missing.</summary>
    [HttpPost("restore-built-ins")]
    [ApiOperation("Re-create the built-in groups and permission targets that are missing", "/Admin/Permissions", Confirm = true, Status = 204)]
    public async Task<IActionResult> RestoreBuiltIns()
    {
        if (Unavailable() is { } unavailable) return unavailable;
        ThrowIfClosed();
        if (await RequireConfirmationAsync() is { } refusal) return refusal;

        await AccessAdmin.RestoreBuiltInsAsync(Actor, Cancellation);

        return Done();
    }
}

/// <summary>The access audit: the Audit page's list.</summary>
[Route(ApiCatalog.RootTemplate + "/audit")]
public class AuditController : AccessApiController
{
    /// <summary>The audit entries, newest first.</summary>
    [HttpGet]
    [ApiOperation("List access-audit entries, newest first, optionally by actor, subject or time", "/Admin/Audit", Response = typeof(AccessAuditEntry), PagedList = true)]
    public async Task<IActionResult> List(string? actor, string? subject, DateTime? from, DateTime? to, int? page, int? pageSize)
    {
        if (Unavailable() is { } unavailable) return unavailable;

        var filter = new AuditFilter(
            string.IsNullOrWhiteSpace(actor) ? null : actor.Trim(),
            string.IsNullOrWhiteSpace(subject) ? null : subject.Trim(),
            from?.ToUniversalTime(),
            to?.ToUniversalTime());

        return RespondPage(await AccessAdmin.AuditAsync(filter, 1000, Cancellation), page, pageSize);
    }
}
