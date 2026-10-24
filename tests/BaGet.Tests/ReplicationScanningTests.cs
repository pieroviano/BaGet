using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace BaGet.Tests;

/// <summary>
/// A pull replication stores artifacts without going through the controller that checks the scanning policy, so the replicators must
/// apply the same deploy rules (NuGet: the dependency check before storing; Docker: the stored-image check, undone when refused).
/// </summary>
public class ReplicationScanningTests : IAsyncLifetime
{
    private const int High = 2, Critical = 3;

    private readonly ITestOutputHelper _output;
    private FakeOsvServer _osv = null!;
    private ReplicationHarness _h = null!;
    private FakeTrivyRunner _trivy = null!;

    public ReplicationScanningTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _osv = await FakeOsvServer.StartAsync();
        _trivy = new FakeTrivyRunner();
        _h = new ReplicationHarness(
            _output,
            new Dictionary<string, string>
            {
                { "Scanning:Enabled", "true" },
                { "Scanning:OsvUrl", _osv.Url },
                { "Scanning:RescanInterval", "00:00:00" },
                { "Scanning:ScanOnDeploy", "false" },
                { "Scanning:TrivyPath", "trivy" },
                { "Scanning:TrivyRegistryHost", "registry.test" },
            },
            services =>
            {
                services.RemoveAll<ITrivyRunner>();
                services.AddSingleton<ITrivyRunner>(_trivy);
            });
    }

    public async ValueTask DisposeAsync()
    {
        _h.Dispose();
        await _osv.DisposeAsync();
    }

    private async Task BlockDeploysAsync(string repository)
    {
        await _h.Local.WithAsync(async services =>
        {
            var key = (await services.GetRequiredService<IRepositoryService>().FindAsync(repository, Token))!.Key;
            await services.GetRequiredService<IScanPolicyService>()
                .SaveAsync(ScanPolicyScope.Repository, key, new ScanPolicyValues(null, null, High, false), "tester", Token);

            return true;
        });
    }

    [Fact]
    public async Task APullDoesNotStoreANuGetPackageTheDeployPolicyRefuses()
    {
        await _h.Peer.App.AddPackageAsync(TestPackages.Build("Vuln.Pkg", "1.0.0", dependencies: new[] { ("Newtonsoft.Json", "[9.0.1,)") }), Token);
        _osv.Add("NuGet", "Newtonsoft.Json", "9.0.1", "GHSA-json", "HIGH", "13.0.1");
        await BlockDeploysAsync(Repository.DefaultName);
        var key = await _h.CreateAsync(direction: ReplicationDirection.Pull);

        var run = await _h.RunAsync(key);

        Assert.Equal(1, run.Failures);
        Assert.Equal(0, run.Transferred);
        Assert.Contains("GHSA-json", run.Detail);
        Assert.Empty((await _h.InventoryAsync(_h.Local)).Items);
    }

    [Fact]
    public async Task APullStoresANuGetPackageTheDeployPolicyAccepts()
    {
        await _h.Peer.App.AddPackageAsync(TestPackages.Build("Clean.Pkg", "1.0.0", dependencies: new[] { ("Newtonsoft.Json", "[13.0.1,)") }), Token);
        await BlockDeploysAsync(Repository.DefaultName);
        var key = await _h.CreateAsync(direction: ReplicationDirection.Pull);

        var run = await _h.RunAsync(key);

        Assert.Equal(0, run.Failures);
        Assert.Equal(1, run.Transferred);
    }

    [Fact]
    public async Task APullRemovesADockerImageTheDeployPolicyRefuses()
    {
        await _h.Local.App.CreateDockerRepositoryAsync("img", cancellationToken: Token);
        await _h.Peer.App.CreateDockerRepositoryAsync("img", cancellationToken: Token);
        var bad = OciTestImage.Create("bad");
        await PushImageAsync(_h.Peer, "svc", "latest", bad);
        _trivy.Hits.Add(new ScanHit("CVE-2024-0005", "openssl", "1.1.1", Critical, "A flaw", "https://avd.example.test/CVE-2024-0005", "1.1.2"));
        await BlockDeploysAsync("img");
        var key = await _h.CreateAsync("d", "img", "img", ReplicationDirection.Pull);

        var run = await _h.RunAsync(key);

        Assert.Equal(1, run.Failures);
        using var scope = await _h.Local.App.CreateRepositoryScopeAsync("img", Token);
        var registry = scope.ServiceProvider.GetRequiredService<IOciRegistry>();
        Assert.Null(await registry.GetManifestAsync("svc", "latest", Token));
        Assert.Null(await registry.GetManifestAsync("svc", bad.ManifestDigest, Token));
    }

    [Fact]
    public async Task APullQueuesAScanForADockerImageTheDeployPolicyDoesNotRefuse()
    {
        await _h.Local.App.CreateDockerRepositoryAsync("img", cancellationToken: Token);
        await _h.Peer.App.CreateDockerRepositoryAsync("img", cancellationToken: Token);
        var image = OciTestImage.Create("fine");
        await PushImageAsync(_h.Peer, "svc", "latest", image);
        await BlockDeploysAsync("img");
        var key = await _h.CreateAsync("d", "img", "img", ReplicationDirection.Pull);

        var run = await _h.RunAsync(key);

        Assert.Equal(0, run.Failures);
        // Judged straight away, like a push: the result exists without waiting for a sweep.
        using var inner = await _h.Local.App.CreateRepositoryScopeAsync("img", Token);
        var result = await inner.ServiceProvider.GetRequiredService<IScanService>().FindAsync("svc", image.ManifestDigest, Token);
        Assert.NotNull(result?.ScannedAt);
    }

    [Fact]
    public async Task ARefusedSourceWinsReplacementKeepsTheLocalNpmVersion()
    {
        await _h.Local.App.CreateNpmRepositoryAsync("web", cancellationToken: Token);
        await _h.Peer.App.CreateNpmRepositoryAsync("web", cancellationToken: Token);
        var local = NpmTestPackages.Tarball("pkg", "1.0.0", description: "local build");
        var bad = NpmTestPackages.Tarball("pkg", "1.0.0", description: "peer build", dependencies: new Dictionary<string, string> { ["lodash"] = "4.17.0" });
        await PublishNpmAsync(_h.Local, local);
        await PublishNpmAsync(_h.Peer, bad);
        _osv.Add("npm", "lodash", "4.17.0", "GHSA-lodash", "HIGH", "4.17.21");
        await BlockDeploysAsync("web");
        var key = await _h.CreateAsync("c", "web", "web", ReplicationDirection.Pull, conflict: ReplicationConflict.SourceWins);

        var run = await _h.RunAsync(key);

        Assert.Equal(1, run.Failures);
        Assert.Equal(0, run.Transferred);
        Assert.Equal(local.Integrity, (await _h.InventoryAsync(_h.Local, "web")).Items.Single().Digest);
    }

    [Fact]
    public async Task ARefusedSourceWinsReplacementKeepsTheLocalDockerTag()
    {
        await _h.Local.App.CreateDockerRepositoryAsync("img", cancellationToken: Token);
        await _h.Peer.App.CreateDockerRepositoryAsync("img", cancellationToken: Token);
        var good = OciTestImage.Create("good");
        var bad = OciTestImage.Create("bad");
        await PushImageAsync(_h.Local, "svc", "latest", good);
        await PushImageAsync(_h.Peer, "svc", "latest", bad);
        _trivy.Hits.Add(new ScanHit("CVE-2024-0005", "openssl", "1.1.1", Critical, "A flaw", "https://avd.example.test/CVE-2024-0005", "1.1.2"));
        await BlockDeploysAsync("img");
        var key = await _h.CreateAsync("d", "img", "img", ReplicationDirection.Pull, conflict: ReplicationConflict.SourceWins);

        var run = await _h.RunAsync(key);

        Assert.Equal(1, run.Failures);
        using var scope = await _h.Local.App.CreateRepositoryScopeAsync("img", Token);
        var registry = scope.ServiceProvider.GetRequiredService<IOciRegistry>();
        var kept = await registry.GetManifestAsync("svc", "latest", Token);
        Assert.NotNull(kept);
        Assert.Equal(good.ManifestDigest, kept!.Digest);
    }

    private static async Task PublishNpmAsync(AdminApiHarness side, NpmTarballFile tarball)
    {
        using var request = NpmTestPackages.Json(System.Net.Http.HttpMethod.Put, NpmTestPackages.Path("/r/web/npm/", tarball.Name), NpmTestPackages.PublishBody(tarball), AdminApiHarness.GlobalKey);
        using var response = await side.Client.SendAsync(request, Token);
        Assert.Equal(System.Net.HttpStatusCode.Created, response.StatusCode);
    }

    private static async Task PushImageAsync(AdminApiHarness side, string image, string tag, OciTestImage built)
    {
        using var scope = await side.App.CreateRepositoryScopeAsync("img", Token);
        var registry = scope.ServiceProvider.GetRequiredService<IOciRegistry>();
        await registry.PutBlobAsync(image, built.ConfigDigest, new System.IO.MemoryStream(built.Config), Token);
        for (var i = 0; i < built.Layers.Count; i++) await registry.PutBlobAsync(image, built.LayerDigests[i], new System.IO.MemoryStream(built.Layers[i]), Token);
        await registry.PutManifestAsync(image, tag, OciTestImage.ManifestType, built.Manifest, Token);
    }
}
