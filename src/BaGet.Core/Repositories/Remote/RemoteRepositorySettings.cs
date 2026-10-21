using System;
using Microsoft.AspNetCore.DataProtection;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BaGet.Core;

/// <summary>
/// The protocol a remote repository's upstream speaks.
/// </summary>
public enum RemoteProtocol
{
    /// <summary>NuGet V3 (<c>index.json</c> service index).</summary>
    V3 = 0,

    /// <summary>NuGet V2 (OData/Atom feed).</summary>
    V2 = 1,
}

/// <summary>
/// What a <see cref="RepositoryKind.Remote"/> repository knows about its upstream: kept as JSON in
/// <see cref="Repository.Settings"/>. The password never appears in clear there: it is stored as
/// <see cref="ProtectedPassword"/> (see <see cref="IRemoteCredentialProtector"/>) and <see cref="Password"/> only
/// exists in memory.
/// </summary>
public sealed class RemoteRepositorySettings
{
    /// <summary>Longest accepted <see cref="Url"/>.</summary>
    public const int MaxUrlLength = 2048;

    /// <summary>The largest cache period accepted: one year.</summary>
    public const int MaxTtlSeconds = 365 * 24 * 3600;

    /// <summary>The upstream: a V3 <c>index.json</c> URL or a V2 feed URL (absolute http or https).</summary>
    public string? Url { get; set; }

    /// <summary>The protocol of <see cref="Url"/>.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public RemoteProtocol Protocol { get; set; } = RemoteProtocol.V3;

    /// <summary>The user name sent with HTTP Basic to the upstream's host, if any.</summary>
    public string? Username { get; set; }

    /// <summary>
    /// The clear password (or token). In memory only: it is not serialized; <see cref="ProtectedPassword"/> is.
    /// </summary>
    [JsonIgnore]
    public string? Password { get; set; }

    /// <summary>The password as stored (protected). Opaque to everything but <see cref="RemoteSettingsCodec"/>.</summary>
    public string? ProtectedPassword { get; set; }

    /// <summary>
    /// How long the upstream's version list and metadata of a package are reused before it is asked again. 0 asks
    /// every time (what the mirror always did).
    /// </summary>
    public int MetadataTtlSeconds { get; set; } = 600;

    /// <summary>How long "the upstream does not have it" (404) is remembered. 0 asks every time.</summary>
    public int NegativeTtlSeconds { get; set; } = 60;

    /// <summary>
    /// Offline mode: the upstream is never contacted; only what is already stored (and cached in memory) is served.
    /// </summary>
    public bool Offline { get; set; }

    /// <summary>
    /// Whether packages fetched from the upstream are stored (database and files) so later requests, and requests
    /// while the upstream is down, are served locally. Off, the repository is a pass-through: metadata comes from the
    /// upstream (cached by <see cref="MetadataTtlSeconds"/>), downloads stream through and nothing is kept (so search
    /// and the package pages stay empty).
    /// </summary>
    public bool StoreLocally { get; set; } = true;

    /// <summary>The time before a download from the upstream times out.</summary>
    public int DownloadTimeoutSeconds { get; set; } = 600;

    /// <summary>Whether a user name or password is configured.</summary>
    [JsonIgnore]
    public bool HasCredentials => !string.IsNullOrEmpty(Username) || !string.IsNullOrEmpty(Password);

    /// <summary>The upstream URL as a <see cref="Uri"/>, or null when it is missing or invalid.</summary>
    [JsonIgnore]
    public Uri? UrlOrNull => Uri.TryCreate(Url, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) ? uri : null;

    /// <summary>A copy that can be changed without affecting this one.</summary>
    public RemoteRepositorySettings Clone() => (RemoteRepositorySettings)MemberwiseClone();

    /// <summary>The settings the <c>Mirror:*</c> options describe (no cache periods: the mirror always asked).</summary>
    public static RemoteRepositorySettings FromMirror(MirrorOptions options)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));

        return new RemoteRepositorySettings
        {
            Url = options.PackageSource?.AbsoluteUri,
            Protocol = options.Legacy ? RemoteProtocol.V2 : RemoteProtocol.V3,
            MetadataTtlSeconds = 0,
            NegativeTtlSeconds = 0,
            Offline = false,
            StoreLocally = true,
            DownloadTimeoutSeconds = options.PackageDownloadTimeoutSeconds,
        };
    }

    /// <summary>The problems with these settings, empty when they are usable.</summary>
    public IReadOnlyList<string> Validate()
    {
        var problems = new List<string>();

        if (string.IsNullOrWhiteSpace(Url))
        {
            problems.Add("The upstream URL is required.");
        }
        else if (Url!.Length > MaxUrlLength || UrlOrNull == null)
        {
            problems.Add("The upstream URL must be an absolute http or https URL.");
        }
        else if (UrlOrNull!.UserInfo.Length > 0)
        {
            problems.Add("Put the credentials in the user name and password fields, not in the URL.");
        }
        else if (!string.IsNullOrEmpty(UrlOrNull.Query) && Protocol == RemoteProtocol.V2)
        {
            problems.Add("The V2 feed URL must not have a query string.");
        }

        if (!Enum.IsDefined(typeof(RemoteProtocol), Protocol)) problems.Add("Unknown protocol.");
        if (MetadataTtlSeconds < 0 || MetadataTtlSeconds > MaxTtlSeconds) problems.Add($"The metadata cache period must be between 0 and {MaxTtlSeconds} seconds.");
        if (NegativeTtlSeconds < 0 || NegativeTtlSeconds > MaxTtlSeconds) problems.Add($"The negative cache period must be between 0 and {MaxTtlSeconds} seconds.");
        if (DownloadTimeoutSeconds < 1 || DownloadTimeoutSeconds > 24 * 3600) problems.Add("The download timeout must be between 1 second and 24 hours.");
        if (Username != null && Username.Length > 256) problems.Add("The user name is too long.");
        if (Password != null && Password.Length > 4096) problems.Add("The password is too long.");
        if (!string.IsNullOrEmpty(Username) && Username!.Contains(':')) problems.Add("The user name cannot contain ':' (HTTP Basic).");

        return problems;
    }

    /// <summary>
    /// A key that changes when something that affects how the upstream is contacted changes, so a pooled client is
    /// rebuilt exactly then. Contains no secret (the password is hashed in).
    /// </summary>
    internal string TransportKey()
    {
        var password = Password ?? string.Empty;
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(password));

        return string.Join("|", Url, Protocol, Username, Convert.ToHexString(hash, 0, 8), DownloadTimeoutSeconds);
    }
}

/// <summary>
/// Turns <see cref="RemoteRepositorySettings"/> into the JSON kept in <see cref="Repository.Settings"/> and back,
/// protecting the password on the way out.
/// </summary>
public sealed class RemoteSettingsCodec
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly IRemoteCredentialProtector? _protector;

    /// <param name="protector">Protects passwords. Null refuses to store one.</param>
    public RemoteSettingsCodec(IRemoteCredentialProtector? protector = null)
    {
        _protector = protector;
    }

    /// <summary>
    /// The JSON to store. A <see cref="RemoteRepositorySettings.Password"/> is protected into
    /// <see cref="RemoteRepositorySettings.ProtectedPassword"/>; with none set, the already protected value is kept.
    /// </summary>
    /// <exception cref="InvalidOperationException">There is a password and nothing to protect it with.</exception>
    public string Serialize(RemoteRepositorySettings settings)
    {
        if (settings == null) throw new ArgumentNullException(nameof(settings));

        var copy = settings.Clone();
        if (!string.IsNullOrEmpty(copy.Password))
        {
            copy.ProtectedPassword = (_protector ?? throw new InvalidOperationException(
                "Credentials cannot be stored: no data protection is configured on this host."))
                .Protect(copy.Password!);
        }

        return JsonSerializer.Serialize(copy, Json);
    }

    /// <summary>
    /// Reads stored JSON. The password is unprotected; one that cannot be (keys lost) reads as absent rather than
    /// failing the repository.
    /// </summary>
    /// <returns>The settings, or null when <paramref name="json"/> is empty or not valid.</returns>
    public RemoteRepositorySettings? Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;

        RemoteRepositorySettings? settings;
        try
        {
            settings = JsonSerializer.Deserialize<RemoteRepositorySettings>(json, Json);
        }
        catch (JsonException)
        {
            return null;
        }

        if (settings != null && !string.IsNullOrEmpty(settings.ProtectedPassword) && _protector != null)
        {
            settings.Password = _protector.TryUnprotect(settings.ProtectedPassword!);
        }

        return settings;
    }
}

/// <summary>
/// Protects the passwords of remote repositories at rest.
/// </summary>
public interface IRemoteCredentialProtector
{
    /// <summary>Protects <paramref name="secret"/>.</summary>
    string Protect(string secret);

    /// <summary>The secret, or null if <paramref name="protectedValue"/> cannot be unprotected (keys lost or changed).</summary>
    string? TryUnprotect(string protectedValue);
}

/// <summary>
/// <see cref="IRemoteCredentialProtector"/> over ASP.NET Core data protection (the host's key ring).
/// </summary>
public sealed class DataProtectionCredentialProtector : IRemoteCredentialProtector
{
    private readonly Microsoft.AspNetCore.DataProtection.IDataProtector _protector;

    public DataProtectionCredentialProtector(Microsoft.AspNetCore.DataProtection.IDataProtectionProvider provider)
    {
        _protector = (provider ?? throw new ArgumentNullException(nameof(provider)))
            .CreateProtector("BaGet.RemoteRepositories.Credentials.v1");
    }

    /// <inheritdoc/>
    public string Protect(string secret) => _protector.Protect(secret);

    /// <inheritdoc/>
    public string? TryUnprotect(string protectedValue)
    {
        try
        {
            return _protector.Unprotect(protectedValue);
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return null;
        }
    }
}
