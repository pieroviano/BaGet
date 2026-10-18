using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.EntityFrameworkCore;

namespace BaGet.Identity;

public enum OwnerChangeResult
{
    Success,

    /// <summary>
    /// No user or organization has that name.
    /// </summary>
    UnknownAccount,

    AlreadyOwner,

    NotOwner,

    /// <summary>
    /// A package ID with owners must keep at least one.
    /// </summary>
    LastOwner,
}

/// <summary>
/// Package owners, and the organizations users own packages through.
/// </summary>
public class PackageOwnershipService
{
    private readonly IContext _context;
    private readonly SystemTime _time;

    public PackageOwnershipService(IContext context, SystemTime time)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _time = time ?? throw new ArgumentNullException(nameof(time));
    }

    public async Task<IReadOnlyList<PackageOwner>> GetOwnersAsync(string? packageId, CancellationToken cancellationToken)
    {
        var id = packageId?.ToLowerInvariant();

        #pragma warning disable CS8602, CS8604
        return await _context.PackageOwners
            .AsNoTracking()
            .Where(o => o.PackageId.ToLower() == id)
            .OrderBy(o => o.OwnerName)
            .ToListAsync(cancellationToken);
        #pragma warning restore CS8602, CS8604
    }

    /// <summary>
    /// The account names a user acts as: their own and every organization they belong to.
    /// </summary>
    public async Task<IReadOnlyList<string>?> GetAccountNamesAsync(string? userId, string? userName, CancellationToken cancellationToken)
    {
        #pragma warning disable CS8602, CS8604
        var organizations = await _context.OrganizationMembers
            .AsNoTracking()
            .Where(m => m.UserId == userId && m.Organization != null)
            .Select(m => m.Organization.Name)
            .ToListAsync(cancellationToken);
        #pragma warning restore CS8602, CS8604

        #pragma warning disable CS8619
        return new[] { userName }.Concat(organizations).ToList();
        #pragma warning restore CS8619
    }

    public async Task<bool> IsOwnerAsync(string? userId, string? userName, string? packageId, CancellationToken cancellationToken)
    {
        #pragma warning disable CS8604
        var names = (await GetAccountNamesAsync(userId, userName, cancellationToken))
            .Select(n => n.ToLowerInvariant())
            .ToList();
        #pragma warning restore CS8604
        var id = packageId?.ToLowerInvariant();

        #pragma warning disable CS8602, CS8604
        return await _context.PackageOwners
            .AnyAsync(o => o.PackageId.ToLower() == id && names.Contains(o.OwnerName.ToLower()), cancellationToken);
        #pragma warning restore CS8602, CS8604
    }

    /// <summary>
    /// The package IDs the user owns, directly or through an organization.
    /// </summary>
    public async Task<IReadOnlyList<string>?> GetOwnedPackageIdsAsync(string? userId, string? userName, CancellationToken cancellationToken)
    {
        #pragma warning disable CS8604
        var names = (await GetAccountNamesAsync(userId, userName, cancellationToken))
            .Select(n => n.ToLowerInvariant())
            .ToList();
        #pragma warning restore CS8604

        #pragma warning disable CS8602, CS8604
        var ids = await _context.PackageOwners
            .AsNoTracking()
            .Where(o => names.Contains(o.OwnerName.ToLower()))
            .Select(o => o.PackageId)
            .ToListAsync(cancellationToken);
        #pragma warning restore CS8602, CS8604

        #pragma warning disable CS8619
        return ids.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(i => i, StringComparer.OrdinalIgnoreCase).ToList();
        #pragma warning restore CS8619
    }

    public async Task<bool> HasOwnersAsync(string? packageId, CancellationToken cancellationToken)
    {
        var id = packageId?.ToLowerInvariant();

        #pragma warning disable CS8602
        return await _context.PackageOwners?.AnyAsync(o => o.PackageId.ToLower() == id, cancellationToken);
        #pragma warning restore CS8602
    }

    public async Task<bool> AccountExistsAsync(string? name, CancellationToken cancellationToken)
    {
        var normalized = name?.ToUpperInvariant();
        var lower = name?.ToLowerInvariant();

        #pragma warning disable CS8602
        return await _context.Users.AnyAsync(u => u.NormalizedUserName == normalized, cancellationToken)
               || await _context.Organizations?.AnyAsync(o => o.Name.ToLower() == lower, cancellationToken);
        #pragma warning restore CS8602
    }

    public async Task<OwnerChangeResult> AddOwnerAsync(string? packageId, string? ownerName, CancellationToken cancellationToken)
    {
        if (!await AccountExistsAsync(ownerName, cancellationToken)) return OwnerChangeResult.UnknownAccount;

        var owners = await GetOwnersAsync(packageId, cancellationToken);
        if (owners.Any(o => string.Equals(o.OwnerName, ownerName, StringComparison.OrdinalIgnoreCase)))
        {
            return OwnerChangeResult.AlreadyOwner;
        }

        _context.PackageOwners?.Add(new PackageOwner { PackageId = packageId, OwnerName = ownerName });
        await _context.SaveChangesAsync(cancellationToken);

        return OwnerChangeResult.Success;
    }

    public async Task<OwnerChangeResult> RemoveOwnerAsync(string? packageId, string ownerName, CancellationToken cancellationToken)
    {
        var id = packageId?.ToLowerInvariant();
        #pragma warning disable CS8602, CS8604
        var owners = await _context.PackageOwners
            .Where(o => o.PackageId.ToLower() == id)
            .ToListAsync(cancellationToken);
        #pragma warning restore CS8602, CS8604

        var owner = owners.FirstOrDefault(o => string.Equals(o.OwnerName, ownerName, StringComparison.OrdinalIgnoreCase));
        if (owner == null) return OwnerChangeResult.NotOwner;
        if (owners.Count == 1) return OwnerChangeResult.LastOwner;

        _context.PackageOwners.Remove(owner);
        await _context.SaveChangesAsync(cancellationToken);

        return OwnerChangeResult.Success;
    }

    /// <summary>
    /// Mark or unmark the ID as reserved for its owners. Only the superuser may do this.
    /// </summary>
    public async Task<bool> SetVerifiedAsync(string packageId, bool verified, CancellationToken cancellationToken)
    {
        var id = packageId.ToLowerInvariant();
        #pragma warning disable CS8602, CS8604
        var owners = await _context.PackageOwners
            .Where(o => o.PackageId.ToLower() == id)
            .ToListAsync(cancellationToken);
        #pragma warning restore CS8602, CS8604

        if (owners.Count == 0) return false;

        owners.ForEach(o => o.IsVerified = verified);
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<bool> IsVerifiedAsync(string packageId, CancellationToken cancellationToken)
    {
        var id = packageId.ToLowerInvariant();

        #pragma warning disable CS8602
        return await _context.PackageOwners?.AnyAsync(o => o.PackageId.ToLower() == id && o.IsVerified, cancellationToken);
        #pragma warning restore CS8602
    }

    public async Task<IReadOnlyList<Organization>?> GetOrganizationsAsync(string? userId, CancellationToken cancellationToken)
    {
        #pragma warning disable CS8602, CS8604, CS8619
        return await _context.OrganizationMembers
            .AsNoTracking()
            .Where(m => m.UserId == userId && m.Organization != null)
            .Select(m => m.Organization)
            .OrderBy(o => o.Name)
            .ToListAsync(cancellationToken);
        #pragma warning restore CS8602, CS8604, CS8619
    }

    /// <summary>
    /// Create an organization with its creator as the first administrator. False if
    /// the name is already a user or an organization.
    /// </summary>
    public async Task<bool> CreateOrganizationAsync(string name, string? creatorUserId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name) || await AccountExistsAsync(name, cancellationToken)) return false;

        var organization = new Organization { Name = name.Trim(), Created = _time.UtcNow };
        _context.Organizations?.Add(organization);
        _context.OrganizationMembers?.Add(new OrganizationMember
        {
            Organization = organization,
            UserId = creatorUserId,
            IsAdmin = true,
        });

        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<IReadOnlyList<OrganizationMember>> GetMembersAsync(string? organizationName, CancellationToken cancellationToken)
    {
        var name = organizationName?.ToLowerInvariant();

        #pragma warning disable CS8602, CS8604
        return await _context.OrganizationMembers
            .AsNoTracking()
            .Where(m => m.Organization != null && m.Organization.Name.ToLower() == name)
            .ToListAsync(cancellationToken);
        #pragma warning restore CS8602, CS8604
    }

    public async Task<bool> IsOrganizationAdminAsync(string? organizationName, string? userId, CancellationToken cancellationToken)
    {
        var members = await GetMembersAsync(organizationName, cancellationToken);

        return members.Any(m => m.UserId == userId && m.IsAdmin);
    }

    public async Task<bool> AddMemberAsync(string organizationName, string userId, bool isAdmin, CancellationToken cancellationToken)
    {
        var name = organizationName.ToLowerInvariant();
        #pragma warning disable CS8602
        var organization = await _context.Organizations?.FirstOrDefaultAsync(o => o.Name.ToLower() == name, cancellationToken);
        #pragma warning restore CS8602
        if (organization == null) return false;

        var members = await GetMembersAsync(organizationName, cancellationToken);
        if (members.Any(m => m.UserId == userId)) return false;

        _context.OrganizationMembers?.Add(new OrganizationMember
        {
            OrganizationKey = organization.Key,
            UserId = userId,
            IsAdmin = isAdmin,
        });
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }

    /// <summary>
    /// Remove a member. The last administrator cannot be removed.
    /// </summary>
    public async Task<bool> RemoveMemberAsync(string organizationName, string userId, CancellationToken cancellationToken)
    {
        var name = organizationName.ToLowerInvariant();
        #pragma warning disable CS8602, CS8604
        var members = await _context.OrganizationMembers
            .Where(m => m.Organization != null && m.Organization.Name.ToLower() == name)
            .ToListAsync(cancellationToken);
        #pragma warning restore CS8602, CS8604

        var member = members.FirstOrDefault(m => m.UserId == userId);
        if (member == null) return false;
        if (member.IsAdmin && members.Count(m => m.IsAdmin) == 1) return false;

        _context.OrganizationMembers.Remove(member);
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }
}