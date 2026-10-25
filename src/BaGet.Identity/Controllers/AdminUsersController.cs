using System.Threading;
using System.Threading.Tasks;
using BaGet.Identity.Mvc;
using BaGet.Identity.Workflows;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Identity.Controllers;

/// <summary>
/// The administrator's account screens: the user list (<c>/admin/users</c>), one account (<c>/admin/users/{name}</c>), its deletion
/// (<c>/admin/users/{name}/delete</c>) and the packages it owns (<c>/admin/users/{name}/packages</c>). They replace the Razor Pages
/// <c>/Admin/Users</c>, <c>/Admin/UserDetail</c>, <c>/Admin/DeleteUser</c> and <c>/Admin/UserPackages</c> (same URLs, same
/// <c>?handler=</c> values, same <c>asp-page</c> names through <see cref="LegacyPageAttribute"/>); the logic is in the workflows,
/// which the obsolete page models call too. Administrators only (<see cref="AdminControllerBase"/>); every action asks the
/// administrator's password again and is refused with 503 while the read-only gate is closed, as the pages did.
/// </summary>
public sealed class AdminUsersController : AdminControllerBase
{
    // ---------------------------------------------------------------- Users

    /// <summary>Lists the users.</summary>
    [Route("admin/users"), HttpGet, HttpHead, PageHandler, LegacyPage("/Admin/Users")]
    public async Task<IActionResult> Index(string? q, [ModelBinder(Name = "p")] int pageNumber = 1, CancellationToken cancellationToken = default)
    {
        var workflow = StartUsers(q, pageNumber);
        await workflow.GetAsync(cancellationToken);

        return ShowUsers(workflow, null);
    }

    /// <summary>Enables a self-registered account that waits for approval.</summary>
    [Route("admin/users"), HttpPost, PageHandler("Approve"), LegacyPage("/Admin/Users")]
    public async Task<IActionResult> Approve(string userName, string? password, string? q, [ModelBinder(Name = "p")] int pageNumber = 1, CancellationToken cancellationToken = default)
    {
        var workflow = StartUsers(q, pageNumber);

        return ShowUsers(workflow, await workflow.ApproveAsync(userName, password, cancellationToken));
    }

    /// <summary>Makes a user an administrator.</summary>
    [Route("admin/users"), HttpPost, PageHandler("Promote"), LegacyPage("/Admin/Users")]
    public async Task<IActionResult> Promote(string userName, string? password, string? q, [ModelBinder(Name = "p")] int pageNumber = 1, CancellationToken cancellationToken = default)
    {
        var workflow = StartUsers(q, pageNumber);

        return ShowUsers(workflow, await workflow.PromoteAsync(userName, password, cancellationToken));
    }

    /// <summary>Removes a user's administrator flag.</summary>
    [Route("admin/users"), HttpPost, PageHandler("Demote"), LegacyPage("/Admin/Users")]
    public async Task<IActionResult> Demote(string userName, string? password, string? q, [ModelBinder(Name = "p")] int pageNumber = 1, CancellationToken cancellationToken = default)
    {
        var workflow = StartUsers(q, pageNumber);

        return ShowUsers(workflow, await workflow.DemoteAsync(userName, password, cancellationToken));
    }

    /// <summary>Replaces a user's password with a generated one and shows it once (the response is the screen, not a redirect).</summary>
    [Route("admin/users"), HttpPost, PageHandler("ResetPassword"), LegacyPage("/Admin/Users")]
    public async Task<IActionResult> ResetPassword(string userName, string? password, string? resetEmail, string? q, [ModelBinder(Name = "p")] int pageNumber = 1, CancellationToken cancellationToken = default)
    {
        var workflow = StartUsers(q, pageNumber);

        return ShowUsers(workflow, await workflow.ResetPasswordAsync(userName, password, resetEmail, cancellationToken));
    }

    /// <summary>Mails a user a password reset link.</summary>
    [Route("admin/users"), HttpPost, PageHandler("SendResetLink"), LegacyPage("/Admin/Users")]
    public async Task<IActionResult> SendResetLink(string userName, string? password, string? q, [ModelBinder(Name = "p")] int pageNumber = 1, CancellationToken cancellationToken = default)
    {
        var workflow = StartUsers(q, pageNumber);

        return ShowUsers(workflow, await workflow.SendResetLinkAsync(userName, password, cancellationToken));
    }

    /// <summary>Creates an account (typed, generated or invited password).</summary>
    [Route("admin/users"), HttpPost, PageHandler("Create"), LegacyPage("/Admin/Users")]
    public async Task<IActionResult> Create(
        string? newUserName,
        string? newEmail,
        string? newPassword,
        bool invite,
        string? password,
        string? q,
        [ModelBinder(Name = "p")] int pageNumber = 1,
        CancellationToken cancellationToken = default)
    {
        var workflow = StartUsers(q, pageNumber);

        return ShowUsers(workflow, await workflow.CreateAsync(newUserName, newEmail, newPassword, invite, password, cancellationToken));
    }

    /// <summary>A form posted to the screen's own URL, with no handler: the screen is rendered (the page had no default handler).</summary>
    [Route("admin/users"), HttpPost, PageHandler, ImplicitPageHandler, LegacyPage("/Admin/Users")]
    public async Task<IActionResult> IndexPost(string? q, [ModelBinder(Name = "p")] int pageNumber = 1, CancellationToken cancellationToken = default)
    {
        var workflow = StartUsers(q, pageNumber);
        await workflow.GetAsync(cancellationToken);

        return ShowUsers(workflow, null);
    }

    private UsersWorkflow StartUsers(string? q, int pageNumber)
    {
        var workflow = new UsersWorkflow(HttpContext, Url);
        workflow.Model.Q = q;
        workflow.Model.PageNumber = pageNumber;

        return workflow;
    }

    private IActionResult ShowUsers(UsersWorkflow workflow, IActionResult? result)
    {
        Flash(workflow.Model);

        return result ?? View("Index", workflow.Model);
    }

    // ---------------------------------------------------------------- UserDetail

    /// <summary>Shows one account.</summary>
    [Route("admin/users/{name}"), HttpGet, HttpHead, PageHandler, LegacyPage("/Admin/UserDetail")]
    public async Task<IActionResult> Detail(string name, string? package, CancellationToken cancellationToken)
    {
        var workflow = StartDetail(name, package);

        return ShowDetail(workflow, await workflow.GetAsync(cancellationToken));
    }

    /// <summary>Enables the account when it waits for approval.</summary>
    [Route("admin/users/{name}"), HttpPost, PageHandler("Approve"), LegacyPage("/Admin/UserDetail")]
    public async Task<IActionResult> DetailApprove(string name, string? package, string? password, CancellationToken cancellationToken)
    {
        var workflow = StartDetail(name, package);

        return ShowDetail(workflow, await workflow.ApproveAsync(password, cancellationToken));
    }

    /// <summary>Revokes one of the account's API keys.</summary>
    [Route("admin/users/{name}"), HttpPost, PageHandler("RevokeKey"), LegacyPage("/Admin/UserDetail")]
    public async Task<IActionResult> RevokeKey(string name, string? package, int key, string? password, CancellationToken cancellationToken)
    {
        var workflow = StartDetail(name, package);

        return ShowDetail(workflow, await workflow.RevokeKeyAsync(key, password, cancellationToken));
    }

    /// <summary>Adds the account to a group.</summary>
    [Route("admin/users/{name}"), HttpPost, PageHandler("AddGroup"), LegacyPage("/Admin/UserDetail")]
    public async Task<IActionResult> AddGroup(string name, string? package, string? group, string? password, CancellationToken cancellationToken)
    {
        var workflow = StartDetail(name, package);

        return ShowDetail(workflow, await workflow.AddGroupAsync(group, password, cancellationToken));
    }

    /// <summary>Removes the account from a group.</summary>
    [Route("admin/users/{name}"), HttpPost, PageHandler("RemoveGroup"), LegacyPage("/Admin/UserDetail")]
    public async Task<IActionResult> RemoveGroup(string name, string? package, string? group, string? password, CancellationToken cancellationToken)
    {
        var workflow = StartDetail(name, package);

        return ShowDetail(workflow, await workflow.RemoveGroupAsync(group, password, cancellationToken));
    }

    /// <summary>A form posted to the screen's own URL, with no handler: the screen is rendered (the page had no default handler).</summary>
    [Route("admin/users/{name}"), HttpPost, PageHandler, ImplicitPageHandler, LegacyPage("/Admin/UserDetail")]
    public async Task<IActionResult> DetailPost(string name, string? package, CancellationToken cancellationToken)
    {
        var workflow = StartDetail(name, package);

        return ShowDetail(workflow, await workflow.GetAsync(cancellationToken));
    }

    private UserDetailWorkflow StartDetail(string? name, string? package)
    {
        var workflow = new UserDetailWorkflow(HttpContext, Url);
        workflow.Model.Name = name;
        workflow.Model.Package = package;

        return workflow;
    }

    private IActionResult ShowDetail(UserDetailWorkflow workflow, IActionResult? result)
    {
        Flash(workflow.Model);

        return result ?? View("Detail", workflow.Model);
    }

    // ---------------------------------------------------------------- DeleteUser

    /// <summary>Shows what deleting the account does.</summary>
    [Route("admin/users/{name}/delete"), HttpGet, HttpHead, PageHandler, LegacyPage("/Admin/DeleteUser")]
    public async Task<IActionResult> DeleteUser(string name, CancellationToken cancellationToken)
    {
        var workflow = StartDeletion(name, null);

        return ShowDeletion(workflow, await workflow.GetAsync(cancellationToken));
    }

    /// <summary>Deletes the account after the administrator's password.</summary>
    [Route("admin/users/{name}/delete"), HttpPost, PageHandler, LegacyPage("/Admin/DeleteUser")]
    public async Task<IActionResult> DeleteUserConfirmed(string name, string? soleOwned, string? password, CancellationToken cancellationToken)
    {
        var workflow = StartDeletion(name, soleOwned);

        return ShowDeletion(workflow, await workflow.PostAsync(password, cancellationToken));
    }

    private DeleteUserWorkflow StartDeletion(string? name, string? soleOwned)
    {
        var workflow = new DeleteUserWorkflow(HttpContext, Url);
        workflow.Model.Name = name;
        if (soleOwned != null) workflow.Model.SoleOwned = soleOwned;

        return workflow;
    }

    private IActionResult ShowDeletion(DeleteUserWorkflow workflow, IActionResult? result)
    {
        if (workflow.RedirectMessage != null) TempData["Message"] = workflow.RedirectMessage;
        if (workflow.RedirectError != null) TempData["Error"] = workflow.RedirectError;

        return result ?? View("Delete", workflow.Model);
    }

    // ---------------------------------------------------------------- UserPackages

    /// <summary>Lists the package IDs one user owns.</summary>
    [Route("admin/users/{name}/packages"), HttpGet, HttpHead, PageHandler, LegacyPage("/Admin/UserPackages"), RepositoryPage]
    public async Task<IActionResult> UserPackages(string name, CancellationToken cancellationToken)
    {
        var workflow = new AdminPackagesWorkflow(HttpContext, Url);
        workflow.OfUser.Name = name;

        return await workflow.GetUserAsync(cancellationToken) ?? View("Packages", workflow.OfUser);
    }
}
