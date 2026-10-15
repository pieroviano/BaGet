using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace BaGet.Core.Tests.Authentication
{
    public class ApiKeyAuthenticationServiceTests
    {
        private static ApiKeyAuthenticationService Target(string configuredApiKey)
        {
            var options = new Mock<IOptionsSnapshot<BaGetOptions>>();
            options.Setup(o => o.Value).Returns(new BaGetOptions { ApiKey = configuredApiKey });

            return new ApiKeyAuthenticationService(options.Object);
        }

        private static Task<bool> Authenticate(string configuredApiKey, string providedApiKey)
            => Target(configuredApiKey).AuthenticateAsync(providedApiKey, CancellationToken.None);

        [Fact]
        public async Task AcceptsTheConfiguredApiKey()
        {
            Assert.True(await Authenticate("secret", "secret"));
        }

        [Fact]
        public async Task RejectsAWrongApiKey()
        {
            Assert.False(await Authenticate("secret", "wrong"));
        }

        [Fact]
        public async Task RejectsAMissingApiKeyWhenOneIsConfigured()
        {
            Assert.False(await Authenticate("secret", null));
            Assert.False(await Authenticate("secret", ""));
        }

        [Fact]
        public async Task ComparisonIsCaseSensitive()
        {
            Assert.False(await Authenticate("secret", "SECRET"));
        }

        [Fact]
        public async Task RejectsAnApiKeyWithSurroundingWhitespace()
        {
            Assert.False(await Authenticate("secret", " secret "));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task AcceptsAnythingWhenNoApiKeyIsConfigured(string configuredApiKey)
        {
            // Documented behaviour: an unset API key leaves the server open. This is what
            // makes package deletion succeed without a key on a default install.
            Assert.True(await Authenticate(configuredApiKey, "anything"));
            Assert.True(await Authenticate(configuredApiKey, null));
            Assert.True(await Authenticate(configuredApiKey, ""));
        }
    }
}
