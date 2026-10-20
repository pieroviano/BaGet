using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using Xunit;

namespace BaGet.Tests;

/// <summary>
/// Drives NuGet's own client against the BaGet test host with a V2 source address.
/// The core provider list contains both the V2 and V3 resource providers and picks by
/// the shape of the source, so pointing it at "api/v2" exercises the V2 providers -
/// the same code path "nuget list" and older Install-Package use. This is the evidence
/// that the hand-written OData feed is shaped the way a real client expects, rather
/// than merely being well-formed XML.
/// </summary>
public class NuGetV2ClientIntegrationTests : IDisposable
{
    private readonly BaGetApplication _app;
    private readonly HttpClient _client;
    private readonly Stream? _packageStream;

    private readonly SourceRepository _repository;
    private readonly SourceCacheContext _cache;
    private readonly NuGet.Common.ILogger _logger;
    private readonly CancellationToken _cancellationToken;

    public NuGetV2ClientIntegrationTests(ITestOutputHelper output)
    {
        _app = new BaGetApplication(output);
        _client = _app.CreateDefaultClient();
        _packageStream = TestResources.GetResourceStream(TestResources.Package);

        // A V2 source is the feed root, not a service index.
        var sourceUri = new Uri(_app.Server.BaseAddress, "api/v2");
        var packageSource = new PackageSource(sourceUri.AbsoluteUri);

        var providers = new List<Lazy<INuGetResourceProvider>>
        {
            new Lazy<INuGetResourceProvider>(() => new HttpSourceResourceProviderTestHost(_client)),
        };
        providers.AddRange(Repository.Provider.GetCoreV3());

        _repository = new SourceRepository(packageSource, providers);
        _cache = new SourceCacheContext { NoCache = true, MaxAge = new DateTimeOffset(), DirectDownload = true };
        _logger = NuGet.Common.NullLogger.Instance;
        _cancellationToken = CancellationToken.None;
    }

    [Fact]
    public async Task TheClientListsEveryVersionOfAPackage()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await _app.AddPackageAsync(_packageStream, cancellationToken);

        var resource = await _repository.GetResourceAsync<FindPackageByIdResource>(_cancellationToken);
        #pragma warning disable CS8602
        var versions = await resource.GetAllVersionsAsync(
            "TestData", _cache, _logger, _cancellationToken);
        #pragma warning restore CS8602

        Assert.Equal(new[] { "1.2.3" }, versions.Select(v => v.ToNormalizedString()));
    }

    [Fact]
    public async Task TheClientFindsNoVersionsForAnUnknownPackage()
    {
        var resource = await _repository.GetResourceAsync<FindPackageByIdResource>(_cancellationToken);
        #pragma warning disable CS8602
        var versions = await resource.GetAllVersionsAsync(
            "No.Such.Package", _cache, _logger, _cancellationToken);
        #pragma warning restore CS8602

        Assert.Empty(versions);
    }

    [Fact]
    public async Task TheClientReadsADependencyGroup()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await _app.AddPackageAsync(_packageStream, cancellationToken);

        var resource = await _repository.GetResourceAsync<FindPackageByIdResource>(_cancellationToken);
        #pragma warning disable CS8602
        var info = await resource.GetDependencyInfoAsync(
            "TestData", NuGetVersion.Parse("1.2.3"), _cache, _logger, _cancellationToken);
        #pragma warning restore CS8602

        Assert.NotNull(info);
        Assert.Equal("TestData", info.PackageIdentity.Id);
    }

    [Fact]
    public async Task TheClientDownloadsThePackage()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await _app.AddPackageAsync(_packageStream, cancellationToken);

        var resource = await _repository.GetResourceAsync<FindPackageByIdResource>(_cancellationToken);

        using var downloaded = new MemoryStream();
        #pragma warning disable CS8602
        var copied = await resource.CopyNupkgToStreamAsync(
            "TestData",
            NuGetVersion.Parse("1.2.3"),
            downloaded,
            _cache,
            _logger,
            _cancellationToken);
        #pragma warning restore CS8602

        Assert.True(copied);
        Assert.NotEqual(0, downloaded.Length);
    }

    [Fact]
    public async Task TheClientSearches()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await _app.AddPackageAsync(_packageStream, cancellationToken);

        var resource = await _repository.GetResourceAsync<PackageSearchResource>(_cancellationToken);
        #pragma warning disable CS8602
        var results = await resource.SearchAsync(
            "TestData",
            new SearchFilter(includePrerelease: false),
            skip: 0,
            take: 20,
            log: _logger,
            cancellationToken: _cancellationToken);
        #pragma warning restore CS8602

        Assert.Contains("TestData", results.Select(r => r.Identity.Id));
    }

    public void Dispose()
    {
        _packageStream?.Dispose();
        _client?.Dispose();
        _app?.Dispose();
    }
}