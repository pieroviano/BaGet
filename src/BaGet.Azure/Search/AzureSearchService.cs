using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Protocol.Models;
using Microsoft.Azure.Search;
using NuGet.Versioning;

namespace BaGet.Azure
{
    using QueryType = Microsoft.Azure.Search.Models.QueryType;
    using SearchParameters = Microsoft.Azure.Search.Models.SearchParameters;

    public class AzureSearchService : ISearchService
    {
        private readonly SearchIndexClient _searchClient;
        private readonly IUrlGenerator _url;
        private readonly IFrameworkCompatibilityService _frameworks;

        public AzureSearchService(
            SearchIndexClient searchClient,
            IUrlGenerator url,
            IFrameworkCompatibilityService frameworks)
        {
            _searchClient = searchClient ?? throw new ArgumentNullException(nameof(searchClient));
            _url = url ?? throw new ArgumentNullException(nameof(url));
            _frameworks = frameworks ?? throw new ArgumentNullException(nameof(frameworks));
        }

        public async Task<SearchResponse> SearchAsync(
            SearchRequest request,
            CancellationToken cancellationToken)
        {
            var searchText = BuildSeachQuery(request.Query, request.PackageType, request.Framework);
            var filter = BuildSearchFilter(request.IncludePrerelease, request.IncludeSemVer2);
            var parameters = new SearchParameters
            {
                IncludeTotalResultCount = true,
                QueryType = QueryType.Full,
                Skip = request.Skip,
                Top = request.Take,
                Filter = filter
            };

            var response = await _searchClient.Documents.SearchAsync<PackageDocument>(
                searchText,
                parameters,
                cancellationToken: cancellationToken);

            var results = new List<SearchResult>();

            foreach (var result in response.Results)
            {
                var document = result.Document;
                var versions = new List<SearchResultVersion>();

                if (document.Versions.Length != document.VersionDownloads.Length)
                {
                    throw new InvalidOperationException($"Invalid document {document.Key} with mismatched versions");
                }

                for (var i = 0; i < document.Versions.Length; i++)
                {
                    var version = NuGetVersion.Parse(document.Versions[i]);

                    versions.Add(new SearchResultVersion
                    {
                        RegistrationLeafUrl = _url.GetRegistrationLeafUrl(document.Id, version),
                        Version = document.Versions[i],
                        Downloads = long.Parse(document.VersionDownloads[i]),
                    });
                }

                var iconUrl = document.HasEmbeddedIcon
                    ? _url.GetPackageIconDownloadUrl(document.Id, NuGetVersion.Parse(document.Version))
                    : document.IconUrl;

                results.Add(new SearchResult
                {
                    PackageId =  document.Id,
                    Version = document.Version,
                    Description = document.Description,
                    Authors = document.Authors,
                    IconUrl = iconUrl,
                    LicenseUrl = document.LicenseUrl,
                    ProjectUrl = document.ProjectUrl,
                    RegistrationIndexUrl = _url.GetRegistrationIndexUrl(document.Id),
                    Summary = document.Summary,
                    Tags = document.Tags,
                    Title = document.Title,
                    TotalDownloads = document.TotalDownloads,
                    Versions = versions
                });
            }

            return new SearchResponse
            {
                TotalHits = response.Count.Value,
                Data = results,
                Context = SearchContext.Default(_url.GetPackageMetadataResourceUrl())
            };
        }

        public async Task<AutocompleteResponse> AutocompleteAsync(
            AutocompleteRequest request,
            CancellationToken cancellationToken)
        {
            // TODO: Do a prefix search on the package id field. This needs an index field that
            // preserves the id as a whole, as wildcard terms bypass lexical analysis and the "id"
            // field uses the default analyzer, which breaks ids apart on "." and "-".
            // TODO: Support the package type filter. "packageTypes" is searchable but not
            // filterable, so it can only be expressed as a query clause, which would also mean
            // reinterpreting the caller's query as full Lucene syntax.
            // See: https://github.com/loic-sharma/BaGet/issues/291
            var parameters = new SearchParameters
            {
                IncludeTotalResultCount = true,
                Skip = request.Skip,
                Top = request.Take,

                // The index holds one document per package id per search filter, so without this
                // the same id is returned up to four times and the total hit count is inflated.
                Filter = BuildSearchFilter(request.IncludePrerelease, request.IncludeSemVer2),
            };

            var response = await _searchClient.Documents.SearchAsync<PackageDocument>(
                request.Query,
                parameters,
                cancellationToken: cancellationToken);

            var results = response.Results
                .Select(r => r.Document.Id)
                .ToList()
                .AsReadOnly();

            return new AutocompleteResponse
            {
                TotalHits = response.Count.Value,
                Data = results,
                Context = AutocompleteContext.Default
            };
        }

        public Task<AutocompleteResponse> ListPackageVersionsAsync(
            VersionsRequest request,
            CancellationToken cancellationToken)
        {
            // TODO: Support versions autocomplete. This needs an exact lookup of a single package
            // id, which the index cannot do today: "id" is stored with the author's casing and is
            // analyzed by the default analyzer (which splits ids on "." and "-"), so neither a
            // case-sensitive OData "id eq" filter nor a term query is reliable. It needs an index
            // field holding the lowercased id, which is a schema change plus a reindex.
            // See: https://github.com/loic-sharma/BaGet/issues/291
            throw new NotImplementedException();
        }

        public async Task<DependentsResponse> FindDependentsAsync(
            string packageId,
            CancellationToken cancellationToken)
        {
            var query = BuildDependentsQuery(packageId);
            var parameters = new SearchParameters
            {
                IncludeTotalResultCount = true,
                QueryType = QueryType.Full,
                Skip = 0,
                Top = 20,
            };

            var response = await _searchClient.Documents.SearchAsync<PackageDocument>(query, parameters, cancellationToken: cancellationToken);
            var results = response.Results
                .Select(r => new PackageDependent
                {
                    Id = r.Document.Id,
                    Description = r.Document.Description,
                    TotalDownloads = r.Document.TotalDownloads
                })
                .ToList()
                .AsReadOnly();

            return new DependentsResponse
            {
                TotalHits = response.Count.Value,
                Data = results
            };
        }

        /// <summary>
        /// Builds the query that finds the packages which depend on <paramref name="packageId"/>.
        /// It runs with <see cref="QueryType.Full"/>, the full Lucene query syntax.
        /// </summary>
        internal static string BuildDependentsQuery(string packageId)
        {
            if (packageId == null)
            {
                throw new ArgumentNullException(nameof(packageId));
            }

            // "dependencies" is indexed with ExactMatchCustomAnalyzer (a keyword tokenizer plus a
            // lowercase filter) and holds lowercased ids, so the whole id must reach the analyzer
            // as a single entity.
            return $"dependencies:{EscapeQueryPhrase(packageId.ToLowerInvariant())}";
        }

        /// <summary>
        /// Turns <paramref name="value"/> into a quoted phrase, which the full Lucene query parser
        /// evaluates as a single literal entity, so that the value cannot change the meaning of the
        /// query it is embedded in.
        /// </summary>
        /// <remarks>
        /// In an unquoted term the full Lucene syntax treats
        /// <c>+ - &amp; | ! ( ) { } [ ] ^ " ~ * ? : \ /</c> as operators, and whitespace splits one
        /// term into several - whitespace is not in that list and cannot be escaped, yet it is what
        /// lets <c>AND</c>, <c>OR</c> and <c>NOT</c> become operators. Quoting solves both at once:
        /// placement decides whether a symbol is an operator, and inside quotation marks every
        /// character is content (wildcard, fuzzy, proximity and regex constructs are documented as
        /// applying to a term, not to a phrase). That leaves exactly two significant characters,
        /// each escaped here with a single backslash, the documented escape:
        /// <list type="bullet">
        /// <item><description><c>"</c>, which would end the phrase early;</description></item>
        /// <item><description><c>\</c>, the escape character itself, which would otherwise swallow
        /// the character after it - a trailing one would escape the closing quotation mark.</description></item>
        /// </list>
        /// Neither is legal in a NuGet package id, so an ordinary id is emitted verbatim between
        /// quotation marks and no backslash ever reaches the parser for a real id.
        /// See https://learn.microsoft.com/azure/search/query-lucene-syntax.
        /// </remarks>
        internal static string EscapeQueryPhrase(string value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            var builder = new StringBuilder(value.Length + 2);

            builder.Append('"');

            foreach (var character in value)
            {
                if (character == '"' || character == '\\')
                {
                    builder.Append('\\');
                }

                builder.Append(character);
            }

            builder.Append('"');

            return builder.ToString();
        }

        private string BuildSeachQuery(string query, string packageType, string framework)
        {
            var queryBuilder = new StringBuilder();

            if (!string.IsNullOrEmpty(query))
            {
                queryBuilder.Append(query.TrimEnd().TrimEnd('*'));
                queryBuilder.Append('*');
            }

            if (!string.IsNullOrEmpty(packageType))
            {
                queryBuilder.Append(" +packageTypes:");
                queryBuilder.Append(packageType);
            }

            if (!string.IsNullOrEmpty(framework))
            {
                var frameworks = _frameworks.FindAllCompatibleFrameworks(framework);

                queryBuilder.Append(" +frameworks:(");
                queryBuilder.Append(string.Join(" ", frameworks));
                queryBuilder.Append(')');
            }

            return queryBuilder.ToString();
        }

        /// <summary>
        /// Builds the OData filter that selects the single document matching these search filters.
        /// <see cref="IndexActionBuilder"/> writes one document per <see cref="SearchFilters"/>
        /// value, using the same enum name as its "searchFilters" field.
        /// </summary>
        internal static string BuildSearchFilter(bool includePrerelease, bool includeSemVer2)
        {
            var searchFilters = SearchFilters.Default;

            if (includePrerelease)
            {
                searchFilters |= SearchFilters.IncludePrerelease;
            }

            if (includeSemVer2)
            {
                searchFilters |= SearchFilters.IncludeSemVer2;
            }

            return $"searchFilters eq '{searchFilters}'";
        }
    }
}
