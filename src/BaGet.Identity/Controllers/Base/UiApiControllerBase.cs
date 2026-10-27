using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.Application;
using BaGet.Identity.Application.Screens;
using BaGet.Identity.Application.UseCases;
using BaGet.Identity.Mvc;
using BaGet.Identity.Mvc.UI;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace BaGet.Identity.Controllers.Base;

/// <summary>
/// Base of every controller of the JSON UI API (<c>/api/ui/...</c>): the twin of a view controller that does the work of a screen and
/// returns its model, so a single-page application (or any client) can be written against it alone, and the view controller only renders
/// what <see cref="UiApiInvoker"/> got from it.
/// <list type="bullet">
/// <item><description><b>Authentication</b> (<see cref="UiApiSecurityFilter"/>): API keys only (<c>Authorization: Bearer</c> or <c>X-NuGet-ApiKey</c>), like <c>/api/v1</c>; the
/// browser session and HTTP Basic are never accepted over HTTP, so there is no antiforgery (see <see cref="UiApiCaller"/>).</description></item>
/// <item><description><b>Answers</b> are <see cref="ScreenResult{T}"/> through <see cref="Respond{T}"/>: <c>status</c>, <c>model</c>, <c>errors</c>, <c>next</c>.</description></item>
/// <item><description><b>No <c>[ApiController]</c></b>: its automatic 400 on an invalid model would differ between an HTTP call and the
/// in-process call of a view controller. An action validates explicitly and answers <see cref="ScreenResult{T}.Invalid(T, string, int?)"/>.</description></item>
/// <item><description><b>Filters do not run in process</b>: the view controller's base runs the same <see cref="ScreenGate"/> before it calls the
/// twin, and its own antiforgery validation and cookie session.</description></item>
/// </list>
/// A twin is marked <see cref="UiApiAttribute"/> (never <see cref="LegacyPageAttribute"/>), routed under <c>api/ui/</c> (a repository
/// screen also at <c>api/ui/r/{repo}/...</c> through <see cref="RepositoryPageAttribute"/>), and takes the request body as JSON
/// (<c>[FromBody]</c>) or multipart (<c>[FromForm]</c>, file uploads).
/// </summary>
[TypeFilter(typeof(UiApiSecurityFilter))]
[Produces("application/json")]
// Not [ApiController], so ApiExplorer would skip the twins: they all belong in the OpenAPI document "ui".
[ApiExplorerSettings(IgnoreApi = false)]
public abstract class UiApiControllerBase : ControllerBase
{
    /// <summary>The API key behind the request, or null for an anonymous caller.</summary>
    protected BaGetPrincipal? KeyCaller => UiApiCaller.Key(HttpContext);

    /// <summary>The screen gate (the same one the view controllers ask).</summary>
    protected ScreenGate Gate
        => HttpContext.RequestServices.GetService<ScreenGate>()
           ?? ActivatorUtilities.CreateInstance<ScreenGate>(HttpContext.RequestServices);

    /// <summary>
    /// A use case, resolved when the action runs (not injected into the constructor): the account use cases are registered only while accounts are
    /// enabled, and the gate must be able to answer 404 first.
    /// </summary>
    [NonAction]
    protected T UseCase<T>() where T : notnull => HttpContext.RequestServices.GetRequiredService<T>();

    /// <summary>
    /// What the request binding already refused (a value that is not a number, a malformed body), by field, for a use case that must not act while
    /// there is any. A use case validates its form itself; this only carries what it cannot see.
    /// </summary>
    [NonAction]
    protected IReadOnlyDictionary<string, string[]> BindingErrors()
        => ModelState
            .Where(entry => entry.Value is { Errors.Count: > 0 })
            .ToDictionary(
                entry => entry.Key,
                entry => entry.Value!.Errors.Select(e => string.IsNullOrEmpty(e.ErrorMessage) ? "The value is not valid." : e.ErrorMessage).ToArray());

    /// <summary>The answer of an action: <paramref name="result"/> with the HTTP status of its decision.</summary>
    [NonAction]
    protected ActionResult<ScreenResult<T>> Respond<T>(ScreenResult<T> result)
        => new ActionResult<ScreenResult<T>>(new UiApiResult(result ?? throw new ArgumentNullException(nameof(result))));

    /// <summary>The answer for a gate's refusal: the screen has no model, so the result is of the action's own model type with only the decision set.</summary>
    [NonAction]
    protected static IScreenResult Refusal(GateResult gate)
        => gate.Message != null
            ? gate.Outcome switch
            {
                GateOutcome.SignIn => ScreenResult<object>.SignIn(gate.Message),
                GateOutcome.Forbidden => ScreenResult<object>.Forbidden(gate.Message),
                _ => Refusal(gate.Outcome),
            }
            : Refusal(gate.Outcome);

    /// <summary>The answer for a gate's refusal, without a message.</summary>
    [NonAction]
    protected static IScreenResult Refusal(GateOutcome outcome)
        => outcome switch
        {
            GateOutcome.SignIn => ScreenResult<object>.SignIn(),
            GateOutcome.Forbidden => ScreenResult<object>.Forbidden(),
            GateOutcome.AccessUnavailable => ScreenResult<object>.GoTo(new ScreenNext("/Admin/AccessUnavailable")),
            _ => ScreenResult<object>.NotFound(),
        };
}

/// <summary>
/// Base of the account screens' JSON twins: accounts enabled (else 404), mail configured when <see cref="RequiresEmail"/>, signed in when
/// <see cref="RequiresSignIn"/> (else 401), through the same <see cref="ScreenGate"/> as <c>AccountControllerBase</c>. Account self-service
/// (password, two-factor, external logins, API keys, sign-in and out, deletion) and package ownership screens are <b>cookie only</b> (<see cref="CookieOnly"/>):
/// over HTTP, where the cookie is never accepted, they answer 403 "use the web screen" to everybody; the web screen reaches them in process.
/// </summary>
public abstract class AccountUiApiControllerBase : UiApiControllerBase, IAsyncActionFilter
{
    /// <summary>Whether the screen needs a signed-in user.</summary>
    protected virtual bool RequiresSignIn => false;

    /// <summary>Whether the screen needs mail to be configured.</summary>
    protected virtual bool RequiresEmail => false;

    /// <summary>Whether the twin is screen-only over HTTP (account self-service, package ownership): 403 for every caller. The administration bases lift it.</summary>
    protected virtual bool CookieOnly => true;

    /// <summary>What the screen requires of the caller; a derived base widens it (role, writes).</summary>
    protected virtual GateRequest Requirements(ActionExecutingContext context)
    {
        // The per-action forms of the two requirements, like AccountControllerBase reads them.
        var metadata = context.ActionDescriptor.EndpointMetadata ?? (IList<object>)Array.Empty<object>();

        return new GateRequest(
            RequiresSignIn: RequiresSignIn || metadata.OfType<RequiresSignInAttribute>().Any(),
            RequiresEmail: RequiresEmail || metadata.OfType<RequiresEmailAttribute>().Any(),
            KeyCaller: KeyCaller,
            CookieOnly: CookieOnly)
        {
            // This filter runs only when the twin is called over HTTP (the in-process call of a view controller skips it).
            OverHttp = true,
        };
    }

    /// <summary>Asks the gate; a refusal is the answer.</summary>
    [NonAction]
    public virtual async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var gate = await Gate.CheckAsync(User, Requirements(context), HttpContext.RequestAborted);
        if (!gate.IsAllowed)
        {
            context.Result = new UiApiResult(Refusal(gate));
            return;
        }

        await next();
    }
}

/// <summary>
/// Base of the administrator screens' JSON twins: the caller is an administrator's API key with the <c>admin:api</c> scope and no repository limit
/// (see <see cref="ScreenGate.IsAdministratorKey"/>): 401 with no key, 403 for any other key, the global <c>ApiKey</c> included.
/// </summary>
public abstract class AdminUiApiControllerBase : AccountUiApiControllerBase
{
    /// <inheritdoc/>
    protected override bool RequiresSignIn => true;

    /// <inheritdoc/>
    protected override bool CookieOnly => false;

    /// <inheritdoc/>
    protected override GateRequest Requirements(ActionExecutingContext context)
        => base.Requirements(context) with { Role = GateRole.Administrator };
}

/// <summary>
/// Base of the access-control screens' JSON twins (groups, permissions, projects, audit; retention and replication build on it), like
/// <c>AccessControllerBase</c>: administrators, or whoever <see cref="AuthorizeAsync"/> admits (a project's administrators); an
/// administrator is pointed at <c>/admin/access-unavailable</c> where there is no access model; every write is refused with 503 while
/// the server is read-only.
/// </summary>
public abstract class AccessUiApiControllerBase : AdminUiApiControllerBase
{
    /// <summary>Admits a caller who is not a server administrator. The default admits nobody else.</summary>
    protected virtual Task<bool> AuthorizeAsync(CancellationToken cancellationToken) => Task.FromResult(false);

    /// <summary>
    /// Who is using the screen, for a use case: the signed-in account (an API key's owner) and whether the caller is a server administrator rather than
    /// only a project administrator (decided by the same <see cref="ScreenGate"/> that admitted the request).
    /// </summary>
    [NonAction]
    protected async Task<AccessCaller> CallerAsync(CancellationToken cancellationToken)
        => new(User, await Gate.IsAdministratorAsync(User, KeyCaller, cancellationToken));

    /// <summary>The page name of the screen the running action serves (<see cref="UiApiAttribute.Screen"/>), so a base can widen one screen of a controller.</summary>
    [NonAction]
    protected string? ScreenName()
        => ControllerContext.ActionDescriptor.EndpointMetadata.OfType<UiApiAttribute>().FirstOrDefault()?.Screen;

    /// <inheritdoc/>
    protected override GateRequest Requirements(ActionExecutingContext context)
        => base.Requirements(context) with
        {
            Role = GateRole.AccessAdministrator,
            IsWrite = !(HttpMethods.IsGet(Request.Method) || HttpMethods.IsHead(Request.Method)),
            AlsoAllow = AuthorizeAsync,
        };
}
