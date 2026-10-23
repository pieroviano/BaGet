using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace BaGet.Tests;

/// <summary>
/// Vulnerability scanning of Docker repositories (P7): images are scanned by Trivy (a fake stands in for the executable), the
/// policy blocks a pull with <c>403 DENIED</c> and removes a pushed image that reaches the deploy threshold, and without Trivy images
/// are marked unscanned.
/// </summary>
public class ScanningDockerTests
{
    private const int Low = 0, High = 2, Critical = 3;

    private readonly ITestOutputHelper _output;

    public ScanningDockerTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static ScanHit Hit(string id, int severity, string component = "openssl", string version = "1.1.1") =>
        new(id, component, version, severity, "A flaw in " + component, "https://avd.example.test/" + id, "1.1.2");

    private async Task<(ScanningHarness Harness, FakeTrivyRunner Trivy)> StartAsync(bool configured = true)
    {
        var trivy = new FakeTrivyRunner();
        var settings = configured
            ? new Dictionary<string, string> { { "Scanning:TrivyPath", "trivy" }, { "Scanning:TrivyRegistryHost", "registry.test" } }
            : new Dictionary<string, string>();
        var harness = await ScanningHarness.StartAsync(
            _output,
            services =>
            {
                services.RemoveAll<ITrivyRunner>();
                services.AddSingleton<ITrivyRunner>(trivy);
            },
            settings);
        await harness.App.CreateDockerRepositoryAsync("libs", cancellationToken: TestContext.Current.CancellationToken);

        return (harness, trivy);
    }

    private static AuthenticationHeaderValue Basic() => new("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes("ci:" + ScanningHarness.Key)));

    private static async Task<HttpResponseMessage> SendAsync(ScanningHarness harness, HttpMethod method, string path, byte[]? body = null, string? contentType = null)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body != null)
        {
            request.Content = new ByteArrayContent(body);
            if (contentType != null) request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        }

        request.Headers.Authorization = Basic();

        return await harness.Client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static async Task PushBlobAsync(ScanningHarness harness, string image, byte[] content)
    {
        using var start = await SendAsync(harness, HttpMethod.Post, $"/v2/{image}/blobs/uploads/");
        Assert.Equal(HttpStatusCode.Accepted, start.StatusCode);
        using var finish = await SendAsync(harness, HttpMethod.Put, start.Headers.Location!.ToString() + "?digest=" + Uri.EscapeDataString(OciTestImage.Digest(content)), content, "application/octet-stream");
        Assert.Equal(HttpStatusCode.Created, finish.StatusCode);
    }

    private static async Task<HttpResponseMessage> PushImageAsync(ScanningHarness harness, string image, string tag, OciTestImage content)
    {
        await PushBlobAsync(harness, image, content.Config);
        foreach (var layer in content.Layers) await PushBlobAsync(harness, image, layer);

        return await SendAsync(harness, HttpMethod.Put, $"/v2/{image}/manifests/{tag}", content.Manifest, OciTestImage.ManifestType);
    }

    private static async Task<string> TextAsync(HttpResponseMessage response) => await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

    [Fact]
    public async Task AnImageIsScannedByTrivyUnderItsRegistryNameAndDigest()
    {
        var (harness, trivy) = await StartAsync();
        await using var _ = harness;
        trivy.Hits.Add(Hit("CVE-2024-0001", Critical));
        trivy.Hits.Add(Hit("CVE-2024-0002", Low, "zlib", "1.2.0"));
        var image = OciTestImage.Create("one");
        (await PushImageAsync(harness, "libs/app", "latest", image)).Dispose();

        var result = (await harness.ScanAsync("libs", "app", image.ManifestDigest))!;

        Assert.Equal("trivy", result.Scanner);
        Assert.Equal(Critical, result.Highest);
        Assert.Equal(2, result.FindingCount);
        Assert.Equal(new[] { "registry.test/libs/app@" + image.ManifestDigest }, trivy.References);
        var found = (await harness.ResultAsync("libs", "app", image.ManifestDigest))!;
        Assert.Equal("openssl", found.Findings!.First().Component);
        Assert.Equal("1.1.2", found.Findings!.First().FixedVersion);
        Assert.Equal("https://avd.example.test/CVE-2024-0001", found.Findings!.First().AdvisoryUrl);
    }

    [Fact]
    public async Task ABlockDownloadPolicyRefusesAManifestPullWith403DeniedAndTheReason()
    {
        var (harness, trivy) = await StartAsync();
        await using var _ = harness;
        trivy.Hits.Add(Hit("CVE-2024-0003", Critical));
        var vulnerable = OciTestImage.Create("vulnerable");
        (await PushImageAsync(harness, "libs/app", "latest", vulnerable)).Dispose();
        await harness.ScanAsync("libs", "app", vulnerable.ManifestDigest);
        await harness.SetPolicyAsync("libs", blockDownload: High);

        using var byTag = await SendAsync(harness, HttpMethod.Get, "/v2/libs/app/manifests/latest");
        using var byDigest = await SendAsync(harness, HttpMethod.Get, "/v2/libs/app/manifests/" + vulnerable.ManifestDigest);
        using var head = await SendAsync(harness, HttpMethod.Head, "/v2/libs/app/manifests/latest");

        foreach (var response in new[] { byTag, byDigest, head }) Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using var error = JsonDocument.Parse(await TextAsync(byTag));
        var entry = error.RootElement.GetProperty("errors")[0];
        Assert.Equal("DENIED", entry.GetProperty("code").GetString());
        Assert.Contains("CVE-2024-0003", entry.GetProperty("message").GetString());
    }

    [Fact]
    public async Task AnImageWithoutFindingsAboveTheThresholdIsPulledAsUsual()
    {
        var (harness, trivy) = await StartAsync();
        await using var _ = harness;
        trivy.Hits.Add(Hit("CVE-2024-0004", Low));
        var image = OciTestImage.Create("fine");
        (await PushImageAsync(harness, "libs/app", "latest", image)).Dispose();
        await harness.ScanAsync("libs", "app", image.ManifestDigest);
        await harness.SetPolicyAsync("libs", blockDownload: High);

        using var response = await SendAsync(harness, HttpMethod.Get, "/v2/libs/app/manifests/latest");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ABlockDeployPolicyScansAPushedImageAndRemovesItWhenItReachesTheThreshold()
    {
        var (harness, trivy) = await StartAsync();
        await using var _ = harness;
        trivy.Hits.Add(Hit("CVE-2024-0005", Critical));
        await harness.SetPolicyAsync("libs", blockDeploy: High);
        var image = OciTestImage.Create("rejected");

        using var put = await PushImageAsync(harness, "libs/app", "latest", image);

        Assert.Equal(HttpStatusCode.Forbidden, put.StatusCode);
        Assert.Contains("CVE-2024-0005", await TextAsync(put));
        // Pushed and judged straight away: nothing of it is left to pull.
        await harness.SetPolicyAsync("libs");
        using var pull = await SendAsync(harness, HttpMethod.Get, "/v2/libs/app/manifests/latest");
        Assert.Equal(HttpStatusCode.NotFound, pull.StatusCode);

        trivy.Hits.Clear();
        trivy.Hits.Add(Hit("CVE-2024-0006", Low));
        await harness.SetPolicyAsync("libs", blockDeploy: High);
        var fine = OciTestImage.Create("accepted");
        using var accepted = await PushImageAsync(harness, "libs/app", "latest", fine);
        Assert.Equal(HttpStatusCode.Created, accepted.StatusCode);
    }

    [Fact]
    public async Task WithoutTrivyImagesAreUnscannedAndNothingIsBlocked()
    {
        var (harness, trivy) = await StartAsync(configured: false);
        await using var _ = harness;
        var image = OciTestImage.Create("unscanned");
        await harness.SetPolicyAsync("libs", blockDownload: Low, blockDeploy: Low);
        using var put = await PushImageAsync(harness, "libs/app", "latest", image);

        var status = await harness.RunAsync("libs", sp => Task.FromResult(sp.GetRequiredService<IScanService>().GetEngineStatus()));
        var summary = await harness.RunAsync("libs", sp => sp.GetRequiredService<IScanService>().SummarizeAsync(TestContext.Current.CancellationToken));
        var scan = await Assert.ThrowsAsync<ScanUnavailableException>(() => harness.ScanAsync("libs", "app", image.ManifestDigest));
        using var pull = await SendAsync(harness, HttpMethod.Get, "/v2/libs/app/manifests/latest");

        Assert.Equal(HttpStatusCode.Created, put.StatusCode);
        Assert.False(status.Available);
        Assert.Contains("Trivy is not configured", status.Reason);
        Assert.Contains("Trivy is not configured", scan.Message);
        Assert.Equal(1, summary.Artifacts);
        Assert.Equal(1, summary.Unscanned);
        Assert.Equal(HttpStatusCode.OK, pull.StatusCode);
        Assert.Empty(trivy.References);
    }

    [Fact]
    public async Task ATrivyThatFailsLeavesAFailedResultAndTheImagePullable()
    {
        var (harness, trivy) = await StartAsync();
        await using var _ = harness;
        trivy.Failure = "Trivy failed (exit code 1): database unavailable";
        var image = OciTestImage.Create("failing");
        (await PushImageAsync(harness, "libs/app", "latest", image)).Dispose();
        await harness.SetPolicyAsync("libs", blockDownload: Low);

        var result = (await harness.ScanAsync("libs", "app", image.ManifestDigest))!;
        using var pull = await SendAsync(harness, HttpMethod.Get, "/v2/libs/app/manifests/latest");

        Assert.Null(result.ScannedAt);
        Assert.Contains("database unavailable", result.Error);
        Assert.Equal(HttpStatusCode.OK, pull.StatusCode);
    }

    [Fact]
    public async Task OnlyImageManifestsAreScannedNotIndexesOrArtifactsThatRefer()
    {
        var (harness, trivy) = await StartAsync();
        await using var _ = harness;
        var image = OciTestImage.Create("platform");
        (await PushImageAsync(harness, "libs/app", "latest", image)).Dispose();
        var signature = OciTestImage.Create("signature", subject: image.ManifestDigest, artifactType: "application/vnd.dev.cosign.artifact.sig.v1+json");
        (await PushImageAsync(harness, "libs/app", "sig", signature)).Dispose();

        var artifacts = await harness.RunAsync("libs", sp => sp.GetFormatService<IFormatScanner>(RepositoryFormat.Docker)!.ListArtifactsAsync(TestContext.Current.CancellationToken));

        Assert.Equal(new[] { image.ManifestDigest }, artifacts.Select(a => a.Version).ToArray());
        Assert.True(DockerFormatScanner.IsImage(OciTestImage.ManifestType, OciTestImage.ConfigType, null));
        Assert.False(DockerFormatScanner.IsImage(OciTestImage.IndexType, null, null));
        Assert.False(DockerFormatScanner.IsImage(OciTestImage.ManifestType, "application/vnd.dev.cosign.artifact.sig.v1+json", null));
        Assert.False(DockerFormatScanner.IsImage(OciTestImage.ManifestType, OciTestImage.ConfigType, "sha256:abc"));
    }
}
