using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Protocol.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace BaGet.Web;

/// <summary>
/// The logic of the front page and the package list, shared by <see cref="HomeController"/> and the obsolete <see cref="IndexModel"/>.
/// </summary>
public sealed class IndexWorkflow
{
    /// <summary>Results per page.</summary>
    public const int ResultsPerPage = 20;

    private readonly ISearchService _search;
    private readonly StatisticsService _statistics;

    /// <summary>Creates the workflow.</summary>
    public IndexWorkflow(ISearchService search, StatisticsService statistics)
    {
        _search = search ?? throw new ArgumentNullException(nameof(search));
        _statistics = statistics ?? throw new ArgumentNullException(nameof(statistics));
    }

    /// <summary>The orders the search provider can apply (only relevance when it says nothing).</summary>
    public IReadOnlyCollection<SearchSort> SupportedSorts
        => _search.SupportedSorts ?? new[] { SearchSort.Relevance };

    /// <summary>False when the requested sort is not a defined value (answered 400).</summary>
    public static bool IsValid(IndexViewModel model) => Enum.IsDefined(typeof(SearchSort), model.SortBy);

    /// <summary>Fills the results of <paramref name="model"/>: the totals on the front page, else one page of search results.</summary>
    public async Task LoadAsync(IndexViewModel model, HttpRequest? request, CancellationToken cancellationToken)
    {
        model.SupportedSorts = SupportedSorts;
        model.IsHome = IsFrontPage(model, request);
        if (model.IsHome)
        {
            model.Packages = Array.Empty<SearchResult>();
            if (_statistics.IsSupported)
            {
                model.Totals = await _statistics.GetAsync(cancellationToken);
            }

            return;
        }

        var packageType = model.PackageType == "any" ? null : model.PackageType;
        var framework = model.Framework == "any" ? null : model.Framework;

        var search = await _search.SearchAsync(
            new SearchRequest
            {
                Skip = (model.PageIndex - 1) * ResultsPerPage,
                Take = ResultsPerPage,
                IncludePrerelease = model.Prerelease,
                IncludeSemVer2 = true,
                PackageType = packageType,
                Framework = framework,
                Query = model.Query,

                // A provider that cannot sort ignores the request, so the page stays honest
                // about it by not offering the choice.
                SortBy = model.CanSort && model.SupportedSorts.Contains(model.SortBy) ? model.SortBy : SearchSort.Relevance,
            },
            cancellationToken);

        model.Packages = search.Data;
        model.LastUpdated = await _statistics.GetLastUpdatedAsync(
            (model.Packages ?? Array.Empty<SearchResult>()).Select(p => p.PackageId).ToList(),
            cancellationToken,
            includePrerelease: model.Prerelease,
            includeSemVer2: true);
    }

    private static bool IsFrontPage(IndexViewModel model, HttpRequest? request)
    {
        if (request == null) return false;

        // The front page of a repository is its root: "/" for the default, "/r/{name}" for another.
        var path = request.Path.Value?.TrimEnd('/');
        var root = request.HttpContext.GetRouteValue(Routes.RepositoryRouteKey) is string repository ? "/r/" + repository : string.Empty;
        if (!string.Equals(path ?? string.Empty, root, StringComparison.OrdinalIgnoreCase)) return false;

        return !request.Query.ContainsKey("q")
            && string.IsNullOrWhiteSpace(model.Query)
            && model.PackageType == "any"
            && model.Framework == "any"
            && model.Prerelease
            && model.PageIndex == 1;
    }
}
