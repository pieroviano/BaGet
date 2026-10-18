using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Xunit;

namespace BaGet.Tests;

/// <summary>
/// The V2 (OData) feed. These drive the HTTP surface a V2 client actually calls -
/// see also <see cref="NuGetV2ClientIntegrationTests"/>, which drives NuGet's own
/// V2 client against it.
/// </summary>
public class V2FeedIntegrationTests : IDisposable
{
    private static readonly XNamespace Atom = "http://www.w3.org/2005/Atom";
    private static readonly XNamespace DataServices = "http://schemas.microsoft.com/ado/2007/08/dataservices";

    private readonly BaGetApplication _app;
    private readonly HttpClient _client;
    private readonly Stream? _packageStream;

    public V2FeedIntegrationTests(ITestOutputHelper output)
    {
        _app = new BaGetApplication(output);
        _client = _app.CreateDefaultClient();
        _packageStream = TestResources.GetResourceStream(TestResources.Package);
    }

    #pragma warning disable CS8602
    private static IEnumerable<XElement> Entries(string document)
        => XDocument.Parse(document).Root.Elements(Atom + "entry");
    #pragma warning restore CS8602

    private static string? PropertyOf(XElement entry, string name)
        => entry.Descendants(DataServices + name).FirstOrDefault()?.Value;

    [Fact]
    public async Task TheServiceDocumentAdvertisesThePackagesCollection()
    {
        using var response = await _client.GetAsync("api/v2");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var document = XDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Contains(
            "Packages",
            document.Descendants()
                .Where(e => e.Name.LocalName == "collection")
                .Select(e => e.Attribute("href")?.Value));
    }

    [Fact]
    public async Task TheMetadataDocumentParsesAndDescribesTheEntity()
    {
        using var response = await _client.GetAsync("api/v2/$metadata");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Parsing is the assertion: a malformed EDM breaks every V2 client.
        var document = XDocument.Parse(await response.Content.ReadAsStringAsync());

        var entityType = document.Descendants()
            .Single(e => e.Name.LocalName == "EntityType");
        #pragma warning disable CS8602
        Assert.Equal("V2FeedPackage", entityType.Attribute("Name").Value);
        #pragma warning restore CS8602

        #pragma warning disable CS8602
        var properties = entityType.Elements()
            .Where(e => e.Name.LocalName == "Property")
            .Select(e => e.Attribute("Name").Value)
            .ToList();
        #pragma warning restore CS8602

        Assert.Contains("Id", properties);
        Assert.Contains("Version", properties);
        Assert.Contains("IsLatestVersion", properties);
        Assert.Contains("Dependencies", properties);
    }

    [Fact]
    public async Task FindPackagesByIdReturnsEveryVersion()
    {
        await _app.AddPackageAsync(_packageStream);

        using var response = await _client.GetAsync("api/v2/FindPackagesById()?id='TestData'");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var entries = Entries(await response.Content.ReadAsStringAsync()).ToList();

        Assert.Single(entries);
        Assert.Equal("1.2.3", PropertyOf(entries[0], "NormalizedVersion"));
    }

    [Fact]
    public async Task FindPackagesByIdAcceptsAnUnquotedId()
    {
        await _app.AddPackageAsync(_packageStream);

        using var response = await _client.GetAsync("api/v2/FindPackagesById()?id=TestData");

        Assert.Single(Entries(await response.Content.ReadAsStringAsync()));
    }

    [Fact]
    public async Task FindPackagesByIdIsEmptyForAnUnknownId()
    {
        using var response = await _client.GetAsync("api/v2/FindPackagesById()?id='No.Such.Package'");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(Entries(await response.Content.ReadAsStringAsync()));
    }

    [Fact]
    public async Task AnEntryPointsAtADownloadablePackage()
    {
        await _app.AddPackageAsync(_packageStream);

        using var feed = await _client.GetAsync("api/v2/FindPackagesById()?id='TestData'");
        var entry = Entries(await feed.Content.ReadAsStringAsync()).Single();

        #pragma warning disable CS8602
        var source = entry.Element(Atom + "content").Attribute("src").Value;
        #pragma warning restore CS8602

        // The advertised content URL has to actually serve the nupkg.
        using var download = await _client.GetAsync(source);
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.NotEqual(0, (await download.Content.ReadAsByteArrayAsync()).Length);
    }

    [Fact]
    public async Task TheLatestVersionFlagsAreSet()
    {
        await _app.AddPackageAsync(_packageStream);

        using var response = await _client.GetAsync("api/v2/FindPackagesById()?id='TestData'");
        var entry = Entries(await response.Content.ReadAsStringAsync()).Single();

        // A single stable version is both the latest and the absolute latest.
        Assert.Equal("true", PropertyOf(entry, "IsLatestVersion"));
        Assert.Equal("true", PropertyOf(entry, "IsAbsoluteLatestVersion"));
        Assert.Equal("false", PropertyOf(entry, "IsPrerelease"));
    }

    [Fact]
    public async Task PackagesByKeyReturnsOneEntry()
    {
        await _app.AddPackageAsync(_packageStream);

        using var response = await _client.GetAsync("api/v2/Packages(Id='TestData',Version='1.2.3')");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var document = XDocument.Parse(await response.Content.ReadAsStringAsync());
        #pragma warning disable CS8602
        Assert.Equal("entry", document.Root.Name.LocalName);
        #pragma warning restore CS8602
        Assert.Equal("1.2.3", PropertyOf(document.Root, "NormalizedVersion"));
    }

    [Fact]
    public async Task PackagesByKeyIsNotFoundForAnUnknownVersion()
    {
        await _app.AddPackageAsync(_packageStream);

        using var response = await _client.GetAsync("api/v2/Packages(Id='TestData',Version='9.9.9')");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SearchFindsAPushedPackage()
    {
        await _app.AddPackageAsync(_packageStream);

        using var response = await _client.GetAsync("api/v2/Search()?searchTerm=''&includePrerelease=false");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var entries = Entries(await response.Content.ReadAsStringAsync()).ToList();

        #pragma warning disable CS8602
        Assert.Contains("TestData", entries.Select(e => e.Element(Atom + "title").Value));
        #pragma warning restore CS8602
    }

    [Fact]
    public async Task PackagesListsWhatTheFeedHolds()
    {
        await _app.AddPackageAsync(_packageStream);

        using var response = await _client.GetAsync("api/v2/Packages()");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotEmpty(Entries(await response.Content.ReadAsStringAsync()));
    }

    [Fact]
    public async Task TheFeedIsAtomEncoded()
    {
        using var response = await _client.GetAsync("api/v2/Packages()");

        #pragma warning disable CS8602
        Assert.Equal("application/atom+xml", response.Content.Headers.ContentType.MediaType);
        #pragma warning restore CS8602
    }

    public void Dispose()
    {
        _packageStream?.Dispose();
        _client?.Dispose();
        _app?.Dispose();
    }
}

/// <summary>
/// The V2 feed with "Feeds:EnableV2" switched off.
/// </summary>
public class V2FeedDisabledTests : IDisposable
{
    private readonly BaGetApplication _app;
    private readonly HttpClient _client;

    public V2FeedDisabledTests(ITestOutputHelper output)
    {
        _app = new BaGetApplication(
            output,
            configOverrides: new Dictionary<string, string>
            {
                { "Feeds:EnableV2", "false" },
            });

        _client = _app.CreateDefaultClient();
    }

    [Theory]
    [InlineData("api/v2")]
    [InlineData("api/v2/$metadata")]
    [InlineData("api/v2/Packages()")]
    [InlineData("api/v2/FindPackagesById()?id='TestData'")]
    public async Task TheFeedIsNotFound(string path)
    {
        using var response = await _client.GetAsync(path);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PushingStillWorks()
    {
        // Turning off the read feed must not turn off the publish endpoint, which
        // shares the "api/v2" prefix.
        using var stream = TestResources.GetResourceStream(TestResources.Package);

        await _app.AddPackageAsync(stream);

        using var response = await _client.GetAsync("v3/registration/testdata/index.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    public void Dispose()
    {
        _client?.Dispose();
        _app?.Dispose();
    }
}