using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Versioning;

namespace BaGet.Core;

/// <summary>
/// <see cref="INpmRegistry"/> over the npm tables and the storage service. Rows live in the repository of the request (the
/// context's query filter and stamping), tarballs under <c>npm/{name}/-/{file}</c> of that repository's storage prefix.
/// </summary>
public class NpmRegistryService : INpmRegistry
{
    private const string TarballContentType = "application/octet-stream";

    private readonly IContext _context;
    private readonly IStorageService _storage;
    private readonly IRepositoryContext _repository;
    private readonly IPackageAuthorizationService _authorization;
    private readonly IUrlGenerator _urls;
    private readonly SystemTime _time;
    private readonly MaintenanceGate _gate;
    private readonly IOptionsSnapshot<NpmOptions>? _options;
    private readonly ILogger<NpmRegistryService> _logger;

    /// <param name="context">The database.</param>
    /// <param name="storage">Where tarballs are kept.</param>
    /// <param name="repository">The repository of the request.</param>
    /// <param name="authorization">Decides who may publish, tag, deprecate and unpublish.</param>
    /// <param name="urls">Builds the registry address that tarball URLs start with.</param>
    /// <param name="time">The clock.</param>
    /// <param name="gate">The read-only gate, checked before a tarball is stored.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="options">The <c>Npm:*</c> options.</param>
    public NpmRegistryService(
        IContext context,
        IStorageService storage,
        IRepositoryContext repository,
        IPackageAuthorizationService authorization,
        IUrlGenerator urls,
        SystemTime time,
        MaintenanceGate gate,
        ILogger<NpmRegistryService> logger,
        IOptionsSnapshot<NpmOptions>? options = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
        _urls = urls ?? throw new ArgumentNullException(nameof(urls));
        _time = time ?? throw new ArgumentNullException(nameof(time));
        _gate = gate ?? throw new ArgumentNullException(nameof(gate));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options;
    }

    private TimeSpan UnpublishWindow => _options?.Value.UnpublishWindow ?? TimeSpan.FromHours(72);

    // ---- reads ----------------------------------------------------------------------------------------------------------

    /// <inheritdoc/>
    public async Task<NpmPackageInfo?> GetPackageAsync(string name, CancellationToken cancellationToken)
    {
        var package = await _context.NpmPackages!.AsNoTracking().FirstOrDefaultAsync(p => p.Name == name, cancellationToken).ConfigureAwait(false);
        if (package == null) return null;

        var versions = await _context.NpmVersions!.AsNoTracking().Where(v => v.PackageKey == package.Key).ToListAsync(cancellationToken).ConfigureAwait(false);
        var tags = await _context.NpmDistTags!.AsNoTracking().Where(t => t.PackageKey == package.Key).ToListAsync(cancellationToken).ConfigureAwait(false);

        return new NpmPackageInfo(
            package.Name!,
            DateTime.SpecifyKind(package.Created, DateTimeKind.Utc),
            DateTime.SpecifyKind(package.Modified, DateTimeKind.Utc),
            tags.ToDictionary(t => t.Tag!, t => t.Version!, StringComparer.Ordinal),
            versions.Select(ToInfo).OrderBy(v => v.Version, new SemanticVersionComparer()).ToList());
    }

    private static NpmVersionInfo ToInfo(NpmVersion row)
    {
        JsonObject manifest;
        try
        {
            manifest = JsonNode.Parse(row.Manifest ?? "{}") as JsonObject ?? new JsonObject();
        }
        catch (JsonException)
        {
            manifest = new JsonObject();
        }

        return new NpmVersionInfo(
            row.Version!,
            DateTime.SpecifyKind(row.Published, DateTimeKind.Utc),
            row.Deprecated,
            row.TarballSize,
            row.Integrity!,
            row.Shasum!,
            manifest);
    }

    private sealed class SemanticVersionComparer : IComparer<string>
    {
        public int Compare(string? x, string? y)
        {
            var okX = SemanticVersion.TryParse(x ?? string.Empty, out var a);
            var okY = SemanticVersion.TryParse(y ?? string.Empty, out var b);
            if (okX && okY) return a!.CompareTo(b!);

            return string.CompareOrdinal(x, y);
        }
    }

    /// <inheritdoc/>
    public async Task<JsonObject?> GetPackumentAsync(string name, bool abbreviated, CancellationToken cancellationToken)
    {
        var package = await GetPackageAsync(name, cancellationToken).ConfigureAwait(false);

        return package == null ? null : BuildPackument(package, abbreviated);
    }

    // The fields `npm install` reads (the registry's "corgi" document); everything else is dropped to keep the answer small.
    private static readonly string[] InstallFields =
    {
        "name", "version", "dependencies", "optionalDependencies", "peerDependencies", "peerDependenciesMeta", "bundleDependencies",
        "bundledDependencies", "bin", "directories", "engines", "funding", "os", "cpu", "libc", "dist", "deprecated",
        "hasInstallScript", "_hasShrinkwrap",
    };

    private JsonObject BuildPackument(NpmPackageInfo package, bool abbreviated)
    {
        var registry = _urls.GetNpmRegistryUrl();
        var versions = new JsonObject();
        var time = new JsonObject
        {
            ["created"] = Iso(package.Created),
            ["modified"] = Iso(package.Modified),
        };

        foreach (var version in package.Versions)
        {
            var manifest = abbreviated ? Pick(version.Manifest) : (JsonObject)version.Manifest.DeepClone();

            var dist = manifest["dist"] as JsonObject ?? new JsonObject();
            dist["tarball"] = $"{registry}{package.Name}/-/{NpmNames.TarballFileName(package.Name, version.Version)}";
            dist["integrity"] = version.Integrity;
            dist["shasum"] = version.Shasum;
            manifest["dist"] = dist;
            manifest["name"] = package.Name;
            manifest["version"] = version.Version;
            if (version.Deprecated != null) manifest["deprecated"] = version.Deprecated; else manifest.Remove("deprecated");
            if (!abbreviated) manifest["_id"] = $"{package.Name}@{version.Version}";

            versions[version.Version] = manifest;
            time[version.Version] = Iso(version.Published);
        }

        var tags = new JsonObject();
        foreach (var tag in package.DistTags.OrderBy(t => t.Key, StringComparer.Ordinal)) tags[tag.Key] = tag.Value;

        if (abbreviated)
        {
            return new JsonObject
            {
                ["name"] = package.Name,
                ["modified"] = Iso(package.Modified),
                ["dist-tags"] = tags,
                ["versions"] = versions,
            };
        }

        var latest = package.Latest;
        var packument = new JsonObject
        {
            ["_id"] = package.Name,
            ["_rev"] = package.Revision,
            ["name"] = package.Name,
            ["dist-tags"] = tags,
            ["versions"] = versions,
            ["time"] = time,
        };

        if (latest?.Description != null) packument["description"] = latest.Description;
        if (latest?.Readme != null) packument["readme"] = latest.Readme;
        foreach (var field in new[] { "license", "homepage", "keywords", "repository", "author", "maintainers", "bugs" })
        {
            if (latest?.Manifest[field] is JsonNode node) packument[field] = node.DeepClone();
        }

        return packument;
    }

    private static JsonObject Pick(JsonObject manifest)
    {
        var picked = new JsonObject();
        foreach (var field in InstallFields)
        {
            if (manifest[field] is JsonNode node) picked[field] = node.DeepClone();
        }

        return picked;
    }

    private static string Iso(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc).ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);

    /// <inheritdoc/>
    public async Task<NpmTarball?> OpenTarballAsync(string name, string file, CancellationToken cancellationToken)
    {
        if (!NpmNames.TryParseTarballFile(name, file, out var version)) return null;

        var row = await _context.NpmVersions!.AsNoTracking()
            .Join(_context.NpmPackages!.Where(p => p.Name == name), v => v.PackageKey, p => p.Key, (v, p) => v)
            .FirstOrDefaultAsync(v => v.Version == version, cancellationToken).ConfigureAwait(false);
        if (row == null) return null;

        try
        {
            var content = await _storage.GetAsync(StoragePath(name, version), cancellationToken).ConfigureAwait(false);

            return new NpmTarball(content, row.TarballSize, row.Integrity!);
        }
        catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException)
        {
            _logger.LogError("The tarball of {Package}@{Version} is missing from storage.", name, version);

            return null;
        }
    }

    private string StoragePath(string name, string version) => RepositoryStorage.Scope(_repository.Current, NpmNames.TarballPath(name, version));

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<string, string>?> GetDistTagsAsync(string name, CancellationToken cancellationToken)
    {
        var package = await _context.NpmPackages!.AsNoTracking().FirstOrDefaultAsync(p => p.Name == name, cancellationToken).ConfigureAwait(false);
        if (package == null) return null;

        var tags = await _context.NpmDistTags!.AsNoTracking().Where(t => t.PackageKey == package.Key).ToListAsync(cancellationToken).ConfigureAwait(false);

        return tags.ToDictionary(t => t.Tag!, t => t.Version!, StringComparer.Ordinal);
    }

    /// <inheritdoc/>
    public async Task<NpmSearchPage> SearchAsync(
        string? text,
        int skip,
        int take,
        Func<string, CancellationToken, Task<bool>>? canRead = null,
        CancellationToken cancellationToken = default)
    {
        IQueryable<NpmPackage> query = _context.NpmPackages!.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(text))
        {
            var needle = text.Trim().ToLowerInvariant();
            query = query.Where(p => p.Name!.Contains(needle));
        }

        skip = Math.Max(skip, 0);
        take = Math.Clamp(take, 0, 250);

        int total;
        List<NpmPackage> page;
        if (canRead == null)
        {
            total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
            page = await query.OrderBy(p => p.Name).Skip(skip).Take(take).ToListAsync(cancellationToken).ConfigureAwait(false);
        }
        else
        {
            // Only the packages the caller may read count: the names are ordered, filtered and paged here, and only the
            // page's packages are loaded in full.
            var visible = new List<string>();
            foreach (var name in await query.OrderBy(p => p.Name).Select(p => p.Name!).ToListAsync(cancellationToken).ConfigureAwait(false))
            {
                if (await canRead(name, cancellationToken).ConfigureAwait(false)) visible.Add(name);
            }

            total = visible.Count;
            var names = visible.Skip(skip).Take(take).ToList();
            page = (await _context.NpmPackages!.AsNoTracking().Where(p => names.Contains(p.Name!)).ToListAsync(cancellationToken).ConfigureAwait(false))
                .OrderBy(p => p.Name, StringComparer.Ordinal).ToList();
        }

        var keys = page.Select(p => p.Key).ToList();
        var tags = await _context.NpmDistTags!.AsNoTracking().Where(t => keys.Contains(t.PackageKey) && t.Tag == "latest").ToListAsync(cancellationToken).ConfigureAwait(false);
        var wanted = page.Select(p => (p.Key, Version: tags.FirstOrDefault(t => t.PackageKey == p.Key)?.Version)).ToList();
        var versions = await _context.NpmVersions!.AsNoTracking().Where(v => keys.Contains(v.PackageKey)).ToListAsync(cancellationToken).ConfigureAwait(false);

        var hits = new List<NpmSearchHit>();
        foreach (var package in page)
        {
            var own = versions.Where(v => v.PackageKey == package.Key).ToList();
            var taggedVersion = wanted.First(w => w.Key == package.Key).Version;
            var chosen = own.FirstOrDefault(v => v.Version == taggedVersion)
                         ?? own.FirstOrDefault(v => v.Version == NpmNames.Highest(own.Select(o => o.Version!)));
            var info = chosen == null ? null : ToInfo(chosen);

            hits.Add(new NpmSearchHit(package.Name!, info?.Version, info?.Description, info?.Keywords ?? Array.Empty<string>(), DateTime.SpecifyKind(package.Modified, DateTimeKind.Utc)));
        }

        return new NpmSearchPage(total, hits);
    }

    // ---- authorization --------------------------------------------------------------------------------------------------

    private async Task<NpmResult?> DenyAsync(BaGetPrincipal? caller, PackageAction action, string name, CancellationToken cancellationToken)
    {
        if (caller == null) return NpmResult.Fail(NpmStatus.Unauthorized, "Authentication is required.");

        if (_repository.Current.Kind != RepositoryKind.Local)
        {
            return NpmResult.Fail(NpmStatus.Forbidden, "This repository takes no changes.");
        }

        if (!await _authorization.AuthorizeAsync(caller, action, name, cancellationToken).ConfigureAwait(false))
        {
            return NpmResult.Fail(NpmStatus.Forbidden, $"You are not allowed to {Describe(action)} {name}.");
        }

        return null;
    }

    private static string Describe(PackageAction action) => action switch
    {
        PackageAction.Push => "publish to",
        PackageAction.Annotate => "deprecate",
        _ => "delete from",
    };

    private static bool TryName(string name, out string normalized, out NpmResult? failure)
    {
        failure = null;
        if (NpmNames.TryParse(name, out normalized, out var error)) return true;

        failure = NpmResult.Fail(NpmStatus.Invalid, error);
        return false;
    }

    // ---- publish --------------------------------------------------------------------------------------------------------

    /// <inheritdoc/>
    public async Task<NpmResult> PublishAsync(BaGetPrincipal? caller, NpmPublication publication, CancellationToken cancellationToken)
    {
        if (publication == null) throw new ArgumentNullException(nameof(publication));

        if (!TryName(publication.Name, out var name, out var failure)) return failure!;

        if (!NpmNames.TryParseVersion(publication.Version, out var version))
        {
            return NpmResult.Fail(NpmStatus.Invalid, $"'{publication.Version}' is not a valid semantic version.");
        }

        if (!NpmNames.IsValidTag(publication.Tag, out var tagError)) return NpmResult.Fail(NpmStatus.Invalid, tagError);

        var denied = await DenyAsync(caller, PackageAction.Push, name, cancellationToken).ConfigureAwait(false);
        if (denied != null) return denied;

        // Hash and check the bytes before anything is stored: what the client computed must be what arrived.
        var tarball = publication.Tarball;
        tarball.Position = 0;
        var (length, integrity, shasum) = await HashAsync(tarball, cancellationToken).ConfigureAwait(false);

        if (publication.ExpectedLength is long declared && declared != length)
        {
            return NpmResult.Fail(NpmStatus.Invalid, $"The tarball has {length} bytes, not the {declared} the request declares.");
        }

        if (publication.ExpectedIntegrity != null && !string.Equals(publication.ExpectedIntegrity, integrity, StringComparison.Ordinal))
        {
            return NpmResult.Fail(NpmStatus.Invalid, "The tarball does not match the integrity (sha512) the request declares.");
        }

        if (publication.ExpectedShasum != null && !string.Equals(publication.ExpectedShasum, shasum, StringComparison.OrdinalIgnoreCase))
        {
            return NpmResult.Fail(NpmStatus.Invalid, "The tarball does not match the shasum (sha1) the request declares.");
        }

        // The tarball is the package it says it is.
        tarball.Position = 0;
        var inner = await NpmTarballReader.ReadPackageJsonAsync(tarball, cancellationToken).ConfigureAwait(false);
        if (inner == null) return NpmResult.Fail(NpmStatus.Invalid, "The tarball is not a gzip-compressed tar with a package.json in its top folder.");

        var innerName = inner["name"] is JsonValue n && n.TryGetValue<string>(out var innerNameText) ? innerNameText : null;
        var innerVersion = inner["version"] is JsonValue v && v.TryGetValue<string>(out var innerVersionText) ? innerVersionText : null;
        if (!string.Equals(innerName, name, StringComparison.Ordinal) || !string.Equals(innerVersion, version, StringComparison.Ordinal))
        {
            return NpmResult.Fail(NpmStatus.Invalid, $"The tarball's package.json is {innerName}@{innerVersion}, not {name}@{version}.");
        }

        _gate.ThrowIfClosed();

        var package = await _context.NpmPackages!.FirstOrDefaultAsync(p => p.Name == name, cancellationToken).ConfigureAwait(false);
        var isNewPackage = package == null;
        if (package != null)
        {
            var existing = await _context.NpmVersions!.AsNoTracking().Where(x => x.PackageKey == package.Key).Select(x => x.Version!).ToListAsync(cancellationToken).ConfigureAwait(false);
            var clash = existing.FirstOrDefault(x => NpmNames.Comparable(x) == NpmNames.Comparable(version));
            if (clash != null)
            {
                return NpmResult.Fail(NpmStatus.Conflict, $"You cannot publish over the previously published version {clash}.");
            }
        }

        var path = StoragePath(name, version);
        tarball.Position = 0;
        var stored = await _storage.PutAsync(path, tarball, TarballContentType, cancellationToken).ConfigureAwait(false);
        if (stored == StoragePutResult.Conflict)
        {
            // A file of an earlier attempt that never got its row: the version is free, so the file is replaced.
            await _storage.DeleteAsync(path, cancellationToken).ConfigureAwait(false);
            tarball.Position = 0;
            stored = await _storage.PutAsync(path, tarball, TarballContentType, cancellationToken).ConfigureAwait(false);
        }

        var now = _time.UtcNow;
        try
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

            if (package == null)
            {
                package = new NpmPackage { Name = name, Created = now, Modified = now };
                _context.NpmPackages!.Add(package);
                await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }

            _context.NpmVersions!.Add(new NpmVersion
            {
                PackageKey = package.Key,
                Version = version,
                Integrity = integrity,
                Shasum = shasum,
                TarballSize = length,
                Manifest = BuildStoredManifest(publication.Manifest, name, version, integrity, shasum).ToJsonString(),
                Published = now,
            });

            await SetTagAsync(package.Key, publication.Tag, version, cancellationToken).ConfigureAwait(false);

            // The first version of a package is always "latest", whatever tag it was published with: a package without it
            // cannot be installed by name.
            if (isNewPackage && publication.Tag != "latest") await SetTagAsync(package.Key, "latest", version, cancellationToken).ConfigureAwait(false);

            package.Modified = now;
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException e) when (_context.IsUniqueConstraintViolationException(e))
        {
            // A concurrent publish of the same version won; its file is the one at the path, so it stays.
            return NpmResult.Fail(NpmStatus.Conflict, $"You cannot publish over the previously published version {version}.");
        }
        catch
        {
            if (stored == StoragePutResult.Success) await TryDeleteAsync(path).ConfigureAwait(false);

            throw;
        }

        await _authorization.OnPushedAsync(caller!, name, isNewPackage, cancellationToken).ConfigureAwait(false);

        return NpmResult.Created($"Published {name}@{version}.", name);
    }

    /// <inheritdoc/>
    public async Task<NpmResult> PublishTarballAsync(BaGetPrincipal? caller, Stream tarball, string tag, CancellationToken cancellationToken)
    {
        if (tarball == null) throw new ArgumentNullException(nameof(tarball));

        tarball.Position = 0;
        var inner = await NpmTarballReader.ReadPackageJsonAsync(tarball, cancellationToken).ConfigureAwait(false);
        if (inner == null) return NpmResult.Fail(NpmStatus.Invalid, "The file is not an npm tarball: a gzip-compressed tar with a package.json in its top folder.");

        var name = inner["name"] is JsonValue n && n.TryGetValue<string>(out var nameText) ? nameText : null;
        var version = inner["version"] is JsonValue v && v.TryGetValue<string>(out var versionText) ? versionText : null;
        if (name == null || version == null) return NpmResult.Fail(NpmStatus.Invalid, "The package.json has no name or no version.");

        // Authorize before reading the file again: an unauthorized caller learns nothing about the repository.
        if (!TryName(name, out var normalized, out var failure)) return failure!;

        return await PublishAsync(caller, new NpmPublication(normalized, version, inner, string.IsNullOrWhiteSpace(tag) ? "latest" : tag.Trim(), tarball), cancellationToken).ConfigureAwait(false);
    }

    private static JsonObject BuildStoredManifest(JsonObject sent, string name, string version, string integrity, string shasum)
    {
        var manifest = (JsonObject)sent.DeepClone();

        // The registry decides these; the client's copies are discarded.
        manifest.Remove("_attachments");
        manifest.Remove("deprecated");
        manifest.Remove("dist-tags");
        manifest["name"] = name;
        manifest["version"] = version;

        var dist = new JsonObject();
        if (sent["dist"] is JsonObject sentDist)
        {
            foreach (var property in sentDist)
            {
                if (property.Key is "tarball" or "integrity" or "shasum") continue;

                dist[property.Key] = property.Value?.DeepClone();
            }
        }

        dist["integrity"] = integrity;
        dist["shasum"] = shasum;
        manifest["dist"] = dist;

        return manifest;
    }

    private static async Task<(long Length, string Integrity, string Shasum)> HashAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var sha512 = IncrementalHash.CreateHash(HashAlgorithmName.SHA512);
        using var sha1 = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
        var buffer = new byte[81920];
        long length = 0;
        int read;
        while ((read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            sha512.AppendData(buffer, 0, read);
            sha1.AppendData(buffer, 0, read);
            length += read;
        }

        return (length, "sha512-" + Convert.ToBase64String(sha512.GetHashAndReset()), Convert.ToHexString(sha1.GetHashAndReset()).ToLowerInvariant());
    }

    private async Task TryDeleteAsync(string path)
    {
        try
        {
            await _storage.DeleteAsync(path, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(e, "Could not delete {Path}.", path);
        }
    }

    // Points a tag at a version, keeping one row per (package, tag) as the unique index demands.
    private async Task SetTagAsync(int packageKey, string tag, string version, CancellationToken cancellationToken)
    {
        var existing = await _context.NpmDistTags!.FirstOrDefaultAsync(t => t.PackageKey == packageKey && t.Tag == tag, cancellationToken).ConfigureAwait(false);
        if (existing != null)
        {
            existing.Version = version;
        }
        else
        {
            _context.NpmDistTags!.Add(new NpmDistTag { PackageKey = packageKey, Tag = tag, Version = version });
        }
    }

    // ---- dist-tags ------------------------------------------------------------------------------------------------------

    /// <inheritdoc/>
    public async Task<NpmResult> SetDistTagAsync(BaGetPrincipal? caller, string name, string tag, string version, CancellationToken cancellationToken)
    {
        if (!TryName(name, out name, out var failure)) return failure!;
        if (!NpmNames.IsValidTag(tag, out var tagError)) return NpmResult.Fail(NpmStatus.Invalid, tagError);

        if (caller == null) return NpmResult.Fail(NpmStatus.Unauthorized, "Authentication is required.");

        var package = await _context.NpmPackages!.FirstOrDefaultAsync(p => p.Name == name, cancellationToken).ConfigureAwait(false);
        if (package == null) return NpmResult.Fail(NpmStatus.NotFound, $"There is no package {name}.");

        var denied = await DenyAsync(caller, PackageAction.Push, name, cancellationToken).ConfigureAwait(false);
        if (denied != null) return denied;

        var rows = await _context.NpmVersions!.AsNoTracking().Where(v => v.PackageKey == package.Key).Select(v => v.Version!).ToListAsync(cancellationToken).ConfigureAwait(false);
        var stored = rows.FirstOrDefault(v => v == version) ?? rows.FirstOrDefault(v => NpmNames.Comparable(v) == NpmNames.Comparable(version ?? string.Empty));
        if (stored == null) return NpmResult.Fail(NpmStatus.NotFound, $"{name} has no version {version}.");

        await SetTagAsync(package.Key, tag, stored, cancellationToken).ConfigureAwait(false);
        package.Modified = _time.UtcNow;
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return NpmResult.Ok($"+{tag}: {name}@{stored}");
    }

    /// <inheritdoc/>
    public async Task<NpmResult> RemoveDistTagAsync(BaGetPrincipal? caller, string name, string tag, CancellationToken cancellationToken)
    {
        if (!TryName(name, out name, out var failure)) return failure!;
        if (caller == null) return NpmResult.Fail(NpmStatus.Unauthorized, "Authentication is required.");

        var package = await _context.NpmPackages!.FirstOrDefaultAsync(p => p.Name == name, cancellationToken).ConfigureAwait(false);
        if (package == null) return NpmResult.Fail(NpmStatus.NotFound, $"There is no package {name}.");

        var denied = await DenyAsync(caller, PackageAction.Push, name, cancellationToken).ConfigureAwait(false);
        if (denied != null) return denied;

        var row = await _context.NpmDistTags!.FirstOrDefaultAsync(t => t.PackageKey == package.Key && t.Tag == tag, cancellationToken).ConfigureAwait(false);
        if (row == null) return NpmResult.Fail(NpmStatus.NotFound, $"{name} has no tag {tag}.");

        _context.NpmDistTags!.Remove(row);
        package.Modified = _time.UtcNow;
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return NpmResult.Ok($"-{tag}: {name}");
    }

    // ---- deprecate ------------------------------------------------------------------------------------------------------

    /// <inheritdoc/>
    public async Task<NpmResult> DeprecateAsync(BaGetPrincipal? caller, string name, IReadOnlyDictionary<string, string?> messages, CancellationToken cancellationToken)
    {
        if (!TryName(name, out name, out var failure)) return failure!;
        if (caller == null) return NpmResult.Fail(NpmStatus.Unauthorized, "Authentication is required.");

        var package = await _context.NpmPackages!.FirstOrDefaultAsync(p => p.Name == name, cancellationToken).ConfigureAwait(false);
        if (package == null) return NpmResult.Fail(NpmStatus.NotFound, $"There is no package {name}.");

        var denied = await DenyAsync(caller, PackageAction.Annotate, name, cancellationToken).ConfigureAwait(false);
        if (denied != null) return denied;

        var rows = await _context.NpmVersions!.Where(v => v.PackageKey == package.Key).ToListAsync(cancellationToken).ConfigureAwait(false);
        var changed = 0;
        foreach (var message in messages)
        {
            var row = rows.FirstOrDefault(r => r.Version == message.Key);
            if (row == null) return NpmResult.Fail(NpmStatus.NotFound, $"{name} has no version {message.Key}.");

            var text = string.IsNullOrWhiteSpace(message.Value) ? null : message.Value;
            if (row.Deprecated == text) continue;

            row.Deprecated = text;
            changed++;
        }

        if (changed > 0)
        {
            package.Modified = _time.UtcNow;
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return NpmResult.Ok();
    }

    // ---- unpublish ------------------------------------------------------------------------------------------------------

    /// <inheritdoc/>
    public async Task<NpmResult> UnpublishVersionsAsync(
        BaGetPrincipal? caller,
        string name,
        IReadOnlyCollection<string> versions,
        IReadOnlyDictionary<string, string>? newTags,
        CancellationToken cancellationToken)
    {
        if (!TryName(name, out name, out var failure)) return failure!;
        if (caller == null) return NpmResult.Fail(NpmStatus.Unauthorized, "Authentication is required.");

        var package = await _context.NpmPackages!.FirstOrDefaultAsync(p => p.Name == name, cancellationToken).ConfigureAwait(false);
        if (package == null) return NpmResult.Fail(NpmStatus.NotFound, $"There is no package {name}.");

        var rows = await _context.NpmVersions!.Where(v => v.PackageKey == package.Key).ToListAsync(cancellationToken).ConfigureAwait(false);
        var removed = new List<NpmVersion>();
        foreach (var version in versions.Distinct(StringComparer.Ordinal))
        {
            var row = rows.FirstOrDefault(r => r.Version == version);
            if (row == null) return NpmResult.Fail(NpmStatus.NotFound, $"{name} has no version {version}.");

            removed.Add(row);
        }

        return await RemoveAsync(caller, package, rows, removed, newTags, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<NpmResult> UnpublishPackageAsync(BaGetPrincipal? caller, string name, CancellationToken cancellationToken)
    {
        if (!TryName(name, out name, out var failure)) return failure!;
        if (caller == null) return NpmResult.Fail(NpmStatus.Unauthorized, "Authentication is required.");

        var package = await _context.NpmPackages!.FirstOrDefaultAsync(p => p.Name == name, cancellationToken).ConfigureAwait(false);
        if (package == null) return NpmResult.Fail(NpmStatus.NotFound, $"There is no package {name}.");

        var rows = await _context.NpmVersions!.Where(v => v.PackageKey == package.Key).ToListAsync(cancellationToken).ConfigureAwait(false);

        return await RemoveAsync(caller, package, rows, rows.ToList(), newTags: null, cancellationToken).ConfigureAwait(false);
    }

    private async Task<NpmResult> RemoveAsync(
        BaGetPrincipal caller,
        NpmPackage package,
        List<NpmVersion> all,
        List<NpmVersion> removed,
        IReadOnlyDictionary<string, string>? newTags,
        CancellationToken cancellationToken)
    {
        var name = package.Name!;

        // Deploy lets an owner take back a recent mistake (npmjs allows it for 72 hours); anything older is a deletion.
        var window = UnpublishWindow;
        var now = _time.UtcNow;
        var young = window > TimeSpan.Zero
                    && removed.All(r => now - DateTime.SpecifyKind(r.Published, DateTimeKind.Utc) <= window);
        var denied = await DenyAsync(caller, young ? PackageAction.Push : PackageAction.Manage, name, cancellationToken).ConfigureAwait(false);
        if (denied != null) return denied;

        _gate.ThrowIfClosed();

        var remaining = all.Where(r => !removed.Contains(r)).ToList();
        var tags = await _context.NpmDistTags!.Where(t => t.PackageKey == package.Key).ToListAsync(cancellationToken).ConfigureAwait(false);

        _context.NpmVersions!.RemoveRange(removed);

        if (remaining.Count == 0)
        {
            _context.NpmDistTags!.RemoveRange(tags);
            _context.NpmPackages!.Remove(package);
        }
        else
        {
            var kept = remaining.Select(r => r.Version!).ToHashSet(StringComparer.Ordinal);
            foreach (var tag in tags.ToList())
            {
                if (kept.Contains(tag.Version!)) continue;

                if (newTags != null && newTags.TryGetValue(tag.Tag!, out var target) && kept.Contains(target))
                {
                    tag.Version = target;
                }
                else
                {
                    _context.NpmDistTags!.Remove(tag);
                    tags.Remove(tag);
                }
            }

            // "latest" must point somewhere, or the package cannot be installed by name.
            if (!tags.Any(t => t.Tag == "latest"))
            {
                var highest = NpmNames.Highest(kept);
                if (highest != null) _context.NpmDistTags!.Add(new NpmDistTag { PackageKey = package.Key, Tag = "latest", Version = highest });
            }

            package.Modified = now;
        }

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // The rows are gone; a failing file delete leaves an unreferenced tarball, not a broken package.
        foreach (var row in removed) await TryDeleteAsync(StoragePath(name, row.Version!)).ConfigureAwait(false);

        return NpmResult.Ok(remaining.Count == 0 ? $"Unpublished {name}." : $"Unpublished {string.Join(", ", removed.Select(r => name + "@" + r.Version))}.");
    }

    /// <inheritdoc/>
    public async Task<NpmResult> DeleteTarballAsync(BaGetPrincipal? caller, string name, string file, CancellationToken cancellationToken)
    {
        if (!TryName(name, out name, out var failure)) return failure!;
        if (!NpmNames.TryParseTarballFile(name, file, out var version)) return NpmResult.Fail(NpmStatus.NotFound, "There is no such file.");
        if (caller == null) return NpmResult.Fail(NpmStatus.Unauthorized, "Authentication is required.");

        var package = await _context.NpmPackages!.AsNoTracking().FirstOrDefaultAsync(p => p.Name == name, cancellationToken).ConfigureAwait(false);
        var published = package != null && await _context.NpmVersions!.AnyAsync(v => v.PackageKey == package.Key && v.Version == version, cancellationToken).ConfigureAwait(false);
        if (published) return await UnpublishVersionsAsync(caller, name, new[] { version }, null, cancellationToken).ConfigureAwait(false);

        // The version is already unpublished (the earlier step of the client's sequence): only the file is left.
        var denied = await DenyAsync(caller, PackageAction.Push, name, cancellationToken).ConfigureAwait(false);
        if (denied != null) return denied;

        _gate.ThrowIfClosed();
        await _storage.DeleteAsync(StoragePath(name, version), cancellationToken).ConfigureAwait(false);

        return NpmResult.Ok();
    }
}
