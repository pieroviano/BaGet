using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Protocol.Models;
using Microsoft.Azure.Cosmos.Table;

namespace BaGet.Azure
{
    public class TableSearchService : ISearchService
    {
        private const string TableName = "Packages";

        private readonly CloudTable _table;
        private readonly ISearchResponseBuilder _responseBuilder;

        public TableSearchService(
            CloudTableClient client,
            ISearchResponseBuilder responseBuilder)
        {
            _table = client?.GetTableReference(TableName) ?? throw new ArgumentNullException(nameof(client));
            _responseBuilder = responseBuilder ?? throw new ArgumentNullException(nameof(responseBuilder));
        }

        public async Task<SearchResponse> SearchAsync(
            SearchRequest request,
            CancellationToken cancellationToken)
        {
            var results = await SearchAsync(
                request.Query,
                request.Skip,
                request.Take,
                request.IncludePrerelease,
                request.IncludeSemVer2,
                cancellationToken);

            return _responseBuilder.BuildSearch(results.Page, results.TotalHits);
        }

        public async Task<AutocompleteResponse> AutocompleteAsync(
            AutocompleteRequest request,
            CancellationToken cancellationToken)
        {
            var results = await SearchAsync(
                request.Query,
                request.Skip,
                request.Take,
                request.IncludePrerelease,
                request.IncludeSemVer2,
                cancellationToken);

            var packageIds = results.Page.Select(p => p.PackageId).ToList();

            return _responseBuilder.BuildAutocomplete(packageIds, results.TotalHits);
        }

        public Task<AutocompleteResponse> ListPackageVersionsAsync(
            VersionsRequest request,
            CancellationToken cancellationToken)
        {
            // TODO: Support versions autocomplete.
            // See: https://github.com/loic-sharma/BaGet/issues/291
            var response = _responseBuilder.BuildAutocomplete(new List<string>(), totalHits: 0);

            return Task.FromResult(response);
        }

        public Task<DependentsResponse> FindDependentsAsync(string packageId, CancellationToken cancellationToken)
        {
            var response = _responseBuilder.BuildDependents(new List<PackageDependent>());

            return Task.FromResult(response);
        }

        /// <summary>
        /// A page of package registrations and the number of registrations that matched,
        /// disregarding skip and take.
        /// </summary>
        private readonly struct SearchPage
        {
            public SearchPage(List<PackageRegistration> page, long totalHits)
            {
                Page = page;
                TotalHits = totalHits;
            }

            public List<PackageRegistration> Page { get; }

            public long TotalHits { get; }
        }

        private async Task<SearchPage> SearchAsync(
            string searchText,
            int skip,
            int take,
            bool includePrerelease,
            bool includeSemVer2,
            CancellationToken cancellationToken)
        {
            var query = new TableQuery<PackageEntity>();
            query = query.Where(GenerateSearchFilter(searchText, includePrerelease, includeSemVer2));
            query.TakeCount = 500;

            var results = await LoadPackagesAsync(query, maxPartitions: skip + take, cancellationToken);

            var registrations = results
                .GroupBy(p => p.Id, StringComparer.OrdinalIgnoreCase)
                .Select(group => new PackageRegistration(group.Key, group.ToList()))
                .ToList();

            // The matches are counted before paging, so this is a true total whenever the
            // whole match set was loaded. Table storage cannot count matches it has not read,
            // and the scan above stops after "skip + take" partitions, so for larger result
            // sets this under-reports and saturates at "skip + take".
            // TODO: Count the matching partitions exactly.
            // See: https://github.com/loic-sharma/BaGet/issues/291
            var totalHits = registrations.Count;

            var page = registrations
                .Skip(skip)
                .Take(take)
                .ToList();

            return new SearchPage(page, totalHits);
        }

        private async Task<IReadOnlyList<Package>> LoadPackagesAsync(
            TableQuery<PackageEntity> query,
            int maxPartitions,
            CancellationToken cancellationToken)
        {
            var results = new List<Package>();

            var partitions = 0;
            string lastPartitionKey = null;
            TableContinuationToken token = null;
            do
            {
                var segment = await _table.ExecuteQuerySegmentedAsync(query, token, cancellationToken);

                token = segment.ContinuationToken;

                foreach (var result in segment.Results)
                {
                    if (lastPartitionKey != result.PartitionKey)
                    {
                        lastPartitionKey = result.PartitionKey;
                        partitions++;

                        if (partitions > maxPartitions)
                        {
                            break;
                        }
                    }

                    results.Add(result.AsPackage());
                }
            }
            while (token != null);

            return results;
        }

        private string GenerateSearchFilter(string searchText, bool includePrerelease, bool includeSemVer2)
        {
            var result = "";

            if (!string.IsNullOrWhiteSpace(searchText))
            {
                // Filter to rows where the "searchText" prefix matches on the partition key.
                var prefix = searchText.TrimEnd().Split(separator: null).Last();

                var prefixLower = prefix;
                var prefixUpper = prefix + "~";

                var partitionLowerFilter = TableQuery.GenerateFilterCondition(
                    "PartitionKey",
                    QueryComparisons.GreaterThanOrEqual,
                    prefixLower);

                var partitionUpperFilter = TableQuery.GenerateFilterCondition(
                    "PartitionKey",
                    QueryComparisons.LessThanOrEqual,
                    prefixUpper);

                result = GenerateAnd(partitionLowerFilter, partitionUpperFilter);
            }

            // Filter to rows that are listed.
            result = GenerateAnd(
                result,
                GenerateIsTrue(nameof(PackageEntity.Listed)));

            if (!includePrerelease)
            {
                result = GenerateAnd(
                    result,
                    GenerateIsFalse(nameof(PackageEntity.IsPrerelease)));
            }

            if (!includeSemVer2)
            {
                result = GenerateAnd(
                    result,
                    TableQuery.GenerateFilterConditionForInt(
                        nameof(PackageEntity.SemVerLevel),
                        QueryComparisons.Equal,
                        0));
            }

            return result;

            string GenerateAnd(string left, string right)
            {
                if (string.IsNullOrEmpty(left)) return right;

                return TableQuery.CombineFilters(left, TableOperators.And, right);
            }

            string GenerateIsTrue(string propertyName)
            {
                return TableQuery.GenerateFilterConditionForBool(
                    propertyName,
                    QueryComparisons.Equal,
                    givenValue: true);
            }

            string GenerateIsFalse(string propertyName)
            {
                return TableQuery.GenerateFilterConditionForBool(
                    propertyName,
                    QueryComparisons.Equal,
                    givenValue: false);
            }
        }
    }
}
