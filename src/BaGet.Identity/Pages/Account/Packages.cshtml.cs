using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NuGet.Versioning;

namespace BaGet.Identity.Pages.Account;

/// <summary>
/// The package IDs the signed-in user owns, directly or through an organization, and the
/// way into each one's management page. Ownership is per repository, so the IDs are grouped by
/// the repositories the user may read (<see cref="IRepositoryAccess"/>); a repository where
/// the user owns nothing is not listed.
/// </summary>
public class PackagesModel : AccountPageModel
{
    protected override bool RequiresSignIn => true;

    /// <summary>The owned packages of each repository that has any, <c>default</c> first.</summary>
    public IReadOnlyList<RepositoryPackages> Groups { get; private set; } = Array.Empty<RepositoryPackages>();

    /// <summary>Whether the user owns nothing in any repository.</summary>
    public bool IsEmpty => Groups.Count == 0;

    /// <summary>
    /// Whether the list needs repository headings: more than the default repository holds packages of the user.
    /// </summary>
    public bool ShowRepositoryNames => Groups.Any(g => !g.Repository.IsDefault);

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var access = HttpContext.RequestServices.GetRequiredService<IRepositoryAccess>();
        var scopes = HttpContext.RequestServices.GetRequiredService<IServiceScopeFactory>();
        var userId = UserManager.GetUserId(User);
        var groups = new List<RepositoryPackages>();

        foreach (var repository in await access.ReadableAsync(User, cancellationToken))
        {
            // A scope pinned to the repository: ownership and packages are repository-scoped tables.
            using var scope = scopes.CreateRepositoryScope(repository);

            // Ownership, not the package table, decides what is listed: an ID whose every
            // version was deleted is still the user's, and hiding it would hide the page
            // where its owners are managed.
            var ids = await scope.ServiceProvider.GetRequiredService<PackageOwnershipService>().GetOwnedPackageIdsAsync(
                userId,
                User.Identity?.Name,
                cancellationToken);

            if (ids == null || ids.Count == 0) continue;

            if (repository.Format == RepositoryFormat.Npm)
            {
                groups.Add(new RepositoryPackages(repository, await SummariseNpmAsync(scope.ServiceProvider.GetRequiredService<INpmRegistry>(), ids, cancellationToken)));
                continue;
            }

            if (repository.Format == RepositoryFormat.Maven)
            {
                groups.Add(new RepositoryPackages(repository, await SummariseMavenAsync(scope.ServiceProvider.GetRequiredService<IMavenRepository>(), ids, cancellationToken)));
                continue;
            }

            groups.Add(new RepositoryPackages(
                repository,
                Summarise(ids, await ListVersionsAsync(
                    scope.ServiceProvider.GetRequiredService<IContext>(), ids, cancellationToken))));
        }

        Groups = groups;
    }

    /// <summary>
    /// Every owned ID's versions, in one query, projected to the four columns the page
    /// shows. The page cost must not grow with how successful a publisher the user is:
    /// a per-ID <see cref="IPackageDatabase.FindAsync(string, bool, CancellationToken)"/>
    /// would be six round trips each - it eagerly loads the dependencies, package types,
    /// target frameworks, deprecation and vulnerabilities of every version - to compute
    /// three numbers from them.
    /// </summary>
    /// <remarks>
    /// Read through <see cref="IContext"/>, which is what
    /// <see cref="PackageOwnershipService.GetOwnedPackageIdsAsync"/> already requires:
    /// this page cannot work on a database without one either way, so routing it through
    /// <see cref="IPackageDatabase"/> bought nothing.
    /// </remarks>
    internal static async Task<List<OwnedVersion>> ListVersionsAsync(
        IContext context,
        IReadOnlyList<string>? ids,
        CancellationToken cancellationToken)
    {
        #pragma warning disable CS8604
        var lowered = ids.Select(id => id.ToLowerInvariant()).ToList();
        #pragma warning restore CS8604

        // Unlisted versions included: they are the owner's own, and an ID whose versions
        // are all unlisted would otherwise look empty.
        #pragma warning disable CS8602, CS8604
        return await context.Packages
            .AsNoTracking()
            .Where(p => lowered.Contains(p.Id.ToLower()))
            .Select(p => new OwnedVersion
            {
                Id = p.Id,
                NormalizedVersion = p.NormalizedVersionString,
                OriginalVersion = p.OriginalVersionString,
                Listed = p.Listed,
                Downloads = p.Downloads,
            })
            .ToListAsync(cancellationToken);
        #pragma warning restore CS8602, CS8604
    }

    // A Maven repository keeps its own tables too: the owned ids are group:artifact, summarised one artifact at a time.
    private static async Task<List<OwnedPackage>> SummariseMavenAsync(IMavenRepository maven, IReadOnlyList<string> ids, CancellationToken cancellationToken)
    {
        var owned = new List<OwnedPackage>(ids.Count);
        foreach (var id in ids)
        {
            var found = MavenNames.TryParseArtifactName(id, out var groupId, out var artifactId);
            var artifact = found ? await maven.GetArtifactAsync(groupId, artifactId, cancellationToken) : null;
            owned.Add(new OwnedPackage
            {
                Id = artifact?.Name ?? id,
                LatestVersion = artifact?.Latest?.Version,
                LatestVersionIsListed = artifact != null,
                VersionCount = artifact?.Versions.Count ?? 0,
                Downloads = 0,
            });
        }

        return owned;
    }

    // An npm repository keeps its own tables: the summary comes from the registry, one package at a time (a user owns few).
    private static async Task<List<OwnedPackage>> SummariseNpmAsync(INpmRegistry registry, IReadOnlyList<string> ids, CancellationToken cancellationToken)
    {
        var owned = new List<OwnedPackage>(ids.Count);
        foreach (var id in ids)
        {
            var package = await registry.GetPackageAsync(id.ToLowerInvariant(), cancellationToken);
            owned.Add(new OwnedPackage
            {
                Id = package?.Name ?? id,
                LatestVersion = package?.Latest?.Version,
                LatestVersionIsListed = package != null,
                VersionCount = package?.Versions.Count ?? 0,
                Downloads = 0,
            });
        }

        return owned;
    }

    /// <summary>
    /// The highest version is decided here rather than in the query: version ordering is
    /// SemVer's, which no database sorts correctly.
    /// </summary>
    internal static List<OwnedPackage> Summarise(IReadOnlyList<string>? ids, List<OwnedVersion> versions)
    {
        #pragma warning disable CS8621, CS8714
        var byId = versions
            .GroupBy(v => v.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);
        #pragma warning restore CS8621, CS8714

        #pragma warning disable CS8602
        var owned = new List<OwnedPackage>(ids.Count);
        #pragma warning restore CS8602
        foreach (var id in ids)
        {
            if (!byId.TryGetValue(id, out var found)) found = new List<OwnedVersion>();

            #pragma warning disable CS8604
            var latest = found
                .OrderByDescending(v => NuGetVersion.Parse(v.OriginalVersion ?? v.NormalizedVersion))
                .FirstOrDefault();
            #pragma warning restore CS8604

            #pragma warning disable CS8604
            owned.Add(new OwnedPackage
            {
                Id = latest?.Id ?? id,
                LatestVersion = latest == null
                    ? null
                    : NuGetVersion.Parse(latest.OriginalVersion ?? latest.NormalizedVersion).ToNormalizedString(),
                LatestVersionIsListed = latest?.Listed ?? false,
                VersionCount = found.Count,
                Downloads = found.Sum(v => v.Downloads),
            });
            #pragma warning restore CS8604
        }

        return owned;
    }

    /// <summary>
    /// The columns the summary needs, and nothing else.
    /// </summary>
    internal class OwnedVersion
    {
        public string? Id { get; set; }

        public string? NormalizedVersion { get; set; }

        public string? OriginalVersion { get; set; }

        public bool Listed { get; set; }

        public long Downloads { get; set; }
    }

    /// <summary>
    /// The packages the user owns in one repository.
    /// </summary>
    public sealed record RepositoryPackages(RepositoryInfo Repository, IReadOnlyList<OwnedPackage> Packages);

    /// <summary>
    /// One owned package ID, summarised across its versions.
    /// </summary>
    public class OwnedPackage
    {
        public string? Id { get; set; }

        /// <summary>
        /// The highest version, listed or not, or null when no version remains.
        /// </summary>
        public string? LatestVersion { get; set; }

        public bool LatestVersionIsListed { get; set; }

        public int VersionCount { get; set; }

        public long Downloads { get; set; }
    }
}