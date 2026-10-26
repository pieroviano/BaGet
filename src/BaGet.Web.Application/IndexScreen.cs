using System;
using System.Collections.Generic;
using BaGet.Core;
using BaGet.Protocol.Models;

namespace BaGet.Web;

/// <summary>What the visitor asked the front page or the package list for (the query string of <c>/</c>, <c>/packages</c>).</summary>
/// <param name="Query">The search text (<c>q</c>).</param>
/// <param name="PageIndex">The 1-based page of results (<c>p</c>).</param>
/// <param name="PackageType">The package type filter, <c>any</c> for none.</param>
/// <param name="Framework">The target framework filter, <c>any</c> for none.</param>
/// <param name="Prerelease">Whether prerelease versions are included.</param>
/// <param name="SortBy">The requested order.</param>
public sealed record IndexQuery(
    string? Query = null,
    int PageIndex = 1,
    string PackageType = "any",
    string Framework = "any",
    bool Prerelease = true,
    SearchSort SortBy = SearchSort.Relevance);

/// <summary>What the front page and the package list show: the search that was asked for and its results.</summary>
public sealed record IndexScreen
{
    /// <summary>The search text.</summary>
    public string? Query { get; init; }

    /// <summary>The 1-based page of results.</summary>
    public int PageIndex { get; init; } = 1;

    /// <summary>The package type filter, <c>any</c> for none.</summary>
    public string PackageType { get; init; } = "any";

    /// <summary>The target framework filter, <c>any</c> for none.</summary>
    public string Framework { get; init; } = "any";

    /// <summary>Whether prerelease versions are included.</summary>
    public bool Prerelease { get; init; } = true;

    /// <summary>The requested order.</summary>
    public SearchSort SortBy { get; init; } = SearchSort.Relevance;

    /// <summary>The orders the search provider can apply on the server; with only the default there is nothing to choose.</summary>
    public IReadOnlyList<SearchSort> SupportedSorts { get; init; } = new[] { SearchSort.Relevance };

    /// <summary>True for the front page: the repository's root with no search. Everything else lists packages.</summary>
    public bool IsHome { get; init; }

    /// <summary>The feed's totals, shown on the front page; null when the database cannot report them.</summary>
    public FeedStatistics? Totals { get; init; }

    /// <summary>The page of results (empty on the front page).</summary>
    public IReadOnlyList<SearchResult> Packages { get; init; } = Array.Empty<SearchResult>();

    /// <summary>When each listed package ID was last published, by ID; empty when unsupported.</summary>
    public IReadOnlyDictionary<string, DateTime> LastUpdated { get; init; } = new Dictionary<string, DateTime>();
}
