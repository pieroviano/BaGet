using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BaGet.Tests;

/// <summary>
/// Two first publishes of one new package: both find no package row, and the loser's insert of the row hits the unique index.
/// The race is made deterministic by publishing the other version from inside the loser's tarball store, i.e. after its lookup
/// and before its insert.
/// </summary>
public class NpmConcurrentPublishTests : IDisposable
{
    private readonly BaGetApplication _app;

    public NpmConcurrentPublishTests(ITestOutputHelper output)
    {
        _app = new BaGetApplication(output, configOverrides: new Dictionary<string, string> { { "ApiKey", "secret-key" } });
        _app.CreateNpmRepositoryAsync("web").GetAwaiter().GetResult();
    }

    public void Dispose() => _app.Dispose();

    private static NpmPublication Publication(string name, string version, NpmTarballFile tarball)
        => new(name, version, new JsonObject { ["name"] = name, ["version"] = version }, "latest", new MemoryStream(tarball.Bytes), tarball.Integrity, tarball.Shasum);

    /// <summary>Runs <paramref name="other"/> (a publish through another scope) when the loser stores its tarball.</summary>
    private async Task<(NpmResult Result, IServiceScope Scope)> PublishRacingAsync(string name, string version, Func<Task> other)
    {
        var scope = await _app.CreateRepositoryScopeAsync("web", TestContext.Current.CancellationToken);
        var storage = new RacingStorage(scope.ServiceProvider.GetRequiredService<IStorageService>(), other);
        var registry = ActivatorUtilities.CreateInstance<NpmRegistryService>(scope.ServiceProvider, storage);
        var result = await registry.PublishAsync(BaGetPrincipal.Superuser, Publication(name, version, NpmTestPackages.Tarball(name, version)), TestContext.Current.CancellationToken);

        return (result, scope);
    }

    private async Task PublishPlainAsync(string name, string version)
    {
        using var scope = await _app.CreateRepositoryScopeAsync("web", TestContext.Current.CancellationToken);
        var result = await scope.ServiceProvider.GetRequiredService<INpmRegistry>()
            .PublishAsync(BaGetPrincipal.Superuser, Publication(name, version, NpmTestPackages.Tarball(name, version)), TestContext.Current.CancellationToken);
        Assert.Equal(NpmStatus.Created, result.Status);
    }

    private async Task<bool> StoredAsync(string name, string version)
    {
        using var scope = await _app.CreateRepositoryScopeAsync("web", TestContext.Current.CancellationToken);
        var repository = scope.ServiceProvider.GetRequiredService<IRepositoryContext>().Current;
        try
        {
            (await scope.ServiceProvider.GetRequiredService<IStorageService>()
                .GetAsync(RepositoryStorage.Scope(repository, NpmNames.TarballPath(name, version)), CancellationToken.None)).Dispose();

            return true;
        }
        catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException)
        {
            return false;
        }
    }

    [Fact]
    public async Task TheLoserOfTheRaceForANewPackageAddsItsVersionToTheWinnersPackage()
    {
        var (result, scope) = await PublishRacingAsync("race-pkg", "2.0.0", () => PublishPlainAsync("race-pkg", "1.0.0"));
        using (scope)
        {
            Assert.True(result.Status == NpmStatus.Created, result.Status + ": " + result.Message);

            var package = await scope.ServiceProvider.GetRequiredService<INpmRegistry>().GetPackageAsync("race-pkg", TestContext.Current.CancellationToken);
            Assert.Equal(new[] { "1.0.0", "2.0.0" }, package!.Versions.Select(v => v.Version).OrderBy(v => v));
            Assert.Equal("2.0.0", package.DistTags["latest"]);
        }

        Assert.True(await StoredAsync("race-pkg", "1.0.0"));
        Assert.True(await StoredAsync("race-pkg", "2.0.0"));
    }

    [Fact]
    public async Task TheLoserOfTheRaceForTheSameVersionIsAConflictAndTheWinnersTarballStays()
    {
        var (result, scope) = await PublishRacingAsync("race-same", "1.0.0", () => PublishPlainAsync("race-same", "1.0.0"));
        using (scope)
        {
            Assert.Equal(NpmStatus.Conflict, result.Status);
            Assert.Contains("1.0.0", result.Message);
            var package = await scope.ServiceProvider.GetRequiredService<INpmRegistry>().GetPackageAsync("race-same", TestContext.Current.CancellationToken);
            Assert.Equal("1.0.0", Assert.Single(package!.Versions).Version);
        }

        Assert.True(await StoredAsync("race-same", "1.0.0"));
    }

    private sealed class RacingStorage : IStorageService
    {
        private readonly IStorageService _inner;
        private Func<Task>? _beforeFirstPut;

        public RacingStorage(IStorageService inner, Func<Task> beforeFirstPut)
        {
            _inner = inner;
            _beforeFirstPut = beforeFirstPut;
        }

        public Task<Stream> GetAsync(string path, CancellationToken cancellationToken = default) => _inner.GetAsync(path, cancellationToken);

        public Task<Uri> GetDownloadUriAsync(string path, CancellationToken cancellationToken = default) => _inner.GetDownloadUriAsync(path, cancellationToken);

        public async Task<StoragePutResult> PutAsync(string path, Stream content, string contentType, CancellationToken cancellationToken = default)
        {
            var before = Interlocked.Exchange(ref _beforeFirstPut, null);
            if (before != null) await before();

            return await _inner.PutAsync(path, content, contentType, cancellationToken);
        }

        public Task DeleteAsync(string path, CancellationToken cancellationToken = default) => _inner.DeleteAsync(path, cancellationToken);
    }
}
