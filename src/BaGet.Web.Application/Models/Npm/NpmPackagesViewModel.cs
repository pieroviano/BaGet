using System;
using System.Collections.Generic;
using BaGet.Core;
using Microsoft.AspNetCore.Html;
using BaGet.Web.Application.UseCases;

namespace BaGet.Web.Application.Models.Npm;

/// <summary>What the npm front page (<c>/npm-packages</c>) shows: this page of search results and the registry address.</summary>
public sealed class NpmPackagesViewModel
{
    /// <summary>The search text.</summary>
    public string? Query { get; init; }

    /// <summary>The page, from 1.</summary>
    public int PageIndex { get; init; } = 1;

    /// <summary>This page of results.</summary>
    public NpmSearchPage Result { get; init; } = new(0, Array.Empty<NpmSearchHit>());

    /// <summary>The address npm is configured with for this repository.</summary>
    public string RegistryUrl { get; init; } = string.Empty;

    /// <summary>Whether another page follows.</summary>
    public bool HasNext => PageIndex * NpmBrowseUseCase.ResultsPerPage < Result.Total;
}
