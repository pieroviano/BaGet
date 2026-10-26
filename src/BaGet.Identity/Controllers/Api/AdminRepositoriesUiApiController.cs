using System.Threading;
using System.Threading.Tasks;
using BaGet.Identity.Application;
using BaGet.Identity.Mvc;
using BaGet.Identity.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Identity.Controllers;

/// <summary>
/// The JSON twin of the administrator's repository screens (<c>/api/ui/admin/repositories</c>): the list with the create forms, one repository
/// (<c>{name}</c>: anonymous read, upstream, cache, members, Maven policy) and its deletion (<c>{name}/delete</c>). <see cref="AdminRepositoriesController"/> calls
/// it in process. Every write is refused with 503 while the server is read-only; creating or changing a remote repository, purging its cache and testing its
/// connection ask the administrator's password in the body; a refused change answers 400 with the screen's model and the reason. No answer carries a password.
/// <para>
/// The create handlers live under <c>create/...</c> so that no repository name (<c>format</c>, <c>virtual</c>...) can shadow them.
/// </para>
/// </summary>
[Route("api/ui/admin/repositories")]
public sealed class AdminRepositoriesUiApiController : AdminUiApiControllerBase
{
    private AdminRepositoriesUseCase List => UseCase<AdminRepositoriesUseCase>();

    private AdminRepositoryEditUseCase Edit => UseCase<AdminRepositoryEditUseCase>();

    private AdminRepositoryDeleteUseCase Delete => UseCase<AdminRepositoryDeleteUseCase>();

    private string ReturnPage => Request.Path + Request.QueryString;

    // ---------------------------------------------------------------- Repositories

    /// <summary>Lists the repositories (narrowed by <paramref name="format"/>, <paramref name="kind"/>, <paramref name="q"/>) and describes the create forms.</summary>
    [HttpGet(""), UiApi("/Admin/Repositories")]
    public async Task<ActionResult<ScreenResult<RepositoriesViewModel>>> GetRepositories(string? format = null, string? kind = null, string? q = null, CancellationToken cancellationToken = default)
        => Respond(await List.GetAsync(User, format, kind, q, ReturnPage, cancellationToken));

    /// <summary>Creates a local NuGet repository (<c>name</c>).</summary>
    [HttpPost(""), UiApi("/Admin/Repositories")]
    public async Task<ActionResult<ScreenResult<RepositoriesViewModel>>> Create([FromBody] RepositoryCreateForm? form, CancellationToken cancellationToken = default)
        => Respond(await List.CreateAsync(User, form, ReturnPage, cancellationToken));

    /// <summary>Creates a local repository of a format other than NuGet (<c>name</c>, <c>format</c>, <c>fields</c>, <c>anonymousRead</c>).</summary>
    [HttpPost("create/format"), UiApi("/Admin/Repositories", Handler = "Format")]
    public async Task<ActionResult<ScreenResult<RepositoriesViewModel>>> CreateFormat([FromBody] RepositoryCreateForm? form, CancellationToken cancellationToken = default)
        => Respond(await List.CreateFormatAsync(User, form, ReturnPage, cancellationToken));

    /// <summary>Creates a virtual repository with no members (<c>name</c>, <c>format</c>, <c>anonymousRead</c>).</summary>
    [HttpPost("create/virtual"), UiApi("/Admin/Repositories", Handler = "Virtual")]
    public async Task<ActionResult<ScreenResult<RepositoriesViewModel>>> CreateVirtual([FromBody] RepositoryCreateForm? form, CancellationToken cancellationToken = default)
        => Respond(await List.CreateVirtualAsync(User, form, ReturnPage, cancellationToken));

    /// <summary>Creates a remote repository (<c>name</c>, <c>format</c>, <c>remote</c>, <c>anonymousRead</c>, <c>password</c>).</summary>
    [HttpPost("create/remote"), UiApi("/Admin/Repositories", Handler = "Remote")]
    public async Task<ActionResult<ScreenResult<RepositoriesViewModel>>> CreateRemote([FromBody] RepositoryCreateForm? form, CancellationToken cancellationToken = default)
        => Respond(await List.CreateRemoteAsync(User, form, ReturnPage, cancellationToken));

    // ---------------------------------------------------------------- RepositoryEdit

    /// <summary>Shows one repository.</summary>
    [HttpGet("{name}"), UiApi("/Admin/RepositoryEdit")]
    public async Task<ActionResult<ScreenResult<RepositoryEditViewModel>>> GetRepository(string name, CancellationToken cancellationToken = default)
        => Respond(await Edit.GetAsync(User, name, ReturnPage, cancellationToken));

    /// <summary>Saves the repository (<c>anonymousRead</c>; a remote one also <c>remote</c> and <c>password</c>).</summary>
    [HttpPost("{name}"), UiApi("/Admin/RepositoryEdit")]
    public async Task<ActionResult<ScreenResult<RepositoryEditViewModel>>> Save(string name, [FromBody] RepositoryEditRequest? request, CancellationToken cancellationToken = default)
        => Respond(await Edit.SaveAsync(User, name, request, ReturnPage, cancellationToken));

    /// <summary>Saves the policy of a local Maven repository (<c>policy</c>, <c>redeploy</c>, <c>snapshotBuilds</c>).</summary>
    [HttpPost("{name}/maven-settings"), UiApi("/Admin/RepositoryEdit", Handler = "MavenSettings")]
    public async Task<ActionResult<ScreenResult<RepositoryEditViewModel>>> MavenSettings(string name, [FromBody] RepositoryEditRequest? request, CancellationToken cancellationToken = default)
        => Respond(await Edit.SaveMavenSettingsAsync(User, name, request, ReturnPage, cancellationToken));

    /// <summary>Asks a remote repository's upstream with the unsaved <c>remote</c> values and saves nothing (<c>password</c>).</summary>
    [HttpPost("{name}/test"), UiApi("/Admin/RepositoryEdit", Handler = "Test")]
    public async Task<ActionResult<ScreenResult<RepositoryEditViewModel>>> Test(string name, [FromBody] RepositoryEditRequest? request, CancellationToken cancellationToken = default)
        => Respond(await Edit.TestAsync(User, name, request, ReturnPage, cancellationToken));

    /// <summary>Deletes everything a remote repository cached (<c>password</c>).</summary>
    [HttpPost("{name}/purge"), UiApi("/Admin/RepositoryEdit", Handler = "Purge")]
    public async Task<ActionResult<ScreenResult<RepositoryEditViewModel>>> Purge(string name, [FromBody] RepositoryEditRequest? request, CancellationToken cancellationToken = default)
        => Respond(await Edit.PurgeAsync(User, name, request, ReturnPage, cancellationToken));

    /// <summary>Deletes one cached package of a remote repository (<c>packageId</c>, <c>packageVersion</c>, <c>password</c>).</summary>
    [HttpPost("{name}/purge-package"), UiApi("/Admin/RepositoryEdit", Handler = "PurgePackage")]
    public async Task<ActionResult<ScreenResult<RepositoryEditViewModel>>> PurgePackage(string name, [FromBody] RepositoryEditRequest? request, CancellationToken cancellationToken = default)
        => Respond(await Edit.PurgePackageAsync(User, name, request, ReturnPage, cancellationToken));

    /// <summary>Adds a member at the end of a virtual repository (<c>member</c>).</summary>
    [HttpPost("{name}/add-member"), UiApi("/Admin/RepositoryEdit", Handler = "AddMember")]
    public async Task<ActionResult<ScreenResult<RepositoryEditViewModel>>> AddMember(string name, [FromBody] RepositoryEditRequest? request, CancellationToken cancellationToken = default)
        => Respond(await Edit.AddMemberAsync(User, name, request, ReturnPage, cancellationToken));

    /// <summary>Removes a member of a virtual repository (<c>member</c>).</summary>
    [HttpPost("{name}/remove-member"), UiApi("/Admin/RepositoryEdit", Handler = "RemoveMember")]
    public async Task<ActionResult<ScreenResult<RepositoryEditViewModel>>> RemoveMember(string name, [FromBody] RepositoryEditRequest? request, CancellationToken cancellationToken = default)
        => Respond(await Edit.RemoveMemberAsync(User, name, request, ReturnPage, cancellationToken));

    /// <summary>Moves a member of a virtual repository up or down (<c>member</c>, <c>direction</c>).</summary>
    [HttpPost("{name}/move-member"), UiApi("/Admin/RepositoryEdit", Handler = "MoveMember")]
    public async Task<ActionResult<ScreenResult<RepositoryEditViewModel>>> MoveMember(string name, [FromBody] RepositoryEditRequest? request, CancellationToken cancellationToken = default)
        => Respond(await Edit.MoveMemberAsync(User, name, request, ReturnPage, cancellationToken));

    /// <summary>Sets the member of a virtual repository that receives uploads (<c>deploy</c>).</summary>
    [HttpPost("{name}/deploy"), UiApi("/Admin/RepositoryEdit", Handler = "Deploy")]
    public async Task<ActionResult<ScreenResult<RepositoryEditViewModel>>> Deploy(string name, [FromBody] RepositoryEditRequest? request, CancellationToken cancellationToken = default)
        => Respond(await Edit.SetDeployAsync(User, name, request, ReturnPage, cancellationToken));

    /// <summary>Shows where a package version would come from in a virtual repository (<c>previewPackageId</c>, <c>previewVersion</c>).</summary>
    [HttpPost("{name}/preview"), UiApi("/Admin/RepositoryEdit", Handler = "Preview")]
    public async Task<ActionResult<ScreenResult<RepositoryEditViewModel>>> Preview(string name, [FromBody] RepositoryEditRequest? request, CancellationToken cancellationToken = default)
        => Respond(await Edit.PreviewAsync(User, name, request, ReturnPage, cancellationToken));

    // ---------------------------------------------------------------- RepositoryDelete

    /// <summary>Shows what deleting the repository does.</summary>
    [HttpGet("{name}/delete"), UiApi("/Admin/RepositoryDelete")]
    public async Task<ActionResult<ScreenResult<RepositoryDeleteViewModel>>> GetDeletion(string name, CancellationToken cancellationToken = default)
        => Respond(await Delete.GetAsync(name, cancellationToken));

    /// <summary>Deletes the repository (refused while it holds packages or is a member of a virtual repository).</summary>
    [HttpPost("{name}/delete"), UiApi("/Admin/RepositoryDelete")]
    public async Task<ActionResult<ScreenResult<RepositoryDeleteViewModel>>> DeleteRepository(string name, CancellationToken cancellationToken = default)
        => Respond(await Delete.DeleteAsync(name, cancellationToken));
}
