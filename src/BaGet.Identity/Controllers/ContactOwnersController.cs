using System.Threading;
using System.Threading.Tasks;
using BaGet.Identity.Mvc;
using BaGet.Identity.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Identity.Controllers;

/// <summary>
/// Lets a signed-in user write to the owners of a package (<c>/packages/{id}/contact-owners</c>, also at <c>/r/{repo}/…</c>). It
/// replaces the <c>/Packages/ContactOwners</c> Razor Page (same URLs and form fields, same gates: accounts enabled, mail configured,
/// signed in); the work is done by <see cref="ContactOwnersUiApiController"/> (the JSON twin, over <c>ContactOwnersUseCase</c>), which this
/// controller calls in process and renders.
/// </summary>
[Route("packages/{id}/contact-owners")]
[LegacyPage("/Packages/ContactOwners")]
[RepositoryPage(NotOnVirtual = true)]
public sealed class ContactOwnersController : AccountControllerBase
{
    private const string ViewName = "Index";

    /// <inheritdoc/>
    protected override bool RequiresSignIn => true;

    /// <inheritdoc/>
    protected override bool RequiresEmail => true;

    /// <summary>Shows the form; 404 when the package has no owner.</summary>
    [HttpGet, HttpHead, PageHandler]
    public async Task<IActionResult> Index([FromRoute] string? id, CancellationToken cancellationToken)
        => Render(await Api<ContactOwnersUiApiController, ContactOwnersViewModel>(api => api.Get(id, cancellationToken)), ViewName);

    /// <summary>Sends the message to the owners.</summary>
    [HttpPost, PageHandler]
    public async Task<IActionResult> Send([FromRoute] string? id, [FromForm] ContactOwnersForm form, CancellationToken cancellationToken)
        => Render(await Api<ContactOwnersUiApiController, ContactOwnersViewModel>(api => api.Send(id, form, cancellationToken)), ViewName);
}
