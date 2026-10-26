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
using BaGet.Identity.Application;
using BaGet.Identity.Mvc;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static BaGet.Tests.AdminTestSupport;

namespace BaGet.Tests;

/// <summary>
/// Authentication, antiforgery and the gates of <c>/api/ui</c> (plan, "Verification" / <c>UiApiAccessTests</c>), driven through the real pipeline with test
/// twins that derive from each base of the family (account, administrator, access). A track that adds a family of screens extends this class with its own
/// cases (anonymous / user / admin / key / limited key / no antiforgery / read-only).
/// </summary>
public class UiApiAccessTests : IDisposable
{
    private const string GlobalKey = "global-key";

    private readonly BaGetApplication _app;
    private readonly Browser _anonymous;
    private readonly Browser _alice;
    private readonly Browser _root;
    private string _rootKey = string.Empty;
    private string _aliceKey = string.Empty;
    private string _rootPushOnlyKey = string.Empty;

    public UiApiAccessTests(ITestOutputHelper output)
    {
        _app = new BaGetApplication(
            output,
            configOverrides: new Dictionary<string, string> { { "Accounts:Enabled", "true" }, { "ApiKey", GlobalKey } },
            configureServices: services => services.AddControllers().AddApplicationPart(typeof(UiApiAccessTests).Assembly));
        _anonymous = new Browser(_app);
        _alice = new Browser(_app);
        _root = new Browser(_app);
        SetUpAsync().GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        _anonymous.Dispose();
        _alice.Dispose();
        _root.Dispose();
        _app.Dispose();
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task SetUpAsync()
    {
        await CreateUserAsync(_app, "alice");
        await CreateUserAsync(_app, "root", administrator: true);
        await SignInAsync(_alice, "alice");
        await SignInAsync(_root, "root");
        await _app.CreateRepositoryAsync("second", Token);

        _rootKey = await KeyAsync("root", new[] { ApiKeyScopes.Admin });
        _rootPushOnlyKey = await KeyAsync("root", new[] { ApiKeyScopes.Push });
        _aliceKey = await KeyAsync("alice", new[] { ApiKeyScopes.Push });
    }

    private async Task<string> KeyAsync(string user, string[] scopes, int[]? repositories = null)
    {
        using var scope = _app.Services.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<ApiKeyService>()
            .CreateAsync((await FindAsync(_app, user))!.Id, "test", scopes, "*", null, repositories, Token);
    }

    private sealed record Answer(HttpStatusCode Status, JsonNode? Json, HttpResponseHeaders Headers, string Body);

    private async Task<Answer> SendAsync(Browser browser, HttpMethod method, string path, string? key = null, string? antiforgery = null, bool bearer = true)
    {
        using var request = new HttpRequestMessage(method, path);
        if (key != null)
        {
            request.Headers.Authorization = bearer
                ? new AuthenticationHeaderValue("Bearer", key)
                : new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes("root:" + key)));
        }

        if (antiforgery != null) request.Headers.Add("RequestVerificationToken", antiforgery);
        if (method != HttpMethod.Get) request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

        using var response = await browser.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync(Token);

        return new Answer(response.StatusCode, body.Length > 0 && body[0] == '{' ? JsonNode.Parse(body) : null, response.Headers, body);
    }

    private async Task<string> TokenAsync(Browser browser)
    {
        var answer = await SendAsync(browser, HttpMethod.Get, "/api/ui/antiforgery");
        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal("RequestVerificationToken", answer.Json!["headerName"]!.GetValue<string>());

        return answer.Json["requestToken"]!.GetValue<string>();
    }

    // ---- antiforgery ----

    [Fact]
    public async Task TheAntiforgeryEndpointIsAnonymousAndNeverCached()
    {
        var answer = await SendAsync(_anonymous, HttpMethod.Get, "/api/ui/antiforgery");

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.False(string.IsNullOrEmpty(answer.Json!["requestToken"]!.GetValue<string>()));
        Assert.True(answer.Headers.CacheControl?.NoStore);
    }

    [Fact]
    public async Task ACookieWriteWithoutTheAntiforgeryHeaderIs400AndWithItRuns()
    {
        var refused = await SendAsync(_alice, HttpMethod.Post, "/api/ui/test-probe/account");
        Assert.Equal(HttpStatusCode.BadRequest, refused.Status);
        Assert.Equal("Invalid", refused.Json!["status"]!.GetValue<string>());

        var accepted = await SendAsync(_alice, HttpMethod.Post, "/api/ui/test-probe/account", antiforgery: await TokenAsync(_alice));
        Assert.Equal(HttpStatusCode.OK, accepted.Status);
        Assert.Equal("account", accepted.Json!["model"]!.GetValue<string>());
    }

    [Fact]
    public async Task AnAnonymousWriteWithoutTheTokenIs400BeforeTheGateAndWithItIs401()
    {
        var refused = await SendAsync(_anonymous, HttpMethod.Post, "/api/ui/test-probe/account");
        Assert.Equal(HttpStatusCode.BadRequest, refused.Status);

        var signIn = await SendAsync(_anonymous, HttpMethod.Post, "/api/ui/test-probe/account", antiforgery: await TokenAsync(_anonymous));
        Assert.Equal(HttpStatusCode.Unauthorized, signIn.Status);
        Assert.Equal("SignIn", signIn.Json!["status"]!.GetValue<string>());
        Assert.Contains("Bearer", string.Join(",", signIn.Headers.WwwAuthenticate), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AKeyWriteNeedsNoAntiforgeryHeader()
    {
        var answer = await SendAsync(_anonymous, HttpMethod.Post, "/api/ui/test-probe/admin", key: _rootKey);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal("admin", answer.Json!["model"]!.GetValue<string>());
    }

    [Fact]
    public async Task AKeyIsAcceptedAsBasicPasswordToo()
    {
        var answer = await SendAsync(_anonymous, HttpMethod.Post, "/api/ui/test-probe/admin", key: _rootKey, bearer: false);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
    }

    [Fact]
    public async Task CredentialsThatDoNotAuthenticateAre401NotAnonymous()
    {
        var answer = await SendAsync(_anonymous, HttpMethod.Get, "/api/ui/stats", key: "not-a-key");

        Assert.Equal(HttpStatusCode.Unauthorized, answer.Status);
    }

    // ---- the account base: cookie only ----

    [Fact]
    public async Task AccountSelfServiceIsCookieOnlyAKeyIs403()
    {
        var withKey = await SendAsync(_anonymous, HttpMethod.Post, "/api/ui/test-probe/account", key: _aliceKey);

        Assert.Equal(HttpStatusCode.Forbidden, withKey.Status);
        Assert.Equal("Forbidden", withKey.Json!["status"]!.GetValue<string>());
    }

    // ---- the administrator base ----

    [Fact]
    public async Task AnAdministratorScreenIsAnAdminSessionOrAnAdminApiKey()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(_anonymous, HttpMethod.Get, "/api/ui/test-probe/admin")).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(_alice, HttpMethod.Get, "/api/ui/test-probe/admin")).Status);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(_root, HttpMethod.Get, "/api/ui/test-probe/admin")).Status);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(_anonymous, HttpMethod.Get, "/api/ui/test-probe/admin", key: _rootKey)).Status);
    }

    [Fact]
    public async Task AKeyWithoutTheAdminScopeOrOfANonAdministratorIs403OnAdministratorScreens()
    {
        // An administrator's key without admin:api, and a plain user's key.
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(_anonymous, HttpMethod.Get, "/api/ui/test-probe/admin", key: _rootPushOnlyKey)).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(_anonymous, HttpMethod.Get, "/api/ui/test-probe/admin", key: _aliceKey)).Status);
    }

    [Fact]
    public async Task TheGlobalKeyAdministersWhenOneIsConfigured()
    {
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(_anonymous, HttpMethod.Get, "/api/ui/test-probe/admin", key: GlobalKey)).Status);
    }

    [Fact]
    public async Task AKeyLimitedToARepositoryCannotAdminister()
    {
        var defaultKey = (await _app.Services.GetRequiredService<IRepositoryService>().FindAsync("default", Token))!.Key;
        var limited = await KeyAsync("root", new[] { ApiKeyScopes.Admin }, new[] { defaultKey });

        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(_anonymous, HttpMethod.Get, "/api/ui/test-probe/admin", key: limited)).Status);
    }

    // ---- the access base ----

    [Fact]
    public async Task AnAccessScreenIsRefusedWith503WhileTheServerIsReadOnly()
    {
        var gate = _app.Services.GetRequiredService<MaintenanceGate>();
        var reads = await SendAsync(_anonymous, HttpMethod.Get, "/api/ui/test-probe/access", key: _rootKey);
        Assert.Equal(HttpStatusCode.OK, reads.Status);

        gate.Close("Copying the database");
        try
        {
            Assert.Equal(HttpStatusCode.OK, (await SendAsync(_anonymous, HttpMethod.Get, "/api/ui/test-probe/access", key: _rootKey)).Status);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, (await SendAsync(_anonymous, HttpMethod.Post, "/api/ui/test-probe/access", key: _rootKey)).Status);
        }
        finally
        {
            gate.Open();
        }

        Assert.Equal(HttpStatusCode.OK, (await SendAsync(_anonymous, HttpMethod.Post, "/api/ui/test-probe/access", key: _rootKey)).Status);
    }

    [Fact]
    public async Task AnAccessScreenAdmitsWhomAuthorizeAsyncAdmitsAndNobodyElse()
    {
        // The probe admits nobody but server administrators.
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(_alice, HttpMethod.Get, "/api/ui/test-probe/access")).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(_anonymous, HttpMethod.Get, "/api/ui/test-probe/access", key: _aliceKey)).Status);
    }

    // ---- repository screens ----

    [Fact]
    public async Task ARepositoryScreenIsAnonymousWhereTheRepositoryIsOpenAndAnUnknownRepositoryIsAJson404()
    {
        var plain = await SendAsync(_anonymous, HttpMethod.Get, "/api/ui/stats");
        var second = await SendAsync(_anonymous, HttpMethod.Get, "/api/ui/r/second/stats");
        var unknown = await SendAsync(_anonymous, HttpMethod.Get, "/api/ui/r/nope/stats");

        Assert.Equal(HttpStatusCode.OK, plain.Status);
        Assert.Equal(HttpStatusCode.OK, second.Status);
        Assert.Equal(HttpStatusCode.NotFound, unknown.Status);
        Assert.Equal("NotFound", unknown.Json!["status"]!.GetValue<string>());
    }

    [Fact]
    public async Task AKeyLimitedToRepositoriesSeesAnotherRepositoryAs404()
    {
        var defaultKey = (await _app.Services.GetRequiredService<IRepositoryService>().FindAsync("default", Token))!.Key;
        var limited = await KeyAsync("alice", new[] { ApiKeyScopes.Push }, new[] { defaultKey });

        Assert.Equal(HttpStatusCode.OK, (await SendAsync(_anonymous, HttpMethod.Get, "/api/ui/stats", key: limited)).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(_anonymous, HttpMethod.Get, "/api/ui/r/second/stats", key: limited)).Status);
    }

    // ---- browse screens: front page, package, report abuse, REST API page ----

    [Fact]
    public async Task TheFrontPageAndTheListAreAnonymousAndABadQueryIsAPlain400()
    {
        var home = await SendAsync(_anonymous, HttpMethod.Get, "/api/ui/home");
        var list = await SendAsync(_anonymous, HttpMethod.Get, "/api/ui/packages?q=anything");
        var secondHome = await SendAsync(_anonymous, HttpMethod.Get, "/api/ui/r/second/home");

        Assert.Equal(HttpStatusCode.OK, home.Status);
        Assert.True(home.Json!["model"]!["isHome"]!.GetValue<bool>());
        Assert.Equal(HttpStatusCode.OK, list.Status);
        Assert.False(list.Json!["model"]!["isHome"]!.GetValue<bool>());
        Assert.Equal(HttpStatusCode.OK, secondHome.Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await SendAsync(_anonymous, HttpMethod.Get, "/api/ui/packages?p=0")).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await SendAsync(_anonymous, HttpMethod.Get, "/api/ui/packages?sortBy=99")).Status);
    }

    [Fact]
    public async Task APackageScreenIsAnonymousAndOffersTheManageLinkOnlyToSomeoneWhoMayManage()
    {
        using (var package = TestResources.GetResourceStream(TestResources.Package))
        {
            await _app.AddPackageAsync(package, Token);
        }

        var anonymous = await SendAsync(_anonymous, HttpMethod.Get, "/api/ui/packages/TestData/1.2.3");
        var missing = await SendAsync(_anonymous, HttpMethod.Get, "/api/ui/packages/NoSuchPackage");
        var administrator = await SendAsync(_root, HttpMethod.Get, "/api/ui/packages/TestData/1.2.3");

        Assert.Equal(HttpStatusCode.OK, anonymous.Status);
        Assert.True(anonymous.Json!["model"]!["found"]!.GetValue<bool>());
        Assert.Equal("TestData", anonymous.Json["model"]!["package"]!["id"]!.GetValue<string>());
        Assert.Null(anonymous.Json["model"]!["manageUrl"]);
        Assert.False(missing.Json!["model"]!["found"]!.GetValue<bool>());
        Assert.EndsWith("/packages/TestData/manage", administrator.Json!["model"]!["manageUrl"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AReportAbusePostFollowsTheAntiforgeryRuleAndRefusesAnEmptyFormWithTheReasons()
    {
        var withoutHeader = await SendAsync(_alice, HttpMethod.Post, "/api/ui/packages/TestData/1.2.3/report-abuse");
        Assert.Equal(HttpStatusCode.BadRequest, withoutHeader.Status);
        Assert.Null(withoutHeader.Json!["model"]);

        // Past the antiforgery check (cookie with the header, or a key) the empty form is refused with the reasons by field.
        var withHeader = await SendAsync(_alice, HttpMethod.Post, "/api/ui/packages/TestData/1.2.3/report-abuse", antiforgery: await TokenAsync(_alice));
        var withKey = await SendAsync(_anonymous, HttpMethod.Post, "/api/ui/packages/TestData/1.2.3/report-abuse", key: _aliceKey);
        foreach (var answer in new[] { withHeader, withKey })
        {
            Assert.Equal(HttpStatusCode.BadRequest, answer.Status);
            Assert.Equal("Invalid", answer.Json!["status"]!.GetValue<string>());
            Assert.NotNull(answer.Json["errors"]!["Details"]);
            Assert.NotNull(answer.Json["errors"]!["Reason"]);
        }
    }

    [Fact]
    public async Task TheRestApiScreenIsForAdministratorsOnly()
    {
        const string path = "/api/ui/admin/api";

        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(_anonymous, HttpMethod.Get, path)).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(_alice, HttpMethod.Get, path)).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(_anonymous, HttpMethod.Get, path, key: _rootPushOnlyKey)).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(_anonymous, HttpMethod.Get, path, key: _aliceKey)).Status);

        foreach (var answer in new[] { await SendAsync(_root, HttpMethod.Get, path), await SendAsync(_anonymous, HttpMethod.Get, path, key: _rootKey) })
        {
            Assert.Equal(HttpStatusCode.OK, answer.Status);
            var model = answer.Json!["model"]!;
            Assert.EndsWith("/api/v1", model["apiBase"]!.GetValue<string>(), StringComparison.Ordinal);
            Assert.NotEmpty(model["endpoints"]!.AsArray());

            // Flat JSON: no reflection objects, no key.
            Assert.DoesNotContain("descriptor", answer.Body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(_rootKey, answer.Body, StringComparison.Ordinal);
        }
    }

    // ---- no secrets ----

    [Fact]
    public async Task NoResponseEchoesTheCredentialsOfTheRequest()
    {
        foreach (var path in new[] { "/api/ui/stats", "/api/ui/test-probe/admin", "/api/ui/antiforgery" })
        {
            var answer = await SendAsync(_anonymous, HttpMethod.Get, path, key: _rootKey);

            Assert.DoesNotContain(_rootKey, answer.Body, StringComparison.Ordinal);
            Assert.DoesNotContain("protectedPassword", answer.Body, StringComparison.OrdinalIgnoreCase);
        }
    }
}

// The twins below live in the test assembly; UiApiAccessTests registers it as an application part. They only exist to drive each base of the family.

/// <summary>An account twin (cookie only, signed in).</summary>
[Route("api/ui/test-probe/account")]
public sealed class ProbeAccountUiApiController : AccountUiApiControllerBase
{
    protected override bool RequiresSignIn => true;

    [HttpPost, UiApi("/Test/Probe")]
    public ActionResult<ScreenResult<string>> Post() => Respond(ScreenResult<string>.Ok("account"));
}

/// <summary>An administrator twin.</summary>
[Route("api/ui/test-probe/admin")]
public sealed class ProbeAdminUiApiController : AdminUiApiControllerBase
{
    [HttpGet, UiApi("/Test/Probe")]
    public ActionResult<ScreenResult<string>> Get() => Respond(ScreenResult<string>.Ok("admin"));

    [HttpPost, UiApi("/Test/Probe", Handler = "Change")]
    public ActionResult<ScreenResult<string>> Post() => Respond(ScreenResult<string>.Ok("admin"));
}

/// <summary>An access twin (writes are refused while the server is read-only).</summary>
[Route("api/ui/test-probe/access")]
public sealed class ProbeAccessUiApiController : AccessUiApiControllerBase
{
    [HttpGet, UiApi("/Test/ProbeAccess")]
    public ActionResult<ScreenResult<string>> Get() => Respond(ScreenResult<string>.Ok("access"));

    [HttpPost, UiApi("/Test/ProbeAccess", Handler = "Change")]
    public ActionResult<ScreenResult<string>> Post() => Respond(ScreenResult<string>.Ok("access"));
}
