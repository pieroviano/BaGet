using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Xunit;
using Xunit.Abstractions;

namespace BaGet.Tests;

/// <summary>
/// An account that signs in only through an external provider has no password to type again, and
/// its user name is not a secret, so a stolen session must not be able to change its email, add a
/// password, delete it or switch two-factor off. It has to sign in again with its provider first,
/// and that proof lasts a few minutes.
/// </summary>
public class AccountReauthenticationTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly List<IDisposable> _disposables = new List<IDisposable>();
    private readonly MutableTime _time = new MutableTime();

    public AccountReauthenticationTests(ITestOutputHelper output) => _output = output;

    public void Dispose()
    {
        foreach (var d in _disposables) d.Dispose();
    }

    private sealed class MutableTime : SystemTime
    {
        public DateTime Now { get; set; } = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public override DateTime UtcNow => Now;
    }

    /// <summary>An application with a provider, and "alice" signed in through it with no password.</summary>
    private async Task<(BaGetApplication App, Browser Browser, string UserId)> StartSignedInAsync()
    {
        var app = new BaGetApplication(
            _output,
            configOverrides: new Dictionary<string, string>
            {
                { "Accounts:Enabled", "true" },
                { "ApiKey", "" },
                { "Accounts:OpenIdConnect:Authority", "https://idp.example.com" },
                { "Accounts:OpenIdConnect:ClientId", "baget" },
                { "Accounts:OpenIdConnect:ClientSecret", "secret" },
            },
            configureServices: services =>
            {
                services.AddSingleton<SystemTime>(_time);
                services.AddTransient<IStartupFilter, ExternalCookieFilter>();
                services.PostConfigure<OpenIdConnectOptions>(
                    AccountsServiceCollectionExtensions.OpenIdConnectScheme,
                    options => options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(
                        new OpenIdConnectConfiguration
                        {
                            Issuer = "https://idp.example.com",
                            AuthorizationEndpoint = "https://idp.example.com/authorize",
                        }));
            });
        var browser = new Browser(app);
        _disposables.Add(app);
        _disposables.Add(browser);

        using (await browser.GetAsync("test/external?sub=sub-1&name=alice&email=alice@example.com&verified=true")) { }
        using (var callback = await browser.GetAsync("account/login?handler=Callback"))
        {
            Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        }

        var alice = await FindAsync(app);
        Assert.False(await HasPasswordAsync(app));

        return (app, browser, alice.Id);
    }

    private static async Task<BaGetUser> FindAsync(BaGetApplication app)
    {
        using var scope = app.Services.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<UserManager<BaGetUser>>().FindByNameAsync("alice");
    }

    private static async Task<bool> HasPasswordAsync(BaGetApplication app)
    {
        using var scope = app.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<BaGetUser>>();

        return await users.HasPasswordAsync(await users.FindByNameAsync("alice"));
    }

    /// <summary>The provider answers the re-authentication, then the browser comes back to the callback.</summary>
    private static async Task<HttpResponseMessage> ReauthenticateAsync(Browser browser, string xsrf, string sub = "sub-1")
    {
        using (await browser.GetAsync($"test/external?sub={sub}&name=alice&xsrf={Uri.EscapeDataString(xsrf)}")) { }

        return await browser.GetAsync("account/reauthenticate?handler=Callback");
    }

    private static Task<HttpResponseMessage> ChangeEmailAsync(Browser browser, string password)
        => browser.PostAsync(
            "account/settings",
            new Dictionary<string, string> { { "newEmail", "attacker@example.com" }, { "password", password } },
            action: "account/settings?handler=ChangeEmail");

    private static async Task<string> EmailAsync(BaGetApplication app) => (await FindAsync(app)).Email;

    private static Task<HttpResponseMessage> SetPasswordAsync(Browser browser)
        => browser.PostAsync(
            "account/settings",
            new Dictionary<string, string> { { "newPassword", "Passw0rd!" }, { "confirmPassword", "Passw0rd!" } },
            action: "account/settings?handler=ChangePassword");

    [Fact]
    public async Task TheUserNameNoLongerConfirmsAChangeOfEmail()
    {
        var (app, browser, _) = await StartSignedInAsync();

        using (await ChangeEmailAsync(browser, "alice")) { }
        using (await ChangeEmailAsync(browser, "")) { }

        Assert.Equal("alice@example.com", await EmailAsync(app));
    }

    [Fact]
    public async Task TheUserNameNoLongerConfirmsDeletingTheAccount()
    {
        var (app, browser, _) = await StartSignedInAsync();

        using (await browser.PostAsync(
                   "account/settings",
                   new Dictionary<string, string> { { "password", "alice" } },
                   action: "account/settings?handler=Delete")) { }

        Assert.NotNull(await FindAsync(app));
    }

    [Fact]
    public async Task AStolenSessionCannotSetAPasswordToGetPastTheCheck()
    {
        var (app, browser, _) = await StartSignedInAsync();

        using (await SetPasswordAsync(browser)) { }

        Assert.False(await HasPasswordAsync(app));
    }

    [Theory]
    [InlineData("Disable")]
    [InlineData("Reset")]
    public async Task TheUserNameNoLongerTurnsTwoFactorOffOrResetsIt(string handler)
    {
        var (app, browser, _) = await StartSignedInAsync();
        using (var scope = app.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<BaGetUser>>();
            var alice = await users.FindByNameAsync("alice");
            alice.TwoFactorEnabled = true;
            await users.UpdateAsync(alice);
        }

        using (await browser.PostAsync(
                   "account/twofactor",
                   new Dictionary<string, string> { { "password", "alice" } },
                   action: "account/twofactor?handler=" + handler)) { }

        Assert.True((await FindAsync(app)).TwoFactorEnabled);
    }

    [Fact]
    public async Task AFreshSignInWithTheProviderConfirmsTheActionsForAFewMinutes()
    {
        var (app, browser, id) = await StartSignedInAsync();

        using (var back = await ReauthenticateAsync(browser, id))
        {
            Assert.Equal(HttpStatusCode.Redirect, back.StatusCode);
        }

        // Whatever is typed does not matter now.
        using (await ChangeEmailAsync(browser, "")) { }
        Assert.Equal("attacker@example.com", await EmailAsync(app));
    }

    [Fact]
    public async Task TheProofLapsesAfterTheWindow()
    {
        var (app, browser, id) = await StartSignedInAsync();
        using (await ReauthenticateAsync(browser, id)) { }

        _time.Now += BaGet.Identity.Pages.Account.AccountPageModel.ReauthenticationWindow + TimeSpan.FromSeconds(1);
        using (await ChangeEmailAsync(browser, "")) { }

        Assert.Equal("alice@example.com", await EmailAsync(app));
    }

    [Fact]
    public async Task ThePasswordCanBeSetAfterReauthenticating()
    {
        var (app, browser, id) = await StartSignedInAsync();
        using (await ReauthenticateAsync(browser, id)) { }

        using (await SetPasswordAsync(browser)) { }

        Assert.True(await HasPasswordAsync(app));
    }

    [Fact]
    public async Task AnAnswerForAnotherAccountOfTheProviderIsNotAccepted()
    {
        var (app, browser, id) = await StartSignedInAsync();

        using (var back = await ReauthenticateAsync(browser, id, sub: "someone-else"))
        {
            Assert.Equal(HttpStatusCode.OK, back.StatusCode);
        }

        using (await ChangeEmailAsync(browser, "")) { }
        Assert.Equal("alice@example.com", await EmailAsync(app));
    }

    [Fact]
    public async Task AnAnswerToASignInStartedElsewhereIsNotAccepted()
    {
        var (app, browser, _) = await StartSignedInAsync();

        using (await ReauthenticateAsync(browser, "another-users-id")) { }
        using (await ChangeEmailAsync(browser, "")) { }

        Assert.Equal("alice@example.com", await EmailAsync(app));
    }

    [Fact]
    public async Task TheProviderIsAskedToAuthenticateAgain()
    {
        var (_, browser, _) = await StartSignedInAsync();

        using var response = await browser.PostAsync(
            "account/settings",
            new Dictionary<string, string>(),
            action: "account/reauthenticate",
            tokenPage: "account/settings");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location.ToString();
        Assert.StartsWith("https://idp.example.com/authorize", location);
        Assert.Contains("prompt=login", location);
        Assert.Contains("max_age=0", location);
    }

    [Fact]
    public async Task TheSettingsPageOffersSigningInAgainInsteadOfTypingTheUserName()
    {
        var (_, browser, id) = await StartSignedInAsync();

        var html = await browser.GetPageAsync("account/settings");
        Assert.Contains("formaction=\"/account/reauthenticate", html);
        Assert.DoesNotContain("Type your user name", html);

        using (await ReauthenticateAsync(browser, id)) { }
        Assert.Contains("signed in again", await browser.GetPageAsync("account/settings"));
    }
}
