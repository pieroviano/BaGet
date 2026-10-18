using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
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
/// The administrator role: who may reach the administrator pages, promoting and demoting,
/// and the rules that keep at least one administrator.
/// </summary>
public class AdministratorTests : IDisposable
{
    private const string Password = "Passw0rd!";

    private readonly ITestOutputHelper _output;
    private readonly List<IDisposable> _disposables = new List<IDisposable>();

    public AdministratorTests(ITestOutputHelper output) => _output = output;

    public void Dispose()
    {
        foreach (var d in _disposables) d.Dispose();
    }

    private (BaGetApplication App, Browser Browser) Start(bool accounts = true)
    {
        var app = new BaGetApplication(_output, configOverrides: new Dictionary<string, string>
        {
            { "Accounts:Enabled", accounts ? "true" : "false" },
            { "ApiKey", "" },
        });
        _disposables.Add(app);

        return (app, NewBrowser(app));
    }

    private Browser NewBrowser(BaGetApplication app)
    {
        var browser = new Browser(app);
        _disposables.Add(browser);

        return browser;
    }

    private static async Task<T> WithAsync<T>(BaGetApplication app, Func<IServiceProvider, Task<T>> action)
    {
        using var scope = app.Services.CreateScope();

        return await action(scope.ServiceProvider);
    }

    private static Task<AdministratorChange> PromoteAsync(BaGetApplication app, string userName)
        => WithAsync(app, p => p.GetRequiredService<AdministratorService>().PromoteAsync(userName, "test", CancellationToken.None));

    private static Task<AdministratorChange> DemoteAsync(BaGetApplication app, string userName)
        => WithAsync(app, p => p.GetRequiredService<AdministratorService>().DemoteAsync(userName, "test", CancellationToken.None));

    private static async Task<bool> IsAdministratorAsync(BaGetApplication app, string userName)
        => (await WithAsync(app, p => p.GetRequiredService<UserManager<BaGetUser>>().FindByNameAsync(userName))).IsAdministrator;

    private static Task<HttpResponseMessage> PostUsersAsync(Browser browser, string handler, string userName, string password = Password)
        => browser.PostAsync("admin/users", new Dictionary<string, string>
        {
            { "userName", userName },
            { "password", password },
        }, action: $"admin/users?handler={handler}", tokenPage: "account/settings");

    private async Task<(BaGetApplication App, Browser Admin)> StartWithAdministratorAsync()
    {
        var (app, admin) = Start();
        await admin.RegisterAsync("root");
        await PromoteAsync(app, "root");

        return (app, admin);
    }

    // --- Access -------------------------------------------------------------------------

    [Theory]
    [InlineData("admin/users")]
    [InlineData("admin/settings")]
    public async Task AdministratorPagesAreNotFoundWhileAccountsAreDisabled(string url)
    {
        var (_, browser) = Start(accounts: false);

        using var response = await browser.GetAsync(url);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("admin/users")]
    [InlineData("admin/settings")]
    public async Task ANonAdministratorGetsNotFound(string url)
    {
        var (_, browser) = Start();
        await browser.RegisterAsync("alice");

        using var response = await browser.GetAsync(url);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AnAnonymousVisitorIsAskedToSignIn()
    {
        var (_, browser) = Start();

        using var response = await browser.GetAsync("admin/users");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("account/login", response.Headers.Location.ToString());
    }

    [Fact]
    public async Task AnAdministratorSeesTheUsersPage()
    {
        var (app, admin) = await StartWithAdministratorAsync();
        using var other = NewBrowser(app);
        await other.RegisterAsync("alice");

        var html = await admin.GetPageAsync("admin/users");

        Assert.Contains("alice", html);
        Assert.Contains("root", html);
        Assert.Contains("label-info\">Administrator", html);
    }

    [Fact]
    public async Task TheUsersPageFiltersByNameAndClampsThePage()
    {
        var (app, admin) = await StartWithAdministratorAsync();
        using var other = NewBrowser(app);
        await other.RegisterAsync("alice");
        using var third = NewBrowser(app);
        await third.RegisterAsync("bob");

        var filtered = await admin.GetPageAsync("admin/users?q=ALI");

        Assert.Contains("alice", filtered);
        Assert.DoesNotContain(">bob<", filtered);

        // A page beyond the end shows the last one instead of an empty page.
        Assert.Contains("alice", await admin.GetPageAsync("admin/users?page=99"));
    }

    [Fact]
    public async Task TheMenuLinksAreShownOnlyToAdministrators()
    {
        var (app, admin) = await StartWithAdministratorAsync();
        using var user = NewBrowser(app);
        await user.RegisterAsync("alice");

        var adminHtml = await admin.GetPageAsync("stats");
        var userHtml = await user.GetPageAsync("stats");

        Assert.Contains("admin/users", adminHtml);
        Assert.Contains("Server settings", adminHtml);
        Assert.DoesNotContain("admin/users", userHtml);
        Assert.DoesNotContain("Server settings", userHtml);
    }

    [Fact]
    public async Task ADemotionTakesEffectOnTheNextRequest()
    {
        var (app, admin) = await StartWithAdministratorAsync();
        using var second = NewBrowser(app);
        await second.RegisterAsync("alice");
        await PromoteAsync(app, "alice");
        Assert.Equal(HttpStatusCode.OK, (await second.GetAsync("admin/users")).StatusCode);

        using var demotion = await PostUsersAsync(admin, "Demote", "alice");
        Assert.Equal(HttpStatusCode.Redirect, demotion.StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await second.GetAsync("admin/users")).StatusCode);
    }

    // --- Promote and demote -------------------------------------------------------------

    [Fact]
    public async Task AnAdministratorPromotesAUserWithTheirPassword()
    {
        var (app, admin) = await StartWithAdministratorAsync();
        using var other = NewBrowser(app);
        await other.RegisterAsync("alice");

        using var response = await PostUsersAsync(admin, "Promote", "alice");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.True(await IsAdministratorAsync(app, "alice"));
        Assert.Contains("alice is now an administrator.", await admin.GetPageAsync("admin/users"));
    }

    [Fact]
    public async Task APromotionWithAWrongPasswordIsRefused()
    {
        var (app, admin) = await StartWithAdministratorAsync();
        using var other = NewBrowser(app);
        await other.RegisterAsync("alice");

        using var response = await PostUsersAsync(admin, "Promote", "alice", password: "wrong");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.False(await IsAdministratorAsync(app, "alice"));
        Assert.Contains("confirmation is incorrect", await admin.GetPageAsync("admin/users"));
    }

    [Fact]
    public async Task PromotingAnUnknownUserReportsIt()
    {
        var (_, admin) = await StartWithAdministratorAsync();

        using var response = await PostUsersAsync(admin, "Promote", "nobody");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("There is no user with that name.", await admin.GetPageAsync("admin/users"));
    }

    [Fact]
    public async Task ANonAdministratorCannotPostPromote()
    {
        var (app, _) = await StartWithAdministratorAsync();
        using var other = NewBrowser(app);
        await other.RegisterAsync("alice");

        using var response = await PostUsersAsync(other, "Promote", "alice");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.False(await IsAdministratorAsync(app, "alice"));
    }

    [Fact]
    public async Task TheLastAdministratorCannotBeDemoted()
    {
        var (app, admin) = await StartWithAdministratorAsync();

        using var response = await PostUsersAsync(admin, "Demote", "root");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.True(await IsAdministratorAsync(app, "root"));
        Assert.Contains("last administrator cannot be demoted", await admin.GetPageAsync("admin/users"));
    }

    [Fact]
    public async Task AnAdministratorCanBeDemotedWhileAnotherRemains()
    {
        var (app, admin) = await StartWithAdministratorAsync();
        using var other = NewBrowser(app);
        await other.RegisterAsync("alice");
        await PromoteAsync(app, "alice");

        using var response = await PostUsersAsync(admin, "Demote", "root");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.False(await IsAdministratorAsync(app, "root"));
        Assert.True(await IsAdministratorAsync(app, "alice"));
    }

    [Fact]
    public async Task ThePasswordIsRequiredToDemoteToo()
    {
        var (app, admin) = await StartWithAdministratorAsync();
        using var other = NewBrowser(app);
        await other.RegisterAsync("alice");
        await PromoteAsync(app, "alice");

        using var response = await PostUsersAsync(admin, "Demote", "alice", password: "");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.True(await IsAdministratorAsync(app, "alice"));
    }

    // --- Deleting the account -----------------------------------------------------------

    [Fact]
    public async Task TheLastAdministratorCannotDeleteTheirAccount()
    {
        var (app, admin) = await StartWithAdministratorAsync();

        using var response = await admin.PostAsync("account/settings", new Dictionary<string, string>
        {
            { "password", Password },
        }, action: "account/settings?handler=Delete");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("You are the last administrator. Promote another user first.", await response.Content.ReadAsStringAsync());
        Assert.NotNull(await WithAsync(app, p => p.GetRequiredService<UserManager<BaGetUser>>().FindByNameAsync("root")));
    }

    [Fact]
    public async Task AnAdministratorCanDeleteTheirAccountWhileAnotherRemains()
    {
        var (app, admin) = await StartWithAdministratorAsync();
        using var other = NewBrowser(app);
        await other.RegisterAsync("alice");
        await PromoteAsync(app, "alice");

        using var response = await admin.PostAsync("account/settings", new Dictionary<string, string>
        {
            { "password", Password },
        }, action: "account/settings?handler=Delete");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Null(await WithAsync(app, p => p.GetRequiredService<UserManager<BaGetUser>>().FindByNameAsync("root")));
    }

    // --- The service --------------------------------------------------------------------

    [Fact]
    public async Task InitialAdministratorsAreOnlyPromotedWhenTheyExist()
    {
        var (app, browser) = Start();
        await browser.RegisterAsync("alice");
        using var other = NewBrowser(app);
        await other.RegisterAsync("bob");

        await WithAsync(app, async p =>
        {
            await p.GetRequiredService<AdministratorService>()
                .PromoteInitialAdministratorsAsync(" alice ; ghost;;", CancellationToken.None);

            return true;
        });

        Assert.True(await IsAdministratorAsync(app, "alice"));
        Assert.False(await IsAdministratorAsync(app, "bob"));
        Assert.Null(await WithAsync(app, p => p.GetRequiredService<UserManager<BaGetUser>>().FindByNameAsync("ghost")));
    }

    [Fact]
    public async Task InitialAdministratorsToleratesNoNames()
    {
        var (app, _) = Start();

        await WithAsync(app, async p =>
        {
            await p.GetRequiredService<AdministratorService>().PromoteInitialAdministratorsAsync(null, CancellationToken.None);

            return true;
        });
    }

    [Fact]
    public async Task ThePromoteAndDemoteResultsDescribeWhatHappened()
    {
        var (app, browser) = Start();
        await browser.RegisterAsync("alice");

        Assert.Equal(AdministratorChange.UserNotFound, await PromoteAsync(app, "ghost"));
        Assert.Equal(AdministratorChange.UserNotFound, await DemoteAsync(app, "ghost"));
        Assert.Equal(AdministratorChange.Changed, await PromoteAsync(app, "alice"));
        Assert.Equal(AdministratorChange.Unchanged, await PromoteAsync(app, "alice"));
        Assert.Equal(AdministratorChange.LastAdministrator, await DemoteAsync(app, "alice"));
    }

    [Fact]
    public async Task AccountsDisabledRegistersNoAdministratorService()
    {
        var (app, _) = Start(accounts: false);

        await WithAsync(app, p =>
        {
            Assert.Null(p.GetService<AdministratorService>());

            return Task.FromResult(true);
        });
    }
}
