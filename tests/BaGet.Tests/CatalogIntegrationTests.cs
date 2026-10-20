using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Catalog;
using BaGet.Core;
using BaGet.Protocol;
using BaGet.Protocol.Catalog;
using BaGet.Protocol.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BaGet.Tests;

/// <summary>
/// The catalog, read back both raw and through BaGet.Protocol's own catalog processor,
/// which is how a mirror consumes it.
/// </summary>
public class CatalogIntegrationTests : IDisposable
{
    private readonly BaGetApplication _app;
    private readonly HttpClient _client;
    private readonly Stream? _packageStream;

    public CatalogIntegrationTests(ITestOutputHelper output)
    {
        _app = new BaGetApplication(output, configOverrides: new Dictionary<string, string>
        {
            { "PackageDeletionBehavior", "HardDelete" },
        });
        _client = _app.CreateClient();
        _packageStream = TestResources.GetResourceStream(TestResources.Package);
    }

    [Fact]
    public async Task TheServiceIndexAdvertisesTheCatalog()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var index = await new NuGetClientFactory(_client, "http://localhost/v3/index.json")
            .CreateServiceIndexClient()
            .GetAsync(cancellationToken);

        #pragma warning disable CS8602, CS8604
        Assert.Equal(
            "http://localhost/v3/catalog/index.json",
            index.Resources.Single(r => r.Type == "Catalog/3.0.0").ResourceUrl);
        #pragma warning restore CS8602, CS8604
    }

    [Fact]
    public async Task AnEmptyCatalogHasNoPages()
    {
        using var index = await GetJsonAsync("v3/catalog/index.json");

        Assert.Equal(0, index.RootElement.GetProperty("count").GetInt32());
        Assert.Empty(index.RootElement.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task APushAppendsADetailsLeaf()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await _app.AddPackageAsync(_packageStream, cancellationToken);

        var leaves = await ProcessCatalogAsync();

        var leaf = Assert.IsType<PackageDetailsCatalogLeaf>(Assert.Single(leaves));
        Assert.Equal("TestData", leaf.PackageId);
        Assert.Equal("1.2.3", leaf.PackageVersion);
        Assert.True(leaf.Listed);
        Assert.Equal("SHA512", leaf.PackageHashAlgorithm);
        Assert.False(string.IsNullOrEmpty(leaf.PackageHash));
        Assert.True(leaf.PackageSize > 0);
        Assert.StartsWith("http://localhost/v3/catalog/data/", leaf.CatalogLeafUrl);
    }

    [Fact]
    public async Task ADeleteAppendsADeleteLeafAfterTheDetails()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await _app.AddPackageAsync(_packageStream, cancellationToken);

        using (var delete = await _client.DeleteAsync("api/v2/package/TestData/1.2.3", cancellationToken))
        {
            Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        }

        var leaves = await ProcessCatalogAsync();

        Assert.Collection(
            leaves,
            l => Assert.IsType<PackageDetailsCatalogLeaf>(l),
            l => Assert.Equal("TestData", Assert.IsType<PackageDeleteCatalogLeaf>(l).PackageId));
    }

    [Fact]
    public async Task CommitTimestampsStrictlyIncreaseEvenWhenTheClockDoesNot()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        // The test host's clock is frozen, which is the worst case for a cursor.
        await _app.AddPackageAsync(_packageStream, cancellationToken);
        using (var relist = await _client.PostAsync("api/v2/package/TestData/1.2.3", content: null, cancellationToken))
        {
            Assert.Equal(HttpStatusCode.OK, relist.StatusCode);
        }

        using var index = await GetJsonAsync("v3/catalog/index.json");
        using var page = await GetJsonAsync(index.RootElement.GetProperty("items")[0].GetProperty("@id").GetString());

        var timestamps = page.RootElement.GetProperty("items")
            .EnumerateArray()
            .Select(i => i.GetProperty("commitTimeStamp").GetDateTimeOffset())
            .ToList();

        Assert.Equal(2, timestamps.Count);
        Assert.True(timestamps[1] > timestamps[0]);
    }

    [Fact]
    public async Task RebuildBackfillsAPackageWithNoEntry()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await _app.AddPackageAsync(_packageStream, cancellationToken);

        using (var scope = _app.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IContext>();
            #pragma warning disable CS8602
            context.CatalogLeafs.RemoveRange(context.CatalogLeafs);
            #pragma warning restore CS8602
            await context.SaveChangesAsync(CancellationToken.None);
        }

        (int Added, int Deleted) result;
        using (var scope = _app.Services.CreateScope())
        {
            result = await scope.ServiceProvider.GetRequiredService<CatalogRebuilder>().RebuildAsync(CancellationToken.None);
        }

        Assert.Equal((1, 0), result);
        Assert.IsType<PackageDetailsCatalogLeaf>(Assert.Single(await ProcessCatalogAsync()));
    }

    [Fact]
    public async Task RebuildIsANoOpWhenTheCatalogIsComplete()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await _app.AddPackageAsync(_packageStream, cancellationToken);

        using var scope = _app.Services.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<CatalogRebuilder>().RebuildAsync(CancellationToken.None);

        Assert.Equal((0, 0), result);
    }

    [Theory]
    [InlineData("v3/catalog/page0.json")]
    [InlineData("v3/catalog/data/not-a-commit.json")]
    [InlineData("v3/catalog/data/00000000-0000-0000-0000-000000000000.json")]
    public async Task AMissingDocumentIsNotFound(string url)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var response = await _client.GetAsync(url, cancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<List<CatalogLeaf>> ProcessCatalogAsync()
    {
        var factory = new NuGetClientFactory(_client, "http://localhost/v3/index.json");
        var processor = new RecordingLeafProcessor();

        var catalog = new CatalogProcessor(
            new NullCursor(),
            factory.CreateCatalogClient(),
            processor,
            new CatalogProcessorOptions
            {
                MinCommitTimestamp = DateTimeOffset.MinValue,
                MaxCommitTimestamp = DateTimeOffset.MaxValue,
            },
            NullLogger<CatalogProcessor>.Instance);

        Assert.True(await catalog.ProcessAsync());

        return processor.Leaves;
    }

    private async Task<JsonDocument> GetJsonAsync(string? url)
    {
        using var response = await _client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    public void Dispose()
    {
        _app.Dispose();
        _client.Dispose();
        #pragma warning disable CS8602
        _packageStream.Dispose();
        #pragma warning restore CS8602
    }

    private class RecordingLeafProcessor : ICatalogLeafProcessor
    {
        public List<CatalogLeaf> Leaves { get; } = new List<CatalogLeaf>();

        public Task<bool> ProcessPackageDetailsAsync(PackageDetailsCatalogLeaf leaf, CancellationToken cancellationToken = default)
        {
            Leaves.Add(leaf);
            return Task.FromResult(true);
        }

        public Task<bool> ProcessPackageDeleteAsync(PackageDeleteCatalogLeaf leaf, CancellationToken cancellationToken = default)
        {
            Leaves.Add(leaf);
            return Task.FromResult(true);
        }
    }
}