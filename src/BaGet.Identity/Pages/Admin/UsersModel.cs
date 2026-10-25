using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.ViewModels;
using BaGet.Identity.Workflows;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Identity.Pages.Admin;

/// <summary>A user as the Users page lists them.</summary>
/// <param name="UserName">The user name.</param>
/// <param name="Email">The email address, if any.</param>
/// <param name="Created">When the account was created.</param>
/// <param name="IsAdministrator">Whether the user is an administrator.</param>
/// <param name="AvatarUrl">The avatar's address, or null for the initial.</param>
/// <param name="PendingApproval">Whether the account waits for an administrator to enable it (<c>Accounts:RequireApproval</c>).</param>
public record UserRow(string? UserName, string? Email, DateTime Created, bool IsAdministrator, string? AvatarUrl, bool PendingApproval = false);

/// <summary>
/// The page model of the Users page when it was a Razor Page: lists the accounts and lets an administrator promote or demote
/// them, reset their password and open their packages or the deletion page.
/// </summary>
/// <remarks>
/// Kept as a shim so that code compiled against it still loads and runs (no breaking change of the public API); there is no page
/// behind it any more: <c>AdminUsersController</c> serves <c>/admin/users</c>. Both call <c>UsersWorkflow</c>.
/// </remarks>
[Obsolete("The Users screen is served by AdminUsersController; this page model has no page any more and will be removed in the next major version.")]
public class UsersModel : AdminPageModel
{
    /// <summary>How many users a page lists.</summary>
    public const int PageSize = UsersViewModel.PageSize;

    private UsersWorkflow? _workflow;

    private UsersWorkflow Workflow => _workflow ??= new UsersWorkflow(HttpContext, Url);

    /// <summary>User-name filter (contains).</summary>
    [BindProperty(SupportsGet = true)]
    public string? Q { get => Workflow.Model.Q; set => Workflow.Model.Q = value; }

    /// <summary>One-based page number. Not "page": Razor Pages reserves that route value for the page path.</summary>
    [BindProperty(SupportsGet = true, Name = "p")]
    public int PageNumber { get => Workflow.Model.PageNumber; set => Workflow.Model.PageNumber = value; }

    /// <summary>The users on this page.</summary>
    public IReadOnlyList<UserRow> Users => Workflow.Model.Users;

    /// <summary>How many accounts, in all pages, wait for approval.</summary>
    public int PendingCount => Workflow.Model.PendingCount;

    /// <summary>The number of pages for the current filter.</summary>
    public int PageCount => Workflow.Model.PageCount;

    /// <summary>Whether the signed in administrator has a password to confirm with.</summary>
    public bool HasPassword => Workflow.Model.HasPassword;

    /// <summary>For an account without a password: it signed in again with its provider a moment ago.</summary>
    public bool Reauthenticated => Workflow.Model.Reauthenticated;

    /// <summary>Whether an external provider is available to sign in again with.</summary>
    public bool CanReauthenticate => Accounts.OpenIdConnect.IsConfigured;

    /// <summary>The signed in administrator's user name; their own row has no actions.</summary>
    public string? CurrentUserName => Workflow.Model.CurrentUserName;

    /// <summary>Whether a reset link can be mailed: SMTP is configured.</summary>
    public bool CanEmailResetLink => EmailConfigured;

    /// <summary>The password just generated. Only ever rendered by the response to the reset itself.</summary>
    public string? GeneratedPassword => Workflow.Model.GeneratedPassword;

    /// <summary>The user <see cref="GeneratedPassword"/> belongs to.</summary>
    public string? GeneratedFor => Workflow.Model.GeneratedFor;

    /// <summary>Whether <see cref="GeneratedPassword"/> is a new account's first password rather than a reset.</summary>
    public bool GeneratedForNewUser => Workflow.Model.GeneratedForNewUser;

    /// <summary>Whether an invitation (a reset link mailed to the new user) can be offered: SMTP is configured.</summary>
    public bool CanInvite => EmailConfigured;

    /// <summary>Whether <paramref name="row"/> is another account, so actions apply to it.</summary>
    public bool CanAct(UserRow row) => Workflow.Model.CanAct(row);

    /// <summary>The address with all but its first letter and domain hidden, for a confirmation.</summary>
    public static string MaskEmail(string? email) => UsersViewModel.MaskEmail(email);

    /// <summary>Outcome of the last action.</summary>
    [TempData]
    public string? Message { get => Workflow.Model.Message; set => Workflow.Model.Message = value; }

    /// <summary>Why the last action was refused.</summary>
    [TempData]
    public string? Error { get => Workflow.Model.Error; set => Workflow.Model.Error = value; }

    /// <summary>Lists the users.</summary>
    public async Task OnGetAsync(CancellationToken cancellationToken) => await Workflow.GetAsync(cancellationToken);

    /// <summary>Enables a self-registered account that waits for approval.</summary>
    public async Task<IActionResult> OnPostApproveAsync(string userName, string? password, CancellationToken cancellationToken)
        => await Workflow.ApproveAsync(userName, password, cancellationToken) ?? Page();

    /// <summary>Makes a user an administrator.</summary>
    public async Task<IActionResult> OnPostPromoteAsync(string userName, string? password, CancellationToken cancellationToken)
        => await Workflow.PromoteAsync(userName, password, cancellationToken) ?? Page();

    /// <summary>Removes a user's administrator flag.</summary>
    public async Task<IActionResult> OnPostDemoteAsync(string userName, string? password, CancellationToken cancellationToken)
        => await Workflow.DemoteAsync(userName, password, cancellationToken) ?? Page();

    /// <summary>Replaces the user's password with a generated one and shows it once.</summary>
    public async Task<IActionResult> OnPostResetPasswordAsync(string userName, string? password, string? resetEmail, CancellationToken cancellationToken)
        => await Workflow.ResetPasswordAsync(userName, password, resetEmail, cancellationToken) ?? Page();

    /// <summary>Mails the user a password reset link.</summary>
    public async Task<IActionResult> OnPostSendResetLinkAsync(string userName, string? password, CancellationToken cancellationToken)
        => await Workflow.SendResetLinkAsync(userName, password, cancellationToken) ?? Page();

    /// <summary>Creates an account (typed, generated or invited password).</summary>
    public async Task<IActionResult> OnPostCreateAsync(
        string? newUserName,
        string? newEmail,
        string? newPassword,
        bool invite,
        string? password,
        CancellationToken cancellationToken)
        => await Workflow.CreateAsync(newUserName, newEmail, newPassword, invite, password, cancellationToken) ?? Page();
}
