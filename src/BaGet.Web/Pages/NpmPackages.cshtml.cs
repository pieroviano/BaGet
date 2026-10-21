using System;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BaGet.Web;

/// <summary>
/// The front page of an npm repository: a search box and the packages whose name contains the text, ordered by name, with the
/// commands that point a client at the registry. Served at <c>/r/{repo}/npm-packages</c> (<see cref="RepositoryPageRouteConvention"/>);
/// <see cref="RepositoryPageFilter"/> requires Read on the repository and answers 404 in a repository that is not npm's.
/// </summary>
public class NpmPackagesModel : PageModel
{
    /// <summary>Packages per page.</summary>
    public const int ResultsPerPage = 20;

    private readonly INpmRegistry _registry;
    private readonly IUrlGenerator _urls;

    private readonly IRepositoryAccess _access;
    private readonly IRepositoryContext _repository;

    public NpmPackagesModel(INpmRegistry registry, IUrlGenerator urls, IRepositoryAccess access, IRepositoryContext repository)
    {
        _access = access ?? throw new ArgumentNullException(nameof(access));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
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

        // The listing names only the packages whose pages the visitor may open.
        var repository = _repository.Current;
        Result = await _registry.SearchAsync(
            Query,
            (PageIndex - 1) * ResultsPerPage,
            ResultsPerPage,
            (name, token) => _access.CanReadPackageAsync(User, repository, name, token),
            cancellationToken);

        return Page();
    }
}
