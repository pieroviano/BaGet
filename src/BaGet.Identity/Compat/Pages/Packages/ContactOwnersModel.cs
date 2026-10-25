using System;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Identity.Pages.Account;
using BaGet.Identity.ViewModels;
using BaGet.Identity.Workflows;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Identity.Pages.Packages;

/// <summary>
/// The page model of the contact-owners page when it was a Razor Page (<c>/packages/{id}/contact-owners</c>): lets a signed-in user
/// write to the owners of a package without learning their addresses.
/// </summary>
/// <remarks>
/// Kept as a shim so that code compiled against it still loads and runs (no breaking change of the public API); there is no page
/// behind it any more: <c>ContactOwnersController</c> serves the screen, and both run <see cref="ContactOwnersWorkflow"/>.
/// </remarks>
[Obsolete("The contact-owners screen is served by ContactOwnersController; this page model has no page any more and will be removed in the next major version.")]
public class ContactOwnersModel : AccountPageModel
{
    /// <summary>The longest message the form accepts, in characters.</summary>
    public const int MaxMessageLength = ContactOwnersViewModel.MaxMessageLength;

    private readonly ContactOwnersViewModel _model = new();
    private ContactOwnersWorkflow? _workflow;

    private ContactOwnersWorkflow Workflow => _workflow ??= new ContactOwnersWorkflow(HttpContext, ModelState, _model, null);

    /// <inheritdoc/>
    protected override bool RequiresSignIn => true;

    /// <inheritdoc/>
    protected override bool RequiresEmail => true;

    /// <summary>The package ID from the address.</summary>
    [BindProperty(SupportsGet = true)]
    public string? Id
    {
        get => _workflow?.Id;
        set => Workflow.Id = value;
    }

    /// <summary>The message to send to the owners.</summary>
    [BindProperty]
    [Required]
    [StringLength(MaxMessageLength)]
    [Display(Name = "Message")]
    public string? Message
    {
        get => _model.Message;
        set => _model.Message = value;
    }

    /// <summary>Whether the sender also gets a copy.</summary>
    [BindProperty]
    [Display(Name = "Send me a copy")]
    public bool CopyToSelf
    {
        get => _model.CopyToSelf;
        set => _model.CopyToSelf = value;
    }

    /// <summary>The package ID as stored, which is the only text that reaches the subject.</summary>
    public string? PackageId => _model.PackageId;

    /// <summary>Whether the message was sent.</summary>
    public bool Sent => _model.Sent;

    /// <summary>Why the message was not sent, or null.</summary>
    public string? Error => _model.Error;

    /// <summary>Whether the sender has a confirmed, deliverable email address.</summary>
    public bool SenderHasEmail => _model.SenderHasEmail;

    /// <summary>Shows the form; 404 when the package has no owner.</summary>
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
        => Map(await Workflow.GetAsync(cancellationToken));

    /// <summary>Sends the message to the owners.</summary>
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
        => Map(await Workflow.PostAsync(cancellationToken));

    private IActionResult Map(IActionResult result) => PackageWorkflowBase.IsShow(result) ? Page() : result;
}
