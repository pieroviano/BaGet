using System;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BaGet.Web;

/// <summary>
/// The page model of the front page of an npm repository when it was a Razor Page.
/// </summary>
/// <remarks>
/// Kept as a shim so that code compiled against it still loads and runs (no breaking change of the public API); there is no page
/// behind it any more: <see cref="NpmBrowseController"/> serves <c>/npm-packages</c> (and <c>/r/{repo}/npm-packages</c>) and
/// shares the logic (<c>NpmPackagesWorkflow</c>).
/// </remarks>
[Obsolete("The npm package list is served by NpmBrowseController; this page model has no page any more and will be removed in the next major version.")]
public class NpmPackagesModel : PageModel
{
    /// <summary>Packages per page.</summary>
    public const int ResultsPerPage = NpmPackagesWorkflow.ResultsPerPage;

    private readonly NpmPackagesWorkflow _workflow;
    private readonly IUrlGenerator _urls;

    public NpmPackagesModel(INpmRegistry registry, IUrlGenerator urls, IRepositoryAccess access, IRepositoryContext repository)
    {
        _workflow = new NpmPackagesWorkflow(registry, urls, access, repository);
        _urls = urls ?? throw new ArgumentNullException(nameof(urls));
    }

    /// <summary>The search text.</summary>
    [BindProperty(Name = "q", SupportsGet = true)]
    public string? Query { get; set; }

    /// <summary>The page, from 1.</summary>
    [BindProperty(Name = "p", SupportsGet = true)]
    [Range(1, int.MaxValue)]
    public int PageIndex { get; set; } = 1;

    /// <summary>This page of results.</summary>
    public NpmSearchPage Result { get; private set; } = new(0, Array.Empty<NpmSearchHit>());

    /// <summary>The address npm is configured with for this repository.</summary>
    public string RegistryUrl => _urls.GetNpmRegistryUrl();

    /// <summary>Whether another page follows.</summary>
    public bool HasNext => PageIndex * ResultsPerPage < Result.Total;

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest();

        Result = (await _workflow.LoadAsync(Query, PageIndex, User, cancellationToken)).Result;

        return Page();
    }
}
