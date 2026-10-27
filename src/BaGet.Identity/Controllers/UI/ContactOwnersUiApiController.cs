using System.Threading;
using System.Threading.Tasks;
using BaGet.Identity.Application.Screens;
using BaGet.Identity.Controllers.Base;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using BaGet.Identity.Application.Models.Packages;
using BaGet.Identity.Application.UseCases.Packages;
using BaGet.Identity.Controllers.FrontEnd;
using BaGet.Identity.Mvc.Attributes;
using BaGet.Identity.Mvc.UI;

namespace BaGet.Identity.Controllers.UI;

/// <summary>
/// The JSON twin of the contact-owners screen: <c>GET /api/ui/packages/{id}/contact-owners</c> (the form's state) and <c>POST</c> to the same
/// address with <c>{ "message": …, "copyToSelf": … }</c> (also under <c>/api/ui/r/{repo}/…</c>). Needs accounts and mail, like the page.
/// Screen only: over HTTP it answers 403 to every caller. <see cref="ContactOwnersController"/> calls it in process and renders the view.
/// </summary>
[Route("api/ui/packages/{id}/contact-owners")]
[RepositoryPage(NotOnVirtual = true)]
public sealed class ContactOwnersUiApiController : AccountUiApiControllerBase
{
    private const string Screen = "/Packages/ContactOwners";

    /// <inheritdoc/>
    protected override bool RequiresSignIn => true;

    /// <inheritdoc/>
    protected override bool RequiresEmail => true;

    private ContactOwnersUseCase UseCase => HttpContext.RequestServices.GetRequiredService<ContactOwnersUseCase>();

    /// <summary>The form's state; 404 when the package has no owner.</summary>
    [HttpGet, UiApi(Screen)]
    public async Task<ActionResult<ScreenResult<ContactOwnersViewModel>>> Get(string? id, CancellationToken cancellationToken)
        => Respond(await UseCase.GetAsync(User, id, cancellationToken));

    /// <summary>Sends the message to the owners (400 with the field errors when the message is missing or too long).</summary>
    [HttpPost, UiApi(Screen)]
    public async Task<ActionResult<ScreenResult<ContactOwnersViewModel>>> Send(string? id, [FromBody] ContactOwnersForm? form, CancellationToken cancellationToken)
        => Respond(await UseCase.SendAsync(User, id, form?.Message, form?.CopyToSelf ?? false, cancellationToken));
}
