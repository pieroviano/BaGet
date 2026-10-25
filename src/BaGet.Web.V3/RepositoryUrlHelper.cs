using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace BaGet.Web;

/// <summary>
/// Keeps page links inside the repository being browsed. ASP.NET Core carries ambient route values (here
/// <c>repo</c>) only to links that target the <i>same</i> page, so <c>Url.Page("/Upload")</c>,
/// <c>asp-page="/Package"</c> and <c>RedirectToPage("/Packages/Manage")</c> on a <c>/r/second/…</c> page would
/// otherwise drop the prefix and leave the repository. This helper adds the request's <c>repo</c> as an explicit route
/// value to every link to a repository page (a page whose action is marked <see cref="BaGet.Identity.Mvc.RepositoryPageAttribute"/>) that does
/// not name one itself. An explicit value always wins, and <c>repo = null</c> is the way to leave for the default
/// repository. A link to any other page is untouched, so no stray <c>?repo=</c> appears on it.
/// <para>
/// The same holds for links to controller actions that are repository pages (<c>Url.Action</c>, <c>asp-action</c>,
/// <c>RedirectToAction</c> to an action marked <see cref="BaGet.Identity.Mvc.RepositoryPageAttribute"/>).
/// </para>
/// <para>
/// <b>Legacy page names.</b> A link to a page that is a Razor Page no more (<c>Url.Page("/Admin/Users")</c>, <c>asp-page</c>,
/// <c>RedirectToPage</c>, in BaGet's views, a host's views or a redirect) is resolved to the controller action that carries
/// <see cref="BaGet.Identity.Mvc.LegacyPageAttribute"/> with that name (<see cref="UiActionMap"/>), keeping <c>handler</c> (as
/// <c>?handler=</c>, like a page) and every other route value, so a page can move to a controller without any link changing.
/// </para>
/// </summary>
public class RepositoryUrlHelper : IUrlHelper
{
    private readonly IUrlHelper _inner;
    private readonly UiActionMap? _actions;

    /// <param name="inner">MVC's own helper for the same context (the endpoint-routing one), which does the work.</param>
    public RepositoryUrlHelper(IUrlHelper inner)
        : this(inner, null)
    {
    }

    internal RepositoryUrlHelper(IUrlHelper inner, UiActionMap? actions)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _actions = actions;
    }

    /// <inheritdoc/>
    public ActionContext ActionContext => _inner.ActionContext;

    /// <inheritdoc/>
    public string? Action(UrlActionContext actionContext)
    {
        if (actionContext == null) throw new ArgumentNullException(nameof(actionContext));

        if (_actions != null && ActionContext.RouteData.Values.TryGetValue(Routes.RepositoryRouteKey, out var repo) && repo is string name)
        {
            var values = actionContext.Values as RouteValueDictionary
                ?? (actionContext.Values == null ? new RouteValueDictionary() : new RouteValueDictionary(actionContext.Values));

            // A missing controller or action is the current one, as MVC's own helper reads it.
            var controller = actionContext.Controller ?? Convert.ToString(ActionContext.RouteData.Values["controller"], System.Globalization.CultureInfo.InvariantCulture);
            var action = actionContext.Action ?? Convert.ToString(ActionContext.RouteData.Values["action"], System.Globalization.CultureInfo.InvariantCulture);
            if (!values.ContainsKey(Routes.RepositoryRouteKey) && controller != null && action != null && _actions.IsRepositoryAction(controller, action))
            {
                values[Routes.RepositoryRouteKey] = name;
                actionContext = new UrlActionContext
                {
                    Action = actionContext.Action,
                    Controller = actionContext.Controller,
                    Values = values,
                    Protocol = actionContext.Protocol,
                    Host = actionContext.Host,
                    Fragment = actionContext.Fragment,
                };
            }
        }

        return _inner.Action(actionContext);
    }

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

        if (routeContext.RouteName == null && values.TryGetValue("page", out var page) && page is string pageName)
        {
            var changed = false;

            if (!values.ContainsKey(Routes.RepositoryRouteKey)
                && ActionContext.RouteData.Values.TryGetValue(Routes.RepositoryRouteKey, out var repo) && repo is string name
                && IsRepositoryPage(pageName))
            {
                values[Routes.RepositoryRouteKey] = name;
                changed = true;
            }

            // A page that is a controller action now: the same URL, found through the action that replaced it.
            if (_actions != null && _actions.TryResolveLegacyPage(pageName, out var target))
            {
                values.Remove("page");
                values["controller"] = target.Controller;
                values["action"] = target.Action;
                if (values.TryGetValue("handler", out var handler) && string.IsNullOrEmpty(handler as string)) values.Remove("handler");
                changed = true;
            }

            if (changed)
            {
                routeContext = new UrlRouteContext
                {
                    RouteName = routeContext.RouteName,
                    Values = values,
                    Protocol = routeContext.Protocol,
                    Host = routeContext.Host,
                    Fragment = routeContext.Fragment,
                };
            }
        }

        return _inner.RouteUrl(routeContext);
    }

    // Page() has already turned a relative page name into an absolute one (UrlHelperBase.NormalizeRouteValuesForPage),
    // so the value is compared as MVC knows pages.
    // A helper built without the action map (the public constructor) knows the pages only by the obsolete static list.
    private bool IsRepositoryPage(string pageName)
    {
        if (_actions != null) return _actions.IsRepositoryPage(pageName);
#pragma warning disable CS0618
        return RepositoryPageRouteConvention.RepositoryPages.Contains(pageName, StringComparer.OrdinalIgnoreCase);
#pragma warning restore CS0618
    }
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

        helper = new RepositoryUrlHelper(_default.GetUrlHelper(context), context.HttpContext.RequestServices.GetService<UiActionMap>());
        items[typeof(RepositoryUrlHelper)] = helper;

        return helper;
    }
}
