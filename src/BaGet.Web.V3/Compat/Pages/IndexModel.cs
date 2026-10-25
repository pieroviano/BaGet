using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Protocol.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BaGet.Web;

/// <summary>The page model of the front page and package list when they were a Razor Page.</summary>
/// <remarks>
/// Kept as a shim so that code compiled against it still loads and runs (no breaking change of the public API); there is no page
/// behind it any more: <see cref="HomeController"/> serves <c>/</c> and <c>/packages</c>. Both share <see cref="IndexWorkflow"/>.
/// </remarks>
[Obsolete("The front page and package list are served by HomeController; this page model has no page any more and will be removed in the next major version.")]
public class IndexModel : PageModel
{
    private readonly IndexWorkflow _workflow;

    /// <summary>Creates the page model.</summary>
    public IndexModel(ISearchService search, StatisticsService statistics)
    {
        _workflow = new IndexWorkflow(search, statistics);
    }

    /// <summary>Results per page.</summary>
    public const int ResultsPerPage = IndexWorkflow.ResultsPerPage;

    /// <summary>The search text.</summary>
    [BindProperty(Name = "q", SupportsGet = true)]
    public string? Query { get; set; }

    /// <summary>The 1-based page of results.</summary>
    [BindProperty(Name = "p", SupportsGet = true)]
    [Range(1, int.MaxValue)]
    public int PageIndex { get; set; } = 1;

    /// <summary>The package type filter, <c>any</c> for none.</summary>
    [BindProperty(SupportsGet = true)]
    public string PackageType { get; set; } = "any";

    /// <summary>The target framework filter, <c>any</c> for none.</summary>
    [BindProperty(SupportsGet = true)]
    public string Framework { get; set; } = "any";

    /// <summary>Whether prerelease versions are included.</summary>
    [BindProperty(SupportsGet = true)]
    public bool Prerelease { get; set; } = true;

    /// <summary>The requested order.</summary>
    [BindProperty(Name = "sortBy", SupportsGet = true)]
    public SearchSort SortBy { get; set; } = SearchSort.Relevance;

    /// <summary>The orders the search provider can apply on the server.</summary>
    public IReadOnlyCollection<SearchSort> SupportedSorts => _workflow.SupportedSorts;

    /// <summary>Whether there is anything to choose among <see cref="SupportedSorts"/>.</summary>
    public bool CanSort => SupportedSorts.Count > 1;

    /// <summary>The page of results.</summary>
    public IReadOnlyList<SearchResult>? Packages { get; private set; }

    /// <summary>True for the front page of the site: the root URL with no search.</summary>
    public bool IsHome { get; private set; }

    /// <summary>The totals of the feed, shown on the front page; null when the database cannot report them.</summary>
    public FeedStatistics? Totals { get; private set; }

    /// <summary>When each listed package ID was last published, by ID; empty when unsupported.</summary>
    public IReadOnlyDictionary<string, DateTime> LastUpdated { get; private set; }
        = new Dictionary<string, DateTime>();

    /// <summary>Runs the search.</summary>
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var model = new IndexViewModel
        {
            Query = Query,
            PageIndex = PageIndex,
            PackageType = PackageType,
            Framework = Framework,
            Prerelease = Prerelease,
            SortBy = SortBy,
        };
        if (!ModelState.IsValid || !IndexWorkflow.IsValid(model)) return BadRequest();

        await _workflow.LoadAsync(model, HttpContext?.Request, cancellationToken);

        IsHome = model.IsHome;
        Totals = model.Totals;
        Packages = model.Packages;
        LastUpdated = model.LastUpdated;

        return Page();
    }
}
