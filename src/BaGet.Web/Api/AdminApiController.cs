using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace BaGet.Web.Api;

/// <summary>
/// Lets only administrators in. The caller is the principal of an API key (<c>Authorization: Basic</c> password, <c>Bearer</c>,
/// <c>X-NuGet-ApiKey</c>), resolved by <see cref="RequestPrincipalResolver.ResolveForWriteAsync"/> for every method, reads
/// included: the browser session is never accepted, so a web page cannot make the administrator's browser call the API. Accepted:
/// the global <c>ApiKey</c> of the server (when one is configured: an open server with no key never makes anyone an
/// administrator), or the key of an administrator that carries <see cref="ApiKeyScopes.Admin"/> and is not limited to repositories.
/// No credentials: 401 with a <c>Bearer</c> challenge. Valid ones without the right: 403.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class AdminApiAuthorizeAttribute : Attribute, IAsyncAuthorizationFilter
{
    internal const string CallerItem = "BaGet.AdminApi.Caller";

    /// <inheritdoc />
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var http = context.HttpContext;
        var services = http.RequestServices;
        var principal = await services.GetRequiredService<RequestPrincipalResolver>().ResolveForWriteAsync(http, http.RequestAborted);

        if (principal == null)
        {
            http.Response.Headers[HeaderNames.WWWAuthenticate] = "Bearer realm=\"BaGet administration API\"";
            context.Result = ApiProblems.Create(401, "unauthorized", "Unauthorized", "Send an administrator's API key with the admin:api scope (Authorization: Bearer <key>), or the server's global API key.");

            return;
        }

        if (principal.IsSuperuser)
        {
            // An open server (no global key) authenticates everybody as the superuser; that must never open the administration.
            if (string.IsNullOrEmpty(services.GetRequiredService<IOptionsSnapshot<BaGetOptions>>().Value.ApiKey))
            {
                context.Result = ApiProblems.Create(403, "forbidden", "Forbidden", "The server has no global ApiKey, so only an administrator's key with the admin:api scope is accepted.");

                return;
            }
        }
        else if (!principal.IsAdministrator || !principal.HasScope(ApiKeyScopes.Admin))
        {
            context.Result = ApiProblems.Create(403, "forbidden", "Forbidden", "The key must belong to an administrator and carry the admin:api scope.");

            return;
        }
        else if (principal.RepositoryKeys != null)
        {
            context.Result = ApiProblems.Create(403, "forbidden", "Forbidden", "A key limited to repositories cannot administer the server.");

            return;
        }

        http.Items[CallerItem] = principal;
    }
}

/// <summary>
/// Turns what the administration services throw into problem answers: an unknown thing is 404, input they refuse is 400, a state
/// that forbids the change is 409, a closed <see cref="MaintenanceGate"/> is 503 with <c>Retry-After</c>. Anything else is a
/// server error and propagates.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class ApiExceptionFilterAttribute : Attribute, IExceptionFilter
{
    /// <inheritdoc />
    public void OnException(ExceptionContext context)
    {
        var result = Map(context.Exception, context.HttpContext);
        if (result == null) return;

        context.Result = result;
        context.ExceptionHandled = true;
    }

    internal static IActionResult? Map(Exception exception, HttpContext http)
    {
        Exception? e = exception;
        for (var depth = 0; depth < 8 && e != null; depth++, e = e.InnerException)
        {
            if (e is MaintenanceException maintenance)
            {
                http.Response.Headers["Retry-After"] = MaintenanceMiddleware.RetryAfterSeconds.ToString(CultureInfo.InvariantCulture);

                return ApiProblems.Create(503, "read-only", "The server is read-only", $"The server is read-only for maintenance ({maintenance.Reason}). Try again later.");
            }
        }

        return exception switch
        {
            KeyNotFoundException => ApiProblems.Create(404, "not-found", "Not found", exception.Message),
            RepositoryExistsException => ApiProblems.Conflict(exception.Message),
            RepositoryProtectedException or RepositoryNotEmptyException or RepositoryReferencedException => ApiProblems.Conflict(exception.Message),
            AccessModelException => ApiProblems.BadRequest(exception.Message),
            ArgumentException => ApiProblems.BadRequest(exception.Message),
            NotSupportedException => ApiProblems.Create(422, "not-supported", "Not supported here", exception.Message),
            // A password that nothing can protect (no data protection on the host): the services say so with this type, as the pages show it.
            InvalidOperationException when exception.GetType() == typeof(InvalidOperationException) && exception.Message.Contains("protect", StringComparison.OrdinalIgnoreCase)
                => ApiProblems.Create(422, "not-supported", "Not supported here", exception.Message),
            _ => null,
        };
    }
}

/// <summary>
/// Base of the administration REST API (<c>/api/v1</c>): <see cref="AdminApiAuthorizeAttribute"/> and
/// <see cref="ApiExceptionFilterAttribute"/> apply to every action, results are JSON written with <see cref="ApiJson.Options"/>, and
/// <see cref="RequireConfirmationAsync"/> is the API's form of the page's "type your password again".
/// </summary>
[ApiController]
[AdminApiAuthorize]
[ApiExceptionFilter]
public abstract class AdminApiController : ControllerBase
{
    /// <summary>The header that carries the key owner's password for an operation whose page asks for it.</summary>
    public const string ConfirmHeader = "X-Confirm-Password";

    /// <summary>The authenticated caller.</summary>
    protected BaGetPrincipal Caller => (BaGetPrincipal)HttpContext.Items[AdminApiAuthorizeAttribute.CallerItem]!;

    /// <summary>The name recorded in audit entries: the key owner, or <c>(global key)</c>.</summary>
    protected string Actor => Caller.UserName ?? "(global key)";

    /// <summary>Cancelled when the caller goes away.</summary>
    protected CancellationToken Cancellation => HttpContext.RequestAborted;

    /// <summary>A service of the request scope.</summary>
    protected T Service<T>() where T : notnull => HttpContext.RequestServices.GetRequiredService<T>();

    /// <summary>Fails with 503 while the server is read-only, as a page's POST does.</summary>
    protected void ThrowIfClosed() => HttpContext.RequestServices.GetService<MaintenanceGate>()?.ThrowIfClosed();

    /// <summary>A JSON answer with <paramref name="status"/>.</summary>
    protected IActionResult Respond(object? body, int status = 200, string? location = null)
    {
        if (location != null) Response.Headers[HeaderNames.Location] = location;

        return new JsonResult(body, ApiJson.Options) { StatusCode = status };
    }

    /// <summary>One page of <paramref name="all"/>.</summary>
    protected IActionResult RespondPage<T>(IReadOnlyList<T> all, int? page, int? pageSize)
    {
        var (number, size) = ApiProblems.Clamp(page, pageSize);

        return Respond(new Paged<T>(all.Skip((number - 1) * size).Take(size).ToList(), number, size, all.Count));
    }

    /// <summary>204.</summary>
    protected IActionResult Done() => new StatusCodeResult(204);

    /// <summary>
    /// The password confirmation of an operation whose page asks for the password: the key owner's password in
    /// <see cref="ConfirmHeader"/>, checked as the page does (a locked-out account is refused, a wrong password counts as a failed
    /// sign-in). The global key has no password: the key itself is the confirmation. An owner without a password (external sign-in
    /// only) has nothing to type, so the operation is refused with a pointer to set one.
    /// </summary>
    /// <returns>Null when confirmed, else the answer to send (nothing was changed).</returns>
    protected async Task<IActionResult?> RequireConfirmationAsync()
    {
        if (Caller.IsSuperuser) return null;

        var users = HttpContext.RequestServices.GetService<UserManager<BaGetUser>>();
        var user = users == null || Caller.UserId == null ? null : await users.FindByIdAsync(Caller.UserId);
        if (users == null || user == null)
        {
            return ApiProblems.Create(403, "confirmation-unavailable", "Confirmation unavailable", "The key's owner could not be found.");
        }

        if (!await users.HasPasswordAsync(user))
        {
            return ApiProblems.Create(403, "confirmation-unavailable", "Confirmation unavailable", "This operation asks for the owner's password and the account has none (it signs in with an external provider). Set a password on the account page first.");
        }

        if (await users.IsLockedOutAsync(user))
        {
            return ApiProblems.Create(403, "locked-out", "Account locked out", "The account is locked out after failed sign-ins. Try again later.");
        }

        var password = Request.Headers[ConfirmHeader].ToString();
        if (string.IsNullOrEmpty(password) || !await users.CheckPasswordAsync(user, password))
        {
            if (!string.IsNullOrEmpty(password)) await users.AccessFailedAsync(user);

            return ApiProblems.Create(403, "confirmation-required", "Confirmation required", $"Send the key owner's password in the {ConfirmHeader} header. The confirmation is incorrect, so nothing was changed.");
        }

        return null;
    }
}
