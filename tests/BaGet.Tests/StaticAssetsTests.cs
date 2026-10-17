using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace BaGet.Tests
{
    /// <summary>
    /// The layout's stylesheets, scripts and icon come from the Razor class libraries'
    /// static web assets. Their URLs must not depend on how the projects are packaged.
    /// </summary>
    public class StaticAssetsTests : IDisposable
    {
        private readonly BaGetApplication _app;
        private readonly HttpClient _client;

        public StaticAssetsTests(ITestOutputHelper output)
        {
            // Development, as Visual Studio runs it: the assets are served from the build
            // output's static web assets manifest rather than a published wwwroot.
            _app = new BaGetApplication(output, environment: "Development");
            _client = _app.CreateClient();
        }

        [Theory]
        [InlineData("")]
        [InlineData("upload")]
        [InlineData("stats")]
        public async Task EveryAssetThePageReferencesIsServed(string page)
        {
            var html = await _client.GetStringAsync(page);

            var assets = Regex.Matches(html, "(?:href|src)=\"(/_content/[^\"]+)\"")
                .Select(m => m.Groups[1].Value)
                .Distinct()
                .ToList();

            Assert.NotEmpty(assets);
            foreach (var asset in assets)
            {
                using var response = await _client.GetAsync(asset);

                Assert.True(response.StatusCode == HttpStatusCode.OK, $"{asset} answered {(int)response.StatusCode}");
            }
        }

        public void Dispose()
        {
            _client.Dispose();
            _app.Dispose();
        }
    }
}
