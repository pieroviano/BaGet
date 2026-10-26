using System.Threading;
using System.Threading.Tasks;
using BaGet.Identity.Mvc;
using BaGet.Identity.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Identity.Controllers;

/// <summary>
/// Where a pushed package stands in validation (<c>/packages/{id}/validation</c>, also at <c>/r/{repo}/…</c>): the newest submission
/// of each version and its steps. It replaces the <c>/Packages/ValidationStatus</c> Razor Page (same URL, same gates); the work is done by
/// <see cref="ValidationStatusUiApiController"/> (the JSON twin, over <c>ValidationStatusUseCase</c>), which this controller calls in
/// process and renders.
/// </summary>
[Route("packages/{id}/validation")]
[LegacyPage("/Packages/ValidationStatus")]
[RepositoryPage(NotOnVirtual = true)]
public sealed class ValidationStatusPageController : AccountControllerBase
{
    private const string ViewName = "Index";

    /// <inheritdoc/>
    protected override bool RequiresSignIn => true;

    /// <summary>Shows the submissions of the ID, 403 without the permission, 404 when validation never saw it.</summary>
    [HttpGet, HttpHead, PageHandler]
    public async Task<IActionResult> Index([FromRoute] string? id, CancellationToken cancellationToken)
        => Render(await Api<ValidationStatusUiApiController, ValidationStatusViewModel>(api => api.Get(id, cancellationToken)), ViewName);
}
