using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.Pages.Admin;
using BaGet.Identity.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BaGet.Identity.Workflows;

/// <summary>
/// The Users screen: lists the accounts and lets an administrator approve, promote or demote them, create an account, reset a
/// password (a generated one shown once, or a link mailed to the user). Every action asks for the administrator's password again,
/// and none is offered or accepted for the administrator's own account (that stays on <c>/account/settings</c>). Shared by
/// <c>AdminUsersController</c> and the obsolete <c>UsersModel</c>.
/// </summary>
internal sealed class UsersWorkflow : AdminWorkflow
{
    private const string NotConfirmed = "The confirmation is incorrect, so nothing was changed.";

    public UsersWorkflow(HttpContext httpContext, IUrlHelper url) : base(httpContext, url)
    {
    }

    /// <summary>The state the screen renders; the caller sets the bound values (<c>Q</c>, <c>PageNumber</c>) before calling a handler.</summary>
    public UsersViewModel Model { get; } = new();

    /// <summary>Lists the users.</summary>
    public async Task GetAsync(CancellationToken cancellationToken) => await LoadAsync(cancellationToken);

    /// <summary>Enables a self-registered account that waits for approval. Asks the password; 503 while the gate is closed.</summary>
    public async Task<IActionResult?> ApproveAsync(string userName, string? password, CancellationToken cancellationToken)
    {
        ThrowIfClosed();

        var current = await UserManager.GetUserAsync(User);
        if (current == null) return new ChallengeResult();

        if (!await IsConfirmedAsync(current, password))
        {
            Model.Error = NotConfirmed;

            return BackToUsers();
        }

        switch (await Services.GetRequiredService<AccountApprovalService>().ApproveAsync(userName, current.UserName, cancellationToken))
        {
            case AccountApprovalResult.Approved:
                Model.Message = $"{userName} was approved and can sign in.";
                break;
            case AccountApprovalResult.NotPending:
                Model.Message = $"{userName} was not waiting for approval.";
                break;
            case AccountApprovalResult.Conflict:
                Model.Error = $"{userName} was changed or removed meanwhile, so nothing was approved. Reload the list.";
                break;
            default:
                Model.Error = "There is no user with that name.";
                break;
        }

        return BackToUsers();
    }

    /// <summary>Makes a user an administrator.</summary>
    public Task<IActionResult?> PromoteAsync(string userName, string? password, CancellationToken cancellationToken)
        => ChangeAsync(userName, password, promote: true, cancellationToken);

    /// <summary>Removes a user's administrator flag.</summary>
    public Task<IActionResult?> DemoteAsync(string userName, string? password, CancellationToken cancellationToken)
        => ChangeAsync(userName, password, promote: false, cancellationToken);

    /// <summary>
    /// Replaces the user's password with a generated one and shows it once. The answer is the screen itself (null) rather than a
    /// redirect, so the password never goes through TempData or a cookie, and it is not cacheable.
    /// </summary>
    public async Task<IActionResult?> ResetPasswordAsync(string userName, string? password, string? resetEmail, CancellationToken cancellationToken)
    {
        var current = await UserManager.GetUserAsync(User);
        if (current == null) return new ChallengeResult();

        var refusal = await RefuseAsync(current, userName, password);
        if (refusal != null)
        {
            Model.Error = refusal;

            return BackToUsers();
        }

        var generated = await Services.GetRequiredService<AccountRecoveryService>()
            .ResetPasswordAsync(userName, cancellationToken, resetEmail);
        if (generated == null)
        {
            Model.Error = "There is no user with that name.";

            return BackToUsers();
        }

        Services.GetRequiredService<ILogger<UsersWorkflow>>()
            .LogWarning("{Admin} reset the password of {User}", current.UserName, userName);

        Http.Response.Headers.CacheControl = "no-store";
        Model.GeneratedPassword = generated;
        Model.GeneratedFor = userName;
        await LoadAsync(cancellationToken);

        return null;
    }

    /// <summary>Mails the user a password reset link. Only while SMTP is configured, and only to a user with an address.</summary>
    public async Task<IActionResult?> SendResetLinkAsync(string userName, string? password, CancellationToken cancellationToken)
    {
        if (!EmailConfigured) return new NotFoundResult();

        var current = await UserManager.GetUserAsync(User);
        if (current == null) return new ChallengeResult();

        var refusal = await RefuseAsync(current, userName, password);
        if (refusal != null)
        {
            Model.Error = refusal;

            return BackToUsers();
        }

        var user = await UserManager.FindByNameAsync(userName);
        if (user == null)
        {
            Model.Error = "There is no user with that name.";
        }
        else if (string.IsNullOrEmpty(user.Email))
        {
            Model.Error = $"{user.UserName} has no email address, so no link can be sent.";
        }
        else
        {
            // No rate limiter: an administrator acts, and no one can use this to probe for accounts.
            var template = PageLink("/Account/ResetPassword", PasswordResetLinkSender.Template);
            if (PasswordResetLinkSender.TryEnqueue(Services, user.Id, template))
            {
                Services.GetRequiredService<ILogger<UsersWorkflow>>()
                    .LogWarning("{Admin} sent a password reset link to {User}", current.UserName, user.UserName);
                Model.Message = $"Reset link sent to {UsersViewModel.MaskEmail(user.Email)}.";
            }
            else
            {
                Model.Error = "The mail queue is full. Try again in a moment.";
            }
        }

        return BackToUsers();
    }

    /// <summary>
    /// Creates an account. The password is typed, generated (shown once on this response, like a reset) or, with
    /// <paramref name="invite"/>, unknown to everyone while the user is mailed a link to choose their own. Asks for the
    /// administrator's password; refused with 503 while the <see cref="MaintenanceGate"/> is closed.
    /// </summary>
    public async Task<IActionResult?> CreateAsync(
        string? newUserName,
        string? newEmail,
        string? newPassword,
        bool invite,
        string? password,
        CancellationToken cancellationToken)
    {
        ThrowIfClosed();

        var current = await UserManager.GetUserAsync(User);
        if (current == null) return new ChallengeResult();

        if (!await IsConfirmedAsync(current, password))
        {
            Model.Error = NotConfirmed;

            return BackToUsers();
        }

        if (invite && (!EmailConfigured || string.IsNullOrWhiteSpace(newEmail)))
        {
            Model.Error = EmailConfigured
                ? "An invitation is mailed to the user, so an email address is required."
                : "Email is not configured, so no invitation can be sent. Give a password or generate one.";

            return BackToUsers();
        }

        var mode = invite
            ? NewUserPasswordMode.Invited
            : string.IsNullOrEmpty(newPassword) ? NewUserPasswordMode.Generated : NewUserPasswordMode.Given;
        var created = await Services.GetRequiredService<AdminUserCreationService>()
            .CreateAsync(newUserName, newEmail, mode, newPassword, cancellationToken);
        if (!created.Succeeded)
        {
            Model.Error = created.Error;

            return BackToUsers();
        }

        var logger = Services.GetRequiredService<ILogger<UsersWorkflow>>();
        logger.LogWarning("{Admin} created the user {User}", current.UserName, created.User!.UserName);

        if (mode == NewUserPasswordMode.Generated)
        {
            Http.Response.Headers.CacheControl = "no-store";
            Model.GeneratedPassword = created.GeneratedPassword;
            Model.GeneratedFor = created.User.UserName;
            Model.GeneratedForNewUser = true;
            await LoadAsync(cancellationToken);

            return null;
        }

        if (mode == NewUserPasswordMode.Invited)
        {
            var template = PageLink("/Account/ResetPassword", PasswordResetLinkSender.Template);
            Model.Message = PasswordResetLinkSender.TryEnqueue(Services, created.User.Id, template)
                ? $"{created.User.UserName} was created. An invitation was sent to {UsersViewModel.MaskEmail(created.User.Email)}."
                : $"{created.User.UserName} was created, but the mail queue is full, so no invitation was sent. Use Reset password, Email a reset link.";
        }
        else
        {
            Model.Message = $"{created.User.UserName} was created.";
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

        return await IsConfirmedAsync(current, password) ? null : NotConfirmed;
    }

    private IActionResult BackToUsers()
        => RedirectToScreen("/Admin/Users", new { q = Model.Q, p = Model.PageNumber > 1 ? Model.PageNumber : (int?)null });

    private async Task<IActionResult?> ChangeAsync(string userName, string? password, bool promote, CancellationToken cancellationToken)
    {
        var current = await UserManager.GetUserAsync(User);
        if (current == null) return new ChallengeResult();

        if (!await IsConfirmedAsync(current, password))
        {
            Model.Error = NotConfirmed;
        }
        else
        {
            var change = promote
                ? await Administrators.PromoteAsync(userName, current.UserName, cancellationToken)
                : await Administrators.DemoteAsync(userName, current.UserName, cancellationToken);

            switch (change)
            {
                case AdministratorChange.Changed:
                    Model.Message = promote ? $"{userName} is now an administrator." : $"{userName} is no longer an administrator.";
                    break;
                case AdministratorChange.Unchanged:
                    Model.Message = promote ? $"{userName} is already an administrator." : $"{userName} is not an administrator.";
                    break;
                case AdministratorChange.UserNotFound:
                    Model.Error = "There is no user with that name.";
                    break;
                case AdministratorChange.LastAdministrator:
                    Model.Error = "The last administrator cannot be demoted. Promote another user first.";
                    break;
            }
        }

        return BackToUsers();
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        var context = Services.GetRequiredService<IContext>();

        var query = context.Users.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(Model.Q))
        {
            var term = Model.Q.Trim().ToLowerInvariant();
            #pragma warning disable CS8602
            query = query.Where(u => u.UserName.ToLower().Contains(term));
            #pragma warning restore CS8602
        }

        var total = await query.CountAsync(cancellationToken);
        Model.PageCount = Math.Max(1, (total + UsersViewModel.PageSize - 1) / UsersViewModel.PageSize);
        Model.PageNumber = Math.Min(Math.Max(1, Model.PageNumber), Model.PageCount);

        var page = await query
            .OrderBy(u => u.UserName)
            .Skip((Model.PageNumber - 1) * UsersViewModel.PageSize)
            .Take(UsersViewModel.PageSize)
            .Select(u => new { u.UserName, u.Email, u.Created, u.IsAdministrator, u.PendingApproval })
            .ToListAsync(cancellationToken);

        var avatars = await Services.GetRequiredService<AvatarService>()
            .GetUrlsAsync(page.Select(u => u.UserName).ToList(), 24, cancellationToken);

        #pragma warning disable CS8620
        Model.Users = page
            .Select(u => new UserRow(u.UserName, u.Email, u.Created, u.IsAdministrator, avatars.GetValueOrDefault(u.UserName), u.PendingApproval))
            .ToList();
        #pragma warning restore CS8620

        Model.PendingCount = await Services.GetRequiredService<AccountApprovalService>().CountPendingAsync(cancellationToken);
        Model.CanEmailResetLink = EmailConfigured;
        Model.CanInvite = EmailConfigured;

        Model.CurrentUserName = (await UserManager.GetUserAsync(User))?.UserName;
        await LoadConfirmationAsync(Model);
    }
}
