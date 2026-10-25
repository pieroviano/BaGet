using System.Threading;
using System.Threading.Tasks;
using BaGet.Identity.Mvc;
using BaGet.Identity.Workflows;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Identity.Controllers;

/// <summary>
/// <em>Scan now</em> for one version of an artifact of any format (<c>/scan-artifact</c>, also at <c>/r/{repo}/…</c>). It replaces
/// the <c>/Packages/Scan</c> Razor Page (same URL, same form fields, same gates: signed in, the Manage action on the artifact);
/// the logic is <see cref="ScanWorkflow"/>, shared with the obsolete <c>ScanModel</c>.
/// </summary>
[Route("scan-artifact")]
[LegacyPage("/Packages/Scan")]
[RepositoryPage(AnyFormat = true, NotOnVirtual = true)]
public sealed class ScanArtifactController : AccountControllerBase
{
    private const string ViewName = "Index";

    /// <inheritdoc/>
    protected override bool RequiresSignIn => true;

    /// <summary>Shows the form (and the stored result) of the version.</summary>
    [HttpGet, HttpHead, PageHandler]
    public async Task<IActionResult> Index(string? name, string? version, string? returnUrl, CancellationToken cancellationToken)
    {
        var workflow = new ScanWorkflow(HttpContext, Url, name, version, returnUrl);

        return PackageControllerSupport.Resolve(this, await workflow.GetAsync(cancellationToken), ViewName, workflow.Model);
    }

    /// <summary>Scans the version now (or queues it, for an image) and shows the outcome.</summary>
    [HttpPost, PageHandler]
    public async Task<IActionResult> Scan(string? name, string? version, string? returnUrl, CancellationToken cancellationToken)
    {
        var workflow = new ScanWorkflow(HttpContext, Url, name, version, returnUrl);

        return PackageControllerSupport.Resolve(this, await workflow.PostAsync(cancellationToken), ViewName, workflow.Model);
    }
}
