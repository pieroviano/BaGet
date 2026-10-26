using System;
using System.Collections.Generic;
using System.Linq;
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
using Xunit;
using static BaGet.Tests.AdminTestSupport;

namespace BaGet.Tests;

/// <summary>
/// The JSON twins (<c>/api/ui/admin/groups|permissions|projects|audit</c>) of the access-control screens: who may call them (anonymous / user / administrator
/// session / <c>admin:api</c> key / other keys / a project's administrators by the <c>name</c> route value), the antiforgery rule, the read-only gate, the
/// password confirmation, that the answers carry what the pages show and that nothing secret leaves the server.
/// </summary>
public class AdminAccessUiApiTests : IDisposable
{
    private const string GlobalKey = "global-key";

    private readonly BaGetApplication _app;
    private readonly Browser _anonymous;
    private readonly Browser _alice;
    private readonly Browser _root;
    private readonly Browser _pat;
    private string _adminKey = string.Empty;
    private string _pushOnlyAdminKey = string.Empty;
    private string _aliceKey = string.Empty;
    private string _limitedAdminKey = string.Empty;
    private string _patPushKey = string.Empty;

    public AdminAccessUiApiTests(ITestOutputHelper output)
    {
        _app = new BaGetApplication(output, configOverrides: new Dictionary<string, string> { { "Accounts:Enabled", "true" }, { "ApiKey", GlobalKey } });
        _anonymous = new Browser(_app);
        _alice = new Browser(_app);
        _root = new Browser(_app);
        _pat = new Browser(_app);
        SetUpAsync().GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        _anonymous.Dispose();
        _alice.Dispose();
        _root.Dispose();
        _pat.Dispose();
        _app.Dispose();
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task SetUpAsync()
    {
        await CreateUserAsync(_app, "alice", email: "alice@example.com");
        await CreateUserAsync(_app, "bob");
        await CreateUserAsync(_app, "pat");
        await CreateUserAsync(_app, "root", administrator: true);
        await SignInAsync(_alice, "alice");
        await SignInAsync(_root, "root");
        await SignInAsync(_pat, "pat");

        using (var scope = _app.Services.CreateScope())
        {
            var access = scope.ServiceProvider.GetRequiredService<IAccessAdministration>();
            await access.CreateGroupAsync("team", "The team", "test", Token);
            await access.CreateProjectAsync("shop", "The shop", "test", Token);
            await access.CreateProjectAsync("other", null, "test", Token);
            await access.SetProjectMemberAsync("shop", PrincipalType.User, (await FindAsync(_app, "pat"))!.Id, ProjectRole.Admin, "test", Token);
            var shop = (await access.GetProjectAsync("shop", Token))!.Project.Key;
            await access.CreateTargetAsync("shop-target", new[] { "*" }, null, null, shop, "test", Token);
            await access.CreateTargetAsync("global-target", new[] { "*" }, null, null, null, "test", Token);
        }

        var defaultKey = (await _app.Services.GetRequiredService<IRepositoryService>().FindAsync("default", Token))!.Key;
        _adminKey = await KeyAsync("root", new[] { ApiKeyScopes.Admin });
        _pushOnlyAdminKey = await KeyAsync("root", new[] { ApiKeyScopes.Push });
        _limitedAdminKey = await KeyAsync("root", new[] { ApiKeyScopes.Admin }, new[] { defaultKey });
        _aliceKey = await KeyAsync("alice", new[] { ApiKeyScopes.Push });
        _patPushKey = await KeyAsync("pat", new[] { ApiKeyScopes.Push });
    }

    private async Task<string> KeyAsync(string user, string[] scopes, int[]? repositories = null)
    {
        using var scope = _app.Services.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<ApiKeyService>()
            .CreateAsync((await FindAsync(_app, user))!.Id, "test key of " + user, scopes, "*", null, repositories, Token);
    }

    private async Task<T> WithAsync<T>(Func<IAccessAdministration, Task<T>> action)
    {
        using var scope = _app.Services.CreateScope();

        return await action(scope.ServiceProvider.GetRequiredService<IAccessAdministration>());
    }

    private sealed record Answer(HttpStatusCode Status, JsonNode? Json, HttpResponseHeaders Headers, string Body);

    private async Task<Answer> SendAsync(Browser browser, HttpMethod method, string path, object? body = null, string? key = null, string? antiforgery = null)
    {
        using var request = new HttpRequestMessage(method, path);
        if (key != null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        if (antiforgery != null) request.Headers.Add("RequestVerificationToken", antiforgery);
        if (method != HttpMethod.Get)
        {
            var json = body == null ? "{}" : System.Text.Json.JsonSerializer.Serialize(body, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        using var response = await browser.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync(Token);

        return new Answer(response.StatusCode, text.Length > 0 && text[0] == '{' ? JsonNode.Parse(text) : null, response.Headers, text);
    }

    private Task<Answer> AsAdminAsync(HttpMethod method, string path, object? body = null) => SendAsync(_anonymous, method, path, body, key: _adminKey);

    private static string Status(Answer answer) => answer.Json!["status"]!.GetValue<string>();

    // ---- who may open a screen ----

    public static TheoryData<string> AdministratorScreens => new()
    {
        "/api/ui/admin/groups",
        "/api/ui/admin/groups/team",
        "/api/ui/admin/permissions",
        "/api/ui/admin/audit",
    };

    [Theory]
    [MemberData(nameof(AdministratorScreens))]
    public async Task AnAccessScreenForAdministratorsIsAnAdminSessionOrAnAdminApiKeyAndNobodyElse(string path)
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(_anonymous, HttpMethod.Get, path)).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(_alice, HttpMethod.Get, path)).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(_pat, HttpMethod.Get, path)).Status);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(_root, HttpMethod.Get, path)).Status);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(_anonymous, HttpMethod.Get, path, key: _adminKey)).Status);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(_anonymous, HttpMethod.Get, path, key: GlobalKey)).Status);

        // A key without the administration scope, a limited one, a plain user's key and a project administrator's own keys.
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(_anonymous, HttpMethod.Get, path, key: _pushOnlyAdminKey)).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(_anonymous, HttpMethod.Get, path, key: _limitedAdminKey)).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(_anonymous, HttpMethod.Get, path, key: _aliceKey)).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(_anonymous, HttpMethod.Get, path, key: _patPushKey)).Status);
    }

    [Fact]
    public async Task AProjectAdministratorOpensOnlyTheScreensOfTheirProjectByTheRouteValue()
    {
        // The list: the projects they administer, nothing else.
        var list = await SendAsync(_pat, HttpMethod.Get, "/api/ui/admin/projects");
        Assert.Equal(HttpStatusCode.OK, list.Status);
        Assert.Equal(new[] { "shop" }, list.Json!["model"]!["projects"]!.AsArray().Select(p => p!["name"]!.GetValue<string>()).ToArray());
        Assert.False(list.Json["model"]!["isServerAdministrator"]!.GetValue<bool>());

        Assert.Equal(HttpStatusCode.OK, (await SendAsync(_pat, HttpMethod.Get, "/api/ui/admin/projects/shop")).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(_pat, HttpMethod.Get, "/api/ui/admin/projects/other")).Status);

        // A permission target is open to the administrators of the target's project only.
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(_pat, HttpMethod.Get, "/api/ui/admin/permissions/shop-target")).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(_pat, HttpMethod.Get, "/api/ui/admin/permissions/global-target")).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(_pat, HttpMethod.Get, "/api/ui/admin/permissions")).Status);

        // Only an administrator can hold an administration key, so a project administrator's keys open nothing; and a user who administers no project sees an empty door (404).
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(_anonymous, HttpMethod.Get, "/api/ui/admin/projects/shop", key: _patPushKey)).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(_alice, HttpMethod.Get, "/api/ui/admin/projects")).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(_alice, HttpMethod.Get, "/api/ui/admin/projects/shop")).Status);
    }

    [Fact]
    public async Task AProjectAdministratorManagesMembersAndTargetsButNeverCreatesDeletesOrAssigns()
    {
        var token = await TokenAsync(_pat);

        var member = await SendAsync(_pat, HttpMethod.Post, "/api/ui/admin/projects/shop/set-member", new { type = "User", principalName = "bob", role = "Developer", password = Password }, antiforgery: token);
        Assert.Equal("Next", Status(member));
        Assert.Equal("bob is now Developer of shop.", member.Json!["next"]!["message"]!.GetValue<string>());

        var target = await SendAsync(_pat, HttpMethod.Post, "/api/ui/admin/projects/shop/create-target", new { targetName = "mine", repositoryPatterns = "*" }, antiforgery: token);
        Assert.Equal("Next", Status(target));
        Assert.Equal("/Admin/PermissionDetail", target.Json!["next"]!["screen"]!.GetValue<string>());

        var assign = await SendAsync(_pat, HttpMethod.Post, "/api/ui/admin/projects/shop/assign-repository", new { repository = "default", password = Password }, antiforgery: token);
        Assert.Contains("Only server administrators", assign.Json!["next"]!["error"]!.GetValue<string>(), StringComparison.Ordinal);

        var delete = await SendAsync(_pat, HttpMethod.Post, "/api/ui/admin/projects/shop/delete", new { password = Password }, antiforgery: token);
        Assert.Contains("Only server administrators", delete.Json!["next"]!["error"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.NotNull(await WithAsync(a => a.GetProjectAsync("shop", Token)));

        var create = await SendAsync(_pat, HttpMethod.Post, "/api/ui/admin/projects", new { name = "mine" }, antiforgery: token);
        Assert.Equal(HttpStatusCode.NotFound, create.Status);
        Assert.Null(await WithAsync(a => a.GetProjectAsync("mine", Token)));
    }

    [Fact]
    public async Task AProjectTargetIsDeletedBackToItsProjectForAProjectAdministrator()
    {
        var token = await TokenAsync(_pat);

        var deleted = await SendAsync(_pat, HttpMethod.Post, "/api/ui/admin/permissions/shop-target/delete", new { password = Password }, antiforgery: token);

        Assert.Equal("Next", Status(deleted));
        Assert.Equal("/Admin/ProjectDetail", deleted.Json!["next"]!["screen"]!.GetValue<string>());
        Assert.Equal("shop", deleted.Json["next"]!["routeValues"]!["name"]!.GetValue<string>());
        Assert.Null(await WithAsync(a => a.GetTargetAsync("shop-target", Token)));

        // The pattern preview of a project target only ever shows the project's repositories.
        var preview = await SendAsync(_anonymous, HttpMethod.Post, "/api/ui/admin/projects/shop/preview", new { targetName = "x", repositoryPatterns = "*" }, key: _adminKey);
        Assert.Empty(preview.Json!["model"]!["preview"]!["repositories"]!.AsArray());
    }

    [Fact]
    public async Task AnUnknownGroupTargetOrProjectIsAJson404()
    {
        foreach (var path in new[] { "/api/ui/admin/groups/nobody", "/api/ui/admin/permissions/nobody", "/api/ui/admin/projects/nobody" })
        {
            var answer = await SendAsync(_root, HttpMethod.Get, path);

            Assert.Equal(HttpStatusCode.NotFound, answer.Status);
            Assert.Equal("NotFound", Status(answer));
        }
    }

    // ---- writes ----

    private async Task<string> TokenAsync(Browser browser)
        => (await SendAsync(browser, HttpMethod.Get, "/api/ui/antiforgery")).Json!["requestToken"]!.GetValue<string>();

    [Fact]
    public async Task ACookieWriteNeedsTheAntiforgeryHeaderAndAKeyWriteDoesNot()
    {
        var refused = await SendAsync(_root, HttpMethod.Post, "/api/ui/admin/groups", new { name = "cookie-group" });
        Assert.Equal(HttpStatusCode.BadRequest, refused.Status);
        Assert.Null(await WithAsync(a => a.GetGroupAsync("cookie-group", Token)));

        var withKey = await AsAdminAsync(HttpMethod.Post, "/api/ui/admin/groups", new { name = "key-group", description = "by key" });
        Assert.Equal(HttpStatusCode.OK, withKey.Status);
        Assert.Equal("Next", Status(withKey));
        Assert.Equal("/Admin/GroupDetail", withKey.Json!["next"]!["screen"]!.GetValue<string>());
        Assert.Equal("key-group", withKey.Json["next"]!["routeValues"]!["name"]!.GetValue<string>());
        Assert.Equal("Group key-group was created.", withKey.Json["next"]!["message"]!.GetValue<string>());

        var withCookie = await SendAsync(_root, HttpMethod.Post, "/api/ui/admin/groups", new { name = "cookie-group" }, antiforgery: await TokenAsync(_root));
        Assert.Equal(HttpStatusCode.OK, withCookie.Status);
        Assert.NotNull(await WithAsync(a => a.GetGroupAsync("cookie-group", Token)));

        // The audit entry names the account that made the change.
        var audit = await WithAsync(a => a.AuditAsync(50, Token));
        Assert.Contains(audit, e => e.Actor == "root");
    }

    [Fact]
    public async Task ARefusedCreationAnswersTheListWithTheReasonAndKeepsTheForm()
    {
        var answer = await AsAdminAsync(HttpMethod.Post, "/api/ui/admin/groups", new { name = "team", description = "again" });

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal("Ok", Status(answer));
        Assert.False(string.IsNullOrEmpty(answer.Json!["model"]!["error"]!.GetValue<string>()));
        Assert.Equal("team", answer.Json["model"]!["name"]!.GetValue<string>());
        Assert.NotEmpty(answer.Json["model"]!["groups"]!.AsArray());
    }

    [Fact]
    public async Task AWrongPasswordChangesNothingAndTheAnswerSaysSo()
    {
        var alice = (await FindAsync(_app, "alice"))!.Id;
        var answer = await AsAdminAsync(HttpMethod.Post, "/api/ui/admin/groups/team/add-member", new { userId = alice, password = "wrong" });

        Assert.Equal("Next", Status(answer));
        Assert.Contains("confirmation is incorrect", answer.Json!["next"]!["error"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Empty((await WithAsync(a => a.GetGroupAsync("team", Token)))!.Members);
    }

    [Fact]
    public async Task EveryWriteIsRefusedWith503WhileTheServerIsReadOnly()
    {
        var gate = _app.Services.GetRequiredService<MaintenanceGate>();
        gate.Close("Copying the database");
        try
        {
            foreach (var (path, body) in new (string, object)[]
            {
                ("/api/ui/admin/groups", new { name = "ro" }),
                ("/api/ui/admin/groups/team/rename", new { newName = "ro" }),
                ("/api/ui/admin/groups/team/delete", new { password = Password }),
                ("/api/ui/admin/permissions", new { name = "ro", repositoryPatterns = "*" }),
                ("/api/ui/admin/permissions/preview", new { repositoryPatterns = "*" }),
                ("/api/ui/admin/permissions/restore-built-ins", new { password = Password }),
                ("/api/ui/admin/permissions/global-target/save", new { repositoryPatterns = "*", password = Password }),
                ("/api/ui/admin/permissions/global-target/delete", new { password = Password }),
                ("/api/ui/admin/projects", new { name = "ro" }),
                ("/api/ui/admin/projects/shop/set-member", new { type = "User", principalName = "bob", role = "Viewer", password = Password }),
                ("/api/ui/admin/projects/shop/delete", new { password = Password }),
            })
            {
                var answer = await AsAdminAsync(HttpMethod.Post, path, body);

                Assert.True(answer.Status == HttpStatusCode.ServiceUnavailable, $"{path} answered {answer.Status}");
            }

            // Reading still works, and so does a project administrator's read.
            Assert.Equal(HttpStatusCode.OK, (await AsAdminAsync(HttpMethod.Get, "/api/ui/admin/groups")).Status);
            Assert.Equal(HttpStatusCode.OK, (await SendAsync(_pat, HttpMethod.Get, "/api/ui/admin/projects/shop")).Status);
        }
        finally
        {
            gate.Open();
        }
    }

    // ---- what each screen does ----

    [Fact]
    public async Task AGroupIsRenamedFilledEmptiedAndDeleted()
    {
        var alice = (await FindAsync(_app, "alice"))!.Id;

        var renamed = await AsAdminAsync(HttpMethod.Post, "/api/ui/admin/groups/team/rename", new { newName = "crew" });
        Assert.Equal("crew", renamed.Json!["next"]!["routeValues"]!["name"]!.GetValue<string>());
        Assert.Equal("Group team is now called crew.", renamed.Json["next"]!["message"]!.GetValue<string>());

        var added = await AsAdminAsync(HttpMethod.Post, "/api/ui/admin/groups/crew/add-member", new { userId = alice, password = Password });
        Assert.Equal("The member was added.", added.Json!["next"]!["message"]!.GetValue<string>());

        // The member search lists users who are not members yet.
        var search = (await AsAdminAsync(HttpMethod.Get, "/api/ui/admin/groups/crew?q=bo")).Json!["model"]!;
        Assert.Equal("alice", search["group"]!["members"]![0]!["userName"]!.GetValue<string>());
        Assert.Equal("bob", search["found"]![0]!["userName"]!.GetValue<string>());

        var removed = await AsAdminAsync(HttpMethod.Post, "/api/ui/admin/groups/crew/remove-member", new { userId = alice, password = Password });
        Assert.Equal("The member was removed.", removed.Json!["next"]!["message"]!.GetValue<string>());

        var deleted = await AsAdminAsync(HttpMethod.Post, "/api/ui/admin/groups/crew/delete", new { password = Password });
        Assert.Equal("/Admin/Groups", deleted.Json!["next"]!["screen"]!.GetValue<string>());
        Assert.Null(await WithAsync(a => a.GetGroupAsync("crew", Token)));

        // A built-in group cannot be deleted: the detail screen says why.
        var builtIn = await AsAdminAsync(HttpMethod.Post, "/api/ui/admin/groups/Everyone/delete", new { password = Password });
        Assert.Equal("/Admin/GroupDetail", builtIn.Json!["next"]!["screen"]!.GetValue<string>());
        Assert.False(string.IsNullOrEmpty(builtIn.Json["next"]!["error"]!.GetValue<string>()));
    }

    [Fact]
    public async Task APermissionTargetGetsPatternsAGrantAndALastGoodbye()
    {
        var created = await AsAdminAsync(HttpMethod.Post, "/api/ui/admin/permissions", new { name = "docs", repositoryPatterns = "def*", includePatterns = "Contoso.*" });
        Assert.Equal("/Admin/PermissionDetail", created.Json!["next"]!["screen"]!.GetValue<string>());

        var preview = await AsAdminAsync(HttpMethod.Post, "/api/ui/admin/permissions/docs/preview", new { repositoryPatterns = "def*", includePatterns = "Contoso.*", testPackage = "Contoso.Core" });
        Assert.Equal(new[] { "default" }, preview.Json!["model"]!["preview"]!["repositories"]!.AsArray().Select(r => r!.GetValue<string>()).ToArray());
        Assert.True(preview.Json["model"]!["preview"]!["test"]!["matches"]!.GetValue<bool>());

        var save = await AsAdminAsync(HttpMethod.Post, "/api/ui/admin/permissions/docs/save", new { repositoryPatterns = "*", includePatterns = "Fabrikam.*", password = Password });
        Assert.Equal("The patterns were saved.", save.Json!["next"]!["message"]!.GetValue<string>());

        var grant = await AsAdminAsync(HttpMethod.Post, "/api/ui/admin/permissions/docs/set-grant", new { principalType = "Group", principalName = "team", actions = new[] { "Read", "Deploy" }, password = Password });
        Assert.Equal("team now holds Read, Deploy through this target.", grant.Json!["next"]!["message"]!.GetValue<string>());

        var detail = (await AsAdminAsync(HttpMethod.Get, "/api/ui/admin/permissions/docs?principalType=Group&principalName=team")).Json!["model"]!;
        Assert.Equal("Fabrikam.*", detail["includePatterns"]!.GetValue<string>());
        Assert.Contains("team", detail["target"]!["grants"]![0]!["principal"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Equal("Group", detail["principalType"]!.GetValue<string>());

        var principal = detail["target"]!["grants"]![0]!["principalId"]!.GetValue<string>();
        var removed = await AsAdminAsync(HttpMethod.Post, "/api/ui/admin/permissions/docs/remove-grant", new { type = "Group", principalId = principal, password = Password });
        Assert.Equal("The grant was removed.", removed.Json!["next"]!["message"]!.GetValue<string>());

        var gone = await AsAdminAsync(HttpMethod.Post, "/api/ui/admin/permissions/docs/delete", new { password = Password });
        Assert.Equal("/Admin/Permissions", gone.Json!["next"]!["screen"]!.GetValue<string>());
        Assert.Null(await WithAsync(a => a.GetTargetAsync("docs", Token)));

        var unknown = await AsAdminAsync(HttpMethod.Post, "/api/ui/admin/permissions/global-target/set-grant", new { principalType = "Group", principalName = "nobody", actions = new[] { "Read" }, password = Password });
        Assert.Contains("There is no group named 'nobody'", unknown.Json!["next"]!["error"]!.GetValue<string>(), StringComparison.Ordinal);

        var noProject = await AsAdminAsync(HttpMethod.Post, "/api/ui/admin/permissions", new { name = "p", repositoryPatterns = "*", project = "nope" });
        Assert.Contains("There is no project named 'nope'", noProject.Json!["model"]!["error"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AProjectIsCreatedStaffedAssignedAndDeleted()
    {
        var created = await AsAdminAsync(HttpMethod.Post, "/api/ui/admin/projects", new { name = "fresh", displayName = " Fresh " });
        Assert.Equal("/Admin/ProjectDetail", created.Json!["next"]!["screen"]!.GetValue<string>());

        var member = await AsAdminAsync(HttpMethod.Post, "/api/ui/admin/projects/fresh/set-member", new { type = "User", principalName = "bob", role = "Admin", password = Password });
        Assert.Equal("bob is now Admin of fresh.", member.Json!["next"]!["message"]!.GetValue<string>());

        var organization = await AsAdminAsync(HttpMethod.Post, "/api/ui/admin/projects/fresh/set-member", new { type = "Organization", principalName = "bob", role = "Admin", password = Password });
        Assert.Equal("A project member is a user or a group.", organization.Json!["next"]!["error"]!.GetValue<string>());

        var assigned = await AsAdminAsync(HttpMethod.Post, "/api/ui/admin/projects/fresh/assign-repository", new { repository = "default", password = Password });
        Assert.Equal("Repository default is now in fresh.", assigned.Json!["next"]!["message"]!.GetValue<string>());

        var detail = (await AsAdminAsync(HttpMethod.Get, "/api/ui/admin/projects/fresh")).Json!["model"]!;
        Assert.Equal(new[] { "default" }, detail["project"]!["repositories"]!.AsArray().Select(r => r!.GetValue<string>()).ToArray());
        Assert.Equal("Fresh", detail["project"]!["project"]!["displayName"]!.GetValue<string>());

        var memberId = detail["project"]!["members"]![0]!["principalId"]!.GetValue<string>();
        var removed = await AsAdminAsync(HttpMethod.Post, "/api/ui/admin/projects/fresh/remove-member", new { type = "User", principalId = memberId, password = Password });
        Assert.Equal("The member was removed.", removed.Json!["next"]!["message"]!.GetValue<string>());

        var unassigned = await AsAdminAsync(HttpMethod.Post, "/api/ui/admin/projects/fresh/unassign-repository", new { repository = "default", password = Password });
        Assert.Equal("Repository default is no longer in fresh.", unassigned.Json!["next"]!["message"]!.GetValue<string>());

        var deleted = await AsAdminAsync(HttpMethod.Post, "/api/ui/admin/projects/fresh/delete", new { password = Password });
        Assert.Equal("/Admin/Projects", deleted.Json!["next"]!["screen"]!.GetValue<string>());
        Assert.Null(await WithAsync(a => a.GetProjectAsync("fresh", Token)));
    }

    [Fact]
    public async Task TheAuditLogIsFilteredAndABadDateIsNamed()
    {
        await AsAdminAsync(HttpMethod.Post, "/api/ui/admin/groups", new { name = "audited" });

        var all = (await AsAdminAsync(HttpMethod.Get, "/api/ui/admin/audit")).Json!["model"]!;
        Assert.NotEmpty(all["entries"]!.AsArray());

        var filtered = (await AsAdminAsync(HttpMethod.Get, "/api/ui/admin/audit?subject=audited&actor=root")).Json!["model"]!;
        Assert.All(filtered["entries"]!.AsArray(), e => Assert.Contains("audited", e!["subject"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase));
        Assert.Equal("root", filtered["actorFilter"]!.GetValue<string>());

        var bad = (await AsAdminAsync(HttpMethod.Get, "/api/ui/admin/audit?from=yesterday")).Json!["model"]!;
        Assert.Equal("From is not a date (use yyyy-MM-dd).", bad["filterError"]!.GetValue<string>());
    }

    // ---- what the page shows is what the JSON carries ----

    [Fact]
    public async Task ThePagesAndTheirTwinsListTheSameThings()
    {
        var groups = await _root.GetPageAsync("/admin/groups");
        var groupJson = (await SendAsync(_root, HttpMethod.Get, "/api/ui/admin/groups")).Json!["model"]!;
        var groupNames = groupJson["groups"]!.AsArray().Select(g => g!["name"]!.GetValue<string>()).ToList();
        Assert.Contains("team", groupNames);
        Assert.Contains("Everyone", groupNames);
        foreach (var name in groupNames) Assert.Contains(name, groups, StringComparison.Ordinal);

        var targets = await _root.GetPageAsync("/admin/permissions");
        foreach (var name in (await SendAsync(_root, HttpMethod.Get, "/api/ui/admin/permissions")).Json!["model"]!["targets"]!.AsArray().Select(t => t!["name"]!.GetValue<string>()))
        {
            Assert.Contains(name, targets, StringComparison.Ordinal);
        }

        var detailPage = await _root.GetPageAsync("/admin/projects/shop");
        var detail = (await SendAsync(_root, HttpMethod.Get, "/api/ui/admin/projects/shop")).Json!["model"]!;
        Assert.Contains("pat", detail["project"]!["members"]![0]!["principal"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Contains("pat", detailPage, StringComparison.Ordinal);
        Assert.Contains("shop-target", detailPage, StringComparison.Ordinal);
        Assert.True(detail["isServerAdministrator"]!.GetValue<bool>());
        Assert.Contains("default", detail["assignable"]!.AsArray().Select(r => r!.GetValue<string>()));

        // The page of a project administrator lists their project only.
        var patList = await _pat.GetPageAsync("/admin/projects");
        Assert.Contains("shop", patList, StringComparison.Ordinal);
        Assert.DoesNotContain("/admin/projects/other", patList, StringComparison.Ordinal);
    }

    // ---- no secrets ----

    [Fact]
    public async Task NoAccessResponseCarriesASecret()
    {
        foreach (var path in new[]
                 {
                     "/api/ui/admin/groups", "/api/ui/admin/groups/team", "/api/ui/admin/permissions", "/api/ui/admin/permissions/global-target",
                     "/api/ui/admin/projects", "/api/ui/admin/projects/shop", "/api/ui/admin/audit",
                 })
        {
            var answer = await SendAsync(_anonymous, HttpMethod.Get, path, key: _adminKey);

            Assert.Equal(HttpStatusCode.OK, answer.Status);
            Assert.DoesNotContain(_adminKey, answer.Body, StringComparison.Ordinal);
            Assert.DoesNotContain(_aliceKey, answer.Body, StringComparison.Ordinal);
            Assert.DoesNotContain(GlobalKey, answer.Body, StringComparison.Ordinal);
            Assert.DoesNotContain(Password, answer.Body, StringComparison.Ordinal);
            foreach (var word in new[] { "passwordHash", "securityStamp", "hashedKey", "concurrencyStamp", "protectedPassword", "connectionString\":\"" })
            {
                Assert.DoesNotContain(word, answer.Body, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}

/// <summary>While accounts are off the access screens do not exist: a JSON 404, never a failure to build the controller.</summary>
public class AdminAccessUiApiAccountsOffTests : IDisposable
{
    private readonly BaGetApplication _app;

    public AdminAccessUiApiAccountsOffTests(ITestOutputHelper output)
    {
        _app = new BaGetApplication(output);
    }

    public void Dispose() => _app.Dispose();

    [Theory]
    [InlineData("/api/ui/admin/groups")]
    [InlineData("/api/ui/admin/groups/team")]
    [InlineData("/api/ui/admin/permissions")]
    [InlineData("/api/ui/admin/permissions/t")]
    [InlineData("/api/ui/admin/projects")]
    [InlineData("/api/ui/admin/projects/p")]
    [InlineData("/api/ui/admin/audit")]
    public async Task TheScreensAreAJson404(string path)
    {
        using var client = _app.CreateClient();
        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("NotFound", JsonNode.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))!["status"]!.GetValue<string>());
    }
}
