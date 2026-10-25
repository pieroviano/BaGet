using System;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BaGet.Web;

/// <summary>
/// The page model of the front page of a Maven repository when it was a Razor Page.
/// </summary>
/// <remarks>
/// Kept as a shim so that code compiled against it still loads and runs (no breaking change of the public API); there is no page
/// behind it any more: <see cref="MavenBrowseController"/> serves <c>/maven-artifacts</c> (and its <c>/r/{repo}/</c> twin) and
/// shares the logic (<c>MavenArtifactsWorkflow</c>).
/// </remarks>
[Obsolete("The Maven artifact list is served by MavenBrowseController; this page model has no page any more and will be removed in the next major version.")]
public class MavenArtifactsModel : PageModel
{
    /// <summary>Artifacts per page.</summary>
    public const int ResultsPerPage = MavenArtifactsWorkflow.ResultsPerPage;

    private readonly MavenArtifactsWorkflow _workflow;
    private readonly IUrlGenerator _urls;
    private readonly IRepositoryContext _repository;

    public MavenArtifactsModel(IMavenRepository maven, IUrlGenerator urls, IRepositoryAccess access, IRepositoryContext repository)
    {
        _workflow = new MavenArtifactsWorkflow(maven, urls, access, repository);
        _urls = urls ?? throw new ArgumentNullException(nameof(urls));
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

    /// <summary>The kind of the repository: a remote one lists what it cached, a virtual one what its members hold.</summary>
    public RepositoryKind Kind => _repository.Current.Kind;

    /// <summary>The repository's name, which is the <c>id</c> in <c>settings.xml</c> and the POM.</summary>
    public string RepositoryName => _repository.Current.Name;

    /// <summary>Whether another page follows.</summary>
    public bool HasNext => PageIndex * ResultsPerPage < Result.Total;

    internal System.Collections.Generic.IReadOnlyList<InstallSnippet> ConfigurationTabs { get; private set; } = Array.Empty<InstallSnippet>();

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest();

        var model = await _workflow.LoadAsync(Query, PageIndex, User, cancellationToken);
        Result = model.Result;
        Settings = model.Settings;
        ConfigurationTabs = model.ConfigurationTabs;

        return Page();
    }
}
