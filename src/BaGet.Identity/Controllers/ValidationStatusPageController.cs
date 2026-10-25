using System.Threading;
using System.Threading.Tasks;
using BaGet.Identity.Mvc;
using BaGet.Identity.Workflows;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Identity.Controllers;

/// <summary>
/// Where a pushed package stands in validation (<c>/packages/{id}/validation</c>, also at <c>/r/{repo}/…</c>): the newest submission
/// of each version and its steps. It replaces the <c>/Packages/ValidationStatus</c> Razor Page (same URL, same gates); the logic is
/// <see cref="ValidationStatusWorkflow"/>, shared with the obsolete <c>ValidationStatusModel</c>.
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
    {
        var workflow = new ValidationStatusWorkflow(HttpContext, id);

        return PackageControllerSupport.Resolve(this, await workflow.GetAsync(cancellationToken), ViewName, workflow.Model);
    }
}
