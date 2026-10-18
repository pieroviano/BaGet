using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Xunit;

namespace BaGet.Web.Tests;

public class NavLinkTagHelperTests
{
    private static TagHelperOutput Output(params (string Name, string Value)[] attributes)
    {
        var list = new TagHelperAttributeList();
        foreach (var attribute in attributes)
        {
            list.Add(attribute.Name, attribute.Value);
        }

        return new TagHelperOutput(
            "a",
            list,
            (useCachedResult, encoder) =>
                Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));
    }

    private static TagHelperContext Context()
        => new TagHelperContext(
            new TagHelperAttributeList(),
            new Dictionary<object, object>(),
            "test");

    private static IHttpContextAccessor Accessor(
        string viewEnginePath,
        string requestPath = "/",
        string? areaName = null,
        bool withEndpoint = true,
        bool withHttpContext = true)
    {
        if (!withHttpContext)
        {
            return new HttpContextAccessor();
        }

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = requestPath;

        if (withEndpoint)
        {
            var descriptor = new PageActionDescriptor
            {
                ViewEnginePath = viewEnginePath,
                AreaName = areaName,
            };

            httpContext.SetEndpoint(new Endpoint(
                _ => Task.CompletedTask,
                new EndpointMetadataCollection(descriptor),
                "test"));
        }

        return new HttpContextAccessor { HttpContext = httpContext };
    }

    private static string? ClassOf(TagHelperOutput output)
        => output.Attributes.ContainsName("class")
            ? output.Attributes["class"].Value?.ToString()
            : null;

    [Fact]
    public void MarksTheLinkForTheCurrentPageActive()
    {
        var target = new NavLinkTagHelper(Accessor("/Upload")) { Page = "/Upload" };
        var output = Output();

        target.Process(Context(), output);

        Assert.Equal("active", ClassOf(output));
    }

    [Fact]
    public void LeavesALinkForAnotherPageAlone()
    {
        var target = new NavLinkTagHelper(Accessor("/Index")) { Page = "/Upload" };
        var output = Output();

        target.Process(Context(), output);

        Assert.Null(ClassOf(output));
    }

    [Fact]
    public void KeepsTheClassesTheElementAlreadyHas()
    {
        // The helper targets any element carrying "nav-link", so it must add its class
        // rather than replace whatever the markup specified.
        var target = new NavLinkTagHelper(Accessor("/Upload")) { Page = "/Upload" };
        var output = Output(("class", "nav-item highlighted"));

        target.Process(Context(), output);

        var classes = ClassOf(output);
        Assert.Contains("nav-item", classes);
        Assert.Contains("highlighted", classes);
        Assert.Contains("active", classes);
    }

    [Fact]
    public void DoesNotAddTheClassTwice()
    {
        var target = new NavLinkTagHelper(Accessor("/Upload")) { Page = "/Upload" };
        var output = Output(("class", "active"));

        target.Process(Context(), output);

        Assert.Equal("active", ClassOf(output));
    }

    [Fact]
    public void IsNotActiveWithoutAnHttpContext()
    {
        var target = new NavLinkTagHelper(Accessor("/Upload", withHttpContext: false))
        {
            Page = "/Upload",
        };
        var output = Output();

        target.Process(Context(), output);

        Assert.Null(ClassOf(output));
    }

    [Fact]
    public void IsNotActiveWithoutAnEndpoint()
    {
        var target = new NavLinkTagHelper(Accessor("/Upload", withEndpoint: false))
        {
            Page = "/Upload",
        };
        var output = Output();

        target.Process(Context(), output);

        Assert.Null(ClassOf(output));
    }

    [Fact]
    public void IsNotActiveForAPageInAnArea()
    {
        var target = new NavLinkTagHelper(Accessor("/Upload", areaName: "Admin"))
        {
            Page = "/Upload",
        };
        var output = Output();

        target.Process(Context(), output);

        Assert.Null(ClassOf(output));
    }

    [Theory]
    [InlineData("/packages")]
    [InlineData("/")]
    public void ThePackagesLinkIsActiveOnBothOfItsRoutes(string requestPath)
    {
        // _Layout.cshtml links to /packages with no asp-page, and both / and /packages
        // render the Index page.
        var target = new NavLinkTagHelper(Accessor("/Index", requestPath));
        var output = Output();

        target.Process(Context(), output);

        Assert.Equal("active", ClassOf(output));
    }

    [Theory]
    [InlineData("/Packages")]
    [InlineData("/PACKAGES")]
    public void ThePackagesLinkIsActiveWhateverTheCaseOfTheRequestPath(string requestPath)
    {
        // Routing is case-insensitive, so these all render the package list.
        var target = new NavLinkTagHelper(Accessor("/Index", requestPath));
        var output = Output();

        target.Process(Context(), output);

        Assert.Equal("active", ClassOf(output));
    }

    [Fact]
    public void ThePackagesLinkIsActiveWithATrailingSlash()
    {
        var target = new NavLinkTagHelper(Accessor("/Index", "/packages/"));
        var output = Output();

        target.Process(Context(), output);

        Assert.Equal("active", ClassOf(output));
    }

    [Fact]
    public void ThePackagesLinkIsNotActiveOnAnUnrelatedPath()
    {
        var target = new NavLinkTagHelper(Accessor("/Index", "/something-else"));
        var output = Output();

        target.Process(Context(), output);

        Assert.Null(ClassOf(output));
    }
}