using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using BaGet.Protocol.Internal;
using Xunit;

namespace BaGet.Protocol.Tests
{
    public class RawSearchClientTests : IClassFixture<ProtocolFixture>
    {
        private readonly RawSearchClient _target;

        public RawSearchClientTests(ProtocolFixture fixture)
        {
            _target = fixture.SearchClient;
        }

        [Fact]
        public async Task GetDefaultSearchResults()
        {
            var response = await _target.SearchAsync();

            Assert.NotNull(response);
            Assert.Equal(1, response.TotalHits);

            var result = Assert.Single(response.Data);
            Assert.Equal("Test.Package", result.PackageId);
            Assert.Equal("Package Authors", Assert.Single(result.Authors));
            Assert.Equal(TestData.RegistrationIndexInlinedItemsUrl, result.RegistrationIndexUrl);

            var packageType = Assert.Single(result.PackageTypes);
            Assert.Equal("Dependency", packageType.Name);
        }

        [Fact]
        public async Task AddsDefaultParameters()
        {
            var handler = new RecordingHttpMessageHandler(TestData.DefaultSearch);
            var target = new RawSearchClient(new HttpClient(handler), TestData.SearchUrl);

            await target.SearchAsync();

            var url = Assert.Single(handler.Requests);
            var query = TestQueryString.Parse(url);

            Assert.Equal("test.example", url.Host);
            Assert.Equal("/v3/search", url.AbsolutePath);
            Assert.Equal(
                new[] { "prerelease", "semVerLevel", "take" },
                query.Keys.OrderBy(k => k).ToArray());
            Assert.Equal("20", query["take"]);
            Assert.Equal("True", query["prerelease"]);
            Assert.Equal("2.0.0", query["semVerLevel"]);
        }

        [Fact]
        public async Task AddsParameters()
        {
            var handler = new RecordingHttpMessageHandler(TestData.DefaultSearch);
            var target = new RawSearchClient(new HttpClient(handler), TestData.SearchUrl);

            await target.SearchAsync(
                "query",
                skip: 2,
                take: 5,
                includePrerelease: false,
                includeSemVer2: false);

            var url = Assert.Single(handler.Requests);
            var query = TestQueryString.Parse(url);

            Assert.Equal("/v3/search", url.AbsolutePath);
            Assert.Equal(
                new[] { "q", "skip", "take" },
                query.Keys.OrderBy(k => k).ToArray());
            Assert.Equal("2", query["skip"]);
            Assert.Equal("5", query["take"]);
            Assert.Equal("query", query["q"]);
        }

        [Fact]
        public async Task EscapesTheQuery()
        {
            var handler = new RecordingHttpMessageHandler(TestData.DefaultSearch);
            var target = new RawSearchClient(new HttpClient(handler), TestData.SearchUrl);

            await target.SearchAsync("packageid:Test.Package Foo&Bar");

            var url = Assert.Single(handler.Requests);
            var query = TestQueryString.Parse(url);

            Assert.DoesNotContain("Foo&Bar", url.AbsoluteUri);
            Assert.Equal("packageid:Test.Package Foo&Bar", query["q"]);
        }

        [Fact]
        public async Task SkipIsOmittedWhenZero()
        {
            var handler = new RecordingHttpMessageHandler(TestData.DefaultSearch);
            var target = new RawSearchClient(new HttpClient(handler), TestData.SearchUrl);

            await target.SearchAsync("query", skip: 0, take: 5);

            var url = Assert.Single(handler.Requests);
            var query = TestQueryString.Parse(url);

            Assert.DoesNotContain("skip", query.Keys);
            Assert.Equal("5", query["take"]);
        }
    }
}
