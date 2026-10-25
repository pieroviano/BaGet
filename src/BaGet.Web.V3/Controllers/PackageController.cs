using System;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Identity.Mvc;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Web;

/// <summary>
/// The page of a package (<c>/packages/{id}/{version?}</c>, and under <c>/r/{repo}/</c>): the requested version, else the newest
/// listed one. Replaces the <c>/Package</c> Razor Page. The access rule on the
/// package (Read) is the repository check of <see cref="RepositoryPageAttribute"/>.
/// </summary>
[RepositoryPage]
public sealed class PackageController : UiControllerBase
{
    private readonly PackageWorkflow _workflow;

    /// <summary>Creates the controller.</summary>
    public PackageController(PackageWorkflow workflow)
    {
        _workflow = workflow ?? throw new ArgumentNullException(nameof(workflow));
    }

    /// <summary>Shows the package.</summary>
    [Route("packages/{id}/{version?}"), HttpGet, HttpHead, PageHandler, LegacyPage("/Package")]
    public async Task<IActionResult> Show(string id, string? version, CancellationToken cancellationToken)
        => View("Show", await _workflow.LoadAsync(id, version, HttpContext, Url, cancellationToken));
}
