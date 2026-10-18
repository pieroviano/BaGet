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
    public string Query { get; set; }

    [BindProperty(Name = "p", SupportsGet = true)]
    [Range(1, int.MaxValue)]
    public int PageIndex { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public string PackageType { get; set; } = "any";

    [BindProperty(SupportsGet = true)]
    public string Framework { get; set; } = "any";

    [BindProperty(SupportsGet = true)]
    public bool Prerelease { get; set; } = true;

    public IReadOnlyList<SearchResult> Packages { get; private set; }

    /// <summary>
    /// True for the site's front page: the root URL with no search. Everything else,
    /// including "/packages" and an empty search, lists packages.
    /// </summary>
    public bool IsHome { get; private set; }

    /// <summary>
    /// The feed's totals, shown on the front page. Null when the database cannot report them.
    /// </summary>
    public FeedStatistics Totals { get; private set; }

    /// <summary>
    /// When each listed package ID was last published, by ID. Empty when unsupported.
    /// </summary>
    public IReadOnlyDictionary<string, DateTime> LastUpdated { get; private set; }
        = new Dictionary<string, DateTime>();

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest();

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
            },
            cancellationToken);

        Packages = search.Data;
        LastUpdated = await _statistics.GetLastUpdatedAsync(
            (Packages ?? Array.Empty<SearchResult>()).Select(p => p.PackageId).ToList(),
            cancellationToken);

        return Page();
    }

    private bool IsFrontPage()
    {
        var request = HttpContext?.Request;
        if (request == null) return false;

        var path = request.Path.Value?.TrimEnd('/');
        if (!string.IsNullOrEmpty(path)) return false;

        return !request.Query.ContainsKey("q")
            && string.IsNullOrWhiteSpace(Query)
            && PackageType == "any"
            && Framework == "any"
            && Prerelease
            && PageIndex == 1;
    }
}