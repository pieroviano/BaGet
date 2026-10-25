using System.Reflection;
using BaGet.Identity.Mvc;
using BaGet.Identity.Workflows;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace BaGet.Identity.Controllers;

/// <summary>What the package-management controllers share: reading the request the way the page models' bound properties did.</summary>
internal static class PackageControllerSupport
{
    /// <summary>
    /// The name of the handler about to run (<c>[PageHandler("Unlist")]</c> gives <c>Unlist</c>; the default handler null): what a
    /// page model's <c>HandlerMethod.Name</c> was, for the administrator audit line.
    /// </summary>
    public static string? HandlerName(ActionExecutingContext context)
        => (context.ActionDescriptor as ControllerActionDescriptor)?.MethodInfo.GetCustomAttribute<PageHandlerAttribute>()?.Name;

    /// <summary>A route value as text (the identity of the screen's subject comes from the address, never from the body).</summary>
    public static string? Route(ActionExecutingContext context, string key)
        => context.RouteData.Values.TryGetValue(key, out var value) ? value as string : null;

    /// <summary>A form field of a POST, else a query string value: how a page model's <c>SupportsGet</c> property was bound.</summary>
    public static string? FormOrQuery(ActionExecutingContext context, string key)
    {
        var request = context.HttpContext.Request;
        if (request.HasFormContentType && request.Form.TryGetValue(key, out var form) && form.Count > 0)
        {
            var posted = form.ToString();

            return posted.Length == 0 ? null : posted;
        }

        var query = request.Query[key].ToString();

        return query.Length == 0 ? null : query;
    }

    /// <summary>Turns a workflow's "show the screen" marker into the controller's view; any other result is returned as it is.</summary>
    public static IActionResult Resolve(Controller controller, IActionResult result, string view, object model)
        => PackageWorkflowBase.IsShow(result) ? controller.View(view, model) : result;
}
