using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.Extensions.DependencyInjection;

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
            var packages = HttpContext.RequestServices.GetRequiredService<IPackageDatabase>();

            // Ownership, not the package table, decides what is listed: an ID whose every
            // version was deleted is still the user's, and hiding it would hide the page
            // where its owners are managed.
            var ids = await Ownership.GetOwnedPackageIdsAsync(
                UserManager.GetUserId(User),
                User.Identity.Name,
                cancellationToken);

            var owned = new List<OwnedPackage>(ids.Count);
            foreach (var id in ids)
            {
                // Unlisted versions included: they are the owner's own, and an ID whose
                // versions are all unlisted would otherwise look empty.
                var versions = await packages.FindAsync(id, includeUnlisted: true, cancellationToken);
                var latest = versions.OrderByDescending(p => p.Version).FirstOrDefault();

                owned.Add(new OwnedPackage
                {
                    Id = latest?.Id ?? id,
                    LatestVersion = latest?.Version.ToNormalizedString(),
                    LatestVersionIsListed = latest?.Listed ?? false,
                    VersionCount = versions.Count,
                    Downloads = versions.Sum(p => p.Downloads),
                });
            }

            Packages = owned;
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
