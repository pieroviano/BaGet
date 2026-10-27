using System.Threading;
using System.Threading.Tasks;
using BaGet.Identity.Application.Screens;
using BaGet.Identity.Controllers.Base;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using BaGet.Identity.Application.Models.Operations;
using BaGet.Identity.Application.UseCases.Packages;
using BaGet.Identity.Controllers.FrontEnd;
using BaGet.Identity.Mvc.Attributes;
using BaGet.Identity.Mvc.UI;

namespace BaGet.Identity.Controllers.UI;

/// <summary>
/// The JSON twin of the validation status screen: <c>GET /api/ui/packages/{id}/validation</c> (also under <c>/api/ui/r/{repo}/…</c>), the
/// newest submission of each version and the verdict of its steps; 403 without Push or Manage on the ID, 404 when validation never saw it.
/// Screen only: over HTTP it answers 403 to every caller. <see cref="ValidationStatusPageController"/> calls it in process and renders the view.
/// </summary>
[Route("api/ui/packages/{id}/validation")]
[RepositoryPage(NotOnVirtual = true)]
public sealed class ValidationStatusUiApiController : AccountUiApiControllerBase
{
    /// <inheritdoc/>
    protected override bool RequiresSignIn => true;

    private ValidationStatusUseCase UseCase => HttpContext.RequestServices.GetRequiredService<ValidationStatusUseCase>();

    /// <summary>The submissions of the ID.</summary>
    [HttpGet, UiApi("/Packages/ValidationStatus")]
    public async Task<ActionResult<ScreenResult<ValidationStatusViewModel>>> Get(string? id, CancellationToken cancellationToken)
        => Respond(await UseCase.GetAsync(User, id, cancellationToken));
}
