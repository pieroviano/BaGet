using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.HostFiltering;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;

namespace BaGet.Tests
{
    /// <summary>
    /// Every absolute URL BaGet advertises is built from the incoming request's "Host"
    /// header, so a forged host decides where clients are sent. The host must therefore
    /// be checked against the configured allow-list before a request reaches a handler,
    /// and the default must stay permissive: BaGet is routinely run behind a reverse
    /// proxy on an arbitrary host name.
    /// </summary>
    public class AllowedHostsIntegrationTests
    {
        private readonly ITestOutputHelper _output;

        public AllowedHostsIntegrationTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task ServesAnAllowedHost()
        {
            using var app = CreateApplication("baget.example.com");
            using var client = app.CreateDefaultClient();

            using var response = await client.GetAsync("http://baget.example.com/v3/index.json");

            response.EnsureSuccessStatusCode();
            var content = JObject.Parse(await response.Content.ReadAsStringAsync());

            foreach (var resource in content["resources"])
            {
                Assert.StartsWith(
                    "http://baget.example.com/",
                    resource["@id"].Value<string>());
            }
        }

        [Fact]
        public async Task RejectsAHostThatIsNotAllowed()
        {
            using var app = CreateApplication("baget.example.com");
            using var client = app.CreateDefaultClient();

            using var response = await client.GetAsync("http://attacker.example.com/v3/index.json");

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task ServesAnyHostWhenNothingIsConfigured()
        {
            using var app = CreateApplication(allowedHosts: null);
            using var client = app.CreateDefaultClient();

            using var response = await client.GetAsync("http://anything.example.com/v3/index.json");

            response.EnsureSuccessStatusCode();
            var content = JObject.Parse(await response.Content.ReadAsStringAsync());

            foreach (var resource in content["resources"])
            {
                Assert.StartsWith(
                    "http://anything.example.com/",
                    resource["@id"].Value<string>());
            }
        }

        [Fact]
        public async Task ServesEveryHostOfAWhitespaceSeparatedList()
        {
            // A semicolon separated list is normally written with spaces after the
            // separators. Those entries must not become unmatchable patterns.
            using var app = CreateApplication("baget.example.com; cdn.example.com");
            using var client = app.CreateDefaultClient();

            using var allowed = await client.GetAsync("http://cdn.example.com/v3/index.json");
            using var rejected = await client.GetAsync("http://attacker.example.com/v3/index.json");

            allowed.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        }

        [Fact]
        public async Task RejectsAForgedHostWithoutTheHostBuilderDefaults()
        {
            // ConfigureWebHostDefaults happens to add the host filtering middleware, but
            // BaGet must not depend on it: a consumer of AddBaGetWebApplication may build
            // its host with a slim or empty builder, which does not add it.
            using var app = new BaGetApplicationWithoutWebHostDefaults(
                _output,
                new Dictionary<string, string> { { "AllowedHosts", "baget.example.com" } });
            using var client = app.CreateDefaultClient();

            using var allowed = await client.GetAsync("http://baget.example.com/v3/index.json");
            using var rejected = await client.GetAsync("http://attacker.example.com/v3/index.json");

            allowed.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        }

        [Theory]
        [InlineData(null, "*")]
        [InlineData("", "*")]
        [InlineData("   ", "*")]
        [InlineData(";;", "*")]
        [InlineData("*", "*")]
        [InlineData("baget.example.com", "baget.example.com")]
        [InlineData(" baget.example.com ", "baget.example.com")]
        [InlineData("a.example.com; b.example.com", "a.example.com|b.example.com")]
        [InlineData("a.example.com;;b.example.com", "a.example.com|b.example.com")]
        [InlineData("a.example.com;A.EXAMPLE.COM", "a.example.com")]
        [InlineData("*.example.com;example.com:8080", "*.example.com|example.com:8080")]
        public void ParseAllowedHostsMapsConfiguredValues(string configured, string expected)
        {
            var hosts = ConfigureBaGetOptions.ParseAllowedHosts(configured);

            Assert.Equal(expected, string.Join("|", hosts));
        }

        [Theory]
        [InlineData(null, true)]
        [InlineData("*", true)]
        [InlineData("baget.example.com", false)]
        public void EmptyHostsAreOnlyToleratedWhileFilteringIsDisabled(
            string allowedHosts,
            bool expected)
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    { nameof(BaGetOptions.AllowedHosts), allowedHosts },
                })
                .Build();
            var options = new HostFilteringOptions();

            new ConfigureBaGetOptions(configuration).Configure(options);

            Assert.Equal(expected, options.AllowEmptyHosts);
        }

        private BaGetApplication CreateApplication(string allowedHosts)
        {
            var overrides = new Dictionary<string, string>();
            if (allowedHosts != null)
            {
                overrides["AllowedHosts"] = allowedHosts;
            }

            return new BaGetApplication(_output, configOverrides: overrides);
        }

        /// <summary>
        /// The same application, hosted without <c>ConfigureWebHostDefaults</c> and so
        /// without any of the middleware that it registers on BaGet's behalf.
        /// </summary>
        private sealed class BaGetApplicationWithoutWebHostDefaults : BaGetApplication
        {
            public BaGetApplicationWithoutWebHostDefaults(
                ITestOutputHelper output,
                Dictionary<string, string> configOverrides)
                : base(output, configOverrides: configOverrides)
            {
            }

            protected override IHostBuilder CreateHostBuilder() => Host.CreateDefaultBuilder();
        }
    }
}
