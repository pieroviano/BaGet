using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace BaGet.Core;

/// <summary>
/// Docker's <see cref="IFormatScanner"/>: container images are scanned by Trivy (<see cref="ScanEngine.Image"/>), one result per
/// image manifest, named by image and manifest digest. Indexes (multi-platform lists), signatures and other artifacts that refer
/// to an image are not images and are not scanned; each platform's manifest is.
/// </summary>
public sealed class DockerFormatScanner : IFormatScanner
{
    private readonly IContext _context;

    public DockerFormatScanner(IContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc/>
    public RepositoryFormat Format => RepositoryFormat.Docker;

    /// <inheritdoc/>
    public ScanEngine Engine => ScanEngine.Image;

    /// <inheritdoc/>
    public Task<IReadOnlyList<ScanComponent>> ListComponentsAsync(string name, string version, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<ScanComponent>>(Array.Empty<ScanComponent>());

    /// <inheritdoc/>
    public async Task<string?> GetImageReferenceAsync(string name, string version, CancellationToken cancellationToken)
    {
        if (_context is NullContext || !OciNames.IsValidDigest(version)) return null;

        // Only an image manifest is scannable (not an index, a signature or another artifact), so only it is "stored" for the gate.
        var row = await _context.OciManifests!
            .AsNoTracking()
            .Join(_context.OciRepositories!.Where(r => r.Name == name), m => m.ImageKey, r => r.Key, (m, r) => new { m.Digest, m.MediaType, m.ArtifactType, m.SubjectDigest })
            .Where(m => m.Digest == version)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        return row != null && IsImage(row.MediaType, row.ArtifactType, row.SubjectDigest) ? name + "@" + version : null;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ScanArtifact>> ListArtifactsAsync(CancellationToken cancellationToken)
    {
        if (_context is NullContext) return Array.Empty<ScanArtifact>();

        var rows = await _context.OciManifests!
            .AsNoTracking()
            .Join(_context.OciRepositories!, m => m.ImageKey, r => r.Key, (m, r) => new { r.Name, m.Digest, m.MediaType, m.ArtifactType, m.SubjectDigest })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows
            .Where(r => IsImage(r.MediaType, r.ArtifactType, r.SubjectDigest))
            .Select(r => new ScanArtifact(r.Name!, r.Digest!))
            .ToList();
    }

    /// <summary>Whether a manifest is a container image (not an index, a signature or another artifact).</summary>
    public static bool IsImage(string? mediaType, string? artifactType, string? subjectDigest)
        => subjectDigest == null
           && mediaType != null
           && OciNames.ImageManifestTypes.Contains(mediaType, StringComparer.Ordinal)
           && (artifactType == null || artifactType.Contains("container.image", StringComparison.Ordinal) || artifactType.Contains("image.config", StringComparison.Ordinal));
}
