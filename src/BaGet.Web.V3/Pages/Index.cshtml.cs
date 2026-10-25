using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Protocol.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;

namespace BaGet.Web;

public class IndexModel : PageModel
{
    private readonly ISearchService _search;
    private readonly StatisticsService _statistics;

    public IndexModel(ISearchService search, StatisticsService statistics)
    {
        _search = search ?? throw new ArgumentNullException(nameof(search));
        _statistics = statistics ?? throw new ArgumentNullException(nameof(statistics));
    }

    public const int ResultsPerPage = 20;

    [BindProperty(Name = "q", SupportsGet = true)]
    public string? Query { get; set; }

    [BindProperty(Name = "p", SupportsGet = true)]
    [Range(1, int.MaxValue)]
    public int PageIndex { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public string PackageType { get; set; } = "any";

    [BindProperty(SupportsGet = true)]
    public string Framework { get; set; } = "any";

    [BindProperty(SupportsGet = true)]
    public bool Prerelease { get; set; } = true;

    [BindProperty(Name = "sortBy", SupportsGet = true)]
    public SearchSort SortBy { get; set; } = SearchSort.Relevance;

    /// <summary>
    /// The orders the search provider can apply on the server. With only the default there
    /// is nothing to choose, so the page shows no sort control.
    /// </summary>
    public IReadOnlyCollection<SearchSort> SupportedSorts
        => _search.SupportedSorts ?? new[] { SearchSort.Relevance };

    public bool CanSort => SupportedSorts.Count > 1;

    public IReadOnlyList<SearchResult>? Packages { get; private set; }

    /// <summary>
    /// True for the site's front page: the root URL with no search. Everything else,
    /// including "/packages" and an empty search, lists packages.
    /// </summary>
    public bool IsHome { get; private set; }

    /// <summary>
    /// The feed's totals, shown on the front page. Null when the database cannot report them.
    /// </summary>
    public FeedStatistics? Totals { get; private set; }

    /// <summary>
    /// When each listed package ID was last published, by ID. Empty when unsupported.
    /// </summary>
    public IReadOnlyDictionary<string, DateTime> LastUpdated { get; private set; }
        = new Dictionary<string, DateTime>();

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid || !Enum.IsDefined(typeof(SearchSort), SortBy)) return BadRequest();

        IsHome = IsFrontPage();
        if (IsHome)
        {
            Packages = Array.Empty<SearchResult>();
            if (_statistics.IsSupported)
            {
                Totals = await _statistics.GetAsync(cancellationToken);
            }

            return Page();
        }

        var packageType = PackageType == "any" ? null : PackageType;
        var framework = Framework == "any" ? null : Framework;

        var search = await _search.SearchAsync(
            new SearchRequest
            {
                Skip = (PageIndex - 1) * ResultsPerPage,
                Take = ResultsPerPage,
                IncludePrerelease = Prerelease,
                IncludeSemVer2 = true,
                PackageType = packageType,
                Framework = framework,
                Query = Query,

                // A provider that cannot sort ignores the request, so the page stays honest
                // about it by not offering the choice.
                SortBy = CanSort && SupportedSorts.Contains(SortBy) ? SortBy : SearchSort.Relevance,
            },
            cancellationToken);

        Packages = search.Data;
        LastUpdated = await _statistics.GetLastUpdatedAsync(
            (Packages ?? Array.Empty<SearchResult>()).Select(p => p.PackageId).ToList(),
            cancellationToken,
            includePrerelease: Prerelease,
            includeSemVer2: true);

        return Page();
    }

    private bool IsFrontPage()
    {
        var request = HttpContext?.Request;
        if (request == null) return false;

        // The front page of a repository is its root: "/" for the default, "/r/{name}" for another.
        var path = request.Path.Value?.TrimEnd('/');
        var root = request.HttpContext.GetRouteValue(Routes.RepositoryRouteKey) is string repository ? "/r/" + repository : string.Empty;
        if (!string.Equals(path ?? string.Empty, root, StringComparison.OrdinalIgnoreCase)) return false;

        return !request.Query.ContainsKey("q")
            && string.IsNullOrWhiteSpace(Query)
            && PackageType == "any"
            && Framework == "any"
            && Prerelease
            && PageIndex == 1;
    }
}