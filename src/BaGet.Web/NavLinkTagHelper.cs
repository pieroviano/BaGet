using System;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace BaGet.Web;

[HtmlTargetElement(Attributes = "nav-link")]
public class NavLinkTagHelper : TagHelper
{
    private const string ActiveClass = "active";
    private const string IndexPage = "/Index";
    private const string PackagesPath = "/packages";

    private readonly IHttpContextAccessor _accessor;

    public NavLinkTagHelper(IHttpContextAccessor accessor)
    {
        _accessor = accessor ?? throw new ArgumentNullException(nameof(accessor));
    }

    [HtmlAttributeName("asp-page")]
    public string? Page { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (!IsActiveLink())
        {
            return;
        }

        // This helper applies to any element carrying "nav-link", so it adds its class
        // instead of replacing the classes the markup already set.
        var existing = output.Attributes.ContainsName("class")
            ? output.Attributes["class"].Value?.ToString()
            : null;

        output.Attributes.SetAttribute("class", WithActiveClass(existing));
    }

    private static string WithActiveClass(string? classes)
    {
        if (string.IsNullOrWhiteSpace(classes))
        {
            return ActiveClass;
        }

        foreach (var name in classes.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (string.Equals(name, ActiveClass, StringComparison.Ordinal))
            {
                return classes;
            }
        }

        return $"{classes} {ActiveClass}";
    }

    private bool IsActiveLink()
    {
        var httpContext = _accessor.HttpContext;
        var pageDescriptor = httpContext?.GetEndpoint()?.Metadata.GetMetadata<PageActionDescriptor>();

        if (pageDescriptor == null) return false;
        if (pageDescriptor.AreaName != null) return false;

        // When the link names a page, that name alone decides whether it is active.
        // Falling through to the path check below would light up every nav link on the
        // pages that check covers.
        if (!string.IsNullOrEmpty(Page))
        {
            return pageDescriptor.ViewEnginePath == Page;
        }

        // The packages link names no page: both "/" and "/packages" render the index.
        if (pageDescriptor.ViewEnginePath != IndexPage) return false;

        // Routing is case-insensitive and tolerates a trailing slash, so the comparison
        // must be too.
        #pragma warning disable CS8602
        var path = httpContext.Request.Path.Value?.TrimEnd('/');
        #pragma warning restore CS8602

        return string.IsNullOrEmpty(path)
               || string.Equals(path, PackagesPath, StringComparison.OrdinalIgnoreCase);
    }
}