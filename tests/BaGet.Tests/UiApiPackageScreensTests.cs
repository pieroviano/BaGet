using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity;
using Microsoft.Extensions.DependencyInjection;
using NuGet.Versioning;
using Xunit;
using static BaGet.Tests.AdminTestSupport;

namespace BaGet.Tests;

/// <summary>
/// The package-management screens and their JSON twins (<c>/api/ui/...</c>): Packages/Manage, NpmManage, MavenManage, DockerManage,
/// ContactOwners, ValidationStatus and Scan. For each family: the page and the twin say the same (model fields, corresponding statuses:
/// sign-in redirect to 401, 403 to 403, 404 to 404), a write through either gives the same state, a cookie write needs the antiforgery
/// header, an API key never manages through these screens, and no response echoes a typed password.
/// </summary>
public class UiApiPackageScreensTests : IDisposable
{
    private readonly BaGetApplication _app;
    private readonly Browser _anonymous;
    private readonly Browser _alice;
    private readonly Browser _bob;
    private readonly Browser _root;
    private string _aliceKey = string.Empty;
    private string _aliceAllKey = string.Empty;

    public UiApiPackageScreensTests(ITestOutputHelper output)
    {
        _app = new BaGetApplication(output, configOverrides: new Dictionary<string, string> { { "Accounts:Enabled", "true" } });
        _anonymous = new Browser(_app);
        _alice = new Browser(_app);
        _bob = new Browser(_app);
        _root = new Browser(_app);
        SetUpAsync().GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        _anonymous.Dispose();
        _alice.Dispose();
        _bob.Dispose();
        _root.Dispose();
        _app.Dispose();
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task SetUpAsync()
    {
        await CreateUserAsync(_app, "alice");
        await CreateUserAsync(_app, "bob");
        await CreateUserAsync(_app, "root", administrator: true);
        await SignInAsync(_alice, "alice");
        await SignInAsync(_bob, "bob");
        await SignInAsync(_root, "root");

        using var scope = _app.Services.CreateScope();
        var keys = scope.ServiceProvider.GetRequiredService<ApiKeyService>();
        var aliceId = (await FindAsync(_app, "alice"))!.Id;
        _aliceKey = await keys.CreateAsync(aliceId, "push", new[] { ApiKeyScopes.Push }, "*", null, Token);
        _aliceAllKey = await keys.CreateAsync(aliceId, "all", ApiKeyScopes.All, "*", null, Token);
    }

    private sealed record Answer(HttpStatusCode Status, JsonNode? Json, string Body);

    private async Task<Answer> JsonAsync(Browser browser, HttpMethod method, string path, object? body = null, bool antiforgery = true, string? key = null)
    {
        using var request = new HttpRequestMessage(method, path);
        if (key != null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        if (method != HttpMethod.Get)
        {
            if (antiforgery)
            {
                using var token = await browser.GetAsync("/api/ui/antiforgery");
                request.Headers.Add("RequestVerificationToken", JsonNode.Parse(await token.Content.ReadAsStringAsync(Token))!["requestToken"]!.GetValue<string>());
            }

            request.Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(body ?? new { }), Encoding.UTF8, "application/json");
        }

        using var response = await browser.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync(Token);

        return new Answer(response.StatusCode, text.Length > 0 && text[0] == '{' ? JsonNode.Parse(text) : null, text);
    }

    private static async Task<HttpStatusCode> StatusAsync(Browser browser, string path)
    {
        using var response = await browser.GetAsync(path);

        return response.StatusCode;
    }

    private async Task<Package?> PackageAsync(string id, string version)
    {
        using var scope = _app.Services.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<IPackageDatabase>().FindOrNullAsync(id, NuGetVersion.Parse(version), includeUnlisted: true, Token);
    }

    // ---- Packages/Manage ----

    [Fact]
    public async Task TheManageScreenAndItsTwinShowTheSame()
    {
        await PushAsync(_app, "alice", "Alice.Package");

        var page = await _alice.GetPageAsync("packages/Alice.Package/manage");
        var json = await JsonAsync(_alice, HttpMethod.Get, "/api/ui/packages/Alice.Package/manage");

        Assert.Equal(HttpStatusCode.OK, json.Status);
        Assert.Equal("Ok", json.Json!["status"]!.GetValue<string>());
        var model = json.Json["model"]!;
        Assert.Equal("Alice.Package", model["id"]!.GetValue<string>());
        Assert.Equal("alice", model["owners"]![0]!["ownerName"]!.GetValue<string>());
        Assert.Equal("1.0.0", model["versions"]![0]!["normalizedVersion"]!.GetValue<string>());
        Assert.True(model["versions"]![0]!["listed"]!.GetValue<bool>());
        Assert.False(model["isAdministrator"]!.GetValue<bool>());
        Assert.Equal("Unlist", model["deletionBehavior"]!.GetValue<string>());
        Assert.Contains("Manage Alice.Package", page);
        Assert.Contains(">1.0.0</a>", page);
        Assert.Contains(">alice</a>", page);
    }

    [Fact]
    public async Task TheManageStatusesCorrespondOnThePageAndOnTheTwin()
    {
        await PushAsync(_app, "alice", "Alice.Package");

        // anonymous: sign-in redirect <-> 401
        Assert.Equal(HttpStatusCode.Redirect, await StatusAsync(_anonymous, "packages/Alice.Package/manage"));
        Assert.Equal(HttpStatusCode.Unauthorized, (await JsonAsync(_anonymous, HttpMethod.Get, "/api/ui/packages/Alice.Package/manage")).Status);

        // somebody else's package: 403 <-> 403
        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(_bob, "packages/Alice.Package/manage"));
        var forbidden = await JsonAsync(_bob, HttpMethod.Get, "/api/ui/packages/Alice.Package/manage");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.Status);
        Assert.Equal("Forbidden", forbidden.Json!["status"]!.GetValue<string>());

        // an administrator opens it; an ID nobody knows is 404 <-> 404
        Assert.Equal(HttpStatusCode.OK, await StatusAsync(_root, "packages/Alice.Package/manage"));
        Assert.Equal(HttpStatusCode.OK, (await JsonAsync(_root, HttpMethod.Get, "/api/ui/packages/Alice.Package/manage")).Status);
        Assert.Equal(HttpStatusCode.NotFound, await StatusAsync(_root, "packages/Nobody.Knows/manage"));
        Assert.Equal(HttpStatusCode.NotFound, (await JsonAsync(_root, HttpMethod.Get, "/api/ui/packages/Nobody.Knows/manage")).Status);

        // an unknown repository: 404 <-> 404
        Assert.Equal(HttpStatusCode.NotFound, await StatusAsync(_alice, "r/nope/packages/Alice.Package/manage"));
        Assert.Equal(HttpStatusCode.NotFound, (await JsonAsync(_alice, HttpMethod.Get, "/api/ui/r/nope/packages/Alice.Package/manage")).Status);
    }

    [Fact]
    public async Task AnAdministratorSeesTheirRoleOnTheTwin()
    {
        await PushAsync(_app, "alice", "Alice.Package");

        var json = await JsonAsync(_root, HttpMethod.Get, "/api/ui/packages/Alice.Package/manage");

        Assert.True(json.Json!["model"]!["isAdministrator"]!.GetValue<bool>());
        Assert.True(json.Json["model"]!["actingAsAdministrator"]!.GetValue<bool>());
    }

    [Fact]
    public async Task AnUnlistThroughTheTwinAndARelistThroughThePageChangeTheSameState()
    {
        await PushAsync(_app, "alice", "Alice.Package");

        var unlist = await JsonAsync(_alice, HttpMethod.Post, "/api/ui/packages/Alice.Package/manage/unlist", new { version = "1.0.0" });
        Assert.Equal(HttpStatusCode.OK, unlist.Status);
        Assert.Contains("is unlisted", unlist.Json!["model"]!["message"]!.GetValue<string>());
        Assert.False(unlist.Json["model"]!["versions"]![0]!["listed"]!.GetValue<bool>());
        Assert.False((await PackageAsync("Alice.Package", "1.0.0"))!.Listed);

        using var relist = await _alice.PostAsync(
            "packages/Alice.Package/manage",
            new Dictionary<string, string> { { "version", "1.0.0" } },
            action: "packages/Alice.Package/manage?handler=Relist");
        Assert.Equal(HttpStatusCode.OK, relist.StatusCode);
        Assert.Contains("is relisted", await relist.Content.ReadAsStringAsync(Token));
        Assert.True((await PackageAsync("Alice.Package", "1.0.0"))!.Listed);
    }

    [Fact]
    public async Task APostThroughThePageAndThroughTheTwinAddAndRemoveTheSameOwner()
    {
        await PushAsync(_app, "alice", "Alice.Package");

        var added = await JsonAsync(_alice, HttpMethod.Post, "/api/ui/packages/Alice.Package/manage/add-owner", new { ownerName = "bob" });
        Assert.Equal("bob is now an owner.", added.Json!["model"]!["message"]!.GetValue<string>());
        Assert.Equal(2, added.Json["model"]!["owners"]!.AsArray().Count);

        using var removed = await _alice.PostAsync(
            "packages/Alice.Package/manage",
            new Dictionary<string, string> { { "ownerName", "bob" } },
            action: "packages/Alice.Package/manage?handler=RemoveOwner");
        Assert.Contains("bob is no longer an owner.", await removed.Content.ReadAsStringAsync(Token));

        var after = await JsonAsync(_alice, HttpMethod.Get, "/api/ui/packages/Alice.Package/manage");
        Assert.Single(after.Json!["model"]!["owners"]!.AsArray());
    }

    [Fact]
    public async Task ACookieWriteOnTheManageTwinNeedsTheAntiforgeryHeader()
    {
        await PushAsync(_app, "alice", "Alice.Package");

        var refused = await JsonAsync(_alice, HttpMethod.Post, "/api/ui/packages/Alice.Package/manage/unlist", new { version = "1.0.0" }, antiforgery: false);

        Assert.Equal(HttpStatusCode.BadRequest, refused.Status);
        Assert.True((await PackageAsync("Alice.Package", "1.0.0"))!.Listed);
    }

    [Fact]
    public async Task AnApiKeyNeverManagesThroughTheTwin()
    {
        await PushAsync(_app, "alice", "Alice.Package");

        // Not even with every scope: managing owners, listing and deletion is a signed-in session's screen.
        var read = await JsonAsync(_anonymous, HttpMethod.Get, "/api/ui/packages/Alice.Package/manage", key: _aliceAllKey);
        var write = await JsonAsync(_anonymous, HttpMethod.Post, "/api/ui/packages/Alice.Package/manage/unlist", new { version = "1.0.0" }, antiforgery: false, key: _aliceAllKey);
        var pushOnly = await JsonAsync(_anonymous, HttpMethod.Post, "/api/ui/packages/Alice.Package/manage/unlist", new { version = "1.0.0" }, antiforgery: false, key: _aliceKey);

        Assert.Equal(HttpStatusCode.Forbidden, read.Status);
        Assert.Equal(HttpStatusCode.Forbidden, write.Status);
        Assert.Equal(HttpStatusCode.Forbidden, pushOnly.Status);
        Assert.True((await PackageAsync("Alice.Package", "1.0.0"))!.Listed);
    }

    [Fact]
    public async Task ATypedPasswordIsNeverEchoedAndAWrongOneDeletesNothing()
    {
        await PushAsync(_app, "alice", "Alice.Package");
        const string typed = "NotThePassword-12345";

        var answer = await JsonAsync(_root, HttpMethod.Post, "/api/ui/packages/Alice.Package/manage/hard-delete", new { version = "1.0.0", password = typed });

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Contains("confirmation is incorrect", answer.Json!["model"]!["message"]!.GetValue<string>());
        Assert.DoesNotContain(typed, answer.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("protectedPassword", answer.Body, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(await PackageAsync("Alice.Package", "1.0.0"));

        // The right one deletes, and a non-administrator may not even try.
        var forbidden = await JsonAsync(_alice, HttpMethod.Post, "/api/ui/packages/Alice.Package/manage/hard-delete", new { version = "1.0.0", password = Password });
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.Status);
        var deleted = await JsonAsync(_root, HttpMethod.Post, "/api/ui/packages/Alice.Package/manage/hard-delete", new { version = "1.0.0", password = Password });
        Assert.Contains("is deleted permanently", deleted.Json!["model"]!["message"]!.GetValue<string>());
        Assert.Null(await PackageAsync("Alice.Package", "1.0.0"));
    }

    [Fact]
    public async Task TheClosedMaintenanceGateIs503OnAPostOfThePageAndOfTheTwin()
    {
        await PushAsync(_app, "alice", "Alice.Package");
        var gate = _app.Services.GetRequiredService<MaintenanceGate>();

        gate.Close("Copying the database");
        try
        {
            var twin = await JsonAsync(_alice, HttpMethod.Post, "/api/ui/packages/Alice.Package/manage/unlist", new { version = "1.0.0" });
            Assert.Equal(HttpStatusCode.ServiceUnavailable, twin.Status);

            using var page = await _alice.PostAsync(
                "packages/Alice.Package/manage",
                new Dictionary<string, string> { { "version", "1.0.0" } },
                action: "packages/Alice.Package/manage?handler=Unlist");
            Assert.Equal(HttpStatusCode.ServiceUnavailable, page.StatusCode);
        }
        finally
        {
            gate.Open();
        }

        Assert.True((await PackageAsync("Alice.Package", "1.0.0"))!.Listed);
    }

    // ---- npm ----

    [Fact]
    public async Task TheNpmManageScreenAndItsTwinAgreeAndWriteTheSameState()
    {
        await _app.CreateNpmRepositoryAsync("web", cancellationToken: Token);
        await PublishNpmAsync("left-pad", "1.0.0");

        var page = await _alice.GetPageAsync("r/web/npm-manage/left-pad");
        var json = await JsonAsync(_alice, HttpMethod.Get, "/api/ui/r/web/npm-manage/left-pad");
        Assert.Equal(HttpStatusCode.OK, json.Status);
        Assert.Equal("left-pad", json.Json!["model"]!["id"]!.GetValue<string>());
        Assert.Equal("left-pad", json.Json["model"]!["package"]!["name"]!.GetValue<string>());
        Assert.Contains("left-pad", page);

        // Statuses: anonymous 302 <-> 401, another user 403 <-> 403, unknown package 404 <-> 404, bad name 404 <-> 404.
        Assert.Equal(HttpStatusCode.Redirect, await StatusAsync(_anonymous, "r/web/npm-manage/left-pad"));
        Assert.Equal(HttpStatusCode.Unauthorized, (await JsonAsync(_anonymous, HttpMethod.Get, "/api/ui/r/web/npm-manage/left-pad")).Status);
        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(_bob, "r/web/npm-manage/left-pad"));
        Assert.Equal(HttpStatusCode.Forbidden, (await JsonAsync(_bob, HttpMethod.Get, "/api/ui/r/web/npm-manage/left-pad")).Status);
        Assert.Equal(HttpStatusCode.NotFound, await StatusAsync(_alice, "r/web/npm-manage/nothing-here"));
        Assert.Equal(HttpStatusCode.NotFound, (await JsonAsync(_alice, HttpMethod.Get, "/api/ui/r/web/npm-manage/nothing-here")).Status);
        Assert.Equal(HttpStatusCode.NotFound, await StatusAsync(_alice, "r/web/npm-manage/Bad%20Name"));
        Assert.Equal(HttpStatusCode.NotFound, (await JsonAsync(_alice, HttpMethod.Get, "/api/ui/r/web/npm-manage/Bad%20Name")).Status);
        // The default repository is not an npm repository.
        Assert.Equal(HttpStatusCode.NotFound, await StatusAsync(_alice, "npm-manage/left-pad"));
        Assert.Equal(HttpStatusCode.NotFound, (await JsonAsync(_alice, HttpMethod.Get, "/api/ui/npm-manage/left-pad")).Status);

        // A tag set through the twin shows on the page, one removed through the page is gone for the twin.
        var set = await JsonAsync(_alice, HttpMethod.Post, "/api/ui/r/web/npm-manage/set-tag/left-pad", new { tag = "stable", version = "1.0.0" });
        Assert.Equal("stable now points at 1.0.0.", set.Json!["model"]!["message"]!.GetValue<string>());
        Assert.Equal("1.0.0", set.Json["model"]!["package"]!["distTags"]!["stable"]!.GetValue<string>());

        using var removed = await _alice.PostAsync(
            "r/web/npm-manage/left-pad",
            new Dictionary<string, string> { { "tag", "stable" } },
            action: "r/web/npm-manage/left-pad?handler=RemoveTag");
        Assert.Contains("The tag stable is removed.", await removed.Content.ReadAsStringAsync(Token));
        var after = await JsonAsync(_alice, HttpMethod.Get, "/api/ui/r/web/npm-manage/left-pad");
        Assert.Null(after.Json!["model"]!["package"]!["distTags"]!["stable"]);
    }

    [Fact]
    public async Task UnpublishingEveryVersionThroughTheNpmTwinLeadsToThePackagesScreen()
    {
        await _app.CreateNpmRepositoryAsync("web", cancellationToken: Token);
        await PublishNpmAsync("left-pad", "1.0.0");

        var wrong = await JsonAsync(_alice, HttpMethod.Post, "/api/ui/r/web/npm-manage/unpublish-all/left-pad", new { password = "wrong-password-1" });
        Assert.Equal("Ok", wrong.Json!["status"]!.GetValue<string>());
        Assert.Contains("confirmation is incorrect", wrong.Json["model"]!["message"]!.GetValue<string>());
        Assert.DoesNotContain("wrong-password-1", wrong.Body, StringComparison.Ordinal);

        var done = await JsonAsync(_alice, HttpMethod.Post, "/api/ui/r/web/npm-manage/unpublish-all/left-pad", new { password = Password });
        Assert.Equal(HttpStatusCode.OK, done.Status);
        Assert.Equal("Next", done.Json!["status"]!.GetValue<string>());
        Assert.Equal("/NpmPackages", done.Json["next"]!["screen"]!.GetValue<string>());
        Assert.Equal(HttpStatusCode.NotFound, (await JsonAsync(_alice, HttpMethod.Get, "/api/ui/r/web/npm-manage/left-pad")).Status);
    }

    [Fact]
    public async Task AnApiKeyNeverManagesAnNpmPackageThroughTheTwin()
    {
        await _app.CreateNpmRepositoryAsync("web", cancellationToken: Token);
        await PublishNpmAsync("left-pad", "1.0.0");

        Assert.Equal(HttpStatusCode.Forbidden, (await JsonAsync(_anonymous, HttpMethod.Get, "/api/ui/r/web/npm-manage/left-pad", key: _aliceAllKey)).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await JsonAsync(_anonymous, HttpMethod.Post, "/api/ui/r/web/npm-manage/set-tag/left-pad", new { tag = "x", version = "1.0.0" }, antiforgery: false, key: _aliceAllKey)).Status);
    }

    private async Task PublishNpmAsync(string name, string version)
    {
        using var client = _app.CreateClient();
        using var request = NpmTestPackages.Json(
            HttpMethod.Put,
            NpmTestPackages.Path("/r/web/npm/", name),
            NpmTestPackages.PublishBody(NpmTestPackages.Tarball(name, version, readme: string.Empty), "latest", null),
            _aliceAllKey);
        using var response = await client.SendAsync(request, Token);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    // ---- Maven and Docker: the gates ----

    [Fact]
    public async Task TheMavenManageGatesCorrespondOnThePageAndOnTheTwin()
    {
        await _app.CreateMavenRepositoryAsync("mvn", cancellationToken: Token);

        // anonymous 302 <-> 401, a user who owns nothing 403 <-> 403, an administrator on an artifact nobody deployed 404 <-> 404,
        // invalid coordinates 404 <-> 404, a key 403.
        Assert.Equal(HttpStatusCode.Redirect, await StatusAsync(_anonymous, "r/mvn/maven-manage/org.acme/lib"));
        Assert.Equal(HttpStatusCode.Unauthorized, (await JsonAsync(_anonymous, HttpMethod.Get, "/api/ui/r/mvn/maven-manage/org.acme/lib")).Status);
        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(_bob, "r/mvn/maven-manage/org.acme/lib"));
        Assert.Equal(HttpStatusCode.Forbidden, (await JsonAsync(_bob, HttpMethod.Get, "/api/ui/r/mvn/maven-manage/org.acme/lib")).Status);
        Assert.Equal(HttpStatusCode.NotFound, await StatusAsync(_root, "r/mvn/maven-manage/org.acme/lib"));
        Assert.Equal(HttpStatusCode.NotFound, (await JsonAsync(_root, HttpMethod.Get, "/api/ui/r/mvn/maven-manage/org.acme/lib")).Status);
        Assert.Equal(HttpStatusCode.NotFound, await StatusAsync(_root, "r/mvn/maven-manage/org.acme/bad%20id"));
        Assert.Equal(HttpStatusCode.NotFound, (await JsonAsync(_root, HttpMethod.Get, "/api/ui/r/mvn/maven-manage/org.acme/bad%20id")).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await JsonAsync(_anonymous, HttpMethod.Get, "/api/ui/r/mvn/maven-manage/org.acme/lib", key: _aliceAllKey)).Status);
        // A NuGet repository has no such screen.
        Assert.Equal(HttpStatusCode.NotFound, await StatusAsync(_root, "maven-manage/org.acme/lib"));
        Assert.Equal(HttpStatusCode.NotFound, (await JsonAsync(_root, HttpMethod.Get, "/api/ui/maven-manage/org.acme/lib")).Status);
    }

    [Fact]
    public async Task TheDockerManageGatesCorrespondOnThePageAndOnTheTwin()
    {
        await _app.CreateDockerRepositoryAsync("dock", cancellationToken: Token);

        // anonymous 302 <-> 401, a user without Delete on the image 403 <-> 403 (the registry's own rule), an administrator on an image
        // nobody pushed 404 <-> 404, a key 403.
        Assert.Equal(HttpStatusCode.Redirect, await StatusAsync(_anonymous, "docker-manage/dock/app"));
        Assert.Equal(HttpStatusCode.Unauthorized, (await JsonAsync(_anonymous, HttpMethod.Get, "/api/ui/docker-manage/dock/app")).Status);
        Assert.Equal(HttpStatusCode.NotFound, await StatusAsync(_root, "docker-manage/dock/app"));
        Assert.Equal(HttpStatusCode.NotFound, (await JsonAsync(_root, HttpMethod.Get, "/api/ui/docker-manage/dock/app")).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await JsonAsync(_anonymous, HttpMethod.Get, "/api/ui/docker-manage/dock/app", key: _aliceAllKey)).Status);
        var plainUser = await StatusAsync(_bob, "docker-manage/dock/app");
        Assert.Equal(plainUser, (await JsonAsync(_bob, HttpMethod.Get, "/api/ui/docker-manage/dock/app")).Status);
    }

    // ---- ContactOwners, ValidationStatus, Scan ----

    [Fact]
    public async Task ContactOwnersIs404OnBothWhileNoMailIsConfigured()
    {
        await PushAsync(_app, "alice", "Alice.Package");

        Assert.Equal(HttpStatusCode.NotFound, await StatusAsync(_bob, "packages/Alice.Package/contact-owners"));
        Assert.Equal(HttpStatusCode.NotFound, (await JsonAsync(_bob, HttpMethod.Get, "/api/ui/packages/Alice.Package/contact-owners")).Status);
    }

    [Fact]
    public async Task TheValidationStatusGatesCorrespondOnThePageAndOnTheTwin()
    {
        await PushAsync(_app, "alice", "Alice.Package");

        Assert.Equal(HttpStatusCode.Redirect, await StatusAsync(_anonymous, "packages/Alice.Package/validation"));
        Assert.Equal(HttpStatusCode.Unauthorized, (await JsonAsync(_anonymous, HttpMethod.Get, "/api/ui/packages/Alice.Package/validation")).Status);
        Assert.Equal(await StatusAsync(_alice, "packages/Alice.Package/validation"), (await JsonAsync(_alice, HttpMethod.Get, "/api/ui/packages/Alice.Package/validation")).Status);
        Assert.Equal(await StatusAsync(_bob, "packages/Alice.Package/validation"), (await JsonAsync(_bob, HttpMethod.Get, "/api/ui/packages/Alice.Package/validation")).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await JsonAsync(_anonymous, HttpMethod.Get, "/api/ui/packages/Alice.Package/validation", key: _aliceAllKey)).Status);
    }

    [Fact]
    public async Task TheScanScreenAndItsTwinAgreeAndARefusedScanKeepsItsStatus()
    {
        await PushAsync(_app, "alice", "Alice.Package");
        const string address = "scan-artifact?name=Alice.Package&version=1.0.0";

        var page = await _alice.GetPageAsync(address);
        var json = await JsonAsync(_alice, HttpMethod.Get, "/api/ui/" + address);
        Assert.Equal(HttpStatusCode.OK, json.Status);
        Assert.Equal("Alice.Package", json.Json!["model"]!["name"]!.GetValue<string>());
        Assert.Equal("1.0.0", json.Json["model"]!["version"]!.GetValue<string>());
        Assert.Contains("Alice.Package", page);

        Assert.Equal(HttpStatusCode.Redirect, await StatusAsync(_anonymous, address));
        Assert.Equal(HttpStatusCode.Unauthorized, (await JsonAsync(_anonymous, HttpMethod.Get, "/api/ui/" + address)).Status);
        Assert.Equal(HttpStatusCode.Forbidden, await StatusAsync(_bob, address));
        Assert.Equal(HttpStatusCode.Forbidden, (await JsonAsync(_bob, HttpMethod.Get, "/api/ui/" + address)).Status);

        // Scanning is off by default: the screen is shown again with the reason, under 409, in both ways.
        using var posted = await _alice.PostAsync(address, new Dictionary<string, string>(), action: address);
        var twin = await JsonAsync(_alice, HttpMethod.Post, "/api/ui/scan-artifact", new { name = "Alice.Package", version = "1.0.0" });
        Assert.Equal(HttpStatusCode.Conflict, posted.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, twin.Status);
        Assert.True(twin.Json!["model"]!["failed"]!.GetValue<bool>());
    }
}

/// <summary>The package screen on a server that refuses writes (<c>IsReadOnlyMode</c>): the page and the twin both show the message and change nothing.</summary>
public class UiApiPackageScreensReadOnlyTests : IDisposable
{
    private readonly BaGetApplication _app;
    private readonly Browser _alice;

    public UiApiPackageScreensReadOnlyTests(ITestOutputHelper output)
    {
        _app = new BaGetApplication(output, configOverrides: new Dictionary<string, string> { { "Accounts:Enabled", "true" }, { "IsReadOnlyMode", "true" } });
        _alice = new Browser(_app);
    }

    public void Dispose()
    {
        _alice.Dispose();
        _app.Dispose();
    }

    [Fact]
    public async Task AWriteOnAReadOnlyServerShowsTheMessageOnThePageAndOnTheTwinAndChangesNothing()
    {
        var token = TestContext.Current.CancellationToken;
        await CreateUserAsync(_app, "alice");
        await SignInAsync(_alice, "alice");
        using (var package = TestPackages.Build("Alice.Package", "1.0.0"))
        {
            await _app.AddPackageAsync(package, token);
        }

        using (var scope = _app.Services.CreateScope())
        {
            Assert.Equal(OwnerChangeResult.Success, await scope.ServiceProvider.GetRequiredService<PackageOwnershipService>().AddOwnerAsync("Alice.Package", "alice", token));
        }

        using var page = await _alice.PostAsync(
            "packages/Alice.Package/manage",
            new Dictionary<string, string> { { "version", "1.0.0" } },
            action: "packages/Alice.Package/manage?handler=Unlist");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains(BaGet.Identity.Application.PackageManageUseCase.ReadOnlyMessage, await page.Content.ReadAsStringAsync(token));

        using var antiforgery = await _alice.GetAsync("/api/ui/antiforgery");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/ui/packages/Alice.Package/manage/unlist")
        {
            Content = new StringContent("{\"version\":\"1.0.0\"}", Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("RequestVerificationToken", JsonNode.Parse(await antiforgery.Content.ReadAsStringAsync(token))!["requestToken"]!.GetValue<string>());
        using var twin = await _alice.SendAsync(request);
        var json = JsonNode.Parse(await twin.Content.ReadAsStringAsync(token))!;
        Assert.Equal(HttpStatusCode.OK, twin.StatusCode);
        Assert.Equal(BaGet.Identity.Application.PackageManageUseCase.ReadOnlyMessage, json["model"]!["message"]!.GetValue<string>());

        using var scope2 = _app.Services.CreateScope();
        Assert.True((await scope2.ServiceProvider.GetRequiredService<IPackageDatabase>().FindOrNullAsync("Alice.Package", NuGetVersion.Parse("1.0.0"), includeUnlisted: true, token))!.Listed);
    }
}
