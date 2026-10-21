using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Versioning;

namespace BaGet.Core;

/// <summary>
/// A remote repository as the administration pages edit it.
/// </summary>
/// <param name="Repository">The repository.</param>
/// <param name="Settings">Its upstream settings. <see cref="RemoteRepositorySettings.Password"/> is always null here: the password is never read back to a page.</param>
/// <param name="HasPassword">Whether a password is stored.</param>
/// <param name="IsMirror">Whether it is the <see cref="Repository.MirrorName"/> repository the default repository fetches through while <c>Mirror:Enabled</c> is set (its URL, protocol and timeout then come from <c>Mirror:*</c>).</param>
public sealed record RemoteRepositoryDetail(
    RepositoryInfo Repository,
    RemoteRepositorySettings Settings,
    bool HasPassword,
    bool IsMirror);

/// <summary>
/// The outcome of asking an upstream whether it answers.
/// </summary>
/// <param name="Success">Whether the upstream answered as the configured protocol requires.</param>
/// <param name="Message">What was found or what failed, without URLs or credentials.</param>
/// <param name="StatusCode">The HTTP status, when there was an answer.</param>
/// <param name="ElapsedMilliseconds">How long the attempt took.</param>
public sealed record RemoteConnectionResult(bool Success, string Message, int? StatusCode, long ElapsedMilliseconds);

/// <summary>
/// What a remote repository holds and remembers.
/// </summary>
/// <param name="Status">The in-memory cache and upstream health (this node).</param>
/// <param name="PackageCount">Package versions stored.</param>
/// <param name="PackageIdCount">Distinct package ids stored.</param>
public sealed record RemoteCacheOverview(RemoteCacheStatus Status, int PackageCount, int PackageIdCount);

/// <summary>
/// What a purge removed.
/// </summary>
/// <param name="Packages">Stored package versions deleted (database rows and files).</param>
/// <param name="CachedAnswers">Remembered upstream answers dropped.</param>
public sealed record PurgeResult(int Packages, int CachedAnswers);

/// <summary>
/// Edits, tests and empties remote repositories (creating goes through <see cref="IRepositoryService.CreateAsync(string, RepositoryFormat, RepositoryKind, RemoteRepositorySettings?, CancellationToken)"/>).
/// What the administrator pages use; writes go through the database context, so they are refused while the
/// <see cref="MaintenanceGate"/> is closed.
/// </summary>
public interface IRemoteRepositoryAdministration
{
    /// <summary>A remote repository by exact name, or null when there is none (or it is not remote).</summary>
    Task<RemoteRepositoryDetail?> FindAsync(string? name, CancellationToken cancellationToken);

    /// <summary>
    /// Saves the upstream settings of a remote repository and forgets what was cached from the old ones. A blank
    /// <see cref="RemoteRepositorySettings.Password"/> keeps the stored one unless <paramref name="clearPassword"/>.
    /// Audited (the audit entry says whether the password changed, never what it is).
    /// </summary>
    /// <exception cref="KeyNotFoundException">No such remote repository.</exception>
    /// <exception cref="ArgumentException">The settings are invalid; the message lists the problems.</exception>
    /// <exception cref="InvalidOperationException">A password cannot be stored (no data protection).</exception>
    Task UpdateAsync(string name, RemoteRepositorySettings input, bool clearPassword, CancellationToken cancellationToken, string? actor = null);

    /// <summary>
    /// Asks the upstream described by <paramref name="input"/> (unsaved) whether it answers, sending the credentials
    /// as a client would. When <paramref name="name"/> is given a blank password means the stored one. Saves nothing.
    /// </summary>
    Task<RemoteConnectionResult> TestConnectionAsync(string? name, RemoteRepositorySettings input, CancellationToken cancellationToken);

    /// <summary>What the repository stores and what this node remembers about its upstream.</summary>
    /// <exception cref="KeyNotFoundException">No such remote repository.</exception>
    Task<RemoteCacheOverview> GetCacheAsync(string name, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes every package the repository stored (database and files) and drops the remembered answers; the next
    /// request fetches again. Audited.
    /// </summary>
    /// <exception cref="KeyNotFoundException">No such remote repository.</exception>
    Task<PurgeResult> PurgeAsync(string name, CancellationToken cancellationToken, string? actor = null);

    /// <summary>
    /// Deletes one stored package (every version when <paramref name="version"/> is null) and drops the answers
    /// remembered about it. Audited.
    /// </summary>
    /// <exception cref="KeyNotFoundException">No such remote repository.</exception>
    Task<PurgeResult> PurgePackageAsync(string name, string packageId, NuGetVersion? version, CancellationToken cancellationToken, string? actor = null);

    /// <summary>
    /// When <c>Mirror:Enabled</c>: creates the <see cref="Repository.MirrorName"/> remote repository from <c>Mirror:*</c> if
    /// it is missing, else brings its URL, protocol and timeout in line with the options. Idempotent; called at startup.
    /// A non-remote repository with that name is left alone (the default repository then reads <c>Mirror:*</c> directly).
    /// </summary>
    Task EnsureMirrorRepositoryAsync(CancellationToken cancellationToken);
}

/// <summary>
/// <see cref="IRemoteRepositoryAdministration"/> over the relational database.
/// </summary>
public class RemoteRepositoryAdministration : IRemoteRepositoryAdministration
{
    private readonly IContext _context;
    private readonly IRepositoryService _repositories;
    private readonly RemoteSettingsCodec _codec;
    private readonly RemoteRepositoryState _state;
    private readonly IRemoteHttpClientFactory _http;
    private readonly IServiceScopeFactory _scopes;
    private readonly IOptions<MirrorOptions> _mirror;
    private readonly ILogger<RemoteRepositoryAdministration> _logger;
    private readonly MaintenanceGate? _gate;
    private readonly SystemTime? _time;
    private readonly IOptionsSnapshot<RemoteOptions>? _remoteOptions;

    public RemoteRepositoryAdministration(
        IContext context,
        IRepositoryService repositories,
        RemoteSettingsCodec codec,
        RemoteRepositoryState state,
        IRemoteHttpClientFactory http,
        IServiceScopeFactory scopes,
        IOptions<MirrorOptions> mirror,
        ILogger<RemoteRepositoryAdministration> logger,
        MaintenanceGate? gate = null,
        SystemTime? time = null,
        IOptionsSnapshot<RemoteOptions>? remoteOptions = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _repositories = repositories ?? throw new ArgumentNullException(nameof(repositories));
        _codec = codec ?? throw new ArgumentNullException(nameof(codec));
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _scopes = scopes ?? throw new ArgumentNullException(nameof(scopes));
        _mirror = mirror ?? throw new ArgumentNullException(nameof(mirror));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _gate = gate;
        _time = time;
        _remoteOptions = remoteOptions;
    }

    private bool BlockPrivate => _remoteOptions?.Value?.BlockPrivateAddresses == true;

    private DateTime Now => _time?.UtcNow ?? DateTime.UtcNow;

    private bool Relational => !(_context is NullContext);

    private bool MirrorActive => _mirror.Value.Enabled && _mirror.Value.PackageSource != null;

    /// <inheritdoc/>
    public async Task<RemoteRepositoryDetail?> FindAsync(string? name, CancellationToken cancellationToken)
    {
        var row = await FindRowOrNullAsync(name, cancellationToken).ConfigureAwait(false);
        if (row == null) return null;

        var settings = _codec.Deserialize(row.Settings) ?? new RemoteRepositorySettings();
        var hasPassword = !string.IsNullOrEmpty(settings.ProtectedPassword);
        settings.Password = null;
        settings.ProtectedPassword = null;

        return new RemoteRepositoryDetail(row.ToInfo(), settings, hasPassword, IsMirror: row.Name == Repository.MirrorName && MirrorActive);
    }

    /// <inheritdoc/>
    public async Task UpdateAsync(string name, RemoteRepositorySettings input, bool clearPassword, CancellationToken cancellationToken, string? actor = null)
    {
        if (input == null) throw new ArgumentNullException(nameof(input));
        _gate?.ThrowIfClosed();

        var row = await FindRowAsync(name, cancellationToken).ConfigureAwait(false);
        var stored = _codec.Deserialize(row.Settings) ?? new RemoteRepositorySettings();

        var settings = input.Clone();
        var passwordChanged = false;
        if (clearPassword)
        {
            settings.Password = null;
            settings.ProtectedPassword = null;
            passwordChanged = !string.IsNullOrEmpty(stored.ProtectedPassword);
        }
        else if (string.IsNullOrEmpty(settings.Password))
        {
            settings.Password = null;
            settings.ProtectedPassword = stored.ProtectedPassword;
        }
        else
        {
            passwordChanged = true;
        }

        // While Mirror:* drives the default repository, where the upstream is stays in the options.
        if (row.Name == Repository.MirrorName && MirrorActive)
        {
            settings.Url = stored.Url;
            settings.Protocol = stored.Protocol;
            settings.DownloadTimeoutSeconds = stored.DownloadTimeoutSeconds;
        }

        // A stored password belongs to the origin and user name it was stored for: pointing the repository elsewhere
        // without typing a new one must not send it to the new server (see TestConnectionAsync).
        if (string.IsNullOrEmpty(settings.Password)
            && !string.IsNullOrEmpty(settings.ProtectedPassword)
            && !SameCredentialScope(stored, settings))
        {
            settings.ProtectedPassword = null;
            passwordChanged = true;
        }

        Validate(settings, BlockPrivate);

        row.Settings = _codec.Serialize(settings);
        AccessAdministration.Audit(_context, Now, actor, "repository.remote.update", name, new
        {
            url = settings.UrlOrNull?.GetLeftPart(UriPartial.Path),
            protocol = settings.Protocol.ToString(),
            username = settings.Username,
            passwordChanged,
            settings.MetadataTtlSeconds,
            settings.NegativeTtlSeconds,
            settings.Offline,
            settings.StoreLocally,
        });
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _state.InvalidateSettings(row.Key);
        _state.Clear(row.Key);
    }

    /// <inheritdoc/>
    public async Task<RemoteConnectionResult> TestConnectionAsync(string? name, RemoteRepositorySettings input, CancellationToken cancellationToken)
    {
        if (input == null) throw new ArgumentNullException(nameof(input));

        var settings = input.Clone();
        var storedPasswordWithheld = false;
        if (string.IsNullOrEmpty(settings.Password) && !string.IsNullOrEmpty(name))
        {
            // The stored password goes only to the upstream it was stored for (the rule saving applies): testing another
            // origin or user name with it would hand it to whoever runs that server.
            var row = await FindRowOrNullAsync(name, cancellationToken).ConfigureAwait(false);
            var stored = _codec.Deserialize(row?.Settings);
            if (stored != null && !string.IsNullOrEmpty(stored.Password))
            {
                if (SameCredentialScope(stored, settings))
                {
                    settings.Password = stored.Password;
                }
                else
                {
                    storedPasswordWithheld = true;
                }
            }
        }

        var problems = settings.Validate(BlockPrivate);
        if (problems.Count > 0) return new RemoteConnectionResult(false, string.Join(" ", problems), null, 0);

        var watch = Stopwatch.StartNew();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));

        try
        {
            if (BlockPrivate)
            {
                var refused = await RemoteAddressPolicy.CheckResolvedAsync(settings.UrlOrNull, timeout.Token).ConfigureAwait(false);
                if (refused != null) return new RemoteConnectionResult(false, refused, null, watch.ElapsedMilliseconds);
            }

            using var client = _http.CreateProbe(settings);

            // Redirects are followed by hand and only within the origin that was typed: following one to another host
            // would let a probe be steered at addresses nobody named.
            var target = settings.UrlOrNull!;
            HttpResponseMessage response;
            var hops = 0;
            while (true)
            {
                response = await client.GetAsync(target, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
                if (!IsRedirect(response.StatusCode) || response.Headers.Location == null) break;

                var next = new Uri(target, response.Headers.Location);
                var elapsed = watch.ElapsedMilliseconds;
                var redirectStatus = (int)response.StatusCode;
                response.Dispose();
                if (!SameOrigin(target, next) || ++hops > MaxProbeRedirects)
                {
                    return new RemoteConnectionResult(
                        false,
                        $"The upstream answered HTTP {redirectStatus} and redirects to another host (or too many times); Test connection does not follow that. Use the final URL.",
                        redirectStatus,
                        elapsed);
                }

                target = next;
            }

            using (response)
            {
                var status = (int)response.StatusCode;

                if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
                {
                    return new RemoteConnectionResult(
                        false,
                        $"The upstream refused the credentials (HTTP {status})."
                        + (storedPasswordWithheld ? " The stored password is only sent while the URL's origin and the user name are unchanged; type the password to test them." : string.Empty),
                        status,
                        watch.ElapsedMilliseconds);
                }

                if (!response.IsSuccessStatusCode)
                {
                    return new RemoteConnectionResult(false, $"The upstream answered HTTP {status} {response.StatusCode}.", status, watch.ElapsedMilliseconds);
                }

                if (settings.Protocol == RemoteProtocol.V3)
                {
                    var body = await ReadCappedAsync(response.Content, MaxProbeBodyBytes, timeout.Token).ConfigureAwait(false);
                    if (body == null)
                    {
                        return new RemoteConnectionResult(false, "The upstream answered, but with far more data than a V3 service index (over 1 MB). Is the protocol right?", status, watch.ElapsedMilliseconds);
                    }

                    try
                    {
                        using var document = JsonDocument.Parse(body);
                        if (document.RootElement.ValueKind == JsonValueKind.Object
                            && document.RootElement.TryGetProperty("resources", out var resources)
                            && resources.ValueKind == JsonValueKind.Array)
                        {
                            return new RemoteConnectionResult(true, $"Connected: V3 service index with {resources.GetArrayLength()} resources.", status, watch.ElapsedMilliseconds);
                        }
                    }
                    catch (JsonException)
                    {
                    }

                    return new RemoteConnectionResult(false, "The upstream answered, but not with a V3 service index (index.json with a resources list). Is the protocol right?", status, watch.ElapsedMilliseconds);
                }

                return new RemoteConnectionResult(true, "Connected: the V2 feed answered.", status, watch.ElapsedMilliseconds);
            }
        }
        catch (Exception e) when (!cancellationToken.IsCancellationRequested)
        {
            if (IsPrivateAddressRefusal(e)) return new RemoteConnectionResult(false, RemoteAddressPolicy.Message, null, watch.ElapsedMilliseconds);

            return new RemoteConnectionResult(false, "Could not reach the upstream: " + RemoteUpstreamClient.Describe(e), null, watch.ElapsedMilliseconds);
        }
    }

    // The most of an answer Test connection reads: a service index is a few KB; anything bigger is not one.
    private const int MaxProbeBodyBytes = 1024 * 1024;
    private const int MaxProbeRedirects = 5;

    private static bool IsRedirect(HttpStatusCode status)
        => status is HttpStatusCode.MovedPermanently or HttpStatusCode.Found or HttpStatusCode.SeeOther
            or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect;

    private static bool IsPrivateAddressRefusal(Exception e)
    {
        for (var current = e; current != null; current = current.InnerException)
        {
            if (current is PrivateAddressException) return true;
        }

        return false;
    }

    // At most 'limit' bytes of the body as text, or null when there is more.
    private static async Task<string?> ReadCappedAsync(HttpContent content, int limit, CancellationToken cancellationToken)
    {
        if (content.Headers.ContentLength > limit) return null;

        using var stream = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var buffer = new System.IO.MemoryStream();
        var chunk = new byte[16 * 1024];
        int read;
        while ((read = await stream.ReadAsync(chunk, cancellationToken).ConfigureAwait(false)) > 0)
        {
            if (buffer.Length + read > limit) return null;

            buffer.Write(chunk, 0, read);
        }

        return System.Text.Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
    }

    /// <inheritdoc/>
    public async Task<RemoteCacheOverview> GetCacheAsync(string name, CancellationToken cancellationToken)
    {
        var row = await FindRowAsync(name, cancellationToken).ConfigureAwait(false);
        var packages = _context.Packages!.IgnoreQueryFilters().AsNoTracking().Where(p => p.RepositoryKey == row.Key);
        var count = await packages.CountAsync(cancellationToken).ConfigureAwait(false);
        var ids = await packages.Select(p => p.Id).Distinct().CountAsync(cancellationToken).ConfigureAwait(false);

        return new RemoteCacheOverview(_state.Status(row.Key), count, ids);
    }

    /// <inheritdoc/>
    public async Task<PurgeResult> PurgeAsync(string name, CancellationToken cancellationToken, string? actor = null)
    {
        _gate?.ThrowIfClosed();
        var row = await FindRowAsync(name, cancellationToken).ConfigureAwait(false);

        var deleted = await DeletePackagesAsync(row, packageId: null, version: null, cancellationToken).ConfigureAwait(false);
        var answers = _state.Clear(row.Key);

        await AuditAsync(actor, "repository.remote.purge", name, new { packages = deleted, answers }, cancellationToken).ConfigureAwait(false);

        return new PurgeResult(deleted, answers);
    }

    /// <inheritdoc/>
    public async Task<PurgeResult> PurgePackageAsync(string name, string packageId, NuGetVersion? version, CancellationToken cancellationToken, string? actor = null)
    {
        if (string.IsNullOrWhiteSpace(packageId)) throw new ArgumentException("A package id is required.", nameof(packageId));
        _gate?.ThrowIfClosed();
        var row = await FindRowAsync(name, cancellationToken).ConfigureAwait(false);

        var deleted = await DeletePackagesAsync(row, packageId.Trim(), version, cancellationToken).ConfigureAwait(false);
        var answers = _state.ClearPackage(row.Key, packageId.Trim());

        await AuditAsync(
            actor,
            "repository.remote.purge-package",
            name,
            new { packageId = packageId.Trim(), version = version?.ToNormalizedString(), packages = deleted, answers },
            cancellationToken).ConfigureAwait(false);

        return new PurgeResult(deleted, answers);
    }

    /// <inheritdoc/>
    public async Task EnsureMirrorRepositoryAsync(CancellationToken cancellationToken)
    {
        if (!MirrorActive || !_repositories.SupportsMultipleRepositories) return;

        var existing = await _context.Repositories!
            .FirstOrDefaultAsync(r => r.Name == Repository.MirrorName, cancellationToken)
            .ConfigureAwait(false);

        var fromOptions = RemoteRepositorySettings.FromMirror(_mirror.Value);
        if (existing == null)
        {
            try
            {
                await _repositories.CreateAsync(
                    Repository.MirrorName, RepositoryFormat.NuGet, RepositoryKind.Remote, fromOptions, cancellationToken).ConfigureAwait(false);
            }
            catch (ArgumentException e) when (BlockPrivate)
            {
                // Mirror:PackageSource is operator configuration; a private one that Remote:BlockPrivateAddresses refuses
                // must not stop the host from starting (the default repository's fetches are refused at connection time).
                _logger.LogWarning("The remote repository {Repository} was not created from the Mirror settings: {Reason}", Repository.MirrorName, e.Message);

                return;
            }

            _state.InvalidateSettings(RemoteRepositoryState.MirrorLookupKey);
            _logger.LogInformation(
                "Created the remote repository {Repository} from the Mirror settings; the default repository fetches through it.",
                Repository.MirrorName);

            return;
        }

        if (existing.Kind != RepositoryKind.Remote)
        {
            _logger.LogWarning(
                "A repository named {Repository} exists and is not a remote one; the default repository reads the Mirror settings directly. Rename or delete it to manage the mirror as a repository.",
                Repository.MirrorName);

            return;
        }

        var settings = _codec.Deserialize(existing.Settings);
        if (settings == null)
        {
            existing.Settings = _codec.Serialize(fromOptions);
        }
        else if (settings.Url == fromOptions.Url
            && settings.Protocol == fromOptions.Protocol
            && settings.DownloadTimeoutSeconds == fromOptions.DownloadTimeoutSeconds)
        {
            return;
        }
        else
        {
            // Mirror:* carries no user name (the repository's own is kept), so only the origin can have moved.
            var moved = !SameOrigin(settings, fromOptions);
            settings.Url = fromOptions.Url;
            settings.Protocol = fromOptions.Protocol;
            settings.DownloadTimeoutSeconds = fromOptions.DownloadTimeoutSeconds;
            if (moved)
            {
                // The stored password was for another origin: never hand it to the new one.
                settings.Password = null;
                settings.ProtectedPassword = null;
            }

            existing.Settings = _codec.Serialize(settings);
        }

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        _state.InvalidateSettings(existing.Key);
    }

    // True while the origin (scheme, host, port) is the one a stored password was saved for.
    private static bool SameOrigin(RemoteRepositorySettings before, RemoteRepositorySettings after)
    {
        var a = before.UrlOrNull;
        var b = after.UrlOrNull;
        if (a == null || b == null) return a == b;

        return string.Equals(a.GetLeftPart(UriPartial.Authority), b.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase);
    }

    private static bool SameOrigin(Uri a, Uri b)
        => string.Equals(a.GetLeftPart(UriPartial.Authority), b.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase);

    // True while the origin and the user name are the ones a stored password was saved for.
    private static bool SameCredentialScope(RemoteRepositorySettings before, RemoteRepositorySettings after)
    {
        return SameOrigin(before, after)
            && string.Equals(before.Username ?? string.Empty, after.Username ?? string.Empty, StringComparison.Ordinal);
    }

    private async Task<int> DeletePackagesAsync(Repository row, string? packageId, NuGetVersion? version, CancellationToken cancellationToken)
    {
        var info = row.ToInfo();
        var deleted = 0;

        while (true)
        {
            var query = _context.Packages!.IgnoreQueryFilters().AsNoTracking().Where(p => p.RepositoryKey == row.Key);
            if (packageId != null) query = query.Where(p => p.Id == packageId);
            if (version != null)
            {
                var normalized = version.ToNormalizedString().ToLowerInvariant();
                query = query.Where(p => p.NormalizedVersionString == normalized);
            }

            var batch = await query
                .OrderBy(p => p.Key)
                .Select(p => new { p.Id, p.NormalizedVersionString })
                .Take(200)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            if (batch.Count == 0) return deleted;

            // The deletion services work in the repository of their scope.
            using var scope = _scopes.CreateRepositoryScope(info);
            var deletion = scope.ServiceProvider.GetRequiredService<IPackageDeletionService>();
            var progressed = 0;
            foreach (var package in batch)
            {
                if (await deletion.TryHardDeletePackageAsync(package.Id, NuGetVersion.Parse(package.NormalizedVersionString!), cancellationToken).ConfigureAwait(false))
                {
                    deleted++;
                    progressed++;
                }
            }

            // A row that cannot be deleted must not loop forever.
            if (progressed == 0) return deleted;
        }
    }

    private async Task AuditAsync(string? actor, string action, string name, object details, CancellationToken cancellationToken)
    {
        AccessAdministration.Audit(_context, Now, actor, action, name, details);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void Validate(RemoteRepositorySettings settings, bool blockPrivateAddresses)
    {
        var problems = settings.Validate(blockPrivateAddresses);
        if (problems.Count > 0) throw new ArgumentException(string.Join(" ", problems));
    }

    private async Task<Repository?> FindRowOrNullAsync(string? name, CancellationToken cancellationToken)
    {
        if (!Relational || string.IsNullOrEmpty(name)) return null;

        return await _context.Repositories!
            .FirstOrDefaultAsync(r => r.Name == name && r.Kind == RepositoryKind.Remote, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<Repository> FindRowAsync(string? name, CancellationToken cancellationToken)
        => await FindRowOrNullAsync(name, cancellationToken).ConfigureAwait(false)
           ?? throw new KeyNotFoundException($"There is no remote repository named '{name}'.");
}
