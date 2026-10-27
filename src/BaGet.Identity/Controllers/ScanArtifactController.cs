using System.Threading;
using System.Threading.Tasks;
using BaGet.Identity.Application.Models;
using BaGet.Identity.Controllers.Base;
using BaGet.Identity.Controllers.UI;
using BaGet.Identity.Mvc;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Identity.Controllers;

/// <summary>
/// <em>Scan now</em> for one version of an artifact of any format (<c>/scan-artifact</c>, also at <c>/r/{repo}/…</c>). It replaces
/// the <c>/Packages/Scan</c> Razor Page (same URL, same form fields, same gates: signed in, the Manage action on the artifact); the work is
/// done by <see cref="ScanArtifactUiApiController"/> (the JSON twin, over <c>ScanArtifactUseCase</c>), which this controller calls in
/// process and renders.
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
        => Render(await Api<ScanArtifactUiApiController, ScanViewModel>(api => api.Get(name, version, returnUrl, cancellationToken)), ViewName);

    /// <summary>Scans the version now (or queues it, for an image) and shows the outcome.</summary>
    [HttpPost, PageHandler]
    public async Task<IActionResult> Scan(string? name, string? version, string? returnUrl, CancellationToken cancellationToken)
        => Render(
            await Api<ScanArtifactUiApiController, ScanViewModel>(api => api.Scan(new ScanArtifactForm { Name = name, Version = version, ReturnUrl = returnUrl }, cancellationToken)),
            ViewName);
}
