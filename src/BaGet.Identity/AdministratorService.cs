using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BaGet.Identity;

/// <summary>
/// The outcome of promoting or demoting a user.
/// </summary>
public enum AdministratorChange
{
    /// <summary>The flag was changed.</summary>
    Changed,

    /// <summary>The user already had the requested state.</summary>
    Unchanged,

    /// <summary>There is no such user.</summary>
    UserNotFound,

    /// <summary>The user is the only administrator, so cannot be demoted.</summary>
    LastAdministrator,
}

/// <summary>
/// Reads and changes <see cref="BaGetUser.IsAdministrator"/>. Read from the database on
/// every check, so a demotion takes effect on the next request.
/// </summary>
public class AdministratorService
{
    private readonly UserManager<BaGetUser> _users;
    private readonly IContext _context;
    private readonly ILogger<AdministratorService> _logger;

    /// <summary>Creates the service.</summary>
    public AdministratorService(UserManager<BaGetUser> users, IContext context, ILogger<AdministratorService> logger)
    {
        _users = users ?? throw new ArgumentNullException(nameof(users));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>Whether the signed in user is an administrator, according to the database.</summary>
    public async Task<bool> IsAdministratorAsync(ClaimsPrincipal? principal, CancellationToken cancellationToken)
    {
        var id = principal == null ? null : _users.GetUserId(principal);

        return !string.IsNullOrEmpty(id)
            && await _context.Users.AnyAsync(u => u.Id == id && u.IsAdministrator, cancellationToken);
    }

    /// <summary>Makes the user an administrator.</summary>
    /// <param name="userName">The user to promote.</param>
    /// <param name="actor">Who asks, for the log.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public Task<AdministratorChange> PromoteAsync(string? userName, string? actor, CancellationToken cancellationToken)
        => SetAsync(userName, true, actor, cancellationToken);

    /// <summary>Removes the user's administrator flag, unless they are the last administrator.</summary>
    /// <param name="userName">The user to demote.</param>
    /// <param name="actor">Who asks, for the log.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public Task<AdministratorChange> DemoteAsync(string? userName, string? actor, CancellationToken cancellationToken)
        => SetAsync(userName, false, actor, cancellationToken);

    /// <summary>Whether the user is an administrator and no other administrator exists.</summary>
    public async Task<bool> IsLastAdministratorAsync(BaGetUser? user, CancellationToken cancellationToken)
    {
        if (user == null || !user.IsAdministrator) return false;

        return await _context.Users.CountAsync(u => u.IsAdministrator, cancellationToken) <= 1;
    }

    /// <summary>
    /// Bootstraps a server without administrators: promotes the semicolon separated user
    /// names, but only while no administrator exists. Once one does, it does nothing, so a
    /// listed name registered later is not promoted and a demotion is not reversed at restart.
    /// A name with no account is logged and skipped.
    /// </summary>
    public async Task PromoteInitialAdministratorsAsync(string? names, CancellationToken cancellationToken)
    {
        if (await _context.Users.AnyAsync(u => u.IsAdministrator, cancellationToken)) return;

        var separators = new[] { ';' };
        foreach (var name in (names ?? string.Empty).Split(separators, StringSplitOptions.RemoveEmptyEntries).Select(n => n.Trim()).Where(n => n.Length > 0))
        {
            if (await SetAsync(name, true, "configuration", cancellationToken) == AdministratorChange.UserNotFound)
            {
                _logger.LogWarning("Initial administrator {User} does not exist and was not promoted", name);
            }
        }
    }

    private async Task<AdministratorChange> SetAsync(string? userName, bool value, string? actor, CancellationToken cancellationToken)
    {
        var user = string.IsNullOrWhiteSpace(userName) ? null : await _users.FindByNameAsync(userName.Trim());
        if (user == null) return AdministratorChange.UserNotFound;
        if (user.IsAdministrator == value) return AdministratorChange.Unchanged;
        if (!value && await IsLastAdministratorAsync(user, cancellationToken)) return AdministratorChange.LastAdministrator;

        user.IsAdministrator = value;
        var result = await _users.UpdateAsync(user); // the concurrency stamp rejects a racing edit of the same user
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join(" ", result.Errors.Select(e => e.Description)));
        }

        _logger.LogWarning("{Actor} {Action} {User} as administrator", actor, value ? "promoted" : "demoted", user.UserName);

        return AdministratorChange.Changed;
    }
}
