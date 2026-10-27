using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using BaGet.Identity.Mvc;
using Microsoft.AspNetCore.Mvc.ActionConstraints;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BaGet.Web.V3;

/// <summary>
/// Which controller action stands for which page, read from the registered actions. It is the one place that knows that
/// <c>/Admin/Users</c> is now <c>AdminUsersController.Index</c>: <see cref="RepositoryUrlHelper"/> asks it to resolve a link to a
/// page (<c>Url.Page</c>, <c>asp-page</c>, <c>RedirectToPage</c>) that has no Razor Page any more, and to find the actions that are
/// repository pages (<see cref="RepositoryPageAttribute"/>); <see cref="NavLinkTagHelper"/> and the coverage tests read the page name
/// of the current endpoint from <see cref="LegacyPageAttribute"/> directly.
/// </summary>
internal sealed class UiActionMap
{
    private readonly IActionDescriptorCollectionProvider _actions;
    private UiActionMapSnapshot? _snapshot;

    public UiActionMap(IActionDescriptorCollectionProvider actions)
    {
        _actions = actions ?? throw new ArgumentNullException(nameof(actions));
    }

    /// <summary>The controller and action that answer <c>GET</c> for page <paramref name="page"/>, when no Razor Page has that name.</summary>
    public bool TryResolveLegacyPage(string page, out (string Controller, string Action) target)
    {
        var snapshot = Current();
        if (!snapshot.RazorPages.Contains(page) && snapshot.LegacyPages.TryGetValue(page, out target)) return true;

        target = default;
        return false;
    }

    /// <summary>Whether page <paramref name="page"/> is served at <c>/r/{repo}/…</c> too (its action is marked <see cref="RepositoryPageAttribute"/>).</summary>
    public bool IsRepositoryPage(string page)
    {
        var snapshot = Current();

        return snapshot.RepositoryLegacyPages.Contains(page);
    }

    /// <summary>Whether the action <paramref name="controller"/>.<paramref name="action"/> is marked <see cref="RepositoryPageAttribute"/>.</summary>
    public bool IsRepositoryAction(string controller, string action)
        => Current().RepositoryActions.Contains(controller + "/" + action);

    private UiActionMapSnapshot Current()
    {
        var collection = _actions.ActionDescriptors;
        var snapshot = Volatile.Read(ref _snapshot);
        if (snapshot != null && snapshot.Version == collection.Version) return snapshot;

        var razorPages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var legacy = new Dictionary<string, (ControllerActionDescriptor Descriptor, int Rank)>(StringComparer.OrdinalIgnoreCase);
        var repositoryLegacy = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var repositoryActions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var descriptor in collection.Items)
        {
            if (descriptor is PageActionDescriptor page)
            {
                razorPages.Add(page.ViewEnginePath);
                continue;
            }

            if (descriptor is not ControllerActionDescriptor action) continue;

            var name = action.EndpointMetadata.OfType<LegacyPageAttribute>().LastOrDefault()?.PageName;
            var isRepositoryPage = action.EndpointMetadata.OfType<RepositoryPageAttribute>().Any();
            if (isRepositoryPage) repositoryActions.Add(action.ControllerName + "/" + action.ActionName);
            if (name == null) continue;

            if (isRepositoryPage) repositoryLegacy.Add(name);

            // The action that answers a plain GET is the page; a handler action posts to the same URL.
            var rank = Rank(action);
            if (!legacy.TryGetValue(name, out var existing) || rank < existing.Rank) legacy[name] = (action, rank);
        }

        snapshot = new UiActionMapSnapshot(
            collection.Version,
            razorPages,
            legacy.ToDictionary(p => p.Key, p => (p.Value.Descriptor.ControllerName, p.Value.Descriptor.ActionName), StringComparer.OrdinalIgnoreCase),
            repositoryLegacy,
            repositoryActions);
        Volatile.Write(ref _snapshot, snapshot);

        return snapshot;
    }

    // 0: GET without a named handler (the page itself), 1: GET with a handler, 2: anything else.
    private static int Rank(ControllerActionDescriptor action)
    {
        var verbs = action.ActionConstraints?.OfType<HttpMethodActionConstraint>().SelectMany(c => c.HttpMethods).ToList();
        var handler = action.MethodInfo.GetCustomAttributes(typeof(PageHandlerAttribute), inherit: true).Cast<PageHandlerAttribute>().FirstOrDefault()?.Name;
        var isGet = verbs == null || verbs.Count == 0 || verbs.Contains("GET", StringComparer.OrdinalIgnoreCase);

        return isGet ? (handler == null ? 0 : 1) : 2;
    }
}
