using System;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.ViewModels;
using BaGet.Identity.Workflows;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Identity.Pages.Admin;

/// <summary>
/// The page model of the delete-user page when it was a Razor Page: an administrator deleting another account, with a password
/// confirmation. Never the administrator's own account, and never the last administrator.
/// </summary>
/// <remarks>
/// Kept as a shim (no breaking change of the public API); <c>AdminUsersController</c> serves <c>/admin/users/{name}/delete</c>.
/// Both call <c>DeleteUserWorkflow</c>.
/// </remarks>
[Obsolete("The delete-user screen is served by AdminUsersController; this page model has no page any more and will be removed in the next major version.")]
public class DeleteUserModel : AdminPageModel
{
    /// <summary>Shown when permanent package deletion is asked for in read-only mode.</summary>
    public const string ReadOnlyMessage = DeleteUserViewModel.ReadOnlyMessage;

    private DeleteUserWorkflow? _workflow;

    private DeleteUserWorkflow Workflow => _workflow ??= new DeleteUserWorkflow(HttpContext, Url);

    /// <summary>The user to delete.</summary>
    [BindProperty(SupportsGet = true)]
    public string? Name { get => Workflow.Model.Name; set => Workflow.Model.Name = value; }

    /// <summary>"keep" (default) or "delete": what to do with the IDs only this user owns.</summary>
    [BindProperty]
    public string SoleOwned { get => Workflow.Model.SoleOwned; set => Workflow.Model.SoleOwned = value; }

    /// <summary>What the deletion would remove or change.</summary>
    public AdminDeletionPreview? Preview => Workflow.Model.Preview;

    /// <summary>Whether the user is the server's only administrator.</summary>
    public bool LastAdministrator => Workflow.Model.LastAdministrator;

    /// <summary>Whether the signed in administrator has a password to confirm with.</summary>
    public bool HasPassword => Workflow.Model.HasPassword;

    /// <summary>For an account without a password: it signed in again with its provider a moment ago.</summary>
    public bool Reauthenticated => Workflow.Model.Reauthenticated;

    /// <summary>Whether an external provider is available to sign in again with.</summary>
    public bool CanReauthenticate => Accounts.OpenIdConnect.IsConfigured;

    /// <summary>Read-only mode: the account may go, but package files are not deleted.</summary>
    public bool IsReadOnlyMode => Workflow.Model.IsReadOnlyMode;

    /// <summary>Why the deletion was refused.</summary>
    public string? Error => Workflow.Model.Error;

    /// <summary>Shows what the deletion does.</summary>
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
        => Flashed(await Workflow.GetAsync(cancellationToken)) ?? Page();

    /// <summary>Deletes the user after the administrator's password, then returns to the users.</summary>
    public async Task<IActionResult> OnPostAsync(string? password, CancellationToken cancellationToken)
        => Flashed(await Workflow.PostAsync(password, cancellationToken)) ?? Page();

    // What the page flashed to the Users page through TempData.
    private IActionResult? Flashed(IActionResult? result)
    {
        if (Workflow.RedirectMessage != null) TempData["Message"] = Workflow.RedirectMessage;
        if (Workflow.RedirectError != null) TempData["Error"] = Workflow.RedirectError;

        return result;
    }
}
