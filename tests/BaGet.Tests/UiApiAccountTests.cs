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
using BaGet.Identity.Application;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static BaGet.Tests.AdminTestSupport;

namespace BaGet.Tests;

/// <summary>
/// The account screens through their JSON twins (<c>/api/ui/account/...</c>, <c>/api/ui/profiles/...</c>) next to the pages they replace: the same
/// state changes and the same refusals (lockout, approval, return-URL rule, passwords asked again), the cookie-only rule (an API key is refused on
/// every self-service screen), antiforgery on writes, and responses that never carry a password, a key's hash or a key's value after its creation.
/// </summary>
public class UiApiAccountTests : IDisposable
{
    private const string PublicUrl = "https://baget.example.com";

    private readonly ITestOutputHelper _output;
    private readonly List<IDisposable> _disposables = new();

    public UiApiAccountTests(ITestOutputHelper output) => _output = output;

    public void Dispose()
    {
        foreach (var disposable in _disposables) disposable.Dispose();
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private BaGetApplication Start(Dictionary<string, string>? extra = null)
    {
        var settings = new Dictionary<string, string> { { "Accounts:Enabled", "true" } };
        foreach (var pair in extra ?? new Dictionary<string, string>()) settings[pair.Key] = pair.Value;

        var app = new BaGetApplication(_output, configOverrides: settings);
        _disposables.Add(app);

        return app;
    }

    private Browser NewBrowser(BaGetApplication app)
    {
        var browser = new Browser(app);
        _disposables.Add(browser);

        return browser;
    }

    private sealed record Answer(HttpStatusCode Status, JsonNode? Json, string Body, HttpResponseHeaders Headers);

    private static async Task<string> TokenAsync(Browser browser)
    {
        using var response = await browser.GetAsync("/api/ui/antiforgery");

        return JsonNode.Parse(await response.Content.ReadAsStringAsync(Token))!["requestToken"]!.GetValue<string>();
    }

    private static async Task<Answer> SendAsync(Browser browser, HttpMethod method, string path, object? body = null, string? key = null, bool antiforgery = true)
    {
        using var request = new HttpRequestMessage(method, path);
        if (key != null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        if (method != HttpMethod.Get)
        {
            if (key == null && antiforgery) request.Headers.Add("RequestVerificationToken", await TokenAsync(browser));
            request.Content = new StringContent(body == null ? "{}" : System.Text.Json.JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        }

        using var response = await browser.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync(Token);

        return new Answer(response.StatusCode, text.Length > 0 && text[0] == '{' ? JsonNode.Parse(text) : null, text, response.Headers);
    }

    private static Task<Answer> GetAsync(Browser browser, string path, string? key = null) => SendAsync(browser, HttpMethod.Get, path, key: key);

    private static Task<Answer> PostAsync(Browser browser, string path, object? body = null, string? key = null, bool antiforgery = true)
        => SendAsync(browser, HttpMethod.Post, path, body, key, antiforgery);

    private async Task<(BaGetApplication App, Browser Browser)> SignedInAsync(string name = "alice", Dictionary<string, string>? extra = null, bool administrator = false)
    {
        var app = Start(extra);
        await CreateUserAsync(app, name, administrator);
        var browser = NewBrowser(app);
        await SignInAsync(browser, name);

        return (app, browser);
    }

    private static async Task<string> KeyForAsync(BaGetApplication app, string user, params string[] scopes)
    {
        using var scope = app.Services.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<ApiKeyService>()
            .CreateAsync((await FindAsync(app, user))!.Id, "test", scopes, "*", null, null, Token);
    }

    // ---- sign in ----

    [Fact]
    public async Task TheLoginTwinAndThePageShowTheSameScreen()
    {
        var app = Start();
        var browser = NewBrowser(app);

        var page = await browser.GetPageAsync("/account/login?returnUrl=%2Fpackages");
        var api = await GetAsync(browser, "/api/ui/account/login?returnUrl=%2Fpackages");

        Assert.Equal(HttpStatusCode.OK, api.Status);
        Assert.Equal("Ok", api.Json!["status"]!.GetValue<string>());
        Assert.Equal("/packages", api.Json["model"]!["returnUrl"]!.GetValue<string>());
        Assert.True(api.Json["model"]!["allowRegistration"]!.GetValue<bool>());
        Assert.Contains("Create one", page, StringComparison.Ordinal);
        Assert.Contains("value=\"/packages\"", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SigningInThroughTheApiSetsTheSameSessionCookieAsThePage()
    {
        var app = Start();
        await CreateUserAsync(app, "alice");
        var browser = NewBrowser(app);

        Assert.Equal(HttpStatusCode.Unauthorized, (await GetAsync(browser, "/api/ui/account/settings")).Status);

        var answer = await PostAsync(browser, "/api/ui/account/login", new { userName = "alice", password = Password });

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal("Next", answer.Json!["status"]!.GetValue<string>());
        Assert.Equal("~/", answer.Json["next"]!["localUrl"]!.GetValue<string>());

        // The cookie the API set is the page's session too.
        Assert.Equal(HttpStatusCode.OK, (await GetAsync(browser, "/api/ui/account/settings")).Status);
        Assert.Contains("Account settings", await browser.GetPageAsync("/account/settings"), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ALoginWithoutTheAntiforgeryHeaderIs400()
    {
        var app = Start();
        await CreateUserAsync(app, "alice");
        var browser = NewBrowser(app);

        var answer = await PostAsync(browser, "/api/ui/account/login", new { userName = "alice", password = Password }, antiforgery: false);

        Assert.Equal(HttpStatusCode.BadRequest, answer.Status);
        Assert.Equal(HttpStatusCode.Unauthorized, (await GetAsync(browser, "/api/ui/account/settings")).Status);
    }

    [Fact]
    public async Task AWrongPasswordIs400WithTheSamePageMessageAndTheResponseNeverEchoesAPassword()
    {
        var app = Start();
        await CreateUserAsync(app, "alice");
        var browser = NewBrowser(app);

        var answer = await PostAsync(browser, "/api/ui/account/login", new { userName = "alice", password = "Wr0ng-Secret!" });
        using var page = await browser.PostAsync("account/login", new Dictionary<string, string> { { "UserName", "alice" }, { "Password", "Wr0ng-Secret!" } });

        Assert.Equal(HttpStatusCode.BadRequest, answer.Status);
        Assert.Equal("Invalid", answer.Json!["status"]!.GetValue<string>());
        Assert.Equal("The user name or password is incorrect.", answer.Json["model"]!["error"]!.GetValue<string>());
        Assert.Equal("alice", answer.Json["model"]!["userName"]!.GetValue<string>());
        Assert.DoesNotContain("Wr0ng-Secret!", answer.Body, StringComparison.Ordinal);
        Assert.Contains("The user name or password is incorrect.", await page.Content.ReadAsStringAsync(Token), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AMissingFieldIsAFieldErrorLikeThePage()
    {
        var app = Start();
        var browser = NewBrowser(app);

        var answer = await PostAsync(browser, "/api/ui/account/login", new { userName = "alice" });

        Assert.Equal(HttpStatusCode.BadRequest, answer.Status);
        Assert.Equal("The Password field is required.", answer.Json!["errors"]!["Password"]![0]!.GetValue<string>());
    }

    [Theory]
    [InlineData("/packages", "/packages")]
    [InlineData("/r/second/packages", "/r/second/packages")]
    [InlineData("https://evil.example/", "~/")]
    [InlineData("//evil.example/", "~/")]
    [InlineData("/\\evil.example", "~/")]
    [InlineData("javascript:alert(1)", "~/")]
    [InlineData("", "~/")]
    public async Task TheReturnUrlRuleIsTheSameThroughTheApi(string returnUrl, string expected)
    {
        var app = Start();
        await CreateUserAsync(app, "alice");
        var browser = NewBrowser(app);

        var answer = await PostAsync(browser, "/api/ui/account/login", new { userName = "alice", password = Password, returnUrl });

        Assert.Equal(expected, answer.Json!["next"]!["localUrl"]!.GetValue<string>());
    }

    [Fact]
    public async Task TheApiLocksAnAccountOutLikeThePageDoes()
    {
        var app = Start();
        await CreateUserAsync(app, "alice");
        var browser = NewBrowser(app);

        for (var i = 0; i < 5; i++)
        {
            await PostAsync(browser, "/api/ui/account/login", new { userName = "alice", password = "wrong" + i });
        }

        var answer = await PostAsync(browser, "/api/ui/account/login", new { userName = "alice", password = Password });

        Assert.Equal(HttpStatusCode.BadRequest, answer.Status);
        Assert.Equal("The account is locked after too many failed attempts. Try again later.", answer.Json!["model"]!["error"]!.GetValue<string>());
        Assert.False(await CanSignInAsync(app, "alice", Password));
    }

    [Fact]
    public async Task AnAccountAwaitingApprovalGetsThePendingAnswerOnlyWithTheRightPassword()
    {
        var app = Start(new Dictionary<string, string> { { "Accounts:RequireApproval", "true" } });
        using (var scope = app.Services.CreateScope())
        {
            var created = await scope.ServiceProvider.GetRequiredService<UserManager<BaGetUser>>().CreateAsync(
                new BaGetUser { UserName = "newbie", Created = DateTime.UtcNow, PendingApproval = true },
                Password);
            Assert.True(created.Succeeded);
        }

        var browser = NewBrowser(app);
        var right = await PostAsync(browser, "/api/ui/account/login", new { userName = "newbie", password = Password });
        var wrong = await PostAsync(browser, "/api/ui/account/login", new { userName = "newbie", password = "nope" });

        Assert.Contains("waiting for an administrator", right.Json!["model"]!["error"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Equal("The user name or password is incorrect.", wrong.Json!["model"]!["error"]!.GetValue<string>());
    }

    [Fact]
    public async Task ATwoFactorAccountIsSentToTheSecondStepAndARecoveryCodeCompletesTheSignIn()
    {
        var app = Start();
        await CreateUserAsync(app, "alice");
        string code;
        using (var scope = app.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<BaGetUser>>();
            var user = (await users.FindByNameAsync("alice"))!;
            await users.ResetAuthenticatorKeyAsync(user);
            await users.SetTwoFactorEnabledAsync(user, true);
            code = (await users.GenerateNewTwoFactorRecoveryCodesAsync(user, 2))!.First();
        }

        var browser = NewBrowser(app);

        // Without the half-signed-in cookie the second step sends the visitor back to the first.
        var early = await GetAsync(browser, "/api/ui/account/loginwith2fa");
        Assert.Equal("Next", early.Json!["status"]!.GetValue<string>());
        Assert.Equal("/Account/Login", early.Json["next"]!["screen"]!.GetValue<string>());

        var first = await PostAsync(browser, "/api/ui/account/login", new { userName = "alice", password = Password, rememberMe = true });
        Assert.Equal("/Account/LoginWith2fa", first.Json!["next"]!["screen"]!.GetValue<string>());
        Assert.Equal(HttpStatusCode.Unauthorized, (await GetAsync(browser, "/api/ui/account/settings")).Status);

        var wrong = await PostAsync(browser, "/api/ui/account/loginwithrecoverycode", new { recoveryCode = "not-a-code" });
        Assert.Equal(HttpStatusCode.BadRequest, wrong.Status);
        Assert.Equal("The recovery code is not valid, or was already used.", wrong.Json!["model"]!["error"]!.GetValue<string>());

        var done = await PostAsync(browser, "/api/ui/account/loginwithrecoverycode", new { recoveryCode = code });
        Assert.Equal("Next", done.Json!["status"]!.GetValue<string>());
        Assert.Equal(HttpStatusCode.OK, (await GetAsync(browser, "/api/ui/account/settings")).Status);
    }

    [Fact]
    public async Task SigningOutThroughTheApiEndsTheSession()
    {
        var (_, browser) = await SignedInAsync();
        Assert.Equal(HttpStatusCode.OK, (await GetAsync(browser, "/api/ui/account/settings")).Status);

        var answer = await PostAsync(browser, "/api/ui/account/logout");

        Assert.Equal("~/", answer.Json!["next"]!["localUrl"]!.GetValue<string>());
        Assert.Equal(HttpStatusCode.Unauthorized, (await GetAsync(browser, "/api/ui/account/settings")).Status);
    }

    // ---- the cookie-only rule ----

    [Theory]
    [InlineData("GET", "/api/ui/account/login")]
    [InlineData("GET", "/api/ui/account/register")]
    [InlineData("GET", "/api/ui/account/settings")]
    [InlineData("GET", "/api/ui/account/twofactor")]
    [InlineData("GET", "/api/ui/account/apikeys")]
    [InlineData("GET", "/api/ui/account/organizations")]
    [InlineData("GET", "/api/ui/account/packages")]
    [InlineData("GET", "/api/ui/account/access")]
    [InlineData("POST", "/api/ui/account/login")]
    [InlineData("POST", "/api/ui/account/logout")]
    [InlineData("POST", "/api/ui/account/settings/change-password")]
    [InlineData("POST", "/api/ui/account/settings/delete")]
    [InlineData("POST", "/api/ui/account/twofactor/reset")]
    [InlineData("POST", "/api/ui/account/apikeys/create")]
    public async Task AnApiKeyIsRefusedOnEveryAccountScreen(string method, string path)
    {
        var app = Start();
        await CreateUserAsync(app, "alice");
        var key = await KeyForAsync(app, "alice", ApiKeyScopes.Push);
        var browser = NewBrowser(app);

        var answer = await SendAsync(browser, new HttpMethod(method), path, key: key);

        Assert.Equal(HttpStatusCode.Forbidden, answer.Status);
        Assert.Equal("Forbidden", answer.Json!["status"]!.GetValue<string>());
        Assert.DoesNotContain(key, answer.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AKeyThatDoesNotAuthenticateIs401OnAnAccountScreen()
    {
        var app = Start();
        var browser = NewBrowser(app);

        Assert.Equal(HttpStatusCode.Unauthorized, (await GetAsync(browser, "/api/ui/account/login", key: "not-a-key")).Status);
    }

    [Fact]
    public async Task AnAnonymousVisitorIs401OnTheSignedInScreens()
    {
        var app = Start();
        var browser = NewBrowser(app);

        foreach (var path in new[] { "settings", "twofactor", "apikeys", "organizations", "packages", "access", "profile", "reauthenticate" })
        {
            var answer = await GetAsync(browser, "/api/ui/account/" + path);

            Assert.True(answer.Status == HttpStatusCode.Unauthorized, $"{path} answered {answer.Status}");
            Assert.Contains("Bearer", string.Join(",", answer.Headers.WwwAuthenticate), StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task EveryAccountScreenIs404WhileAccountsAreOff()
    {
        using var app = new BaGetApplication(_output);
        using var browser = new Browser(app);

        foreach (var path in new[] { "login", "register", "settings", "apikeys", "organizations" })
        {
            var answer = await GetAsync(browser, "/api/ui/account/" + path);

            Assert.True(answer.Status == HttpStatusCode.NotFound, $"{path} answered {answer.Status}");
            Assert.Equal("NotFound", answer.Json!["status"]!.GetValue<string>());
        }

        Assert.Equal(HttpStatusCode.NotFound, (await GetAsync(browser, "/api/ui/profiles/alice")).Status);
    }

    // ---- registration ----

    [Fact]
    public async Task RegisteringThroughTheApiCreatesTheAccountAndSignsItIn()
    {
        var app = Start();
        var browser = NewBrowser(app);

        var answer = await PostAsync(browser, "/api/ui/account/register", new { userName = "carol", password = Password, confirmPassword = Password });

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal("Next", answer.Json!["status"]!.GetValue<string>());
        Assert.NotNull(await FindAsync(app, "carol"));
        Assert.Equal(HttpStatusCode.OK, (await GetAsync(browser, "/api/ui/account/settings")).Status);
    }

    [Fact]
    public async Task RegistrationErrorsAreFieldErrors()
    {
        var app = Start();
        await CreateUserAsync(app, "taken");
        var browser = NewBrowser(app);

        var mismatch = await PostAsync(browser, "/api/ui/account/register", new { userName = "carol", password = Password, confirmPassword = "other" });
        var taken = await PostAsync(browser, "/api/ui/account/register", new { userName = "taken", password = Password, confirmPassword = Password });

        Assert.Equal("The passwords do not match.", mismatch.Json!["errors"]!["ConfirmPassword"]![0]!.GetValue<string>());
        Assert.Equal("That name is taken.", taken.Json!["errors"]!["UserName"]![0]!.GetValue<string>());
        Assert.DoesNotContain(Password, taken.Body, StringComparison.Ordinal);
        Assert.Null(await FindAsync(app, "carol"));
    }

    [Fact]
    public async Task RegistrationIs404InBothWaysWhenItIsClosed()
    {
        var app = Start(new Dictionary<string, string> { { "Accounts:AllowRegistration", "false" } });
        var browser = NewBrowser(app);

        using var page = await browser.GetAsync("/account/register");
        var api = await GetAsync(browser, "/api/ui/account/register");
        var post = await PostAsync(browser, "/api/ui/account/register", new { userName = "carol", password = Password, confirmPassword = Password });

        Assert.Equal(HttpStatusCode.NotFound, page.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, api.Status);
        Assert.Equal(HttpStatusCode.NotFound, post.Status);
        Assert.Null(await FindAsync(app, "carol"));
    }

    // ---- mail ----

    private static readonly Dictionary<string, string> Mail = new()
    {
        { "Email:Smtp:Host", "smtp.example.com" },
        { "Email:Smtp:From", "baget@example.com" },
        { "Site:PublicUrl", PublicUrl },
    };

    [Fact]
    public async Task TheConfirmationLinkTheApiMailsIsTheSiteLinkAndFollowingItConfirmsTheAddress()
    {
        var app = Start(Mail);
        var browser = NewBrowser(app);

        var registered = await PostAsync(browser, "/api/ui/account/register", new { userName = "carol", email = "carol@example.com", password = Password, confirmPassword = Password });
        Assert.True(registered.Json!["model"]!["confirmationSent"]!.GetValue<bool>());

        var mail = Assert.Single(app.Services.GetRequiredService<CapturingEmailSender>().Sent, m => m.Kind == "confirmation");
        Assert.StartsWith(PublicUrl + "/account/confirmemail?userId=", mail.Payload, StringComparison.Ordinal);

        var query = mail.Payload.Substring(mail.Payload.IndexOf('?') + 1);
        var confirmed = await GetAsync(browser, "/api/ui/account/confirmemail?" + query);

        Assert.True(confirmed.Json!["model"]!["succeeded"]!.GetValue<bool>());
        Assert.True((await FindAsync(app, "carol"))!.EmailConfirmed);
    }

    [Fact]
    public async Task ForgotPasswordAnswersTheSameForAKnownAndAnUnknownAccount()
    {
        var app = Start(Mail);
        await CreateUserAsync(app, "alice", email: "alice@example.com");
        var browser = NewBrowser(app);

        var known = await PostAsync(browser, "/api/ui/account/forgotpassword", new { identifier = "alice" });
        var unknown = await PostAsync(browser, "/api/ui/account/forgotpassword", new { identifier = "nobody" });

        Assert.Equal(known.Status, unknown.Status);
        Assert.True(known.Json!["model"]!["sent"]!.GetValue<bool>());
        Assert.True(unknown.Json!["model"]!["sent"]!.GetValue<bool>());

        var sender = app.Services.GetRequiredService<CapturingEmailSender>();
        for (var i = 0; i < 100 && !sender.Sent.Any(m => m.Kind == "reset-link"); i++) await Task.Delay(50, Token);
        var mail = Assert.Single(sender.Sent, m => m.Kind == "reset-link");
        Assert.StartsWith(PublicUrl + "/account/resetpassword?userId=", mail.Payload, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheMailScreensAre404WithoutMail()
    {
        var app = Start();
        var browser = NewBrowser(app);

        Assert.Equal(HttpStatusCode.NotFound, (await GetAsync(browser, "/api/ui/account/forgotpassword")).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await GetAsync(browser, "/api/ui/account/resetpassword")).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await GetAsync(browser, "/api/ui/account/confirmemail")).Status);
    }

    // ---- settings ----

    [Fact]
    public async Task ChangingThePasswordThroughTheApiDoesWhatThePageDoes()
    {
        var (app, browser) = await SignedInAsync();

        var mismatch = await PostAsync(browser, "/api/ui/account/settings/change-password", new { currentPassword = Password, newPassword = "N3w-Passw0rd!", confirmPassword = "x" });
        var wrong = await PostAsync(browser, "/api/ui/account/settings/change-password", new { currentPassword = "wrong", newPassword = "N3w-Passw0rd!", confirmPassword = "N3w-Passw0rd!" });
        var changed = await PostAsync(browser, "/api/ui/account/settings/change-password", new { currentPassword = Password, newPassword = "N3w-Passw0rd!", confirmPassword = "N3w-Passw0rd!" });

        Assert.Equal("The new passwords do not match.", mismatch.Json!["model"]!["error"]!.GetValue<string>());
        Assert.Equal(HttpStatusCode.BadRequest, wrong.Status);
        Assert.Equal(HttpStatusCode.OK, changed.Status);
        Assert.Equal("Your password has been changed.", changed.Json!["model"]!["message"]!.GetValue<string>());
        Assert.DoesNotContain("N3w-Passw0rd!", changed.Body, StringComparison.Ordinal);
        Assert.True(await CanSignInAsync(app, "alice", "N3w-Passw0rd!"));
        Assert.False(await CanSignInAsync(app, "alice", Password));

        // The session stayed signed in (the security stamp was refreshed).
        Assert.Equal(HttpStatusCode.OK, (await GetAsync(browser, "/api/ui/account/settings")).Status);
    }

    [Fact]
    public async Task DeletingTheAccountAsksThePasswordAgainThroughTheApi()
    {
        var (app, browser) = await SignedInAsync();

        var refused = await PostAsync(browser, "/api/ui/account/settings/delete", new { password = "wrong" });
        Assert.Equal(HttpStatusCode.BadRequest, refused.Status);
        Assert.NotNull(await FindAsync(app, "alice"));

        var done = await PostAsync(browser, "/api/ui/account/settings/delete", new { password = Password });
        Assert.Equal("Next", done.Json!["status"]!.GetValue<string>());
        Assert.Null(await FindAsync(app, "alice"));
        Assert.Equal(HttpStatusCode.Unauthorized, (await GetAsync(browser, "/api/ui/account/settings")).Status);
    }

    [Fact]
    public async Task ChangingTheEmailAsksThePasswordAgainThroughTheApi()
    {
        var (app, browser) = await SignedInAsync();

        var refused = await PostAsync(browser, "/api/ui/account/settings/change-email", new { newEmail = "a@example.com", password = "wrong" });
        var invalid = await PostAsync(browser, "/api/ui/account/settings/change-email", new { newEmail = "not-an-email", password = Password });
        var done = await PostAsync(browser, "/api/ui/account/settings/change-email", new { newEmail = "a@example.com", password = Password });

        Assert.Equal("The confirmation is incorrect, so the email address was not changed.", refused.Json!["model"]!["error"]!.GetValue<string>());
        Assert.Equal("Enter a valid email address.", invalid.Json!["model"]!["error"]!.GetValue<string>());
        Assert.Equal("Your email address has been changed.", done.Json!["model"]!["message"]!.GetValue<string>());
        Assert.Equal("a@example.com", (await FindAsync(app, "alice"))!.Email);
    }

    [Fact]
    public async Task TheTwoFactorScreenOffersTheSetupKeyToTheSessionAndRefusesABadCode()
    {
        var (_, browser) = await SignedInAsync();

        var screen = await GetAsync(browser, "/api/ui/account/twofactor");
        var bad = await PostAsync(browser, "/api/ui/account/twofactor/enable", new { code = "000000" });

        Assert.False(screen.Json!["model"]!["twoFactorEnabled"]!.GetValue<bool>());
        Assert.False(string.IsNullOrEmpty(screen.Json["model"]!["sharedKey"]!.GetValue<string>()));
        Assert.Contains("<svg", screen.Json["model"]!["qrCodeSvg"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.BadRequest, bad.Status);
        Assert.Equal("The code is not valid. Check the authenticator app and try again.", bad.Json!["model"]!["error"]!.GetValue<string>());
    }

    // ---- API keys ----

    [Fact]
    public async Task ApiKeysNeverAppearInAListingOnlyInTheAnswerThatCreatedThem()
    {
        var (app, browser) = await SignedInAsync();

        var created = await PostAsync(browser, "/api/ui/account/apikeys/create", new { description = "ci", scopes = new[] { ApiKeyScopes.Push }, packageGlobPattern = "*", expiresInDays = 30 });
        var value = created.Json!["model"]!["createdKey"]!.GetValue<string>();
        Assert.False(string.IsNullOrEmpty(value));

        var listing = await GetAsync(browser, "/api/ui/account/apikeys");

        Assert.DoesNotContain(value, listing.Body, StringComparison.Ordinal);
        string hash;
        using (var scope = app.Services.CreateScope())
        {
            hash = scope.ServiceProvider.GetRequiredService<IContext>().ApiKeys!.Single().HashedKey!;
        }

        Assert.DoesNotContain(hash, created.Body + listing.Body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hashedKey", listing.Body, StringComparison.OrdinalIgnoreCase);
        var row = listing.Json!["model"]!["keys"]![0]!;
        Assert.Equal("ci", row["description"]!.GetValue<string>());
        Assert.Equal(ApiKeyScopes.Push, row["scopes"]![0]!.GetValue<string>());
        Assert.Null(listing.Json["model"]!["createdKey"]);
    }

    [Fact]
    public async Task AKeyCreatedThroughTheApiAuthenticatesAndRevokingItStopsIt()
    {
        var (app, browser) = await SignedInAsync();
        var created = await PostAsync(browser, "/api/ui/account/apikeys/create", new { scopes = new[] { ApiKeyScopes.Push }, packageGlobPattern = "*" });
        var key = created.Json!["model"]!["createdKey"]!.GetValue<string>();
        var id = created.Json["model"]!["keys"]![0]!["key"]!.GetValue<int>();

        using (var scope = app.Services.CreateScope())
        {
            Assert.NotNull(await scope.ServiceProvider.GetRequiredService<ApiKeyService>().FindValidOrNullAsync(key, Token));
        }

        var revoked = await PostAsync(browser, $"/api/ui/account/apikeys/revoke/{id}");
        Assert.Equal("/Account/ApiKeys", revoked.Json!["next"]!["screen"]!.GetValue<string>());

        using (var scope = app.Services.CreateScope())
        {
            Assert.Null(await scope.ServiceProvider.GetRequiredService<ApiKeyService>().FindValidOrNullAsync(key, Token));
        }
    }

    [Fact]
    public async Task AKeyWithoutAScopeOrWithABadExpiryIsRefusedWithFieldErrors()
    {
        var (app, browser) = await SignedInAsync();

        var noScope = await PostAsync(browser, "/api/ui/account/apikeys/create", new { scopes = Array.Empty<string>(), packageGlobPattern = "*" });
        var badDays = await PostAsync(browser, "/api/ui/account/apikeys/create", new { scopes = new[] { ApiKeyScopes.Push }, packageGlobPattern = "*", expiresInDays = 99999 });
        var admin = await PostAsync(browser, "/api/ui/account/apikeys/create", new { scopes = new[] { ApiKeyScopes.Admin }, packageGlobPattern = "*" });

        Assert.Equal("Choose at least one scope.", noScope.Json!["errors"]!["Scopes"]![0]!.GetValue<string>());
        Assert.NotNull(badDays.Json!["errors"]!["ExpiresInDays"]);
        Assert.Equal(HttpStatusCode.BadRequest, admin.Status);
        using var scope = app.Services.CreateScope();
        Assert.Empty(scope.ServiceProvider.GetRequiredService<IContext>().ApiKeys!);
    }

    // ---- organizations and the overview screens ----

    [Fact]
    public async Task OrganizationsCanBeCreatedThroughTheApiAndAMemberOfAnotherCannotAddMembers()
    {
        var (app, browser) = await SignedInAsync();
        await CreateUserAsync(app, "bob");
        await AddOrganizationAsync(app, "other", ("bob", true));

        var created = await PostAsync(browser, "/api/ui/account/organizations/create", new { name = "mine" });
        var screen = await GetAsync(browser, "/api/ui/account/organizations");
        var forbidden = await PostAsync(browser, "/api/ui/account/organizations/add-member", new { organization = "other", userName = "alice", isAdmin = false });

        Assert.Equal("Created mine.", created.Json!["model"]!["message"]!.GetValue<string>());
        Assert.Equal("mine", screen.Json!["model"]!["organizations"]![0]!["name"]!.GetValue<string>());
        Assert.Equal("alice", screen.Json["model"]!["organizations"]![0]!["members"]![0]!["userName"]!.GetValue<string>());
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.Status);

        // The page answers the same refusal: a bare 403.
        using var page = await browser.PostAsync(
            "account/organizations",
            new Dictionary<string, string> { { "organization", "other" }, { "userName", "alice" } },
            action: "account/organizations?handler=AddMember");
        Assert.Equal(HttpStatusCode.Forbidden, page.StatusCode);
    }

    [Fact]
    public async Task MyPackagesAndMyAccessAreTheScreensThePagesShow()
    {
        var (app, browser) = await SignedInAsync();
        await PushAsync(app, "alice", "Alice.Lib");

        var packages = await GetAsync(browser, "/api/ui/account/packages");
        var access = await GetAsync(browser, "/api/ui/account/access");
        var page = await browser.GetPageAsync("/account/packages");

        Assert.Equal("Alice.Lib", packages.Json!["model"]!["groups"]![0]!["packages"]![0]!["id"]!.GetValue<string>());
        Assert.Contains("Alice.Lib", page, StringComparison.Ordinal);
        Assert.NotNull(access.Json!["model"]!["rows"]);
    }

    [Fact]
    public async Task MyProfileContinuesAtTheProfileAddress()
    {
        var (_, browser) = await SignedInAsync();

        var answer = await GetAsync(browser, "/api/ui/account/profile");
        using var page = await browser.GetAsync("/account/profile");

        Assert.Equal("~/profiles/alice", answer.Json!["next"]!["localUrl"]!.GetValue<string>());
        Assert.Equal(HttpStatusCode.Redirect, page.StatusCode);
        Assert.Equal("/profiles/alice", page.Headers.Location!.ToString());
    }

    // ---- profile ----

    [Fact]
    public async Task AProfileIsPublicDataAKeyMayReadAndAnUnknownNameIs404InBothWays()
    {
        var (app, browser) = await SignedInAsync();
        await PushAsync(app, "alice", "Alice.Lib");
        await app.CreateRepositoryAsync("second", Token);
        var key = await KeyForAsync(app, "alice", ApiKeyScopes.Push);
        var anonymous = NewBrowser(app);

        var viaSession = await GetAsync(browser, "/api/ui/profiles/alice");
        var viaKey = await GetAsync(anonymous, "/api/ui/profiles/alice", key: key);
        var anonymousRead = await GetAsync(anonymous, "/api/ui/profiles/alice");
        var otherRepository = await GetAsync(anonymous, "/api/ui/r/second/profiles/alice");
        var unknownRepository = await GetAsync(anonymous, "/api/ui/r/nope/profiles/alice");
        var unknown = await GetAsync(anonymous, "/api/ui/profiles/nobody");
        using var unknownPage = await anonymous.GetAsync("/profiles/nobody");

        Assert.True(viaSession.Json!["model"]!["isCurrentUser"]!.GetValue<bool>());
        Assert.Equal("Alice.Lib", viaSession.Json["model"]!["packages"]![0]!["id"]!.GetValue<string>());
        Assert.Equal(HttpStatusCode.OK, viaKey.Status);
        Assert.Equal(HttpStatusCode.OK, anonymousRead.Status);
        Assert.False(anonymousRead.Json!["model"]!["isCurrentUser"]!.GetValue<bool>());
        Assert.Equal(HttpStatusCode.OK, otherRepository.Status);
        Assert.Equal(HttpStatusCode.NotFound, unknownRepository.Status);
        Assert.Equal(HttpStatusCode.NotFound, unknown.Status);
        Assert.Equal(HttpStatusCode.NotFound, unknownPage.StatusCode);
        Assert.DoesNotContain(key, viaKey.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AReadOnlyServerStillLetsAUserSignInAndOut()
    {
        var app = Start();
        await CreateUserAsync(app, "alice");
        var browser = NewBrowser(app);
        var gate = app.Services.GetRequiredService<MaintenanceGate>();

        gate.Close("Copying the database");
        try
        {
            var answer = await PostAsync(browser, "/api/ui/account/login", new { userName = "alice", password = Password });
            Assert.Equal(HttpStatusCode.OK, answer.Status);
            Assert.Equal(HttpStatusCode.OK, (await GetAsync(browser, "/api/ui/account/settings")).Status);
        }
        finally
        {
            gate.Open();
        }
    }
}
