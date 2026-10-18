using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BaGet.Identity.Pages.Admin;

/// <summary>A user as the Users page lists them.</summary>
/// <param name="UserName">The user name.</param>
/// <param name="Email">The email address, if any.</param>
/// <param name="Created">When the account was created.</param>
/// <param name="IsAdministrator">Whether the user is an administrator.</param>
/// <param name="AvatarUrl">The avatar's address, or null for the initial.</param>
public record UserRow(string UserName, string Email, DateTime Created, bool IsAdministrator, string AvatarUrl);

/// <summary>
/// Lists the accounts and lets an administrator promote or demote them. Both actions ask for
/// the administrator's password again.
/// </summary>
public class UsersModel : AdminPageModel
{
    /// <summary>How many users a page lists.</summary>
    public const int PageSize = 50;

    /// <summary>User-name filter (contains).</summary>
    [BindProperty(SupportsGet = true)]
    public string Q { get; set; }

    /// <summary>One-based page number.</summary>
    [BindProperty(SupportsGet = true, Name = "page")]
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

    /// <summary>Outcome of the last action.</summary>
    [TempData]
    public string Message { get; set; }

    /// <summary>Why the last action was refused.</summary>
    [TempData]
    public string Error { get; set; }

    /// <summary>Lists the users.</summary>
    public async Task OnGetAsync(CancellationToken cancellationToken) => await LoadAsync(cancellationToken);

    /// <summary>Makes a user an administrator.</summary>
    public Task<IActionResult> OnPostPromoteAsync(string userName, string password, CancellationToken cancellationToken)
        => ChangeAsync(userName, password, promote: true, cancellationToken);

    /// <summary>Removes a user's administrator flag.</summary>
    public Task<IActionResult> OnPostDemoteAsync(string userName, string password, CancellationToken cancellationToken)
        => ChangeAsync(userName, password, promote: false, cancellationToken);

    private async Task<IActionResult> ChangeAsync(string userName, string password, bool promote, CancellationToken cancellationToken)
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

        return RedirectToPage("/Admin/Users", new { q = Q, page = PageNumber > 1 ? PageNumber : (int?)null });
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        var context = HttpContext.RequestServices.GetRequiredService<IContext>();

        var query = context.Users.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(Q))
        {
            var term = Q.Trim().ToLowerInvariant();
            query = query.Where(u => u.UserName.ToLower().Contains(term));
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

        Users = page
            .Select(u => new UserRow(u.UserName, u.Email, u.Created, u.IsAdministrator, avatars.GetValueOrDefault(u.UserName)))
            .ToList();

        var current = await UserManager.GetUserAsync(User);
        HasPassword = current == null || await UserManager.HasPasswordAsync(current);
        Reauthenticated = !HasPassword && await HasReauthenticatedRecentlyAsync();
    }
}
