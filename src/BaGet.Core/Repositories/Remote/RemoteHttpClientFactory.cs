using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BaGet.Core;

/// <summary>
/// Creates the <see cref="HttpClient"/> that talks to a remote repository's upstream. Replace it (a test, a host
/// with its own proxy or certificate rules) by registering another implementation.
/// </summary>
public interface IRemoteHttpClientFactory
{
    /// <summary>
    /// A client for <paramref name="settings"/>: the download timeout applied and, when credentials are configured,
    /// HTTP Basic sent to the upstream's own host only (never to a host a redirect or a service index points to).
    /// </summary>
    HttpClient Create(RemoteRepositorySettings settings);
}

/// <summary>
/// The default <see cref="IRemoteHttpClientFactory"/>.
/// </summary>
public class RemoteHttpClientFactory : IRemoteHttpClientFactory
{
    /// <inheritdoc/>
    public HttpClient Create(RemoteRepositorySettings settings)
    {
        if (settings == null) throw new ArgumentNullException(nameof(settings));

        var handler = CreateInnerHandler();
        if (settings.HasCredentials && settings.UrlOrNull != null)
        {
            handler = new HostScopedBasicAuthHandler(settings.UrlOrNull, settings.Username, settings.Password) { InnerHandler = handler };
        }

        var assembly = Assembly.GetEntryAssembly();
        var version = assembly?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";

        var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(settings.DownloadTimeoutSeconds),
        };
        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", $"{assembly?.GetName().Name ?? "BaGet"}/{version}");

        return client;
    }

    /// <summary>The handler that sends the requests; tests replace it with an in-memory one.</summary>
    protected virtual HttpMessageHandler CreateInnerHandler() => new HttpClientHandler
    {
        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
    };
}

/// <summary>
/// Adds HTTP Basic credentials to requests for one origin (scheme, host, port) and to no other.
/// </summary>
internal sealed class HostScopedBasicAuthHandler : DelegatingHandler
{
    private readonly Uri _origin;
    private readonly AuthenticationHeaderValue _header;

    public HostScopedBasicAuthHandler(Uri origin, string? username, string? password)
    {
        _origin = origin;
        _header = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes((username ?? string.Empty) + ":" + (password ?? string.Empty))));
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri;
        if (request.Headers.Authorization == null
            && uri != null
            && uri.IsAbsoluteUri
            && Uri.Compare(uri, _origin, UriComponents.SchemeAndServer, UriFormat.Unescaped, StringComparison.OrdinalIgnoreCase) == 0)
        {
            request.Headers.Authorization = _header;
        }

        return base.SendAsync(request, cancellationToken);
    }
}
