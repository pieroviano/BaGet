using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Xunit;

namespace BaGet.Storage.Tests;

/// <summary>
/// The object-store emulators the provider tests run against (B2 of the Artifactory-parity plan): Azurite for Azure Blob, MinIO for
/// S3 and fake-gcs-server for Google Cloud Storage. Each starts once per run; a test that needs one skips (<see cref="Assert.Skip(string)"/>)
/// when there is no Docker daemon, as in <c>BaGet.Database.Tests</c>. Aliyun OSS has no emulator.
/// </summary>
public static class StorageContainers
{
    // Pinned: the emulators' behaviour (multipart, conditional writes, resumable sessions) is what the tests exercise.
    public const string AzuriteImage = "mcr.microsoft.com/azure-storage/azurite:3.35.0";
    public const string MinioImage = "minio/minio:RELEASE.2025-09-07T16-13-09Z";
    public const string FakeGcsImage = "fsouza/fake-gcs-server:1.52.2";

    public const string MinioUser = "minioadmin";
    public const string MinioPassword = "minioadmin";

    // The well-known development account of the Azure emulators.
    public const string AzuriteAccount = "devstoreaccount1";
    public const string AzuriteKey = "Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==";

    private static readonly Lazy<Task<IContainer>> Azurite = new(() => StartAsync(
        new ContainerBuilder(AzuriteImage)
            .WithCommand("azurite-blob", "--blobHost", "0.0.0.0", "--skipApiVersionCheck", "--loose")
            .WithPortBinding(10000, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("successfully listening"))
            .Build()));

    private static readonly Lazy<Task<IContainer>> Minio = new(() => StartAsync(
        new ContainerBuilder(MinioImage)
            .WithCommand("server", "/data")
            .WithEnvironment("MINIO_ROOT_USER", MinioUser)
            .WithEnvironment("MINIO_ROOT_PASSWORD", MinioPassword)
            .WithPortBinding(9000, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(9000).ForPath("/minio/health/ready")))
            .Build()));

    private static readonly Lazy<Task<(IContainer Container, int Port)>> FakeGcs = new(StartFakeGcsAsync);

    /// <summary>The blob endpoint of Azurite, <c>http://127.0.0.1:port/devstoreaccount1</c>.</summary>
    public static async Task<string> AzuriteBlobEndpointAsync()
    {
        var container = await Guard(() => Azurite.Value, "Azurite");
        return $"http://127.0.0.1:{container.GetMappedPublicPort(10000)}/{AzuriteAccount}";
    }

    /// <summary>The address of MinIO, <c>http://127.0.0.1:port</c>.</summary>
    public static async Task<string> MinioUrlAsync()
    {
        var container = await Guard(() => Minio.Value, "MinIO");
        return $"http://127.0.0.1:{container.GetMappedPublicPort(9000)}";
    }

    /// <summary>The address of fake-gcs-server, <c>http://127.0.0.1:port</c>.</summary>
    public static async Task<string> FakeGcsUrlAsync()
    {
        var started = await Guard(() => FakeGcs.Value, "fake-gcs-server");
        return $"http://127.0.0.1:{started.Port}";
    }

    private static async Task<(IContainer Container, int Port)> StartFakeGcsAsync()
    {
        // The server writes the address clients must use into the resumable-upload Location it returns, so the published port
        // must be the one it believes in: pick a free port and use it on both sides.
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();

        var container = new ContainerBuilder(FakeGcsImage)
            .WithCommand("-scheme", "http", "-port", port.ToString(), "-backend", "filesystem", "-filesystem-root", "/storage",
                "-external-url", $"http://127.0.0.1:{port}", "-public-host", $"127.0.0.1:{port}")
            .WithPortBinding(port, port)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("server started"))
            .Build();
        await container.StartAsync().ConfigureAwait(false);

        return (container, port);
    }

    private static async Task<IContainer> StartAsync(IContainer container)
    {
        await container.StartAsync().ConfigureAwait(false);
        return container;
    }

    private static async Task<T> Guard<T>(Func<Task<T>> start, string what)
    {
        try
        {
            return await start().ConfigureAwait(false);
        }
        catch (Exception e) when (IsDockerUnavailable(e))
        {
            Assert.Skip($"The {what} emulator needs Docker, which is not available: {e.GetBaseException().Message}");
            throw;
        }
    }

    private static bool IsDockerUnavailable(Exception e)
    {
        for (var inner = e; inner != null; inner = inner.InnerException)
        {
            if ((inner.GetType().FullName ?? string.Empty).Contains("Docker", StringComparison.OrdinalIgnoreCase)) return true;
        }

        return false;
    }
}
