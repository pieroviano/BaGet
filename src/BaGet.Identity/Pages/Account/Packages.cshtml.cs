using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NuGet.Versioning;

namespace BaGet.Identity.Pages.Account
{
    /// <summary>
    /// The package IDs the signed-in user owns, directly or through an organization, and the
    /// way into each one's management page.
    /// </summary>
    public class PackagesModel : AccountPageModel
    {
        protected override bool RequiresSignIn => true;

        public IReadOnlyList<OwnedPackage> Packages { get; private set; } = Array.Empty<OwnedPackage>();

        public async Task OnGetAsync(CancellationToken cancellationToken)
        {
            // Ownership, not the package table, decides what is listed: an ID whose every
            // version was deleted is still the user's, and hiding it would hide the page
            // where its owners are managed.
            var ids = await Ownership.GetOwnedPackageIdsAsync(
                UserManager.GetUserId(User),
                User.Identity.Name,
                cancellationToken);

            if (ids.Count == 0)
            {
                Packages = Array.Empty<OwnedPackage>();
                return;
            }

            Packages = Summarise(ids, await ListVersionsAsync(ids, cancellationToken));
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
        private async Task<List<OwnedVersion>> ListVersionsAsync(
            IReadOnlyList<string> ids,
            CancellationToken cancellationToken)
        {
            var context = HttpContext.RequestServices.GetRequiredService<IContext>();
            var lowered = ids.Select(id => id.ToLowerInvariant()).ToList();

            // Unlisted versions included: they are the owner's own, and an ID whose versions
            // are all unlisted would otherwise look empty.
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
        }

        /// <summary>
        /// The highest version is decided here rather than in the query: version ordering is
        /// SemVer's, which no database sorts correctly.
        /// </summary>
        private static List<OwnedPackage> Summarise(IReadOnlyList<string> ids, List<OwnedVersion> versions)
        {
            var byId = versions
                .GroupBy(v => v.Id, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

            var owned = new List<OwnedPackage>(ids.Count);
            foreach (var id in ids)
            {
                if (!byId.TryGetValue(id, out var found)) found = new List<OwnedVersion>();

                var latest = found
                    .OrderByDescending(v => NuGetVersion.Parse(v.OriginalVersion ?? v.NormalizedVersion))
                    .FirstOrDefault();

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
            }

            return owned;
        }

        /// <summary>
        /// The columns the summary needs, and nothing else.
        /// </summary>
        private class OwnedVersion
        {
            public string Id { get; set; }

            public string NormalizedVersion { get; set; }

            public string OriginalVersion { get; set; }

            public bool Listed { get; set; }

            public long Downloads { get; set; }
        }

        /// <summary>
        /// One owned package ID, summarised across its versions.
        /// </summary>
        public class OwnedPackage
        {
            public string Id { get; set; }

            /// <summary>
            /// The highest version, listed or not, or null when no version remains.
            /// </summary>
            public string LatestVersion { get; set; }

            public bool LatestVersionIsListed { get; set; }

            public int VersionCount { get; set; }

            public long Downloads { get; set; }
        }
    }
}
