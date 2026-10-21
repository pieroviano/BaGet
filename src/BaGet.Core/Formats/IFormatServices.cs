using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace BaGet.Core;

/// <summary>
/// A per-request capability of one format, registered (scoped) beside its <see cref="IFormatModule"/>. It works in the
/// request's repository (<see cref="IRepositoryContext"/>), like every other scoped service.
/// </summary>
public interface IFormatService
{
    /// <summary>The format the service belongs to.</summary>
    RepositoryFormat Format { get; }
}

/// <summary>Finds the capability of a format.</summary>
public static class FormatServices
{
    /// <summary>
    /// The registered <typeparamref name="T"/> of <paramref name="format"/>, or null when the format does not offer it (a
    /// phase that has not implemented it yet). Callers treat null as "not supported", never as an error.
    /// </summary>
    public static T? GetFormatService<T>(this IServiceProvider provider, RepositoryFormat format)
        where T : class, IFormatService
        => provider.GetServices<T>().FirstOrDefault(s => s.Format == format);
}

/// <summary>What happened to an artifact handed to <see cref="IFormatIndexer.IndexAsync"/>.</summary>
public enum FormatIndexResult
{
    /// <summary>The bytes are not a valid artifact of the format.</summary>
    Invalid,

    /// <summary>The same name and version is already in the repository.</summary>
    AlreadyExists,

    /// <summary>Stored and visible.</summary>
    Indexed,

    /// <summary>Stored and queued (validation); not visible yet.</summary>
    Accepted,
}

/// <summary>Reads an artifact, stores it and records its metadata in the repository of the request (deploy).</summary>
public interface IFormatIndexer : IFormatService
{
    /// <summary>Indexes the artifact in <paramref name="artifact"/>.</summary>
    Task<FormatIndexResult> IndexAsync(Stream? artifact, CancellationToken cancellationToken);
}

/// <summary>A search in one repository.</summary>
/// <param name="Query">The text, or null for everything.</param>
/// <param name="Skip">Results to skip.</param>
/// <param name="Take">Results to return.</param>
/// <param name="IncludePrerelease">Whether prerelease versions count.</param>
public sealed record FormatSearchQuery(string? Query, int Skip, int Take, bool IncludePrerelease);

/// <summary>One search result: an artifact name with its newest version.</summary>
public sealed record FormatSearchHit(string Name, string? Version, string? Description, long Downloads);

/// <summary>A page of search results.</summary>
/// <param name="TotalHits">All matches, not only this page.</param>
/// <param name="Hits">This page.</param>
public sealed record FormatSearchPage(long TotalHits, IReadOnlyList<FormatSearchHit> Hits);

/// <summary>Searches the repository of the request in the format's own terms (the protocol's search endpoint uses its richer model).</summary>
public interface IFormatSearchProvider : IFormatService
{
    /// <summary>Runs <paramref name="query"/>.</summary>
    Task<FormatSearchPage> SearchAsync(FormatSearchQuery query, CancellationToken cancellationToken);
}

/// <summary>A stored version, as retention policies see it.</summary>
/// <param name="Name">The artifact name (package id, npm name, Maven coordinates).</param>
/// <param name="Version">The version.</param>
/// <param name="Published">When it was stored.</param>
/// <param name="LastDownloaded">The last download, when known.</param>
/// <param name="Size">Bytes stored for it, when known.</param>
/// <param name="Prerelease">Whether it is a prerelease (npm), a pre-release (NuGet) or a snapshot (Maven).</param>
public sealed record FormatVersion(string Name, string Version, DateTime Published, DateTime? LastDownloaded, long? Size, bool Prerelease);

/// <summary>
/// What retention (phase P6) needs from a format: list what is stored and delete one version, in the repository of the request.
/// No format implements it yet; the interface fixes the shape so the engine can be written once.
/// </summary>
public interface IFormatRetentionHooks : IFormatService
{
    /// <summary>Every stored version of the repository.</summary>
    Task<IReadOnlyList<FormatVersion>> ListVersionsAsync(CancellationToken cancellationToken);

    /// <summary>Deletes one version for good (metadata and files).</summary>
    Task DeleteVersionAsync(string name, string version, CancellationToken cancellationToken);
}

/// <summary>A third-party component of an artifact, named the way a vulnerability database names it.</summary>
/// <param name="Ecosystem">The OSV ecosystem: <c>NuGet</c>, <c>npm</c>, <c>Maven</c>.</param>
/// <param name="Name">The component name in that ecosystem.</param>
/// <param name="Version">Its version.</param>
public sealed record ScanComponent(string Ecosystem, string Name, string Version);

/// <summary>
/// What vulnerability scanning (phase P7) needs from a format: the components an artifact consists of (itself and its
/// dependencies). No format implements it yet.
/// </summary>
public interface IFormatScanner : IFormatService
{
    /// <summary>The components of the stored version, or none when it is unknown.</summary>
    Task<IReadOnlyList<ScanComponent>> ListComponentsAsync(string name, string version, CancellationToken cancellationToken);
}
