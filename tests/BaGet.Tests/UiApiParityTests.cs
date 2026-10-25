using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using BaGet.Identity.Mvc;
using BaGet.Tests.Contracts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BaGet.Tests;

/// <summary>
/// The view of a screen and its JSON twin (<c>/api/ui</c>) say the same thing: every <c>[UiApi]</c> action serves a screen (and handler) that a view action
/// has; every screen that is not split yet is on the pending list; and, for each screen already split, the model the JSON carries is what the
/// page shows, with corresponding statuses (404 to 404, sign-in redirect to 401). A track adds its screens to <see cref="TheSplitScreensShowTheSameInJsonAndInTheView"/>-style
/// cases below as it moves them.
/// </summary>
public class UiApiParityTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "BaGetUiApi", Guid.NewGuid().ToString("N"));
    private readonly List<IDisposable> _disposables = new();

    public UiApiParityTests(ITestOutputHelper output)
    {
        _output = output;
        Directory.CreateDirectory(_directory);
    }

    public void Dispose()
    {
        foreach (var disposable in _disposables) disposable.Dispose();
        try { Directory.Delete(_directory, true); } catch (IOException) { }
    }

    private BaGetApplication Start(Dictionary<string, string>? settings = null)
    {
        var app = new BaGetApplication(_output, configOverrides: settings);
        _disposables.Add(app);

        return app;
    }

    private string WriteFile(string name, string content)
    {
        var path = Path.Combine(_directory, name);
        File.WriteAllText(path, content);

        return path;
    }

    // ---- structure ----

    private static List<ControllerActionDescriptor> Actions(BaGetApplication app)
        => app.Services.GetRequiredService<IActionDescriptorCollectionProvider>().ActionDescriptors.Items.OfType<ControllerActionDescriptor>().ToList();

    [Fact]
    public void EveryUiApiActionServesAScreenAndHandlerThatAViewActionHas()
    {
        using var app = new BaGetApplication(_output, configOverrides: new Dictionary<string, string> { { "Accounts:Enabled", "true" } });
        var actions = Actions(app);

        var twins = actions
            .Select(a => (Action: a, Api: a.MethodInfo.GetCustomAttribute<UiApiAttribute>()))
            .Where(p => p.Api != null)
            .ToList();
        Assert.NotEmpty(twins);

        // (screen, handler) pairs of the view actions.
        var views = actions
            .Where(a => a.EndpointMetadata.OfType<LegacyPageAttribute>().Any())
            .Select(a => (Screen: a.EndpointMetadata.OfType<LegacyPageAttribute>().Last().PageName, Handler: a.MethodInfo.GetCustomAttribute<PageHandlerAttribute>()?.Name))
            .ToHashSet();

        var missing = twins.Where(p => !views.Contains((p.Api!.Screen, p.Api.Handler))).Select(p => $"{p.Action.DisplayName} -> {p.Api!.Screen} handler={p.Api.Handler}").ToList();

        Assert.True(missing.Count == 0, "UI API actions without a view action of that screen and handler:" + Environment.NewLine + string.Join(Environment.NewLine, missing));
    }

    [Fact]
    public void AUiApiActionIsNeverALegacyPageAndLivesUnderApiUi()
    {
        using var app = new BaGetApplication(_output);

        foreach (var action in Actions(app).Where(a => a.MethodInfo.GetCustomAttribute<UiApiAttribute>() != null))
        {
            // Not [LegacyPage]: the route snapshot of the screens (ui-routes.txt) must not see the API.
            Assert.Empty(action.EndpointMetadata.OfType<LegacyPageAttribute>());
            Assert.StartsWith("api/ui/", action.AttributeRouteInfo!.Template!, StringComparison.Ordinal);
            // [ApiController] would answer 400 by itself over HTTP but not in process.
            Assert.DoesNotContain(action.ControllerTypeInfo.GetCustomAttributes(inherit: true), a => a.GetType().Name == "ApiControllerAttribute");
        }
    }

    [Fact]
    public void EveryScreenWithoutATwinIsOnThePendingList()
    {
        using var app = new BaGetApplication(_output, configOverrides: new Dictionary<string, string> { { "Accounts:Enabled", "true" } });
        var actions = Actions(app);

        var served = actions.Select(a => a.MethodInfo.GetCustomAttribute<UiApiAttribute>()?.Screen).Where(s => s != null).ToHashSet();
        var screens = actions
            .SelectMany(a => a.EndpointMetadata.OfType<LegacyPageAttribute>().Take(1))
            .Select(a => a.PageName)
            .Distinct()
            .Where(s => !served.Contains(s))
            .ToList();

        UiApiPending.VerifyListed(UiApiPending.ScreensFile, screens, "Screens without a [UiApi] twin");
    }

    // ---- pilot: legal pages ----

    [Fact]
    public async Task ALegalPageAndItsTwinCarryTheSameRenderedText()
    {
        using var app = Start(new Dictionary<string, string>
        {
            { "Site:TermsOfUsePath", WriteFile("terms.md", "## Our terms\n\nBe **kind**.") },
            { "Site:PrivacyPolicyPath", WriteFile("privacy.md", "# Privacy\n\n<script>alert(1)</script>") },
        });
        using var client = app.CreateClient();
        var token = TestContext.Current.CancellationToken;

        var page = await client.GetStringAsync("/terms", token);
        var json = JsonNode.Parse(await client.GetStringAsync("/api/ui/terms", token))!;

        Assert.Equal("Ok", json["status"]!.GetValue<string>());
        Assert.Equal("Terms of Use", json["model"]!["title"]!.GetValue<string>());
        var html = json["model"]!["contentHtml"]!.GetValue<string>();
        Assert.Contains("<strong>kind</strong>", html);
        Assert.Contains(html.Trim(), page);

        // The privacy policy is rendered safely in both.
        var privacy = JsonNode.Parse(await client.GetStringAsync("/api/ui/privacy", token))!["model"]!["contentHtml"]!.GetValue<string>();
        Assert.DoesNotContain("<script>", privacy, StringComparison.Ordinal);
        Assert.DoesNotContain("<script>alert(1)</script>", await client.GetStringAsync("/privacy", token), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("terms")]
    [InlineData("privacy")]
    public async Task ALegalPageWithNoFileIs404InBothWays(string name)
    {
        using var app = Start();
        using var client = app.CreateClient();
        var token = TestContext.Current.CancellationToken;

        using var page = await client.GetAsync("/" + name, token);
        using var api = await client.GetAsync("/api/ui/" + name, token);

        Assert.Equal(HttpStatusCode.NotFound, page.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, api.StatusCode);
        Assert.Equal("NotFound", JsonNode.Parse(await api.Content.ReadAsStringAsync(token))!["status"]!.GetValue<string>());
    }

    // ---- pilot: statistics ----

    [Fact]
    public async Task TheStatisticsPageAndItsTwinCountTheSame()
    {
        using var app = Start();
        using var client = app.CreateClient();
        var token = TestContext.Current.CancellationToken;
        using (var package = TestResources.GetResourceStream(TestResources.Package))
        {
            await app.AddPackageAsync(package, token);
        }

        var page = await client.GetStringAsync("/stats", token);
        var json = JsonNode.Parse(await client.GetStringAsync("/api/ui/stats", token))!;
        var statistics = json["model"]!["statistics"]!;

        Assert.Equal(1, statistics["packageIds"]!.GetValue<int>());
        Assert.Equal(1, statistics["packageVersions"]!.GetValue<int>());
        Assert.Contains("<h2 id=\"stat-ids\">1</h2>", page);
        Assert.Contains("<h2 id=\"stat-versions\">1</h2>", page);
        Assert.Equal("TestData", statistics["topPackages"]![0]!["id"]!.GetValue<string>());
        Assert.Contains("TestData", page);
    }

    [Fact]
    public async Task TheStatisticsOfAnotherRepositoryAreTheSameScreenUnderItsTwinRoute()
    {
        using var app = Start();
        await app.CreateRepositoryAsync("second", TestContext.Current.CancellationToken);
        using var client = app.CreateClient();
        var token = TestContext.Current.CancellationToken;

        using var page = await client.GetAsync("/r/second/stats", token);
        using var api = await client.GetAsync("/api/ui/r/second/stats", token);
        using var missingPage = await client.GetAsync("/r/nope/stats", token);
        using var missingApi = await client.GetAsync("/api/ui/r/nope/stats", token);

        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Equal(HttpStatusCode.OK, api.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missingPage.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missingApi.StatusCode);
    }

    [Fact]
    public async Task APrivateRepositoryIsASignInForAnAnonymousVisitorOnTheTwinAndOnThePage()
    {
        using var app = Start(new Dictionary<string, string> { { "Accounts:Enabled", "true" } });
        var token = TestContext.Current.CancellationToken;
        using (var scope = app.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<BaGet.Core.IRepositoryService>()
                .CreateAsync("closed", BaGet.Core.RepositoryFormat.NuGet, BaGet.Core.RepositoryKind.Local, new Dictionary<string, string?>(), anonymousRead: false, token);
        }

        using var client = app.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using var page = await client.GetAsync("/r/closed/stats", token);
        using var api = await client.GetAsync("/api/ui/r/closed/stats", token);

        // The page redirects to the sign-in page, the twin answers 401: the same decision in each programming model.
        Assert.Equal(HttpStatusCode.Redirect, page.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, api.StatusCode);
    }
}
