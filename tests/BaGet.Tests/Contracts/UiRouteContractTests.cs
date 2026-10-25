using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BaGet.Tests.Contracts;

/// <summary>
/// Contract 1 of the Razor Pages to MVC migration: every UI endpoint (route template including the <c>/r/{repo}/</c> twins,
/// the <c>/packages</c> alias, catch-alls and constraints, route order, and the HTTP method plus <c>?handler=</c> name of every
/// handler) is listed in <c>Contracts/ui-routes.txt</c>. The file must stay byte-identical after every migration track.
/// </summary>
public class UiRouteContractTests : IDisposable
{
    private readonly BaGetApplication _app;

    public UiRouteContractTests(ITestOutputHelper output)
    {
        _app = new BaGetApplication(output, configOverrides: new Dictionary<string, string>
        {
            { "Accounts:Enabled", "true" },
            { "Validation:Enabled", "true" },
            { "Feeds:EnableV2", "true" },
        });
    }

    public void Dispose() => _app.Dispose();

    [Fact]
    public void TheUiRoutesMatchTheSnapshot()
    {
        var entries = UiRoutes.Read(_app.Services.GetRequiredService<EndpointDataSource>());
        Assert.True(entries.Count > 90, "expected the pages and their /r/{repo}/ twins");

        ContractSnapshot.Verify("ui-routes.txt", string.Join("\n", entries.Select(e => e.ToString())));
    }

    // Razor Pages behaviours the controller base classes must reproduce (plan, "Blocking verification" 3).

    [Fact]
    public async Task AnUnknownHandlerRendersThePageWithItsDefaultHandler()
    {
        using var client = _app.CreateClient();

        using var plain = await client.GetAsync("/stats", TestContext.Current.CancellationToken);
        using var unknown = await client.GetAsync("/stats?handler=NoSuchHandler", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, plain.StatusCode);
        Assert.Equal(HttpStatusCode.OK, unknown.StatusCode);
    }

    [Fact]
    public async Task HeadIsAnsweredByTheGetHandler()
    {
        using var client = _app.CreateClient();

        using var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, "/stats"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
