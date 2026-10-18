using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace BaGet.Tests;

/// <summary>
/// Account settings, email recovery and confirmation, and two-factor authentication,
/// driven through the real pages with a browser-like client.
/// </summary>
public class AccountSettingsTests : IDisposable
{
    private const string Password = "Passw0rd!";

    private readonly ITestOutputHelper _output;
    private readonly List<IDisposable> _disposables = new List<IDisposable>();

    public AccountSettingsTests(ITestOutputHelper output) => _output = output;

    public void Dispose()
    {
        foreach (var d in _disposables) d.Dispose();
    }

    private (BaGetApplication App, Browser Browser) Start(bool accounts = true, bool email = false)
    {
        var settings = new Dictionary<string, string>
        {
            { "Accounts:Enabled", accounts ? "true" : "false" },
            { "ApiKey", "" },
        };
        if (email)
        {
            settings["Email:Smtp:Host"] = "smtp.example.com";
            settings["Email:Smtp:From"] = "baget@example.com";
        }

        var app = new BaGetApplication(_output, configOverrides: settings);
        _disposables.Add(app);

        return (app, NewBrowser(app));
    }

    private Browser NewBrowser(BaGetApplication app)
    {
        var browser = new Browser(app);
        _disposables.Add(browser);

        return browser;
    }

    private static async Task<HttpResponseMessage> SignInAsync(Browser browser, string userName, string password = Password)
        => await browser.PostAsync("account/login", new Dictionary<string, string>
        {
            { "UserName", userName },
            { "Password", password },
        });

    private static async Task<bool> CanSignInAsync(BaGetApplication app, string userName, string password)
    {
        using var browser = new Browser(app);
        using var response = await SignInAsync(browser, userName, password);

        return response.StatusCode == HttpStatusCode.Redirect
               && response.Headers.Location.ToString().IndexOf("2fa", StringComparison.OrdinalIgnoreCase) < 0
               && (await browser.GetAsync("account/settings")).StatusCode == HttpStatusCode.OK;
    }

    private static async Task<T> WithAsync<T>(BaGetApplication app, Func<IServiceProvider, Task<T>> action)
    {
        using var scope = app.Services.CreateScope();

        return await action(scope.ServiceProvider);
    }

    private static Task<BaGetUser> FindAsync(BaGetApplication app, string userName)
        => WithAsync(app, p => p.GetRequiredService<UserManager<BaGetUser>>().FindByNameAsync(userName));

    private static string PathOf(string link) => new Uri(link).PathAndQuery.TrimStart('/');

    // --- Gating -------------------------------------------------------------------------

    [Theory]
    [InlineData("account/settings")]
    [InlineData("account/twofactor")]
    [InlineData("account/forgotpassword")]
    [InlineData("account/resetpassword")]
    [InlineData("account/confirmemail")]
    [InlineData("account/loginwith2fa")]
    [InlineData("account/loginwithrecoverycode")]
    public async Task NoAccountPageIsReachableWhileAccountsAreDisabled(string url)
    {
        var (_, browser) = Start(accounts: false);

        using var response = await browser.GetAsync(url);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("account/forgotpassword")]
    [InlineData("account/resetpassword")]
    [InlineData("account/confirmemail")]
    public async Task EmailPagesAreNotFoundWhileEmailIsNotConfigured(string url)
    {
        var (_, browser) = Start();

        using var response = await browser.GetAsync(url);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("account/settings")]
    [InlineData("account/twofactor")]
    public async Task SettingsPagesAskAnonymousUsersToSignIn(string url)
    {
        var (_, browser) = Start();

        using var response = await browser.GetAsync(url);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("account/login", response.Headers.Location.ToString());
    }

    [Fact]
    public async Task APostWithoutAnAntiforgeryTokenIsRefused()
    {
        var (_, browser) = Start();
        await browser.RegisterAsync("alice");

        using var response = await browser.RawPostAsync("account/settings?handler=ChangePassword", new Dictionary<string, string>
        {
            { "currentPassword", Password },
            { "newPassword", "N3w-Passw0rd!" },
            { "confirmPassword", "N3w-Passw0rd!" },
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // --- Navigation ---------------------------------------------------------------------

    [Fact]
    public async Task TheAccountMenuLinksToSettings()
    {
        var (_, browser) = Start();
        await browser.RegisterAsync("alice");

        Assert.Contains("account/settings", await browser.GetPageAsync("stats"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task LoginOffersForgotPasswordOnlyWhenEmailIsConfigured(bool email)
    {
        var (_, browser) = Start(email: email);

        var html = await browser.GetPageAsync("account/login");

        Assert.Equal(email, html.Contains("account/forgotpassword"));
    }

    // --- Change password ----------------------------------------------------------------

    [Fact]
    public async Task ChangingThePasswordReplacesIt()
    {
        var (app, browser) = Start();
        await browser.RegisterAsync("alice");

        using var response = await browser.PostAsync("account/settings", new Dictionary<string, string>
        {
            { "currentPassword", Password },
            { "newPassword", "N3w-Passw0rd!" },
            { "confirmPassword", "N3w-Passw0rd!" },
        }, action: "account/settings?handler=ChangePassword");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(await CanSignInAsync(app, "alice", Password));
        Assert.True(await CanSignInAsync(app, "alice", "N3w-Passw0rd!"));
    }

    [Fact]
    public async Task ChangingThePasswordNeedsTheCurrentOne()
    {
        var (app, browser) = Start();
        await browser.RegisterAsync("alice");

        using var response = await browser.PostAsync("account/settings", new Dictionary<string, string>
        {
            { "currentPassword", "wrong" },
            { "newPassword", "N3w-Passw0rd!" },
            { "confirmPassword", "N3w-Passw0rd!" },
        }, action: "account/settings?handler=ChangePassword");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(await CanSignInAsync(app, "alice", Password));
        Assert.False(await CanSignInAsync(app, "alice", "N3w-Passw0rd!"));
    }

    // --- Change email -------------------------------------------------------------------

    [Fact]
    public async Task WithoutEmailConfiguredTheAddressChangesDirectly()
    {
        var (app, browser) = Start();
        await browser.RegisterAsync("alice");

        using var response = await browser.PostAsync("account/settings", new Dictionary<string, string>
        {
            { "newEmail", "alice@example.com" },
        }, action: "account/settings?handler=ChangeEmail");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("alice@example.com", (await FindAsync(app, "alice")).Email);
        Assert.Empty(app.Services.GetRequiredService<CapturingEmailSender>().Sent);
    }

    [Fact]
    public async Task WithEmailConfiguredTheAddressChangesOnlyAfterTheLinkIsFollowed()
    {
        var (app, browser) = Start(email: true);
        await RegisterConfirmedAsync(app, browser, "alice", "old@example.com");
        var sender = app.Services.GetRequiredService<CapturingEmailSender>();

        using var response = await browser.PostAsync("account/settings", new Dictionary<string, string>
        {
            { "newEmail", "new@example.com" },
        }, action: "account/settings?handler=ChangeEmail");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("old@example.com", (await FindAsync(app, "alice")).Email);
        var mail = sender.Sent.Last();
        Assert.Equal("new@example.com", mail.To);

        using var followed = await browser.GetAsync(PathOf(mail.Payload));
        Assert.Equal(HttpStatusCode.OK, followed.StatusCode);
        Assert.Equal("new@example.com", (await FindAsync(app, "alice")).Email);

        // The link is spent: replaying it after another change does nothing.
        await browser.PostAsync("account/settings", new Dictionary<string, string> { { "newEmail", "third@example.com" } },
            action: "account/settings?handler=ChangeEmail");
        using var again = await browser.GetAsync(PathOf(mail.Payload));
        Assert.Equal("new@example.com", (await FindAsync(app, "alice")).Email);
    }

    // --- Registration and confirmation --------------------------------------------------

    [Fact]
    public async Task RegisteringWithEmailConfiguredSendsAConfirmationAndBlocksSignInUntilFollowed()
    {
        var (app, browser) = Start(email: true);

        using var registered = await browser.PostAsync("account/register", new Dictionary<string, string>
        {
            { "UserName", "alice" },
            { "Email", "alice@example.com" },
            { "Password", Password },
            { "ConfirmPassword", Password },
        });

        Assert.Equal(HttpStatusCode.OK, registered.StatusCode);
        var mail = Assert.Single(app.Services.GetRequiredService<CapturingEmailSender>().Sent);
        Assert.Equal(("confirmation", "alice@example.com"), (mail.Kind, mail.To));
        Assert.False(await CanSignInAsync(app, "alice", Password));

        using var followed = await browser.GetAsync(PathOf(mail.Payload));
        Assert.Equal(HttpStatusCode.OK, followed.StatusCode);
        Assert.True((await FindAsync(app, "alice")).EmailConfirmed);
        Assert.True(await CanSignInAsync(app, "alice", Password));
    }

    [Fact]
    public async Task RegisteringNeedsAnEmailWhenEmailIsConfigured()
    {
        var (app, browser) = Start(email: true);

        using var registered = await browser.PostAsync("account/register", new Dictionary<string, string>
        {
            { "UserName", "alice" },
            { "Password", Password },
            { "ConfirmPassword", Password },
        });

        Assert.Equal(HttpStatusCode.OK, registered.StatusCode);
        Assert.Null(await FindAsync(app, "alice"));
    }

    [Fact]
    public async Task ABogusConfirmationLinkConfirmsNothing()
    {
        var (app, browser) = Start(email: true);
        await RegisterUnconfirmedAsync(browser, "alice", "alice@example.com");
        var alice = await FindAsync(app, "alice");

        using var response = await browser.GetAsync($"account/confirmemail?userId={alice.Id}&code=bm9wZQ");

        Assert.False((await FindAsync(app, "alice")).EmailConfirmed);
    }

    // --- Forgot / reset password --------------------------------------------------------

    [Fact]
    public async Task ForgotPasswordAnswersTheSameForKnownAndUnknownAccounts()
    {
        var (app, browser) = Start(email: true);
        await RegisterConfirmedAsync(app, browser, "alice", "alice@example.com");
        var sender = app.Services.GetRequiredService<CapturingEmailSender>();
        var before = sender.Sent.Count;

        using var known = await browser.PostAsync("account/forgotpassword", new Dictionary<string, string> { { "Identifier", "alice@example.com" } });
        var knownBody = await known.Content.ReadAsStringAsync();
        var afterKnown = sender.Sent.Count;
        using var unknown = await browser.PostAsync("account/forgotpassword", new Dictionary<string, string> { { "Identifier", "nobody@example.com" } });
        var unknownBody = await unknown.Content.ReadAsStringAsync();

        Assert.Equal(known.StatusCode, unknown.StatusCode);
        Assert.Equal(StripToken(knownBody).Replace("alice@example.com", "X"), StripToken(unknownBody).Replace("nobody@example.com", "X"));
        Assert.Equal(before + 1, afterKnown);
        Assert.Equal(afterKnown, sender.Sent.Count);
        Assert.Equal("reset-link", sender.Sent.Last().Kind);
    }

    [Fact]
    public async Task AResetLinkSetsANewPasswordOnceOnly()
    {
        var (app, browser) = Start(email: true);
        await RegisterConfirmedAsync(app, browser, "alice", "alice@example.com");
        await browser.PostAsync("account/forgotpassword", new Dictionary<string, string> { { "Identifier", "alice" } });
        var link = PathOf(app.Services.GetRequiredService<CapturingEmailSender>().Sent.Last().Payload);

        var form = await browser.GetPageAsync(link);
        Assert.Contains("name=\"Password\"", form);

        using var reset = await PostResetAsync(browser, link, "Fresh-Passw0rd!");
        Assert.Equal(HttpStatusCode.Redirect, reset.StatusCode);
        Assert.True(await CanSignInAsync(app, "alice", "Fresh-Passw0rd!"));

        using var replay = await PostResetAsync(browser, link, "Hijack-Passw0rd!");
        Assert.NotEqual(HttpStatusCode.Redirect, replay.StatusCode);
        Assert.False(await CanSignInAsync(app, "alice", "Hijack-Passw0rd!"));
        Assert.True(await CanSignInAsync(app, "alice", "Fresh-Passw0rd!"));
    }

    private static Task<HttpResponseMessage> PostResetAsync(Browser browser, string link, string password)
    {
        var query = System.Web.HttpUtility.ParseQueryString(new Uri("http://x/" + link).Query);

        return browser.PostAsync(link, new Dictionary<string, string>
        {
            { "UserId", query["userId"] },
            { "Code", query["code"] },
            { "Password", password },
            { "ConfirmPassword", password },
        });
    }

    private static string StripToken(string html)
        => Regex.Replace(html, "name=\"__RequestVerificationToken\"[^>]*>", "");

    // --- Two-factor ---------------------------------------------------------------------

    [Fact]
    public async Task TheTwoFactorPageShowsAnSvgQrCodeAndTheSharedKey()
    {
        var (app, browser) = Start();
        await browser.RegisterAsync("alice");

        var html = await browser.GetPageAsync("account/twofactor");

        Assert.Contains("<svg", html);
        var key = await KeyAsync(app);
        Assert.False(string.IsNullOrEmpty(key));
        Assert.Contains(key[..4], html);
    }

    [Fact]
    public async Task AWrongCodeDoesNotEnableTwoFactor()
    {
        var (app, browser) = Start();
        await browser.RegisterAsync("alice");
        await browser.GetPageAsync("account/twofactor");

        using var response = await browser.PostAsync("account/twofactor", new Dictionary<string, string> { { "code", "000000" } },
            action: "account/twofactor?handler=Enable");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False((await FindAsync(app, "alice")).TwoFactorEnabled);
    }

    [Fact]
    public async Task TwoFactorFullLifecycle()
    {
        var (app, browser) = Start();
        await browser.RegisterAsync("alice");
        await browser.GetPageAsync("account/twofactor");

        // Enable with a real TOTP code; ten recovery codes are shown once.
        using var enabled = await browser.PostAsync("account/twofactor", new Dictionary<string, string> { { "code", await CurrentCodeAsync(app) } },
            action: "account/twofactor?handler=Enable");
        Assert.Equal(HttpStatusCode.OK, enabled.StatusCode);
        Assert.True((await FindAsync(app, "alice")).TwoFactorEnabled);
        var codes = Regex
            .Matches(await enabled.Content.ReadAsStringAsync(), "<code class=\"recovery-code\">([^<]+)</code>")
            .Select(m => m.Groups[1].Value)
            .ToList();
        Assert.Equal(10, codes.Count);
        Assert.Equal(10, codes.Distinct().Count());

        // Sign-in now demands a second factor, and a password alone gets nowhere.
        var second = NewBrowser(app);
        using var first = await SignInAsync(second, "alice");
        Assert.Equal(HttpStatusCode.Redirect, first.StatusCode);
        Assert.Contains("loginwith2fa", first.Headers.Location.ToString().ToLowerInvariant());
        using var gated = await second.GetAsync("account/settings");
        Assert.Equal(HttpStatusCode.Redirect, gated.StatusCode);

        // A wrong code is refused, a right one signs in.
        using var wrong = await second.PostAsync("account/loginwith2fa", new Dictionary<string, string> { { "Code", "000000" } });
        Assert.Equal(HttpStatusCode.OK, wrong.StatusCode);
        using var right = await second.PostAsync("account/loginwith2fa", new Dictionary<string, string> { { "Code", await CurrentCodeAsync(app) } });
        Assert.Equal(HttpStatusCode.Redirect, right.StatusCode);
        using var inside = await second.GetAsync("account/settings");
        Assert.Equal(HttpStatusCode.OK, inside.StatusCode);

        // A recovery code signs in once.
        var third = NewBrowser(app);
        await SignInAsync(third, "alice");
        using var recovered = await third.PostAsync("account/loginwithrecoverycode", new Dictionary<string, string> { { "RecoveryCode", codes[0] } });
        Assert.Equal(HttpStatusCode.Redirect, recovered.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await third.GetAsync("account/settings")).StatusCode);

        var fourth = NewBrowser(app);
        await SignInAsync(fourth, "alice");
        using var reused = await fourth.PostAsync("account/loginwithrecoverycode", new Dictionary<string, string> { { "RecoveryCode", codes[0] } });
        Assert.Equal(HttpStatusCode.OK, reused.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await fourth.GetAsync("account/settings")).StatusCode);

        // Disabling needs the password, then sign-in is password-only again.
        using var refused = await second.PostAsync("account/twofactor", new Dictionary<string, string> { { "password", "wrong" } },
            action: "account/twofactor?handler=Disable");
        Assert.True((await FindAsync(app, "alice")).TwoFactorEnabled);
        using var disabled = await second.PostAsync("account/twofactor", new Dictionary<string, string> { { "password", Password } },
            action: "account/twofactor?handler=Disable");
        Assert.False((await FindAsync(app, "alice")).TwoFactorEnabled);
        Assert.True(await CanSignInAsync(app, "alice", Password));
    }

    [Fact]
    public async Task ResettingTwoFactorReplacesTheKeyAndTurnsItOff()
    {
        var (app, browser) = Start();
        await browser.RegisterAsync("alice");
        await browser.GetPageAsync("account/twofactor");
        await browser.PostAsync("account/twofactor", new Dictionary<string, string> { { "code", await CurrentCodeAsync(app) } },
            action: "account/twofactor?handler=Enable");
        var oldKey = await KeyAsync(app);

        using var reset = await browser.PostAsync("account/twofactor", new Dictionary<string, string> { { "password", Password } },
            action: "account/twofactor?handler=Reset");

        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
        Assert.False((await FindAsync(app, "alice")).TwoFactorEnabled);
        Assert.NotEqual(oldKey, await KeyAsync(app));
    }

    private static Task<string> KeyAsync(BaGetApplication app) => WithAsync(app, async p =>
    {
        var users = p.GetRequiredService<UserManager<BaGetUser>>();
        return await users.GetAuthenticatorKeyAsync(await users.FindByNameAsync("alice"));
    });

    private static async Task<string> CurrentCodeAsync(BaGetApplication app) => Totp(await KeyAsync(app));

    /// <summary>
    /// RFC 6238 with SHA-1, 30 seconds and 6 digits, over the user's Base32 authenticator
    /// key: what an authenticator app computes from the QR code.
    /// </summary>
    private static string Totp(string base32)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var bytes = new List<byte>();
        int buffer = 0, bits = 0;
        foreach (var c in base32.TrimEnd('=').ToUpperInvariant())
        {
            buffer = (buffer << 5) | alphabet.IndexOf(c);
            bits += 5;
            if (bits >= 8)
            {
                bits -= 8;
                bytes.Add((byte)((buffer >> bits) & 0xFF));
            }
        }

        var step = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30;
        var counter = BitConverter.GetBytes(step);
        if (BitConverter.IsLittleEndian) Array.Reverse(counter);

        using var hmac = new HMACSHA1(bytes.ToArray());
        var hash = hmac.ComputeHash(counter);
        var offset = hash[^1] & 0x0F;
        var value = ((hash[offset] & 0x7F) << 24) | (hash[offset + 1] << 16) | (hash[offset + 2] << 8) | hash[offset + 3];

        return (value % 1_000_000).ToString("D6");
    }

    // --- Delete account -----------------------------------------------------------------

    [Fact]
    public async Task DeletingNeedsThePassword()
    {
        var (app, browser) = Start();
        await browser.RegisterAsync("alice");

        using var response = await browser.PostAsync("account/settings", new Dictionary<string, string> { { "password", "wrong" } },
            action: "account/settings?handler=Delete");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(await FindAsync(app, "alice"));
    }

    [Fact]
    public async Task DeletingIsRefusedWhileTheUserIsTheLastOwnerOfAPackage()
    {
        var (app, browser) = Start();
        await browser.RegisterAsync("alice");
        await NewBrowser(app).RegisterAsync("bob");
        await AddOwnerAsync(app, "Shared.Pkg", "alice");
        await AddOwnerAsync(app, "Shared.Pkg", "bob");
        await AddOwnerAsync(app, "Alice.Only", "alice");

        using var response = await browser.PostAsync("account/settings", new Dictionary<string, string> { { "password", Password } },
            action: "account/settings?handler=Delete");

        var html = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Alice.Only", html);
        Assert.DoesNotContain("Shared.Pkg", html);
        Assert.NotNull(await FindAsync(app, "alice"));
    }

    [Fact]
    public async Task DeletingRemovesTheUserTheirKeysMembershipsAndOwnerships()
    {
        var (app, browser) = Start();
        await browser.RegisterAsync("alice");
        await NewBrowser(app).RegisterAsync("bob");
        var alice = await FindAsync(app, "alice");
        var bob = await FindAsync(app, "bob");
        await AddOwnerAsync(app, "Shared.Pkg", "alice");
        await AddOwnerAsync(app, "Shared.Pkg", "bob");
        await WithAsync(app, async p =>
        {
            await p.GetRequiredService<ApiKeyService>().CreateAsync(alice.Id, "ci", new[] { ApiKeyScopes.Push }, "*", null, CancellationToken.None);
            var ownership = p.GetRequiredService<PackageOwnershipService>();
            await ownership.CreateOrganizationAsync("Team", bob.Id, CancellationToken.None);
            await ownership.AddMemberAsync("Team", alice.Id, false, CancellationToken.None);
            return 0;
        });

        using var response = await browser.PostAsync("account/settings", new Dictionary<string, string> { { "password", Password } },
            action: "account/settings?handler=Delete");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Null(await FindAsync(app, "alice"));
        await WithAsync(app, async p =>
        {
            Assert.Empty(await p.GetRequiredService<ApiKeyService>().ListAsync(alice.Id, CancellationToken.None));
            var ownership = p.GetRequiredService<PackageOwnershipService>();
            Assert.Empty(await ownership.GetOrganizationsAsync(alice.Id, CancellationToken.None));
            Assert.Equal("bob", Assert.Single(await ownership.GetOwnersAsync("Shared.Pkg", CancellationToken.None)).OwnerName);
            // The name is free, so a newcomer under it inherits nothing.
            Assert.False(await ownership.AccountExistsAsync("alice", CancellationToken.None));
            return 0;
        });
        Assert.Equal(HttpStatusCode.Redirect, (await browser.GetAsync("account/settings")).StatusCode);
    }

    [Fact]
    public async Task DeletingIsRefusedForTheLastAdminOfAnOrganizationWithOtherMembers()
    {
        var (app, browser) = Start();
        await browser.RegisterAsync("alice");
        await NewBrowser(app).RegisterAsync("bob");
        var alice = await FindAsync(app, "alice");
        var bob = await FindAsync(app, "bob");
        await WithAsync(app, async p =>
        {
            var ownership = p.GetRequiredService<PackageOwnershipService>();
            await ownership.CreateOrganizationAsync("Team", alice.Id, CancellationToken.None);
            await ownership.AddMemberAsync("Team", bob.Id, false, CancellationToken.None);
            return 0;
        });

        using var response = await browser.PostAsync("account/settings", new Dictionary<string, string> { { "password", Password } },
            action: "account/settings?handler=Delete");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Team", await response.Content.ReadAsStringAsync());
        Assert.NotNull(await FindAsync(app, "alice"));
    }

    // --- Helpers ------------------------------------------------------------------------

    private static Task AddOwnerAsync(BaGetApplication app, string packageId, string owner)
        => WithAsync(app, async p =>
        {
            await p.GetRequiredService<PackageOwnershipService>().AddOwnerAsync(packageId, owner, CancellationToken.None);
            return 0;
        });

    private static async Task RegisterUnconfirmedAsync(Browser browser, string userName, string email)
    {
        using var response = await browser.PostAsync("account/register", new Dictionary<string, string>
        {
            { "UserName", userName },
            { "Email", email },
            { "Password", Password },
            { "ConfirmPassword", Password },
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task RegisterConfirmedAsync(BaGetApplication app, Browser browser, string userName, string email)
    {
        await RegisterUnconfirmedAsync(browser, userName, email);
        var mail = app.Services.GetRequiredService<CapturingEmailSender>().Sent.Last();
        using var followed = await browser.GetAsync(PathOf(mail.Payload));
        Assert.Equal(HttpStatusCode.OK, followed.StatusCode);
        using var signedIn = await SignInAsync(browser, userName);
        Assert.Equal(HttpStatusCode.Redirect, signedIn.StatusCode);
    }
}
