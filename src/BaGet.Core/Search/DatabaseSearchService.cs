using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Protocol.Models;
using Microsoft.EntityFrameworkCore;

namespace BaGet.Core
{
    public class DatabaseSearchService : ISearchService
    {
        private readonly IContext _context;
        private readonly IFrameworkCompatibilityService _frameworks;
        private readonly ISearchResponseBuilder _searchBuilder;

        public DatabaseSearchService(
            IContext context,
            IFrameworkCompatibilityService frameworks,
            ISearchResponseBuilder searchBuilder)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _frameworks = frameworks ?? throw new ArgumentNullException(nameof(frameworks));
            _searchBuilder = searchBuilder ?? throw new ArgumentNullException(nameof(searchBuilder));
        }

        public async Task<SearchResponse> SearchAsync(SearchRequest request,  CancellationToken cancellationToken)
        {
            var frameworks = GetCompatibleFrameworksOrNull(request.Framework);

            IQueryable<Package> search = _context.Packages;
            search = ApplySearchQuery(search, request.Query);
            search = ApplySearchFilters(
                search,
                request.IncludePrerelease,
                request.IncludeSemVer2,
                request.PackageType,
                frameworks);

            // "TotalHits" is the number of matches disregarding skip and take. A page of results is
            // drawn from the distinct package ids that match the filters, so the total must count
            // those distinct ids - not the matching rows, of which there is one per package version.
            var totalHits = await CountDistinctPackageIdsAsync(search, cancellationToken);

            var packageIds = search
                .Select(p => p.Id)
                .Distinct()
                .OrderBy(id => id)
                .Skip(request.Skip)
                .Take(request.Take);

            // This query MUST fetch all versions for each package that matches the search,
            // otherwise the results for a package's latest version may be incorrect.
            // If possible, we'll find all these packages in a single query by matching
            // the package IDs in a subquery. Otherwise, run two queries:
            //   1. Find the package IDs that match the search
            //   2. Find all package versions for these package IDs
            if (_context.SupportsLimitInSubqueries)
            {
                search = _context.Packages.Where(p => packageIds.Contains(p.Id));
            }
            else
            {
                var packageIdResults = await packageIds.ToListAsync(cancellationToken);

                search = _context.Packages.Where(p => packageIdResults.Contains(p.Id));
            }

            search = ApplySearchFilters(
                search,
                request.IncludePrerelease,
                request.IncludeSemVer2,
                request.PackageType,
                frameworks);

            var results = await search
                .Include(p => p.Deprecation)
                .Include(p => p.Vulnerabilities)
                .ToListAsync(cancellationToken);
            var groupedResults = results
                .GroupBy(p => p.Id, StringComparer.OrdinalIgnoreCase)
                .Select(group => new PackageRegistration(group.Key, group.ToList()))
                .ToList();

            var response = _searchBuilder.BuildSearch(groupedResults, totalHits);

            await AddOwnersAsync(response, cancellationToken);

            return response;
        }

        /// <summary>
        /// Report each result's owners. A package pushed before accounts existed has none,
        /// and then neither field is reported.
        /// </summary>
        private async Task AddOwnersAsync(SearchResponse response, CancellationToken cancellationToken)
        {
            if (response.Data.Count == 0) return;

            var ids = response.Data.Select(r => r.PackageId.ToLower()).ToList();
            var owners = await _context.PackageOwners
                .AsNoTracking()
                .Where(o => ids.Contains(o.PackageId.ToLower()))
                .ToListAsync(cancellationToken);

            var byId = owners.ToLookup(o => o.PackageId, StringComparer.OrdinalIgnoreCase);
            foreach (var result in response.Data)
            {
                var resultOwners = byId[result.PackageId].ToList();
                if (resultOwners.Count == 0) continue;

                result.Owners = resultOwners.Select(o => o.OwnerName).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
                result.Verified = resultOwners.Any(o => o.IsVerified);
            }
        }

        public async Task<AutocompleteResponse> AutocompleteAsync(
            AutocompleteRequest request,
            CancellationToken cancellationToken)
        {
            IQueryable<Package> search = _context.Packages;

            search = ApplySearchQuery(search, request.Query);
            search = ApplySearchFilters(
                search,
                request.IncludePrerelease,
                request.IncludeSemVer2,
                request.PackageType,
                frameworks: null);

            // See the comment in "SearchAsync": the total disregards skip and take and counts
            // distinct package ids, which is what the page below is drawn from.
            var totalHits = await CountDistinctPackageIdsAsync(search, cancellationToken);

            // Group by package ID so that the column the results are ordered by is part of
            // the query's projection. Ordering before a "Distinct" over a narrower projection
            // is silently dropped, and SQL Server rejects the resulting query outright:
            // "ORDER BY items must appear in the select list if SELECT DISTINCT is specified."
            var packageIds = await search
                .GroupBy(p => p.Id)
                .OrderByDescending(packages => packages.Sum(p => p.Downloads))
                .Select(packages => packages.Key)
                .Skip(request.Skip)
                .Take(request.Take)
                .ToListAsync(cancellationToken);

            return _searchBuilder.BuildAutocomplete(packageIds, totalHits);
        }

        public async Task<AutocompleteResponse> ListPackageVersionsAsync(
            VersionsRequest request,
            CancellationToken cancellationToken)
        {
            var packageId = request.PackageId.ToLower();
            var search = _context
                .Packages
                .Where(p => p.Id.ToLower().Equals(packageId));

            search = ApplySearchFilters(
                search,
                request.IncludePrerelease,
                request.IncludeSemVer2,
                packageType: null,
                frameworks: null);

            var packageVersions = await search
                .Select(p => p.NormalizedVersionString)
                .ToListAsync(cancellationToken);

            // Version enumeration is not paged, so every match is on the single page returned.
            return _searchBuilder.BuildAutocomplete(packageVersions, packageVersions.Count);
        }

        public async Task<DependentsResponse> FindDependentsAsync(string packageId, CancellationToken cancellationToken)
        {
            var dependents = await _context
                .Packages
                .Where(p => p.Listed)
                .OrderByDescending(p => p.Downloads)
                .Where(p => p.Dependencies.Any(d => d.Id == packageId))
                .Take(20)
                .Select(r => new PackageDependent
                {
                    Id = r.Id,
                    Description = r.Description,
                    TotalDownloads = r.Downloads
                })
                .Distinct()
                .ToListAsync(cancellationToken);

            return _searchBuilder.BuildDependents(dependents);
        }

        /// <summary>
        /// Count the packages matched by a filtered query, disregarding skip and take.
        /// A single package is stored as one row per version, so the matching rows must be
        /// reduced to their distinct package ids first. This mirrors the projection that the
        /// paged queries use, so the total is always consistent with the page.
        /// </summary>
        private static Task<int> CountDistinctPackageIdsAsync(
            IQueryable<Package> search,
            CancellationToken cancellationToken)
        {
            return search
                .Select(p => p.Id)
                .Distinct()
                .CountAsync(cancellationToken);
        }

        private IQueryable<Package> ApplySearchQuery(IQueryable<Package> query, string search)
        {
            if (string.IsNullOrEmpty(search))
            {
                return query;
            }

            search = search.ToLowerInvariant();

            return query.Where(p => p.Id.ToLower().Contains(search));
        }

        private IQueryable<Package> ApplySearchFilters(
            IQueryable<Package> query,
            bool includePrerelease,
            bool includeSemVer2,
            string packageType,
            IReadOnlyList<string> frameworks)
        {
            if (!includePrerelease)
            {
                query = query.Where(p => !p.IsPrerelease);
            }

            if (!includeSemVer2)
            {
                query = query.Where(p => p.SemVerLevel != SemVerLevel.SemVer2);
            }

            if (!string.IsNullOrEmpty(packageType))
            {
                query = query.Where(p => p.PackageTypes.Any(t => t.Name == packageType));
            }

            if (frameworks != null)
            {
                query = query.Where(p => p.TargetFrameworks.Any(f => frameworks.Contains(f.Moniker)));
            }

            return query.Where(p => p.Listed);
        }

        private IReadOnlyList<string> GetCompatibleFrameworksOrNull(string framework)
        {
            if (framework == null) return null;

            return _frameworks.FindAllCompatibleFrameworks(framework);
        }
    }
}
