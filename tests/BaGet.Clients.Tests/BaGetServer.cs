using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Web;
using BaGet.Core;
using BaGet.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace BaGet.Clients.Tests;

/// <summary>
/// The real BaGet application (<c>Program.BuildApplication</c>, SQLite and a file store in a temporary folder) listening on a
/// real socket on every interface, so a client in a container reaches it as <c>host.docker.internal:{port}</c>.
/// <c>WebApplicationFactory</c> has no socket, which is why the CLI tests cannot use it. Tarball and registry addresses are built
/// from the <c>Host</c> header the client sent, so they point back at this server.
/// </summary>
public sealed class BaGetServer : IAsyncDisposable
{
    /// <summary>The password of the users <see cref="CreateUserAsync"/> makes.</summary>
    public const string Password = "Passw0rd!";

    private readonly WebApplication _app;
    private readonly string _directory;

    private BaGetServer(WebApplication app, int port, string directory)
    {
        _app = app;
        Port = port;
        _directory = directory;
    }

    /// <summary>The TCP port.</summary>
    public int Port { get; }

    /// <summary>The application's services.</summary>
    public IServiceProvider Services => _app.Services;

    /// <summary><c>host.docker.internal:{port}</c>, as the container reaches the server.</summary>
    public string ContainerAuthority => $"{ClientContainers.HostAlias}:{Port}";

    /// <summary>The npm registry of repository <paramref name="repository"/> as the container addresses it (with a trailing slash).</summary>
    public string RegistryUrl(string repository) => $"http://{ContainerAuthority}/r/{repository}/npm/";

    /// <summary>The <c>.npmrc</c> key of the token for <paramref name="repository"/>'s registry.</summary>
    public string AuthKey(string repository) => $"//{ContainerAuthority}/r/{repository}/npm/:_authToken";

    /// <summary>Starts the application with <paramref name="settings"/> on top of a temporary SQLite database and file store.</summary>
    public static async Task<BaGetServer> StartAsync(IDictionary<string, string>? settings = null)
    {
        var directory = Path.Combine(Path.GetTempPath(), "BaGetClientTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        var all = new Dictionary<string, string>
        {
            ["Database:Type"] = "Sqlite",
            ["Database:ConnectionString"] = "Data Source=" + Path.Combine(directory, "BaGet.db"),
            ["Storage:Type"] = "FileSystem",
            ["Storage:Path"] = Path.Combine(directory, "Packages"),
            ["Search:Type"] = "Database",
            [SettingsOverridesStore.PathKey] = Path.Combine(directory, SettingsOverridesStore.DefaultFileName),
            ["Accounts:Enabled"] = "false",
            ["Accounts:CreateDefaultAdministrator"] = "false",
            ["Serilog:WriteTo:Console:Enabled"] = "false",
            ["Serilog:WriteTo:File:Enabled"] = "false",
            ["Serilog:EventLog:Enabled"] = "false",
            ["Kestrel:Endpoints:Http:Url"] = "http://0.0.0.0:0",
        };
        foreach (var pair in settings ?? new Dictionary<string, string>()) all[pair.Key] = pair.Value;

        var app = Program.BuildApplication(all.Select(p => $"--{p.Key}={p.Value}").ToArray());
        await app.RunMigrationsAsync(CancellationToken.None);
        await app.StartAsync();

        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();

        return new BaGetServer(app, new Uri(address.Replace("[::]", "localhost").Replace("0.0.0.0", "localhost")).Port, directory);
    }

    /// <summary>Creates a local npm repository.</summary>
    public async Task CreateNpmRepositoryAsync(string name, bool anonymousRead = true)
    {
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IRepositoryService>()
            .CreateAsync(name, RepositoryFormat.Npm, RepositoryKind.Local, new Dictionary<string, string?>(), anonymousRead, CancellationToken.None);
    }

    /// <summary>Creates a user with <see cref="Password"/>.</summary>
    public async Task CreateUserAsync(string name, bool administrator = false)
    {
        using var scope = Services.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<UserManager<BaGetUser>>().CreateAsync(
            new BaGetUser { UserName = name, IsAdministrator = administrator, Created = DateTime.UtcNow },
            Password);
        if (!result.Succeeded) throw new InvalidOperationException(string.Join(" ", result.Errors.Select(e => e.Description)));
    }

    /// <summary>Lets <paramref name="user"/> read everything in <paramref name="repository"/> (a private repository has no readers until someone is granted).</summary>
    public async Task GrantReadAsync(string user, string repository)
    {
        using var scope = Services.CreateScope();
        var account = await scope.ServiceProvider.GetRequiredService<UserManager<BaGetUser>>().FindByNameAsync(user) ?? throw new InvalidOperationException("No such user.");
        var access = scope.ServiceProvider.GetRequiredService<IAccessAdministration>();
        var target = $"{user} reads {repository}";
        await access.CreateTargetAsync(target, new[] { repository }, null, null, null, null, CancellationToken.None);
        await access.SetGrantAsync(target, PrincipalType.User, account.Id, AccessAction.Read, null, CancellationToken.None);
    }

    /// <summary>Makes an API key for a user (all scopes), optionally limited to repositories.</summary>
    public async Task<string> CreateKeyAsync(string user, params string[] repositories)
    {
        using var scope = Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<BaGetUser>>();
        var account = await users.FindByNameAsync(user) ?? throw new InvalidOperationException("No such user.");
        var service = scope.ServiceProvider.GetRequiredService<IRepositoryService>();
        var keys = new List<int>();
        foreach (var repository in repositories) keys.Add((await service.FindAsync(repository, CancellationToken.None))!.Key);

        return await scope.ServiceProvider.GetRequiredService<ApiKeyService>()
            .CreateAsync(account.Id, "test", ApiKeyScopes.All, "*", null, keys, CancellationToken.None);
    }

    /// <summary>The package, or null, as the registry holds it.</summary>
    public async Task<NpmPackageInfo?> PackageAsync(string repository, string name)
    {
        using var scope = Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IRepositoryContext>()
            .Use((await scope.ServiceProvider.GetRequiredService<IRepositoryService>().FindAsync(repository, CancellationToken.None))!);

        return await scope.ServiceProvider.GetRequiredService<INpmRegistry>().GetPackageAsync(name, CancellationToken.None);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
