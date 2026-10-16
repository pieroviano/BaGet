using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace BaGet.Tests
{
    /// <summary>
    /// The service index advertises a ReportAbuseUriTemplate, so the address a client
    /// expands that template into has to resolve to a page rather than a 404.
    /// </summary>
    public class ReportAbusePageTests : IDisposable
    {
        private readonly BaGetApplication _app;
        private readonly HttpClient _client;

        public ReportAbusePageTests(ITestOutputHelper output)
        {
            _app = new BaGetApplication(output);
            _client = _app.CreateDefaultClient();
        }

        [Fact]
        public async Task TheExpandedTemplateResolves()
        {
            using var response = await _client.GetAsync("packages/TestData/1.2.3/report-abuse");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task ThePageNamesThePackage()
        {
            using var response = await _client.GetAsync("packages/TestData/1.2.3/report-abuse");

            var content = await response.Content.ReadAsStringAsync();
            Assert.Contains("TestData", content);
            Assert.Contains("1.2.3", content);
        }

        [Fact]
        public async Task ItResolvesForAPackageThatDoesNotExist()
        {
            // A client expands the template from whatever it is looking at, so the page
            // must not depend on the package being present.
            using var response = await _client.GetAsync("packages/No.Such.Package/9.9.9/report-abuse");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task TheVersionLessUrlIsNotTheReportAbusePage()
        {
            // The package page owns "/packages/{id}/{version?}", so a three segment URL
            // is read as that page with "report-abuse" as the version, not as this page.
            // The template always supplies a version, so nothing expands to it.
            using var response = await _client.GetAsync("packages/TestData/report-abuse");

            var content = await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain("Report abuse", content);
        }

        public void Dispose()
        {
            _client?.Dispose();
            _app?.Dispose();
        }
    }
}
