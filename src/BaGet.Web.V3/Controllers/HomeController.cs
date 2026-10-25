using System.Threading;
using System.Threading.Tasks;
using BaGet.Identity.Mvc;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Web;

/// <summary>
/// The front page (<c>/</c>, with no search) and the package list (<c>/packages</c>, <c>/?q=…</c>) of a NuGet repository, at its own
/// route and at <c>/r/{repo}/…</c>. Replaces the <c>/Index</c> Razor Page: same
/// URLs and <c>asp-page="/Index"</c> links (<see cref="LegacyPageAttribute"/>). In a repository of another format the front page
/// redirects to the format's own browse page (<c>FrontPage</c>).
/// </summary>
[RepositoryPage(FrontPage = true)]
public sealed class HomeController : UiControllerBase
{
    private readonly IndexWorkflow _workflow;

    /// <summary>Creates the controller.</summary>
    public HomeController(IndexWorkflow workflow)
    {
        _workflow = workflow ?? throw new System.ArgumentNullException(nameof(workflow));
    }

    /// <summary>The front page or one page of search results.</summary>
    [Route(""), Route("index"), Route("packages"), RouteNotForLinks("", "index"), HttpGet, HttpHead, PageHandler, LegacyPage("/Index")]
    public async Task<IActionResult> Index(IndexViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid || !IndexWorkflow.IsValid(model)) return BadRequest();

        await _workflow.LoadAsync(model, Request, cancellationToken);

        return View("Index", model);
    }
}
