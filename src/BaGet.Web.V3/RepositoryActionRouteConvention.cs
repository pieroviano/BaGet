using System;
using System.Collections.Generic;
using System.Linq;
using BaGet.Identity.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace BaGet.Web;

/// <summary>
/// Makes the controller actions that replace Razor Pages reachable at <c>/r/{repo}/…</c> as well as at their own route: the
/// counterpart of <see cref="RepositoryPageRouteConvention"/> for actions marked <see cref="RepositoryPageAttribute"/> (on the
/// action or on its controller). Every route of such an action (the controller's <c>[Route]</c> combined with the action's own
/// template, aliases included) gets a twin prefixed with <see cref="Routes.RepositoryPathPrefix"/>.
/// <para>
/// Link generation, as for pages: the twin has a lower <c>Order</c> than the original. Without a <c>repo</c> value (explicit or
/// ambient from the current <c>/r/{repo}/</c> request) the twin cannot be filled in, so <c>Url.Action</c>, <c>asp-action</c>,
/// <c>Url.Page</c> (through the legacy page name) and <c>RedirectToAction</c> produce the plain URL; with one they produce the
/// repository URL, so links stay inside the repository being browsed. Passing <c>repo = null</c> explicitly leaves it. The twin is
/// a selector of the same action, not a second route attribute on a second action: that makes link generation ambiguous.
/// </para>
/// <para>
/// The twin selector is a copy of the original (HTTP method constraints, handler constraint, metadata) with an absolute template,
/// so the controller's route prefix is not applied twice.
/// </para>
/// </summary>
public class RepositoryActionRouteConvention : IApplicationModelConvention
{
    /// <summary>Order of the repository twin of a route; below the default 0 so it wins when it can be filled in.</summary>
    public const int TwinOrder = RepositoryPageRouteConvention.TwinOrder;

    /// <inheritdoc/>
    public void Apply(ApplicationModel application)
    {
        if (application == null) throw new ArgumentNullException(nameof(application));

        foreach (var controller in application.Controllers)
        {
            var controllerMarked = controller.Attributes.OfType<RepositoryPageAttribute>().Any();
            foreach (var action in controller.Actions)
            {
                if (controllerMarked || action.Attributes.OfType<RepositoryPageAttribute>().Any())
                {
                    AddTwins(controller, action);
                }
            }
        }
    }

    private static void AddTwins(ControllerModel controller, ActionModel action)
    {
        var controllerRoutes = controller.Selectors.Select(s => s.AttributeRouteModel).Where(r => r != null).ToList();

        // Snapshot: the twins are added to the same list. Twins are never twinned again (idempotent).
        foreach (var selector in action.Selectors.ToList())
        {
            var own = selector.AttributeRouteModel;
            var effective = new List<AttributeRouteModel>();
            if (own == null)
            {
                // The action inherits the controller's routes.
                effective.AddRange(controllerRoutes!);
            }
            else if (controllerRoutes.Count == 0 || AttributeRouteModel.IsOverridePattern(own.Template))
            {
                effective.Add(own);
            }
            else
            {
                effective.AddRange(controllerRoutes.Select(c => AttributeRouteModel.CombineAttributeRouteModel(c, own)!));
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var route in effective)
            {
                var template = (route.Template ?? string.Empty).TrimStart('~', '/');
                if ((template + "/").StartsWith(Routes.RepositoryPathPrefix, StringComparison.Ordinal) || !seen.Add(template)) continue;

                var twin = new SelectorModel(selector)
                {
                    AttributeRouteModel = new AttributeRouteModel
                    {
                        // The leading slash makes the template absolute: the controller's prefix is already part of it.
                        Template = "/" + (Routes.RepositoryPathPrefix + template).TrimEnd('/'),
                        Order = TwinOrder,
                        Name = string.IsNullOrEmpty(route.Name) ? null : Routes.RepositoryRouteName(route.Name),
                        SuppressLinkGeneration = route.SuppressLinkGeneration,
                        SuppressPathMatching = route.SuppressPathMatching,
                    },
                };

                action.Selectors.Add(twin);
            }
        }
    }
}
