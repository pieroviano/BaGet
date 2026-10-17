using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core.Tests.Support;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace BaGet.Core.Tests.Services
{
    public class StoragePackageStagingServiceTests
    {
        public class StageAsync : FactsBase
        {
            [Fact]
            public async Task StagesAnUploadItsOwnerCanReopen()
            {
                var token = await _target.StageAsync(StringStream("a package"), "alice", CancellationToken.None);

                using var reopened = await _target.OpenAsync(token, "alice", CancellationToken.None);
                Assert.Equal("a package", await ToStringAsync(reopened));
            }

            [Fact]
            public async Task TokensAreUnguessable()
            {
                var tokens = new List<string>();
                for (var i = 0; i < 8; i++)
                {
                    tokens.Add(await _target.StageAsync(StringStream("a package"), "alice", CancellationToken.None));
                }

                Assert.Equal(tokens.Count, tokens.Distinct(StringComparer.Ordinal).Count());
                foreach (var token in tokens)
                {
                    // 128 random bits, hex encoded: not a counter, a name, or a timestamp.
                    Assert.Equal(32, token.Length);
                    Assert.All(token, c => Assert.True("0123456789abcdef".Contains(c), c + " is not lowercase hex"));
                }
            }

            [Fact]
            public async Task RejectsANullPackage()
            {
                await Assert.ThrowsAsync<ArgumentNullException>(
                    () => _target.StageAsync(null, "alice", CancellationToken.None));
            }
        }

        public class OpenAsync : FactsBase
        {
            [Fact]
            public async Task AnotherUserCannotOpenAStagedUpload()
            {
                var token = await _target.StageAsync(StringStream("a staged package"), "alice", CancellationToken.None);

                Assert.Null(await _target.OpenAsync(token, "bob", CancellationToken.None));

                // Bob's attempt must not have consumed or damaged Alice's upload.
                using var reopened = await _target.OpenAsync(token, "alice", CancellationToken.None);
                Assert.Equal("a staged package", await ToStringAsync(reopened));
            }

            [Fact]
            public async Task AnUnknownTokenIsNull()
            {
                Assert.Null(await _target.OpenAsync("0123456789abcdef0123456789abcdef", "alice", CancellationToken.None));
            }

            [Fact]
            public async Task AMalformedTokenCannotReachOutsideStaging()
            {
                // A token becomes a storage path, so a caller-supplied one that is not a
                // token must be refused before it gets there.
                await _storage.PutAsync("packages/secret.nupkg", StringStream("not yours"), "application/octet-stream");

                var malformed = new[]
                {
                    null,
                    "",
                    " ",
                    "../packages/secret",
                    "..",
                    "ALICE",
                    "alice.json",
                    "0123456789abcdef0123456789abcdeff",
                };

                foreach (var token in malformed)
                {
                    Assert.Null(await _target.OpenAsync(token, "alice", CancellationToken.None));
                }
            }

            [Fact]
            public async Task AStoreThatForgetsTheUploadIsAnUnknownToken()
            {
                // "Storage:Type=Null" stores nothing and reads back nothing, so a staged
                // upload must report itself unknown rather than hand back a null stream.
                var target = new StoragePackageStagingService(
                    new NullStorageService(),
                    _time.Object,
                    new RecordingLogger<StoragePackageStagingService>());

                var token = await target.StageAsync(StringStream("a package"), "alice", CancellationToken.None);

                Assert.Null(await target.OpenAsync(token, "alice", CancellationToken.None));
            }
        }

        public class RemoveAsync : FactsBase
        {
            [Fact]
            public async Task ForgetsTheUploadAndLeavesNothingStored()
            {
                var token = await _target.StageAsync(StringStream("a package"), "alice", CancellationToken.None);

                await _target.RemoveAsync(token, CancellationToken.None);

                Assert.Null(await _target.OpenAsync(token, "alice", CancellationToken.None));
                Assert.Empty(await _storage.ListAsync("staging", CancellationToken.None));
            }

            [Fact]
            public async Task AnUnknownTokenIsNotAnError()
            {
                await _target.RemoveAsync("0123456789abcdef0123456789abcdef", CancellationToken.None);
                await _target.RemoveAsync("../packages/secret", CancellationToken.None);
            }
        }

        public class SweepAsync : FactsBase
        {
            [Fact]
            public async Task RemovesUploadsStagedBeforeTheCutoffOnly()
            {
                var abandoned = await _target.StageAsync(StringStream("abandoned"), "alice", CancellationToken.None);

                _now = _now.AddHours(12);
                var recent = await _target.StageAsync(StringStream("recent"), "alice", CancellationToken.None);

                var swept = await _target.SweepAsync(_now.AddHours(-6), CancellationToken.None);

                Assert.Equal(1, swept);
                Assert.Null(await _target.OpenAsync(abandoned, "alice", CancellationToken.None));

                using var kept = await _target.OpenAsync(recent, "alice", CancellationToken.None);
                Assert.Equal("recent", await ToStringAsync(kept));
            }

            [Fact]
            public async Task LeavesNothingBehindForWhatItSwept()
            {
                await _target.StageAsync(StringStream("abandoned"), "alice", CancellationToken.None);

                Assert.Equal(1, await _target.SweepAsync(_now.AddDays(1), CancellationToken.None));

                Assert.Empty(await _storage.ListAsync("staging", CancellationToken.None));
            }

            [Fact]
            public async Task SweepsNothingWhenStagingIsEmpty()
            {
                Assert.Equal(0, await _target.SweepAsync(_now.AddDays(1), CancellationToken.None));
            }

            [Fact]
            public async Task TouchesNothingOutsideStaging()
            {
                await _storage.PutAsync("packages/keep.nupkg", StringStream("keep"), "application/octet-stream");
                await _target.StageAsync(StringStream("abandoned"), "alice", CancellationToken.None);

                await _target.SweepAsync(_now.AddDays(1), CancellationToken.None);

                Assert.Equal(
                    "packages/keep.nupkg",
                    Assert.Single(await _storage.ListAsync("packages", CancellationToken.None)));
            }

            [Fact]
            public async Task SaysSoWhenTheStoreCannotBeSwept()
            {
                // A store that cannot list what it holds cannot be swept. Degrade loudly:
                // silence here means staged uploads accumulating unnoticed.
                var logger = new RecordingLogger<StoragePackageStagingService>();
                var target = new StoragePackageStagingService(Mock.Of<IStorageService>(), _time.Object, logger);

                Assert.Equal(0, await target.SweepAsync(_now, CancellationToken.None));

                var warning = Assert.Single(logger.Entries.Where(e => e.Level == LogLevel.Warning));
                Assert.Contains("sweep", warning.Message, StringComparison.OrdinalIgnoreCase);
            }
        }

        public class FactsBase : IDisposable
        {
            protected readonly string _storePath;
            protected readonly FileStorageService _storage;
            protected readonly Mock<SystemTime> _time;
            protected readonly StoragePackageStagingService _target;

            protected DateTime _now = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            public FactsBase()
            {
                _storePath = Path.Combine(Path.GetTempPath(), "BaGetStagingTests", Guid.NewGuid().ToString("N"));

                var options = new Mock<IOptionsSnapshot<FileSystemStorageOptions>>();
                options.Setup(o => o.Value).Returns(() => new FileSystemStorageOptions { Path = _storePath });

                _storage = new FileStorageService(options.Object);

                _time = new Mock<SystemTime>();
                _time.Setup(t => t.UtcNow).Returns(() => _now);

                _target = new StoragePackageStagingService(
                    _storage,
                    _time.Object,
                    new RecordingLogger<StoragePackageStagingService>());
            }

            protected Stream StringStream(string input) => new MemoryStream(Encoding.UTF8.GetBytes(input));

            protected async Task<string> ToStringAsync(Stream input)
            {
                using var reader = new StreamReader(input);

                return await reader.ReadToEndAsync();
            }

            public void Dispose()
            {
                try
                {
                    Directory.Delete(_storePath, recursive: true);
                }
                catch (DirectoryNotFoundException)
                {
                }
            }
        }
    }
}
