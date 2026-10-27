using System;

namespace BaGet.Identity.Mvc.Attributes;

/// <summary>
/// Names the Razor Page that a controller action replaces (<c>[LegacyPage("/Admin/Users")]</c>, the page's view engine path as
/// <c>asp-page</c> and <c>Url.Page</c> know it). The name stays the stable identifier of a screen after the page itself is gone:
/// <list type="bullet">
/// <item><description><b>Links.</b> <c>Url.Page("/Admin/Users")</c>, <c>asp-page="/Admin/Users"</c> and
/// <c>RedirectToPage("/Admin/Users")</c> keep working, in BaGet's views and in a host's own: when no Razor Page of that name
/// exists, <c>RepositoryUrlHelper</c> resolves the call to the action carrying this attribute (the one that answers
/// <c>GET</c> without a named handler) and keeps <c>?handler=</c>, <c>repo</c> and the other route values.</description></item>
/// <item><description><b>Navigation.</b> <c>NavLinkTagHelper</c> compares the name with the link's <c>asp-page</c>.</description></item>
/// <item><description><b>Coverage.</b> <c>ApiOperation.UiPage</c> and <c>docs/ui-coverage.md</c> name pages by it.</description></item>
/// <item><description><b>Responses.</b> An endpoint with this attribute is a UI endpoint: a scanning refusal is answered with an
/// HTML page, and <c>RepositoryResolutionMiddleware</c> treats it as a page (it does not format-check the route).</description></item>
/// </list>
/// Put it on the controller when the controller is one page, on each action when a controller serves several pages (every
/// action of one page then carries the same name).
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class LegacyPageAttribute : Attribute
{
    /// <param name="pageName">The view engine path of the page this action replaces, e.g. <c>/Admin/Users</c>.</param>
    public LegacyPageAttribute(string pageName)
    {
        if (string.IsNullOrWhiteSpace(pageName) || pageName[0] != '/')
        {
            throw new ArgumentException("A page name starts with '/', like /Admin/Users.", nameof(pageName));
        }

        PageName = pageName;
    }

    /// <summary>The page this action replaces, as <c>asp-page</c> writes it (<c>/Admin/Users</c>).</summary>
    public string PageName { get; }
}
