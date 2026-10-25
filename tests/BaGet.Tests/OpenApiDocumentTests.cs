using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Web;
using BaGet.Web.Api;
using BaGet.Web.OpenApi;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static BaGet.Tests.AdminTestSupport;

namespace BaGet.Tests;

/// <summary>
/// The OpenAPI documents (<c>ui</c>, <c>v1</c>, <c>protocols</c>) and the Scalar viewer: what they list (every <c>/api/v1</c> action,
/// every protocol route in its plain and <c>r/{repo}/</c> variants, whatever <c>/api/ui</c> holds) and who may read them
/// (administrators only: cookie or <c>admin:api</c> key; anyone else 404).
/// </summary>
public class OpenApiDocumentTests : IDisposable
{
    private readonly AdminApiHarness _api;
    private readonly ITestOutputHelper _output;

    public OpenApiDocumentTests(ITestOutputHelper output)
    {
        _output = output;
        _api = new AdminApiHarness(output);
    }

    public void Dispose() => _api.Dispose();

    [Fact]
    public async Task TheV1DocumentListsEveryAdministrationOperationAndTheAliasServesTheSameDocument()
    {
        var document = await _api.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, document.Status);
        Assert.StartsWith("3.0", document.Json["openapi"]!.GetValue<string>());
        Assert.Equal("BaGet administration API", document.Json["info"]!["title"]!.GetValue<string>());

        var paths = document.Json["paths"]!.AsObject();
        var endpoints = ApiCatalog.Endpoints(_api.App.Services.GetRequiredService<IActionDescriptorCollectionProvider>());
        Assert.NotEmpty(endpoints);
        foreach (var endpoint in endpoints)
        {
            Assert.True(paths[endpoint.Path]?[endpoint.Method.ToLowerInvariant()] != null, $"{endpoint.Method} {endpoint.Path} is not in the v1 document.");
        }

        Assert.All(paths.Select(p => p.Key), path => Assert.StartsWith("/api/v1/", path));

        var alias = await _api.GetAsync("/api/v1/openapi.json");
        Assert.Equal(HttpStatusCode.OK, alias.Status);
        Assert.Equal(document.Json.ToJsonString(), alias.Json.ToJsonString());
    }

    [Fact]
    public async Task TheProtocolsDocumentListsEveryProtocolRouteInItsPlainAndRepositoryVariants()
    {
        var document = await _api.GetAsync("/openapi/protocols.json");
        Assert.Equal(HttpStatusCode.OK, document.Status);
        var paths = document.Json["paths"]!.AsObject();

        var routes = _api.App.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .Where(e => e.Metadata.GetMetadata<RepositoryRouteMetadata>() != null && !string.IsNullOrEmpty(e.RoutePattern.RawText))
            .ToList();
        Assert.True(routes.Count > 50, "the protocol routes were not found");

        foreach (var route in routes)
        {
            var path = "/" + ProtocolsDocumentTransformer.CleanPath(route.RoutePattern.RawText!);
            var methods = route.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? new[] { "GET" };
            foreach (var method in methods)
            {
                Assert.True(paths[path]?[method.ToLowerInvariant()] != null, $"{method} {path} is not in the protocols document.");
            }
        }

        // The plain route and its r/{repo}/ twin, for each protocol.
        foreach (var path in new[] { "/v3/index.json", "/r/{repo}/v3/index.json", "/v3/search", "/r/{repo}/v3/search" })
        {
            Assert.NotNull(paths[path]?["get"]);
        }

        Assert.Contains(paths.Select(p => p.Key), p => p.Contains("npm", StringComparison.Ordinal));
        Assert.Contains(paths.Select(p => p.Key), p => p.Contains("maven", StringComparison.Ordinal));
        Assert.Contains(paths.Select(p => p.Key), p => p.StartsWith("/v2/", StringComparison.Ordinal));

        var parameter = paths["/r/{repo}/v3/index.json"]!["get"]!["parameters"]!.AsArray().Single()!;
        Assert.Equal("repo", parameter["name"]!.GetValue<string>());
        Assert.Equal("path", parameter["in"]!.GetValue<string>());
    }

    [Fact]
    public async Task TheUiDocumentExistsAndHoldsOnlyUiApiPaths()
    {
        var document = await _api.GetAsync("/openapi/ui.json");
        Assert.Equal(HttpStatusCode.OK, document.Status);
        Assert.StartsWith("3.0", document.Json["openapi"]!.GetValue<string>());

        // Empty until a controller under /api/ui exists; whatever is there lives under the prefix.
        var paths = document.Json["paths"];
        if (paths != null) Assert.All(paths.AsObject().Select(p => p.Key), path => Assert.StartsWith("/api/ui", path));
    }

    [Fact]
    public async Task AnUnknownDocumentIsNotFoundEvenForAnAdministrator()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await _api.GetAsync("/openapi/nope.json")).Status);
    }

    [Fact]
    public async Task NoDocumentCarriesASecret()
    {
        foreach (var name in ApiDocuments.All)
        {
            var body = (await _api.GetAsync(ApiDocuments.JsonPath(name))).Body;
            Assert.DoesNotContain(AdminApiHarness.GlobalKey, body);
            Assert.DoesNotContain(_api.RootKey, body);
        }
    }

    [Fact]
    public async Task AdministratorKeysAndTheGlobalKeyReadTheDocuments()
    {
        foreach (var name in ApiDocuments.All)
        {
            Assert.Equal(HttpStatusCode.OK, (await _api.GetAsync(ApiDocuments.JsonPath(name))).Status);
            Assert.Equal(HttpStatusCode.OK, (await _api.GetAsync(ApiDocuments.JsonPath(name), AdminApiHarness.GlobalKey)).Status);
        }

        // The key as the Basic password, like the rest of the server.
        Assert.Equal(HttpStatusCode.OK, (await _api.SendAsync(HttpMethod.Get, "/openapi/v1.json", bearer: false)).Status);
    }

    [Fact]
    public async Task AnAdministratorsCookieSessionReadsTheDocumentsAndScalar()
    {
        using var root = new Browser(_api.App);
        await SignInAsync(root, "root");

        foreach (var name in ApiDocuments.All)
        {
            using var answer = await root.GetAsync(ApiDocuments.JsonPath(name));
            Assert.Equal(HttpStatusCode.OK, answer.StatusCode);
            JsonNode.Parse(await answer.Content.ReadAsStringAsync(AdminApiHarness.Token));
        }

        using var alias = await root.GetAsync("/api/v1/openapi.json");
        Assert.Equal(HttpStatusCode.OK, alias.StatusCode);

        using var scalar = await root.GetAsync("/scalar/v1");
        Assert.Equal(HttpStatusCode.OK, scalar.StatusCode);
        var html = await scalar.Content.ReadAsStringAsync(AdminApiHarness.Token);
        Assert.Contains("BaGet API", html);

        // The viewer's own scripts sit behind the same gate.
        using var script = await root.GetAsync("/scalar/scalar.aspnetcore.js");
        Assert.Equal(HttpStatusCode.OK, script.StatusCode);
        using var anonymousScript = await _api.Client.GetAsync("/scalar/scalar.aspnetcore.js", AdminApiHarness.Token);
        Assert.Equal(HttpStatusCode.NotFound, anonymousScript.StatusCode);
    }

    [Fact]
    public async Task EverybodyElseGets404()
    {
        var addresses = ApiDocuments.All.Select(ApiDocuments.JsonPath).Concat(new[] { "/api/v1/openapi.json", "/scalar", "/scalar/v1", "/scalar/protocols" }).ToList();

        // Anonymous, a bad key, a user's cookie, a user's key, an administrator's key without the admin scope or limited to repositories.
        using var alice = new Browser(_api.App);
        await SignInAsync(alice, "alice");
        var packageKey = await _api.KeyAsync("root", ApiKeyScopes.All);
        var limitedKey = await _api.KeyAsync("root", repository: Repository.DefaultName);
        var aliceKey = await _api.KeyAsync("alice", ApiKeyScopes.All);

        foreach (var address in addresses)
        {
            Assert.Equal(HttpStatusCode.NotFound, (await _api.GetAsync(address, key: "")).Status);
            Assert.Equal(HttpStatusCode.NotFound, (await _api.GetAsync(address, "not-a-key")).Status);
            Assert.Equal(HttpStatusCode.NotFound, (await _api.GetAsync(address, packageKey)).Status);
            Assert.Equal(HttpStatusCode.NotFound, (await _api.GetAsync(address, limitedKey)).Status);
            Assert.Equal(HttpStatusCode.NotFound, (await _api.GetAsync(address, aliceKey)).Status);

            using var cookie = await alice.GetAsync(address);
            Assert.Equal(HttpStatusCode.NotFound, cookie.StatusCode);
        }
    }

    [Fact]
    public async Task AnOpenServerWithNoGlobalKeyShowsTheDocumentsToNobodyAnonymous()
    {
        using var open = new BaGetApplication(_output, configOverrides: new Dictionary<string, string> { { "Accounts:Enabled", "false" } });
        using var client = open.CreateClient();

        foreach (var name in ApiDocuments.All)
        {
            using var answer = await client.GetAsync(ApiDocuments.JsonPath(name), AdminApiHarness.Token);
            Assert.Equal(HttpStatusCode.NotFound, answer.StatusCode);
        }

        using var scalar = await client.GetAsync("/scalar", AdminApiHarness.Token);
        Assert.Equal(HttpStatusCode.NotFound, scalar.StatusCode);
    }
}
