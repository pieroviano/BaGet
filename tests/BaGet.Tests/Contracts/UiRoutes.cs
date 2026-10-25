using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;

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
        var entries = new List<Entry>();
        foreach (var endpoint in source.Endpoints.OfType<RouteEndpoint>())
        {
            var page = endpoint.Metadata.GetMetadata<CompiledPageActionDescriptor>();
            if (page == null) continue;

            // "GET:" is the default GET handler, "POST:Approve" the handler named by ?handler=Approve. HEAD is answered by the GET
            // handler of a page and is not a separate handler.
            var handlers = page.HandlerMethods
                .Where(h => HttpVerbs.Contains(h.HttpMethod ?? string.Empty)) // OnPageHandlerExecuting & co. parse as method "Page": filters, not handlers
                .Select(h => $"{h.HttpMethod?.ToUpperInvariant()}:{h.Name}")
                .Distinct(StringComparer.Ordinal)
                .OrderBy(h => h, StringComparer.Ordinal)
                .ToList();

            entries.Add(new Entry(page.ViewEnginePath, (endpoint.RoutePattern.RawText ?? string.Empty).ToLowerInvariant().TrimStart('/'), endpoint.Order, handlers));
        }

        return entries
            .OrderBy(e => e.Page, StringComparer.Ordinal)
            .ThenBy(e => e.Template, StringComparer.Ordinal)
            .ToList();
    }
}
