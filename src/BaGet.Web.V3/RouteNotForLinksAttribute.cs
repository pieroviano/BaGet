using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace BaGet.Web;

/// <summary>
/// Lists routes of an action that answer requests but are never produced by link generation. An action with several routes
/// (<c>[Route("")]</c>, <c>[Route("index")]</c>, <c>[Route("packages")]</c>) is linked to by <c>Url.Action</c>, <c>Url.Page</c> (through
/// <see cref="BaGet.Identity.Mvc.LegacyPageAttribute"/>) and <c>asp-page</c> at one of them, and which one is not under the
/// action's control (ASP.NET Core breaks the tie by template text). Listing the others here keeps the address that links have always
/// had: the front page lists packages at <c>/packages</c>, as the former Razor Page's <c>AddPageRoute("/Index", "/packages")</c>
/// alias did, while <c>/</c> and <c>/index</c> still reach it. The repository twins of the listed routes (<c>/r/{repo}/index</c>) are
/// not linked either (<see cref="RepositoryActionRouteConvention"/> copies the setting).
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class RouteNotForLinksAttribute : Attribute
{
    /// <param name="templates">The route templates of the action that links must not use (<c>""</c> is the root).</param>
    public RouteNotForLinksAttribute(params string[] templates)
    {
        Templates = templates ?? throw new ArgumentNullException(nameof(templates));
    }

    /// <summary>The route templates, as written in the <c>[Route]</c> attributes.</summary>
    public string[] Templates { get; }
}

/// <summary>
/// Applies <see cref="RouteNotForLinksAttribute"/>: sets <c>SuppressLinkGeneration</c> on the listed selectors of an action. Must be
/// registered before <see cref="RepositoryActionRouteConvention"/> so the repository twins inherit the setting.
/// </summary>
public sealed class RouteNotForLinksConvention : IApplicationModelConvention
{
    /// <inheritdoc/>
    public void Apply(ApplicationModel application)
    {
        if (application == null) throw new ArgumentNullException(nameof(application));

        foreach (var action in application.Controllers.SelectMany(c => c.Actions))
        {
            var attribute = action.Attributes.OfType<RouteNotForLinksAttribute>().FirstOrDefault();
            if (attribute == null) continue;

            foreach (var selector in action.Selectors)
            {
                var route = selector.AttributeRouteModel;
                if (route != null && attribute.Templates.Contains(route.Template ?? string.Empty, StringComparer.OrdinalIgnoreCase))
                {
                    route.SuppressLinkGeneration = true;
                }
            }
        }
    }
}
