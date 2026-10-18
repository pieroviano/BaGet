using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BaGet.Identity;

/// <summary>
/// What stops a user from deleting their account.
/// </summary>
/// <param name="LastOwnerPackageIds">
/// Package IDs the user is the only direct owner of. Same rule as
/// <see cref="PackageOwnershipService.RemoveOwnerAsync"/>: an owned ID keeps an owner.
/// </param>
/// <param name="Organizations">
/// Organizations the user is the last administrator of while others still belong to it, or
/// that own packages and so cannot be left without anyone to run them. Same rule as
/// <see cref="PackageOwnershipService.RemoveMemberAsync"/>.
/// </param>
public record DeletionBlockers(IReadOnlyList<string> LastOwnerPackageIds, IReadOnlyList<string> Organizations)
{
    public bool Any => LastOwnerPackageIds.Count > 0 || Organizations.Count > 0;
}

/// <summary>
/// Atomically deletes a user together with what hangs off them: API keys, organization memberships
/// and package ownerships. The ownership rows go too, because they are keyed by name and a
/// later user registering the same name would otherwise inherit the packages.
/// </summary>
public class AccountDeletionService
{
    private readonly IContext _context;
    private readonly UserManager<BaGetUser> _users;

    public AccountDeletionService(IContext context, UserManager<BaGetUser> users)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _users = users ?? throw new ArgumentNullException(nameof(users));
    }

    public async Task<DeletionBlockers> GetBlockersAsync(BaGetUser user, CancellationToken cancellationToken)
    {
        var name = user.UserName.ToLowerInvariant();

        var owned = await _context.PackageOwners
            .AsNoTracking()
            .Where(o => o.OwnerName.ToLower() == name)
            .Select(o => o.PackageId)
            .ToListAsync(cancellationToken);

        var lastOwner = new List<string>();
        foreach (var packageId in owned.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(i => i, StringComparer.OrdinalIgnoreCase))
        {
            var id = packageId.ToLowerInvariant();
            if (await _context.PackageOwners.CountAsync(o => o.PackageId.ToLower() == id, cancellationToken) == 1)
            {
                lastOwner.Add(packageId);
            }
        }

        var blocking = new List<string>();
        foreach (var organization in await OrganizationsToLeaveAsync(user, cancellationToken))
        {
            var blocked = organization.OtherMembers > 0
                ? organization.IsLastAdmin
                : await OrganizationOwnsPackagesAsync(organization.Name, cancellationToken);

            if (blocked) blocking.Add(organization.Name);
        }

        return new DeletionBlockers(lastOwner, blocking);
    }

    /// <summary>
    /// Delete the user, or return the reasons it cannot be done.
    /// </summary>
    public async Task<(DeletionBlockers Blockers, IdentityResult Result)> DeleteAsync(BaGetUser user, CancellationToken cancellationToken)
    {
        var blockers = await GetBlockersAsync(user, cancellationToken);
        if (blockers.Any) return (blockers, null);

        var keys = await _context.ApiKeys.Where(k => k.UserId == user.Id).ToListAsync(cancellationToken);
        _context.ApiKeys.RemoveRange(keys);

        var name = user.UserName.ToLowerInvariant();
        var ownerships = await _context.PackageOwners.Where(o => o.OwnerName.ToLower() == name).ToListAsync(cancellationToken);
        _context.PackageOwners.RemoveRange(ownerships);

        foreach (var organization in await OrganizationsToLeaveAsync(user, cancellationToken))
        {
            var members = await _context.OrganizationMembers
                .Where(m => m.OrganizationKey == organization.Key)
                .ToListAsync(cancellationToken);

            // An organization of one with nothing to its name goes with its only member.
            _context.OrganizationMembers.RemoveRange(organization.OtherMembers == 0 ? members : members.Where(m => m.UserId == user.Id));
            if (organization.OtherMembers == 0)
            {
                _context.Organizations.RemoveRange(await _context.Organizations.Where(o => o.Key == organization.Key).ToListAsync(cancellationToken));
            }
        }

        // The cleanup above is only staged. Deleting the user saves it in the same
        // SaveChanges, so the database applies all of it or none of it: a user that cannot
        // be deleted keeps its keys, ownerships and memberships.
        try
        {
            var result = await _users.DeleteAsync(user);
            if (!result.Succeeded) Discard();

            return (blockers, result);
        }
        catch
        {
            Discard();
            throw;
        }
    }

    /// <summary>
    /// Forget the staged cleanup, so a later save in the same scope cannot apply it.
    /// </summary>
    private void Discard()
    {
        if (_context is DbContext context) context.ChangeTracker.Clear();
    }

    private sealed record Membership(int Key, string Name, bool IsLastAdmin, int OtherMembers);

    private async Task<IReadOnlyList<Membership>> OrganizationsToLeaveAsync(BaGetUser user, CancellationToken cancellationToken)
    {
        var mine = await _context.OrganizationMembers
            .AsNoTracking()
            .Where(m => m.UserId == user.Id && m.Organization != null)
            .Select(m => new { m.OrganizationKey, m.IsAdmin, m.Organization.Name })
            .ToListAsync(cancellationToken);

        var result = new List<Membership>();
        foreach (var membership in mine)
        {
            var others = await _context.OrganizationMembers
                .AsNoTracking()
                .Where(m => m.OrganizationKey == membership.OrganizationKey && m.UserId != user.Id)
                .Select(m => m.IsAdmin)
                .ToListAsync(cancellationToken);

            result.Add(new Membership(
                membership.OrganizationKey.GetValueOrDefault(),
                membership.Name,
                membership.IsAdmin && !others.Any(isAdmin => isAdmin),
                others.Count));
        }

        return result;
    }

    private Task<bool> OrganizationOwnsPackagesAsync(string organization, CancellationToken cancellationToken)
    {
        var name = organization.ToLowerInvariant();

        return _context.PackageOwners.AnyAsync(o => o.OwnerName.ToLower() == name, cancellationToken);
    }
}
