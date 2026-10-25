using Microsoft.AspNetCore.Mvc;

namespace BaGet.Identity.Mvc;

/// <summary>
/// Base of every controller that serves a screen of BaGet's own web UI (the replacement of a Razor Page model). What it gives
/// a screen is what a Razor Page had without asking:
/// <list type="bullet">
/// <item><description><b>Antiforgery.</b> <see cref="AutoValidateAntiforgeryTokenAttribute"/>: every unsafe request (POST) without
/// a valid token is answered 400 before anything else runs, as for a page. Never opt a UI action out.</description></item>
/// <item><description><b>Handlers.</b> <see cref="PageHandlerAttribute"/> picks the action by <c>?handler=</c>, so form URLs
/// do not change; <see cref="RequestedHandler"/> is the value.</description></item>
/// <item><description><b>Filters.</b> The controller's own <c>OnActionExecuting</c>/<c>OnActionExecutionAsync</c> override is the
/// replacement of a page model's <c>OnPageHandlerExecuting</c>/<c>OnPageHandlerExecutionAsync</c> and runs first, before
/// the global <c>RepositoryActionFilter</c>, as the model's did before <c>RepositoryPageFilter</c>.</description></item>
/// </list>
/// A UI controller also carries <see cref="LegacyPageAttribute"/> (its page name) and, for a page of a repository,
/// <see cref="RepositoryPageAttribute"/>.
/// </summary>
/// <remarks>
/// <para><b>How to migrate a page</b> (details in <c>docs/plans/razor-pages-to-mvc.md</c>): the page's
/// <c>@page "/x"</c> becomes <c>[Route("x")]</c>; <c>OnGet</c> becomes <c>[HttpGet, HttpHead, PageHandler]</c>; <c>OnPostX</c>
/// becomes <c>[HttpPost, PageHandler("X")]</c> taking <c>[FromForm]</c>/<c>[FromQuery]</c> parameters instead of
/// <c>[BindProperty]</c>; <c>return Page()</c> becomes <c>return View("Name", model)</c>; <c>[TempData]</c> properties stay
/// <c>[TempData]</c> properties of the controller. The view moves to <c>Views/&lt;Controller&gt;/&lt;Name&gt;.cshtml</c> with
/// the view model as <c>@model</c>.</para>
/// </remarks>
[AutoValidateAntiforgeryToken]
public abstract class UiControllerBase : Controller
{
    /// <summary>
    /// The handler the request names (<c>?handler=</c>, or the <c>handler</c> route value), or null for the default handler.
    /// </summary>
    protected string? RequestedHandler => PageHandlerAttribute.RequestedHandler(HttpContext, RouteData);
}
