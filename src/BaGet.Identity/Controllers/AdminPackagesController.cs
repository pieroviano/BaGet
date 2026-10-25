using System.Threading;
using System.Threading.Tasks;
using BaGet.Identity.Mvc;
using BaGet.Identity.Workflows;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Identity.Controllers;

/// <summary>
/// The package IDs that have no owner, in every repository (<c>/admin/packages</c>, and <c>/r/{repo}/admin/packages</c> like the
/// page): those that predate accounts, and those left behind when their only owner's account was deleted. Only administrators and
/// the global API key may change them, so this is where an administrator finds them. It replaces the Razor Page
/// <c>/Admin/Packages</c> (same URL, same <c>asp-page</c> name). The packages of one user are
/// <see cref="AdminUsersController.UserPackages"/>.
/// </summary>
public sealed class AdminPackagesController : AdminControllerBase
{
    /// <summary>Lists the ownerless IDs of every repository.</summary>
    [Route("admin/packages"), HttpGet, HttpHead, PageHandler, LegacyPage("/Admin/Packages"), RepositoryPage]
    public async Task<IActionResult> Index([ModelBinder(Name = "p")] int pageNumber = 1, CancellationToken cancellationToken = default)
    {
        var workflow = new AdminPackagesWorkflow(HttpContext, Url);
        workflow.Ownerless.PageNumber = pageNumber;
        await workflow.GetOwnerlessAsync(cancellationToken);

        return View("Index", workflow.Ownerless);
    }
}
