// The obsolete page convention is still public API (a host can apply it to its own pages), so it keeps its tests.
#pragma warning disable CS0618
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Xunit;

namespace BaGet.Web.Tests;

/// <summary>
/// The page route convention (each repository page gets an <c>r/{repo}/</c> twin of every selector) and the URL helper
/// that carries the request's repository into links to other repository pages.
/// </summary>
public class RepositoryPageRoutingFacts
{
    private static PageRouteModel Model(string viewEnginePath, params string[] templates)
    {
        var model = new PageRouteModel("/Pages" + viewEnginePath + ".cshtml", viewEnginePath);
        foreach (var template in templates)
        {
            model.Selectors.Add(new SelectorModel { AttributeRouteModel = new AttributeRouteModel { Template = template } });
        }

        return model;
    }

    [Fact]
    public void EverySelectorOfARepositoryPageGetsATwinWithALowerOrder()
    {
        var model = Model("/Index", "", "Index", "packages");

        new RepositoryPageRouteConvention().Apply(model);

        var templates = model.Selectors.Select(s => (s.AttributeRouteModel!.Template, s.AttributeRouteModel.Order)).ToList();
        Assert.Equal(6, templates.Count);
        Assert.Contains(("r/{repo}", RepositoryPageRouteConvention.TwinOrder), templates);
        Assert.Contains(("r/{repo}/Index", RepositoryPageRouteConvention.TwinOrder), templates);
        Assert.Contains(("r/{repo}/packages", RepositoryPageRouteConvention.TwinOrder), templates);
        // The originals are untouched.
        Assert.Contains(("packages", (int?)null), templates);
    }

    [Fact]
    public void ParameterisedTemplatesAreAlsoPrefixed()
    {
        var model = Model("/Package", "packages/{id}/{version?}");

        new RepositoryPageRouteConvention().Apply(model);

        Assert.Contains(model.Selectors, s => s.AttributeRouteModel!.Template == "r/{repo}/packages/{id}/{version?}");
    }

    [Fact]
    public void ApplyingTwiceDoesNotTwinTheTwins()
    {
        var model = Model("/Upload", "Upload");
        var convention = new RepositoryPageRouteConvention();

        convention.Apply(model);
        convention.Apply(model);

        Assert.Equal(3, model.Selectors.Count);
    }

    [Fact]
    public void APageThatDoesNotActOnARepositoryKeepsItsOneRoute()
    {
        var model = Model("/Account/Login", "account/login");

        new RepositoryPageRouteConvention().Apply(model);

        Assert.Single(model.Selectors);
    }

    private sealed class RecordingHelper : IUrlHelper
    {
        public UrlRouteContext? Last { get; private set; }

        public ActionContext ActionContext { get; init; } = new();
        public string? Action(UrlActionContext actionContext) => null;
        public string? Content(string? contentPath) => contentPath;
        public bool IsLocalUrl(string? url) => true;
        public string? Link(string? routeName, object? values) => null;

        public string? RouteUrl(UrlRouteContext routeContext)
        {
            Last = routeContext;
            return "ok";
        }
    }

    private static (RepositoryUrlHelper Helper, RecordingHelper Inner) HelperFor(string? repo, string currentPage)
    {
        var routeData = new RouteData();
        routeData.Values["page"] = currentPage;
        if (repo != null) routeData.Values["repo"] = repo;

        var inner = new RecordingHelper { ActionContext = new ActionContext { HttpContext = new DefaultHttpContext(), RouteData = routeData } };

        return (new RepositoryUrlHelper(inner), inner);
    }

    private static RouteValueDictionary Values(RecordingHelper inner)
        => inner.Last!.Values as RouteValueDictionary ?? new RouteValueDictionary(inner.Last.Values);

    [Theory]
    [InlineData("/Upload")]
    [InlineData("/Package")]
    [InlineData("/Packages/Manage")]
    [InlineData("/Profiles/Profile")]
    public void ALinkToARepositoryPageCarriesTheAmbientRepository(string target)
    {
        var (helper, inner) = HelperFor("second", "/Statistics");

        helper.Page(target);

        Assert.Equal("second", Values(inner)["repo"]);
    }

    [Fact]
    public void ALinkToAnotherPageIsLeftAlone()
    {
        var (helper, inner) = HelperFor("second", "/Statistics");

        helper.Page("/Account/Login");

        Assert.False(Values(inner).ContainsKey("repo"));
    }

    [Fact]
    public void AnExplicitRepositoryWinsAndNullLeavesTheRepository()
    {
        var (helper, inner) = HelperFor("second", "/Statistics");

        helper.Page("/Upload", new { repo = "other" });
        Assert.Equal("other", Values(inner)["repo"]);

        helper.Page("/Upload", new { repo = (string?)null });
        Assert.True(Values(inner).ContainsKey("repo"));
        Assert.Null(Values(inner)["repo"]);
    }

    [Fact]
    public void WithoutAnAmbientRepositoryNothingIsAdded()
    {
        var (helper, inner) = HelperFor(null, "/Statistics");

        helper.Page("/Upload");

        Assert.False(Values(inner).ContainsKey("repo"));
    }

    [Fact]
    public void ANamedRouteIsNeverTouched()
    {
        var (helper, inner) = HelperFor("second", "/Statistics");

        helper.RouteUrl(new UrlRouteContext { RouteName = "index", Values = new { page = "/Upload" } });

        Assert.False(Values(inner).ContainsKey("repo"));
    }
}
