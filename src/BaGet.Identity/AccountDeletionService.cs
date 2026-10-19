using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

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
/// <param name="LastAdministrator">
/// The user is the server's only administrator, so deleting them would leave nobody able to
/// change the settings.
/// </param>
public record DeletionBlockers(IReadOnlyList<string?> LastOwnerPackageIds, IReadOnlyList<string?> Organizations, bool LastAdministrator = false)
{
    /// <summary>Whether anything blocks the deletion.</summary>
    public bool Any => LastOwnerPackageIds.Count > 0 || Organizations.Count > 0 || LastAdministrator;
}

/// <summary>What an administrator's deletion of a user did.</summary>
/// <param name="Result">The outcome of deleting the user. Failed with nothing deleted when refused.</param>
/// <param name="KeptWithoutOwners">Sole-owned package IDs left without owners (only admins and the global key manage them).</param>
/// <param name="DeletedPackageIds">Sole-owned package IDs removed with all their versions.</param>
/// <param name="Failures">What could not be removed after the user was deleted; these IDs stay ownerless.</param>
public record AdminDeletionResult(
    IdentityResult Result,
    IReadOnlyList<string?> KeptWithoutOwners,
    IReadOnlyList<string> DeletedPackageIds,
    IReadOnlyList<string?> Failures);

/// <summary>What happens to one organization when an administrator deletes a member.</summary>
/// <param name="Name">The organization.</param>
/// <param name="Outcome">A sentence for the confirmation page.</param>
public record OrganizationOutcome(string? Name, string Outcome);

/// <summary>What deleting a user as an administrator would remove or change.</summary>
/// <param name="ApiKeys">How many API keys go.</param>
/// <param name="DirectPackageIds">Package IDs the user owns directly.</param>
/// <param name="SoleOwnedPackageIds">The subset nobody else owns.</param>
/// <param name="Organizations">What happens to each organization the user belongs to.</param>
public record AdminDeletionPreview(
    int ApiKeys,
    IReadOnlyList<string?> DirectPackageIds,
    IReadOnlyList<string?> SoleOwnedPackageIds,
    IReadOnlyList<OrganizationOutcome> Organizations);

/// <summary>
/// Atomically deletes a user together with what hangs off them: API keys, organization memberships
/// and package ownerships. The ownership rows go too, because they are keyed by name and a
/// later user registering the same name would otherwise inherit the packages.
/// </summary>
public class AccountDeletionService
{
    private readonly IContext _context;
    private readonly UserManager<BaGetUser> _users;
    private readonly IPackageDatabase _packages;
    private readonly IPackageDeletionService _packageDeletion;
    private readonly ILogger<AccountDeletionService> _logger;

    public AccountDeletionService(
        IContext context,
        UserManager<BaGetUser> users,
        IPackageDatabase packages,
        IPackageDeletionService packageDeletion,
        ILogger<AccountDeletionService> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _users = users ?? throw new ArgumentNullException(nameof(users));
        _packages = packages ?? throw new ArgumentNullException(nameof(packages));
        _packageDeletion = packageDeletion ?? throw new ArgumentNullException(nameof(packageDeletion));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<DeletionBlockers> GetBlockersAsync(BaGetUser? user, CancellationToken cancellationToken)
    {
        #pragma warning disable CS8602
        var name = user.UserName?.ToLowerInvariant();
        #pragma warning restore CS8602

        #pragma warning disable CS8602, CS8604
        var owned = await _context.PackageOwners
            .AsNoTracking()
            .Where(o => o.OwnerName.ToLower() == name)
            .Select(o => o.PackageId)
            .ToListAsync(cancellationToken);
        #pragma warning restore CS8602, CS8604

        var lastOwner = new List<string?>();
        foreach (var packageId in owned.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(i => i, StringComparer.OrdinalIgnoreCase))
        {
            var id = packageId?.ToLowerInvariant();
            #pragma warning disable CS8602
            if (await _context.PackageOwners.CountAsync(o => o.PackageId.ToLower() == id, cancellationToken) == 1)
            #pragma warning restore CS8602
            {
                lastOwner.Add(packageId);
            }
        }

        var blocking = new List<string?>();
        foreach (var organization in await OrganizationsToLeaveAsync(user, cancellationToken))
        {
            #pragma warning disable CS8602
            var blocked = organization.OtherMembers > 0
                ? organization.IsLastAdmin
                : await OrganizationOwnsPackagesAsync(organization.Name, cancellationToken);
            #pragma warning restore CS8602

            if (blocked) blocking.Add(organization.Name);
        }

        // Same rule as AdministratorService.IsLastAdministratorAsync.
        var lastAdministrator = user.IsAdministrator
            && await _context.Users.CountAsync(u => u.IsAdministrator, cancellationToken) <= 1;

        return new DeletionBlockers(lastOwner, blocking, lastAdministrator);
    }

    /// <summary>
    /// Delete the user, or return the reasons it cannot be done.
    /// </summary>
    public async Task<(DeletionBlockers Blockers, IdentityResult Result)> DeleteAsync(BaGetUser? user, CancellationToken cancellationToken)
    {
        var blockers = await GetBlockersAsync(user, cancellationToken);
        #pragma warning disable CS8619
        if (blockers.Any) return (blockers, null);
        #pragma warning restore CS8619

        await StageCleanupAsync(user, administrator: false, cancellationToken);

        return (blockers, await SaveDeletionAsync(user));
    }

    /// <summary>
    /// Delete another user as an administrator. Unlike <see cref="DeleteAsync"/> nothing but
    /// <see cref="DeletionBlockers.LastAdministrator"/> blocks it: an organization whose last
    /// administrator leaves hands the role to its longest-standing member, an organization of one
    /// that owns packages is kept without members, and the IDs only this user owned are either
    /// left without owners or deleted. The package deletion runs after the user is gone and
    /// never undoes it: failures are returned.
    /// </summary>
    /// <param name="user">The user to delete.</param>
    /// <param name="deleteSoleOwnedPackages">Delete the sole-owned IDs with all their versions instead of leaving them ownerless.</param>
    /// <param name="actor">The administrator, for the log.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<AdminDeletionResult> DeleteByAdministratorAsync(
        BaGetUser? user, bool deleteSoleOwnedPackages, string? actor, CancellationToken cancellationToken)
    {
        var none = Array.Empty<string>();

        var blockers = await GetBlockersAsync(user, cancellationToken);
        if (blockers.LastAdministrator)
        {
            return new AdminDeletionResult(
                IdentityResult.Failed(new IdentityError { Description = "The last administrator cannot be deleted. Promote another user first." }),
                none, none, none);
        }

        var soleOwned = blockers.LastOwnerPackageIds;
        var userName = user?.UserName;

        await StageCleanupAsync(user, administrator: true, cancellationToken);
        var result = await SaveDeletionAsync(user);
        if (!result.Succeeded) return new AdminDeletionResult(result, none, none, none);

        var deleted = new List<string>();
        var failures = new List<string?>();
        if (deleteSoleOwnedPackages)
        {
            foreach (var id in soleOwned)
            {
                try
                {
                    var versions = await _packages.FindAsync(id, includeUnlisted: true, cancellationToken);
                    var failed = false;
                    foreach (var package in versions)
                    {
                        failed |= !await _packageDeletion.TryHardDeletePackageAsync(id, package.Version, cancellationToken);
                    }

                    #pragma warning disable CS8604, CS8619
                    (failed ? failures : deleted).Add(id);
                    #pragma warning restore CS8604, CS8619
                }
                catch (Exception e) when (e is not OperationCanceledException and not MaintenanceException)
                {
                    _logger.LogError(e, "Could not delete package {Id} of deleted user {User}", id, userName);
                    failures.Add(id);
                }
            }
        }

        var kept = deleteSoleOwnedPackages ? none : soleOwned;
        _logger.LogWarning(
            "{Admin} deleted user {User}; sole-owned IDs {Ids} {Outcome}",
            actor, userName, string.Join(", ", soleOwned), deleteSoleOwnedPackages ? "deleted" : "kept without owners");

        return new AdminDeletionResult(result, kept, deleted, failures);
    }

    /// <summary>What <see cref="DeleteByAdministratorAsync"/> would do to <paramref name="user"/>, for the confirmation page.</summary>
    public async Task<AdminDeletionPreview> PreviewAdministratorDeletionAsync(BaGetUser user, CancellationToken cancellationToken)
    {
        var name = user.UserName?.ToLowerInvariant();
        var blockers = await GetBlockersAsync(user, cancellationToken);

        #pragma warning disable CS8602, CS8604
        var direct = (await _context.PackageOwners
                .AsNoTracking()
                .Where(o => o.OwnerName.ToLower() == name)
                .Select(o => o.PackageId)
                .ToListAsync(cancellationToken))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(i => i, StringComparer.OrdinalIgnoreCase)
            .ToList();
        #pragma warning restore CS8602, CS8604

        var outcomes = new List<OrganizationOutcome>();
        foreach (var organization in await OrganizationsToLeaveAsync(user, cancellationToken))
        {
            string outcome;
            if (organization.OtherMembers == 0)
            {
                #pragma warning disable CS8602
                outcome = await OrganizationOwnsPackagesAsync(organization.Name, cancellationToken)
                    ? "The organization owns packages, so it is kept without members."
                    : "The organization has no other member and is deleted.";
                #pragma warning restore CS8602
            }
            else if (organization.IsLastAdmin)
            {
                #pragma warning disable CS8604
                var successor = await _context.OrganizationMembers
                    .AsNoTracking()
                    .Where(m => m.OrganizationKey == organization.Key && m.UserId != user.Id)
                    .OrderBy(m => m.Key)
                    .Select(m => m.UserId)
                    .FirstAsync(cancellationToken);
                var successorName = (await _users.FindByIdAsync(successor))?.UserName ?? successor;
                #pragma warning restore CS8604

                outcome = $"{successorName}, its longest-standing member, becomes its administrator.";
            }
            else
            {
                outcome = "The user leaves the organization.";
            }

            outcomes.Add(new OrganizationOutcome(organization.Name, outcome));
        }

        #pragma warning disable CS8602
        var keys = await _context.ApiKeys?.CountAsync(k => k.UserId == user.Id, cancellationToken);
        #pragma warning restore CS8602

        return new AdminDeletionPreview(keys, direct, blockers.LastOwnerPackageIds, outcomes);
    }

    /// <summary>
    /// Stage the removal of the user's API keys, ownerships and memberships; nothing is saved
    /// until the user itself is deleted.
    /// </summary>
    /// <param name="user">The user being deleted.</param>
    /// <param name="administrator">
    /// An administrator is deleting: the last administrator of an organization with other members
    /// is replaced by its longest-standing member, and an organization of one that owns packages
    /// is kept. Otherwise those cases were refused beforehand.
    /// </param>
    /// <param name="cancellationToken">The cancellation token.</param>
    private async Task StageCleanupAsync(BaGetUser? user, bool administrator, CancellationToken cancellationToken)
    {
        #pragma warning disable CS8602
        var keys = await _context.ApiKeys?.Where(k => k.UserId == user.Id).ToListAsync(cancellationToken);
        #pragma warning restore CS8602
        _context.ApiKeys?.RemoveRange(keys);

        var name = user?.UserName?.ToLowerInvariant();
        #pragma warning disable CS8602
        var ownerships = await _context.PackageOwners?.Where(o => o.OwnerName.ToLower() == name).ToListAsync(cancellationToken);
        #pragma warning restore CS8602
        _context.PackageOwners?.RemoveRange(ownerships);

        foreach (var organization in await OrganizationsToLeaveAsync(user, cancellationToken))
        {
            #pragma warning disable CS8604
            var members = await _context.OrganizationMembers
                .Where(m => m.OrganizationKey == organization.Key)
                .ToListAsync(cancellationToken);
            #pragma warning restore CS8604

            if (administrator && organization.OtherMembers > 0 && organization.IsLastAdmin)
            {
                members.Where(m => m.UserId != user?.Id).OrderBy(m => m.Key).First().IsAdmin = true;
            }

            // An organization of one with nothing to its name goes with its only member; an
            // administrator's deletion keeps one that owns packages, without members.
            #pragma warning disable CS8602
            var removeOrganization = organization.OtherMembers == 0
                && !(administrator && await OrganizationOwnsPackagesAsync(organization.Name, cancellationToken));
            #pragma warning restore CS8602
            _context.OrganizationMembers.RemoveRange(removeOrganization ? members : members.Where(m => m.UserId == user?.Id));
            if (removeOrganization)
            {
                #pragma warning disable CS8602
                _context.Organizations?.RemoveRange(await _context.Organizations?.Where(o => o.Key == organization.Key).ToListAsync(cancellationToken));
                #pragma warning restore CS8602
            }
        }
    }

    // The cleanup is only staged. Deleting the user saves it in the same SaveChanges, so the
    // database applies all of it or none of it: a user that cannot be deleted keeps its keys,
    // ownerships and memberships.
    private async Task<IdentityResult> SaveDeletionAsync(BaGetUser? user)
    {
        try
        {
            #pragma warning disable CS8604
            var result = await _users.DeleteAsync(user);
            #pragma warning restore CS8604
            if (!result.Succeeded) Discard();

            return result;
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

    private sealed record Membership(int Key, string? Name, bool IsLastAdmin, int OtherMembers);

    private async Task<IReadOnlyList<Membership>> OrganizationsToLeaveAsync(BaGetUser? user, CancellationToken cancellationToken)
    {
        #pragma warning disable CS8602, CS8604
        var mine = await _context.OrganizationMembers
            .AsNoTracking()
            .Where(m => m.UserId == user.Id && m.Organization != null)
            .Select(m => new { m.OrganizationKey, m.IsAdmin, m.Organization.Name })
            .ToListAsync(cancellationToken);
        #pragma warning restore CS8602, CS8604

        var result = new List<Membership>();
        foreach (var membership in mine)
        {
            #pragma warning disable CS8602
            var others = await _context.OrganizationMembers
                .AsNoTracking()
                .Where(m => m.OrganizationKey == membership.OrganizationKey && m.UserId != user.Id)
                .Select(m => m.IsAdmin)
                .ToListAsync(cancellationToken);
            #pragma warning restore CS8602

            result.Add(new Membership(
                membership.OrganizationKey.GetValueOrDefault(),
                membership.Name,
                membership.IsAdmin && !others.Any(isAdmin => isAdmin),
                others.Count));
        }

        return result;
    }

    private Task<bool>? OrganizationOwnsPackagesAsync(string? organization, CancellationToken cancellationToken)
    {
        var name = organization?.ToLowerInvariant();

        #pragma warning disable CS8602
        return _context.PackageOwners?.AnyAsync(o => o.OwnerName.ToLower() == name, cancellationToken);
        #pragma warning restore CS8602
    }
}
