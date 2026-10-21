using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;

namespace BaGet.Web;

/// <summary>
/// Keeps page links inside the repository being browsed. ASP.NET Core carries ambient route values (here
/// <c>repo</c>) only to links that target the <i>same</i> page, so <c>Url.Page("/Upload")</c>,
/// <c>asp-page="/Package"</c> and <c>RedirectToPage("/Packages/Manage")</c> on a <c>/r/second/…</c> page would
/// otherwise drop the prefix and leave the repository. This helper adds the request's <c>repo</c> as an explicit route
/// value to every link to a repository page (<see cref="RepositoryPageRouteConvention.RepositoryPages"/>) that does
/// not name one itself. An explicit value always wins, and <c>repo = null</c> is the way to leave for the default
/// repository. A link to any other page is untouched, so no stray <c>?repo=</c> appears on it.
/// </summary>
public class RepositoryUrlHelper : IUrlHelper
{
    private readonly IUrlHelper _inner;

    /// <param name="inner">MVC's own helper for the same context (the endpoint-routing one), which does the work.</param>
    public RepositoryUrlHelper(IUrlHelper inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    /// <inheritdoc/>
    public ActionContext ActionContext => _inner.ActionContext;

    /// <inheritdoc/>
    public string? Action(UrlActionContext actionContext) => _inner.Action(actionContext);

    /// <inheritdoc/>
    public string? Content(string? contentPath) => _inner.Content(contentPath);

    /// <inheritdoc/>
    public bool IsLocalUrl(string? url) => _inner.IsLocalUrl(url);

    /// <inheritdoc/>
    public string? Link(string? routeName, object? values) => _inner.Link(routeName, values);

    /// <inheritdoc/>
    public string? RouteUrl(UrlRouteContext routeContext)
    {
        if (routeContext == null) throw new ArgumentNullException(nameof(routeContext));

        var values = routeContext.Values as RouteValueDictionary
            ?? (routeContext.Values == null ? new RouteValueDictionary() : new RouteValueDictionary(routeContext.Values));

        if (routeContext.RouteName == null
            && !values.ContainsKey(Routes.RepositoryRouteKey)
            && values.TryGetValue("page", out var page) && page is string pageName
            && ActionContext.RouteData.Values.TryGetValue(Routes.RepositoryRouteKey, out var repo) && repo is string name
            && IsRepositoryPage(pageName))
        {
            values[Routes.RepositoryRouteKey] = name;
            routeContext = new UrlRouteContext
            {
                RouteName = routeContext.RouteName,
                Values = values,
                Protocol = routeContext.Protocol,
                Host = routeContext.Host,
                Fragment = routeContext.Fragment,
            };
        }

        return _inner.RouteUrl(routeContext);
    }

    // Page() has already turned a relative page name into an absolute one (UrlHelperBase.NormalizeRouteValuesForPage),
    // so the value is compared as MVC knows pages.
    private static bool IsRepositoryPage(string pageName)
        => RepositoryPageRouteConvention.RepositoryPages.Contains(pageName, StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Hands out <see cref="RepositoryUrlHelper"/> wrapping MVC's default URL helper; one per request, like the default.
/// </summary>
public class RepositoryUrlHelperFactory : IUrlHelperFactory
{
    private readonly UrlHelperFactory _default = new UrlHelperFactory();

    /// <inheritdoc/>
    public IUrlHelper GetUrlHelper(ActionContext context)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));

        var items = context.HttpContext.Items;
        RepositoryUrlHelper? helper;
        if (items.TryGetValue(typeof(RepositoryUrlHelper), out var cached)
            && (helper = cached as RepositoryUrlHelper) != null
            && ReferenceEquals(helper.ActionContext.RouteData, context.RouteData)
            && ReferenceEquals(helper.ActionContext.ActionDescriptor, context.ActionDescriptor))
        {
            return helper;
        }

        helper = new RepositoryUrlHelper(_default.GetUrlHelper(context));
        items[typeof(RepositoryUrlHelper)] = helper;

        return helper;
    }
}
