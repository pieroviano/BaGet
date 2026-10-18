using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BaGet.Protocol.Models;
using NuGet.Versioning;
using Xunit;

namespace BaGet.Protocol.Tests;

public class NuGetClientTests : IClassFixture<ProtocolFixture>
{
    private readonly NuGetClient _target;

    public NuGetClientTests(ProtocolFixture fixture)
    {
        _target = fixture.NuGetClient;
    }

    [Fact]
    public async Task DownloadsPackage()
    {
        using (var stream = await _target.DownloadPackageAsync("Test.Package", NuGetVersion.Parse("1.0.0")))
        using (var reader = new StreamReader(stream))
        {
            Assert.Equal(TestData.PackageContentNupkg, await reader.ReadToEndAsync());
        }
    }

    [Fact]
    public async Task ThrowsIfPackageToDownloadDoesNotExist()
    {
        var exception = await Assert.ThrowsAsync<PackageNotFoundException>(
            () => _target.DownloadPackageAsync("Test.Package", NuGetVersion.Parse("9.9.9")));

        Assert.Equal("Test.Package", exception.PackageId);
        Assert.Equal(NuGetVersion.Parse("9.9.9"), exception.PackageVersion);
    }

    [Fact]
    public async Task DownloadsPackageManifest()
    {
        using (var stream = await _target.DownloadPackageManifestAsync("Test.Package", NuGetVersion.Parse("1.0.0")))
        using (var reader = new StreamReader(stream))
        {
            Assert.Equal(TestData.PackageContentNuspec, await reader.ReadToEndAsync());
        }
    }

    [Fact]
    public async Task ThrowsIfPackageManifestToDownloadDoesNotExist()
    {
        var exception = await Assert.ThrowsAsync<PackageNotFoundException>(
            () => _target.DownloadPackageManifestAsync("Test.Package", NuGetVersion.Parse("9.9.9")));

        Assert.Equal("Test.Package", exception.PackageId);
        Assert.Equal(NuGetVersion.Parse("9.9.9"), exception.PackageVersion);
    }

    [Fact]
    public async Task ListsPackageVersions()
    {
        // The listed versions come from the package's registration index, which,
        // for this package, has its items inlined.
        var versions = await _target.ListPackageVersionsAsync("Test.Package");

        Assert.Equal(
            new[] { "1.0.0", "2.0.0+build", "3.0.0" },
            versions.Select(v => v.ToFullString()).ToArray());
    }

    [Fact]
    public async Task ListsPackageVersionsSkippingUnlistedVersions()
    {
        // "Paged.Package" has three versions, 2.0.0+build is unlisted.
        var versions = await _target.ListPackageVersionsAsync("Paged.Package");

        Assert.Equal(
            new[] { "1.0.0", "3.0.0" },
            versions.Select(v => v.ToFullString()).ToArray());
    }

    [Fact]
    public async Task ListsPackageVersionsOfPackageThatDoesNotExist()
    {
        var versions = await _target.ListPackageVersionsAsync(Guid.NewGuid().ToString());

        Assert.Empty(versions);
    }

    [Fact]
    public async Task ListsAllPackageVersions()
    {
        // Unlisted versions are only served by the package content resource,
        // which lists 2.0.0 even though the registration marks it unlisted.
        var versions = await _target.ListPackageVersionsAsync("Paged.Package", includeUnlisted: true);

        Assert.Equal(
            new[] { "1.0.0", "2.0.0", "3.0.0" },
            versions.Select(v => v.ToFullString()).ToArray());
    }

    [Fact]
    public async Task ListsAllPackageVersionsOfPackageThatDoesNotExist()
    {
        var versions = await _target.ListPackageVersionsAsync(
            Guid.NewGuid().ToString(),
            includeUnlisted: true);

        Assert.Empty(versions);
    }

    [Fact]
    public async Task GetsPackageMetadataInlined()
    {
        var metadata = await _target.GetPackageMetadataAsync("Test.Package");

        Assert.Equal(3, metadata.Count);
        Assert.All(metadata, m => Assert.Equal("Test.Package", m.PackageId));
        Assert.Equal(
            new[] { "1.0.0", "2.0.0+build", "3.0.0" },
            metadata.Select(m => m.Version).ToArray());
    }

    [Fact]
    public async Task GetsPackageMetadataPaged()
    {
        // "Paged.Package"'s registration index does not inline its items, so
        // each registration page has to be fetched separately.
        var metadata = await _target.GetPackageMetadataAsync("Paged.Package");

        Assert.Equal(3, metadata.Count);
        Assert.All(metadata, m => Assert.Equal("Paged.Package", m.PackageId));
        Assert.Equal(
            new[] { "1.0.0", "2.0.0+build", "3.0.0" },
            metadata.Select(m => m.Version).ToArray());
        Assert.Equal(
            new bool?[] { true, false, true },
            metadata.Select(m => m.Listed).ToArray());
    }

    [Fact]
    public async Task GetsPackageMetadataOfPackageThatDoesNotExist()
    {
        var metadata = await _target.GetPackageMetadataAsync(Guid.NewGuid().ToString());

        Assert.Empty(metadata);
    }

    [Fact]
    public async Task GetsSinglePackageMetadataInlined()
    {
        var metadata = await _target.GetPackageMetadataAsync("Test.Package", NuGetVersion.Parse("2.0.0"));

        #pragma warning disable CS8602
        Assert.Equal("Test.Package", metadata.PackageId);
        #pragma warning restore CS8602
        Assert.Equal("2.0.0+build", metadata.Version);
        Assert.Equal("Test Author", metadata.Authors);
    }

    [Fact]
    public async Task GetsSinglePackageMetadataPaged()
    {
        var metadata = await _target.GetPackageMetadataAsync("Paged.Package", NuGetVersion.Parse("3.0.0"));

        #pragma warning disable CS8602
        Assert.Equal("Paged.Package", metadata.PackageId);
        #pragma warning restore CS8602
        Assert.Equal("3.0.0", metadata.Version);
        Assert.True(metadata.Listed);
    }

    [Fact]
    public async Task ThrowsIfSinglePackageMetadataVersionDoesNotExist()
    {
        await Assert.ThrowsAsync<PackageNotFoundException>(
            () => _target.GetPackageMetadataAsync("Test.Package", NuGetVersion.Parse("9.9.9")));
    }

    [Fact]
    public async Task ThrowsIfSinglePackageMetadataPackageDoesNotExist()
    {
        await Assert.ThrowsAsync<PackageNotFoundException>(
            () => _target.GetPackageMetadataAsync(Guid.NewGuid().ToString(), NuGetVersion.Parse("1.0.0")));
    }

    [Fact]
    public async Task Searches()
    {
        var results = await _target.SearchAsync();

        #pragma warning disable CS8604
        var result = Assert.Single(results);
        #pragma warning restore CS8604

        Assert.Equal("Test.Package", result.PackageId);
        Assert.Equal("3.0.0", result.Version);
        #pragma warning disable CS8604
        Assert.Equal("Package Authors", Assert.Single(result.Authors));
        #pragma warning restore CS8604
    }

    [Fact]
    public async Task Autocompletes()
    {
        #pragma warning disable CS8602
        var results = await _target.AutocompleteAsync();
        #pragma warning restore CS8602

        #pragma warning disable CS8604
        Assert.Equal("Test.Package", Assert.Single(results));
        #pragma warning restore CS8604
    }

    [Fact]
    public async Task PackageExists()
    {
        Assert.True(await _target.ExistsAsync("Test.Package"));
        Assert.True(await _target.ExistsAsync("Test.Package", NuGetVersion.Parse("1.0.0")));
    }

    [Fact]
    public async Task PackageDoesNotExist()
    {
        Assert.False(await _target.ExistsAsync(Guid.NewGuid().ToString()));
        Assert.False(await _target.ExistsAsync("Test.Package", NuGetVersion.Parse("9.9.9")));
    }
}