using System.Threading;
using System.Threading.Tasks;
using BaGet.Identity.Application;
using BaGet.Identity.Application.Models;
using BaGet.Identity.Application.Screens;
using BaGet.Identity.Application.UseCases;
using BaGet.Identity.Controllers.Base;
using BaGet.Identity.Mvc;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

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

/// <summary>The body of the scan twin's POST: the artifact and version to scan, and where the link back goes.</summary>
public sealed class ScanArtifactForm
{
    /// <summary>The artifact name (NuGet id, npm name, Maven <c>group:artifact</c>, Docker image).</summary>
    public string? Name { get; set; }

    /// <summary>The version (Docker: the manifest digest).</summary>
    public string? Version { get; set; }

    /// <summary>Where the link back goes (a local address only).</summary>
    public string? ReturnUrl { get; set; }
}

/// <summary>
/// The JSON twin of <em>Scan now</em>: <c>GET /api/ui/scan-artifact?name=&amp;version=</c> (the stored result) and <c>POST
/// /api/ui/scan-artifact</c> with <c>{ "name": …, "version": … }</c> (scans now, or queues an image; also under <c>/api/ui/r/{repo}/…</c>).
/// 403 without the Manage action on the artifact. A refused scan answers with the screen under 409 (scanning unavailable) or 404 (no such
/// version). Screen only: over HTTP it answers 403 to every caller. <see cref="ScanArtifactController"/> calls it in process and renders the view.
/// </summary>
[Route("api/ui/scan-artifact")]
[RepositoryPage(AnyFormat = true, NotOnVirtual = true)]
public sealed class ScanArtifactUiApiController : AccountUiApiControllerBase
{
    private const string Screen = "/Packages/Scan";

    /// <inheritdoc/>
    protected override bool RequiresSignIn => true;

    private ScanArtifactUseCase UseCase => HttpContext.RequestServices.GetRequiredService<ScanArtifactUseCase>();

    /// <summary>Shows the form (and the stored result) of the version.</summary>
    [HttpGet, UiApi(Screen)]
    public async Task<ActionResult<ScreenResult<ScanViewModel>>> Get(string? name, string? version, string? returnUrl, CancellationToken cancellationToken)
        => Respond(await UseCase.GetAsync(User, name, version, returnUrl, BackUrl(returnUrl), cancellationToken));

    /// <summary>Scans the version now (or queues it, for an image) and shows the outcome.</summary>
    [HttpPost, UiApi(Screen)]
    public async Task<ActionResult<ScreenResult<ScanViewModel>>> Scan([FromBody] ScanArtifactForm? form, CancellationToken cancellationToken)
        => Respond(await UseCase.ScanAsync(User, form?.Name, form?.Version, form?.ReturnUrl, BackUrl(form?.ReturnUrl), cancellationToken));

    // The return address when it is local, else the home page (under the path base).
    private string BackUrl(string? returnUrl)
        => !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : Url.Content("~/") ?? "/";
}
