using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BaGet.Identity.Pages.Admin;

/// <summary>A user as the Users page lists them.</summary>
/// <param name="UserName">The user name.</param>
/// <param name="Email">The email address, if any.</param>
/// <param name="Created">When the account was created.</param>
/// <param name="IsAdministrator">Whether the user is an administrator.</param>
/// <param name="AvatarUrl">The avatar's address, or null for the initial.</param>
public record UserRow(string? UserName, string? Email, DateTime Created, bool IsAdministrator, string? AvatarUrl);

/// <summary>
/// Lists the accounts and lets an administrator promote or demote them, reset their password
/// (a generated one shown once, or a link mailed to them) and open their packages or the
/// deletion page. Every action asks for the administrator's password again, and none is
/// offered or accepted for the administrator's own account: that stays on
/// <c>/account/settings</c>.
/// </summary>
public class UsersModel : AdminPageModel
{
    /// <summary>How many users a page lists.</summary>
    public const int PageSize = 50;

    /// <summary>User-name filter (contains).</summary>
    [BindProperty(SupportsGet = true)]
    public string? Q { get; set; }

    /// <summary>One-based page number. Not "page": Razor Pages reserves that route value for the page path.</summary>
    [BindProperty(SupportsGet = true, Name = "p")]
    public int PageNumber { get; set; } = 1;

    /// <summary>The users on this page.</summary>
    public IReadOnlyList<UserRow> Users { get; private set; } = Array.Empty<UserRow>();

    /// <summary>The number of pages for the current filter.</summary>
    public int PageCount { get; private set; } = 1;

    /// <summary>Whether the signed in administrator has a password to confirm with.</summary>
    public bool HasPassword { get; private set; } = true;

    /// <summary>For an account without a password: it signed in again with its provider a moment ago.</summary>
    public bool Reauthenticated { get; private set; }

    /// <summary>Whether an external provider is available to sign in again with.</summary>
    public bool CanReauthenticate => Accounts.OpenIdConnect.IsConfigured;

    /// <summary>The signed in administrator's user name; their own row has no actions.</summary>
    public string? CurrentUserName { get; private set; }

    /// <summary>Whether a reset link can be mailed: SMTP is configured.</summary>
    public bool CanEmailResetLink => EmailConfigured;

    /// <summary>
    /// The password just generated. Only ever rendered by the response to the reset itself, never
    /// kept in TempData or a cookie.
    /// </summary>
    public string? GeneratedPassword { get; private set; }

    /// <summary>The user <see cref="GeneratedPassword"/> belongs to.</summary>
    public string? GeneratedFor { get; private set; }

    /// <summary>Whether <see cref="GeneratedPassword"/> is a new account's first password rather than a reset.</summary>
    public bool GeneratedForNewUser { get; private set; }

    /// <summary>Whether an invitation (a reset link mailed to the new user) can be offered: SMTP is configured.</summary>
    public bool CanInvite => EmailConfigured;

    /// <summary>Whether <paramref name="row"/> is another account, so actions apply to it.</summary>
    public bool CanAct(UserRow row)
        => !string.Equals(row.UserName, CurrentUserName, StringComparison.OrdinalIgnoreCase);

    /// <summary>The address with all but its first letter and domain hidden, for a confirmation.</summary>
    public static string MaskEmail(string? email)
    {
        var at = email?.LastIndexOf('@') ?? -1;
        if (at < 1) return "the user's address";

        #pragma warning disable CS8602
        return email.Substring(0, 1) + "***" + email.Substring(at);
        #pragma warning restore CS8602
    }

    /// <summary>Outcome of the last action.</summary>
    [TempData]
    public string? Message { get; set; }

    /// <summary>Why the last action was refused.</summary>
    [TempData]
    public string? Error { get; set; }

    /// <summary>Lists the users.</summary>
    public async Task OnGetAsync(CancellationToken cancellationToken) => await LoadAsync(cancellationToken);

    /// <summary>Makes a user an administrator.</summary>
    public Task<IActionResult> OnPostPromoteAsync(string userName, string? password, CancellationToken cancellationToken)
        => ChangeAsync(userName, password, promote: true, cancellationToken);

    /// <summary>Removes a user's administrator flag.</summary>
    public Task<IActionResult> OnPostDemoteAsync(string userName, string? password, CancellationToken cancellationToken)
        => ChangeAsync(userName, password, promote: false, cancellationToken);

    /// <summary>
    /// Replaces the user's password with a generated one and shows it once. The answer is the
    /// page itself rather than a redirect, so the password never goes through TempData or a
    /// cookie, and it is not cacheable. Two-factor is switched off, a lockout lifted and the
    /// user's sessions end. <paramref name="resetEmail"/> (optional) is set and confirmed as the user's address, as
    /// <c>account reset-password --email</c> does.
    /// </summary>
    public async Task<IActionResult> OnPostResetPasswordAsync(string userName, string? password, string? resetEmail, CancellationToken cancellationToken)
    {
        var current = await UserManager.GetUserAsync(User);
        if (current == null) return Challenge();

        var refusal = await RefuseAsync(current, userName, password);
        if (refusal != null)
        {
            Error = refusal;

            return BackToUsers();
        }

        var generated = await HttpContext.RequestServices.GetRequiredService<AccountRecoveryService>()
            .ResetPasswordAsync(userName, cancellationToken, resetEmail);
        if (generated == null)
        {
            Error = "There is no user with that name.";

            return BackToUsers();
        }

        HttpContext.RequestServices.GetRequiredService<ILogger<UsersModel>>()
            .LogWarning("{Admin} reset the password of {User}", current.UserName, userName);

        Response.Headers.CacheControl = "no-store";
        GeneratedPassword = generated;
        GeneratedFor = userName;
        await LoadAsync(cancellationToken);

        return Page();
    }

    /// <summary>Mails the user a password reset link. Only while SMTP is configured, and only to a user with an address.</summary>
    public async Task<IActionResult> OnPostSendResetLinkAsync(string userName, string? password, CancellationToken cancellationToken)
    {
        if (!EmailConfigured) return NotFound();

        var current = await UserManager.GetUserAsync(User);
        if (current == null) return Challenge();

        var refusal = await RefuseAsync(current, userName, password);
        if (refusal != null)
        {
            Error = refusal;

            return BackToUsers();
        }

        var user = await UserManager.FindByNameAsync(userName);
        if (user == null)
        {
            Error = "There is no user with that name.";
        }
        else if (string.IsNullOrEmpty(user.Email))
        {
            Error = $"{user.UserName} has no email address, so no link can be sent.";
        }
        else
        {
            // No rate limiter: an administrator acts, and no one can use this to probe for accounts.
            var template = PageLink("/Account/ResetPassword", PasswordResetLinkSender.Template);
            if (PasswordResetLinkSender.TryEnqueue(HttpContext.RequestServices, user.Id, template))
            {
                HttpContext.RequestServices.GetRequiredService<ILogger<UsersModel>>()
                    .LogWarning("{Admin} sent a password reset link to {User}", current.UserName, user.UserName);
                Message = $"Reset link sent to {MaskEmail(user.Email)}.";
            }
            else
            {
                Error = "The mail queue is full. Try again in a moment.";
            }
        }

        return BackToUsers();
    }

    /// <summary>
    /// Creates an account. The password is typed, generated (shown once on this response, like a reset) or, with
    /// <paramref name="invite"/>, unknown to everyone while the user is mailed a link to choose their own. Asks for
    /// the administrator's password; refused with 503 while the <see cref="MaintenanceGate"/> is closed.
    /// </summary>
    public async Task<IActionResult> OnPostCreateAsync(
        string? newUserName,
        string? newEmail,
        string? newPassword,
        bool invite,
        string? password,
        CancellationToken cancellationToken)
    {
        HttpContext.RequestServices.GetService<MaintenanceGate>()?.ThrowIfClosed();

        var current = await UserManager.GetUserAsync(User);
        if (current == null) return Challenge();

        if (!await IsConfirmedAsync(current, password))
        {
            Error = "The confirmation is incorrect, so nothing was changed.";

            return BackToUsers();
        }

        if (invite && (!EmailConfigured || string.IsNullOrWhiteSpace(newEmail)))
        {
            Error = EmailConfigured
                ? "An invitation is mailed to the user, so an email address is required."
                : "Email is not configured, so no invitation can be sent. Give a password or generate one.";

            return BackToUsers();
        }

        var mode = invite
            ? NewUserPasswordMode.Invited
            : string.IsNullOrEmpty(newPassword) ? NewUserPasswordMode.Generated : NewUserPasswordMode.Given;
        var created = await HttpContext.RequestServices.GetRequiredService<AdminUserCreationService>()
            .CreateAsync(newUserName, newEmail, mode, newPassword, cancellationToken);
        if (!created.Succeeded)
        {
            Error = created.Error;

            return BackToUsers();
        }

        var logger = HttpContext.RequestServices.GetRequiredService<ILogger<UsersModel>>();
        logger.LogWarning("{Admin} created the user {User}", current.UserName, created.User!.UserName);

        if (mode == NewUserPasswordMode.Generated)
        {
            Response.Headers.CacheControl = "no-store";
            GeneratedPassword = created.GeneratedPassword;
            GeneratedFor = created.User.UserName;
            GeneratedForNewUser = true;
            await LoadAsync(cancellationToken);

            return Page();
        }

        if (mode == NewUserPasswordMode.Invited)
        {
            var template = PageLink("/Account/ResetPassword", PasswordResetLinkSender.Template);
            Message = PasswordResetLinkSender.TryEnqueue(HttpContext.RequestServices, created.User.Id, template)
                ? $"{created.User.UserName} was created. An invitation was sent to {MaskEmail(created.User.Email)}."
                : $"{created.User.UserName} was created, but the mail queue is full, so no invitation was sent. Use Reset password, Email a reset link.";
        }
        else
        {
            Message = $"{created.User.UserName} was created.";
        }

        return BackToUsers();
    }

    // Why a reset may not go ahead, or null.
    private async Task<string?> RefuseAsync(BaGetUser current, string userName, string? password)
    {
        if (string.Equals(current.UserName, userName, StringComparison.OrdinalIgnoreCase))
        {
            return "Reset your own password from your account settings.";
        }

        return await IsConfirmedAsync(current, password)
            ? null
            : "The confirmation is incorrect, so nothing was changed.";
    }

    private IActionResult BackToUsers()
        => RedirectToPage("/Admin/Users", new { q = Q, p = PageNumber > 1 ? PageNumber : (int?)null });

    private async Task<IActionResult> ChangeAsync(string userName, string? password, bool promote, CancellationToken cancellationToken)
    {
        var current = await UserManager.GetUserAsync(User);
        if (current == null) return Challenge();

        if (!await IsConfirmedAsync(current, password))
        {
            Error = "The confirmation is incorrect, so nothing was changed.";
        }
        else
        {
            var change = promote
                ? await Administrators.PromoteAsync(userName, current.UserName, cancellationToken)
                : await Administrators.DemoteAsync(userName, current.UserName, cancellationToken);

            switch (change)
            {
                case AdministratorChange.Changed:
                    Message = promote ? $"{userName} is now an administrator." : $"{userName} is no longer an administrator.";
                    break;
                case AdministratorChange.Unchanged:
                    Message = promote ? $"{userName} is already an administrator." : $"{userName} is not an administrator.";
                    break;
                case AdministratorChange.UserNotFound:
                    Error = "There is no user with that name.";
                    break;
                case AdministratorChange.LastAdministrator:
                    Error = "The last administrator cannot be demoted. Promote another user first.";
                    break;
            }
        }

        return RedirectToPage("/Admin/Users", new { q = Q, p = PageNumber > 1 ? PageNumber : (int?)null });
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        var context = HttpContext.RequestServices.GetRequiredService<IContext>();

        var query = context.Users.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(Q))
        {
            var term = Q.Trim().ToLowerInvariant();
            #pragma warning disable CS8602
            query = query.Where(u => u.UserName.ToLower().Contains(term));
            #pragma warning restore CS8602
        }

        var total = await query.CountAsync(cancellationToken);
        PageCount = Math.Max(1, (total + PageSize - 1) / PageSize);
        PageNumber = Math.Min(Math.Max(1, PageNumber), PageCount);

        var page = await query
            .OrderBy(u => u.UserName)
            .Skip((PageNumber - 1) * PageSize)
            .Take(PageSize)
            .Select(u => new { u.UserName, u.Email, u.Created, u.IsAdministrator })
            .ToListAsync(cancellationToken);

        var avatars = await HttpContext.RequestServices.GetRequiredService<AvatarService>()
            .GetUrlsAsync(page.Select(u => u.UserName).ToList(), 24, cancellationToken);

        #pragma warning disable CS8620
        Users = page
            .Select(u => new UserRow(u.UserName, u.Email, u.Created, u.IsAdministrator, avatars.GetValueOrDefault(u.UserName)))
            .ToList();
        #pragma warning restore CS8620

        var current = await UserManager.GetUserAsync(User);
        CurrentUserName = current?.UserName;
        HasPassword = current == null || await UserManager.HasPasswordAsync(current);
        Reauthenticated = !HasPassword && await HasReauthenticatedRecentlyAsync();
    }
}
