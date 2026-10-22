using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BaGet.Web;

/// <summary>
/// The front page of a Maven repository: a search box and the artifacts whose coordinates or POM name or description contain the
/// text, by coordinates, with the configuration that points Maven and Gradle at the repository (<c>settings.xml</c>,
/// <c>distributionManagement</c>). Served at <c>/r/{repo}/maven-artifacts</c> (<see cref="RepositoryPageRouteConvention"/>);
/// <see cref="RepositoryPageFilter"/> requires Read on the repository and answers 404 in a repository that is not Maven's.
/// </summary>
public class MavenArtifactsModel : PageModel
{
    /// <summary>Artifacts per page.</summary>
    public const int ResultsPerPage = 20;

    private readonly IMavenRepository _maven;
    private readonly IUrlGenerator _urls;
    private readonly IRepositoryAccess _access;
    private readonly IRepositoryContext _repository;

    public MavenArtifactsModel(IMavenRepository maven, IUrlGenerator urls, IRepositoryAccess access, IRepositoryContext repository)
    {
        _maven = maven ?? throw new ArgumentNullException(nameof(maven));
        _urls = urls ?? throw new ArgumentNullException(nameof(urls));
        _access = access ?? throw new ArgumentNullException(nameof(access));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <summary>The search text.</summary>
    [BindProperty(Name = "q", SupportsGet = true)]
    public string? Query { get; set; }

    /// <summary>The page, from 1.</summary>
    [BindProperty(Name = "p", SupportsGet = true)]
    [Range(1, int.MaxValue)]
    public int PageIndex { get; set; } = 1;

    /// <summary>This page of results.</summary>
    public MavenSearchPage Result { get; private set; } = new(0, Array.Empty<MavenSearchHit>());

    /// <summary>The repository's policy, redeploy and snapshot retention.</summary>
    public MavenRepositorySettings Settings { get; private set; } = new();

    /// <summary>The address Maven is configured with for this repository.</summary>
    public string RepositoryUrl => _urls.GetMavenRepositoryUrl();

    /// <summary>The repository's name, which is the <c>id</c> in <c>settings.xml</c> and the POM.</summary>
    public string RepositoryName => _repository.Current.Name;

    /// <summary>Whether another page follows.</summary>
    public bool HasNext => PageIndex * ResultsPerPage < Result.Total;

    internal IReadOnlyList<InstallSnippet> ConfigurationTabs { get; private set; } = Array.Empty<InstallSnippet>();

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest();

        // The listing names only the artifacts whose pages the visitor may open.
        var repository = _repository.Current;
        Result = await _maven.SearchAsync(
            Query,
            (PageIndex - 1) * ResultsPerPage,
            ResultsPerPage,
            (name, token) => _access.CanReadPackageAsync(User, repository, name, token),
            cancellationToken);
        Settings = await _maven.GetSettingsAsync(cancellationToken);

        var id = RepositoryName;
        var url = RepositoryUrl;
        ConfigurationTabs = new[]
        {
            new InstallSnippet { Name = "settings.xml", Lines = MavenSnippets.SettingsXml(id) },
            new InstallSnippet { Name = "Deploy (pom.xml)", Lines = MavenSnippets.DistributionManagement(id, url) },
            new InstallSnippet { Name = "Resolve (pom.xml)", Lines = MavenSnippets.Repositories(id, url, Settings.Policy != MavenPolicy.Snapshots, Settings.Policy != MavenPolicy.Releases) },
            new InstallSnippet { Name = "Gradle", Lines = MavenSnippets.GradleRepository(url) },
        };

        return Page();
    }
}
