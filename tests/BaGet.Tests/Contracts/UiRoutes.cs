using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;
using BaGet.Identity.Mvc.Attributes;

namespace BaGet.Tests.Contracts;

/// <summary>
/// Reads the UI endpoints (Razor Pages today, controller actions that stand in for a page later) out of an endpoint data
/// source in one model-independent shape, so the same snapshot describes a page before and after its migration.
/// </summary>
internal static class UiRoutes
{
    /// <summary>One UI endpoint: the page name, the route template as authored (lower case) and what it answers.</summary>
    internal sealed record Entry(string Page, string Template, int Order, IReadOnlyList<string> Handlers)
    {
        public override string ToString() => $"{Template}\t{Page}\torder={Order}\t{string.Join(" ", Handlers)}";
    }

    private static readonly HashSet<string> HttpVerbs = new(StringComparer.OrdinalIgnoreCase) { "GET", "POST", "PUT", "DELETE", "PATCH", "HEAD", "OPTIONS" };

    public static IReadOnlyList<Entry> Read(EndpointDataSource source)
    {
        // (page, template, order) -> handlers; a controller has one endpoint per action, a page one per template.
        var found = new Dictionary<(string Page, string Template, int Order), SortedSet<string>>();
        foreach (var endpoint in source.Endpoints.OfType<RouteEndpoint>())
        {
            var template = (endpoint.RoutePattern.RawText ?? string.Empty).ToLowerInvariant().TrimStart('/');
            var page = endpoint.Metadata.GetMetadata<CompiledPageActionDescriptor>();
            var action = endpoint.Metadata.GetMetadata<ControllerActionDescriptor>();
            var legacy = endpoint.Metadata.GetMetadata<LegacyPageAttribute>();

            string name;
            IEnumerable<string> handlers;
            if (page != null)
            {
                // "GET:" is the default GET handler, "POST:Approve" the handler named by ?handler=Approve. HEAD is answered by
                // the GET handler of a page and is not a separate handler.
                name = page.ViewEnginePath;
                handlers = page.HandlerMethods
                    .Where(h => HttpVerbs.Contains(h.HttpMethod ?? string.Empty)) // OnPageHandlerExecuting & co. parse as method "Page": filters, not handlers
                    .Select(h => $"{h.HttpMethod?.ToUpperInvariant()}:{h.Name}");
            }
            else if (action != null && legacy != null)
            {
                // A controller action that stands in for a page: the same description (HEAD collapsed into GET).
                name = legacy.PageName;
                var handlerAttribute = action.MethodInfo.GetCustomAttribute<PageHandlerAttribute>();
                var handler = handlerAttribute?.Name;
                var verbs = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods
                    .Select(v => v.ToUpperInvariant()).ToList() ?? new List<string> { "GET" };
                if (verbs.Contains("GET")) verbs.Remove("HEAD");

                // A page with no handler method at all (it only renders, e.g. /Admin/AccessUnavailable) is a read-only action
                // without [PageHandler]: it reports no handlers, like the page did.
                // [ImplicitPageHandler] is the same for a verb the page had no handler for (the page renders itself): not a handler.
                handlers = action.MethodInfo.GetCustomAttribute<ImplicitPageHandlerAttribute>() != null
                           || (handlerAttribute == null && verbs.All(v => v == "GET"))
                    ? Enumerable.Empty<string>()
                    : verbs.Select(v => $"{v}:{handler}");
            }
            else
            {
                continue;
            }

            var key = (name, template, endpoint.Order);
            if (!found.TryGetValue(key, out var set)) found[key] = set = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var handler in handlers) set.Add(handler);
        }

        return found
            .Select(p => new Entry(p.Key.Page, p.Key.Template, p.Key.Order, p.Value.ToList()))
            .OrderBy(e => e.Page, StringComparer.Ordinal)
            .ThenBy(e => e.Template, StringComparer.Ordinal)
            .ToList();
    }
}
