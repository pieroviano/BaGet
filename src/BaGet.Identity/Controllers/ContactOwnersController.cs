using System.Threading;
using System.Threading.Tasks;
using BaGet.Identity.Mvc;
using BaGet.Identity.ViewModels;
using BaGet.Identity.Workflows;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Identity.Controllers;

/// <summary>
/// Lets a signed-in user write to the owners of a package (<c>/packages/{id}/contact-owners</c>, also at <c>/r/{repo}/…</c>). It
/// replaces the <c>/Packages/ContactOwners</c> Razor Page (same URLs and form fields, same gates: accounts enabled, mail configured,
/// signed in); the logic is <see cref="ContactOwnersWorkflow"/>, shared with the obsolete <c>ContactOwnersModel</c>.
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
    {
        var model = new ContactOwnersViewModel();

        return Present(await new ContactOwnersWorkflow(HttpContext, ModelState, model, id).GetAsync(cancellationToken), model);
    }

    /// <summary>Sends the message to the owners.</summary>
    [HttpPost, PageHandler]
    public async Task<IActionResult> Send([FromRoute] string? id, [FromForm] ContactOwnersViewModel model, CancellationToken cancellationToken)
        => Present(await new ContactOwnersWorkflow(HttpContext, ModelState, model, id).PostAsync(cancellationToken), model);

    private IActionResult Present(IActionResult result, ContactOwnersViewModel model)
        => PackageControllerSupport.Resolve(this, result, ViewName, model);
}
