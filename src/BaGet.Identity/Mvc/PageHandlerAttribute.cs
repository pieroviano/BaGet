using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ActionConstraints;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;

namespace BaGet.Identity.Mvc;

/// <summary>
/// Selects a controller action by the <c>handler</c> value of the request, the way a Razor Page selects <c>OnPostApprove</c>
/// for <c>?handler=Approve</c>, so every existing form, bookmark and script keeps posting to the same URL. The value is read
/// from the route values first, then from the query string (names are compared ignoring case), exactly as Razor Pages does.
/// <list type="bullet">
/// <item><description><c>[PageHandler("Approve")]</c> next to <c>[HttpPost]</c>: the action runs for a POST whose handler is
/// <c>Approve</c>.</description></item>
/// <item><description><c>[PageHandler]</c> without a name: the <em>default handler</em>, <c>OnGet</c> / <c>OnPost</c> of the
/// page. It runs when the request names no handler <b>or a handler that no sibling action of the same HTTP method has</b>
/// (Razor Pages: an unknown handler is the default handler). The default handler of a page that also has named handlers
/// must carry the attribute, else a request naming a handler would match both and be ambiguous.</description></item>
/// </list>
/// A <c>HEAD</c> request is answered by the <c>GET</c> handlers, so declare <c>[HttpGet, HttpHead]</c> on them. The attribute
/// is an action constraint, so routing does the selection; two actions with the same template, HTTP method and handler name
/// are ambiguous, as two <c>OnPostApprove</c> methods would be.
/// </summary>
/// <remarks>
/// How to migrate: a page model's <c>OnGetCopyStatus</c> becomes <c>[HttpGet, PageHandler("CopyStatus")] CopyStatus()</c>, its
/// <c>OnPost</c> becomes <c>[HttpPost, PageHandler] Post()</c> (or just <c>[HttpPost]</c> when no sibling has a name). Forms keep
/// <c>asp-page-handler="Approve"</c> (the legacy page mapping, see <see cref="LegacyPageAttribute"/>) or use
/// <c>asp-route-handler="Approve"</c> with <c>asp-controller</c>/<c>asp-action</c>: both render <c>?handler=Approve</c>.
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class PageHandlerAttribute : ActionMethodSelectorAttribute
{
    /// <summary>The route value and query string key that names the handler.</summary>
    public const string HandlerKey = "handler";

    private static readonly ConcurrentDictionary<Type, IReadOnlyList<NamedHandler>> Siblings = new();

    /// <param name="name">The handler name (<c>?handler=name</c>), or null for the default handler.</param>
    public PageHandlerAttribute(string? name = null)
    {
        Name = string.IsNullOrWhiteSpace(name) ? null : name;
    }

    /// <summary>The handler name, or null for the default handler.</summary>
    public string? Name { get; }

    /// <summary>
    /// The handler a request asks for: the <c>handler</c> route value, else the <c>handler</c> query string value, else null.
    /// </summary>
    public static string? RequestedHandler(HttpContext httpContext, RouteData? routeData = null)
    {
        if (httpContext == null) throw new ArgumentNullException(nameof(httpContext));

        if (routeData?.Values.TryGetValue(HandlerKey, out var value) == true && Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) is { Length: > 0 } fromRoute)
        {
            return fromRoute;
        }

        var fromQuery = httpContext.Request.Query[HandlerKey].ToString();

        return fromQuery.Length == 0 ? null : fromQuery;
    }

    /// <inheritdoc/>
    public override bool IsValidForRequest(RouteContext routeContext, ActionDescriptor action)
    {
        if (routeContext == null) throw new ArgumentNullException(nameof(routeContext));
        if (action == null) throw new ArgumentNullException(nameof(action));

        var requested = RequestedHandler(routeContext.HttpContext, routeContext.RouteData);

        if (Name != null)
        {
            return string.Equals(Name, requested, StringComparison.OrdinalIgnoreCase);
        }

        // The default handler: valid unless a sibling that answers this HTTP method is named like the request.
        if (requested == null || action is not ControllerActionDescriptor controllerAction)
        {
            return true;
        }

        var verb = routeContext.HttpContext.Request.Method;
        if (HttpMethods.IsHead(verb)) verb = HttpMethods.Get;

        return !Siblings.GetOrAdd(controllerAction.ControllerTypeInfo.AsType(), NamedHandlersOf)
            .Any(h => string.Equals(h.Name, requested, StringComparison.OrdinalIgnoreCase)
                      && (h.Verbs.Count == 0 || h.Verbs.Contains(verb, StringComparer.OrdinalIgnoreCase)));
    }

    private static IReadOnlyList<NamedHandler> NamedHandlersOf(Type controller)
    {
        var result = new List<NamedHandler>();
        foreach (var method in controller.GetMethods(BindingFlags.Public | BindingFlags.Instance))
        {
            var handler = method.GetCustomAttribute<PageHandlerAttribute>();
            if (handler?.Name == null) continue;

            var verbs = method.GetCustomAttributes<HttpMethodAttribute>(inherit: true).SelectMany(a => a.HttpMethods).ToList();
            result.Add(new NamedHandler(handler.Name, verbs));
        }

        return result;
    }

    private sealed record NamedHandler(string Name, IReadOnlyList<string> Verbs);
}
