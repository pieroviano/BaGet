using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace BaGet.Core;

public class NullContext : IContext
{
    public DatabaseFacade Database => throw new NotImplementedException();

    public DbSet<Package>? Packages { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public DbSet<PackageDependency>? PackageDependencies { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public DbSet<PackageDeprecation>? PackageDeprecations { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public DbSet<PackageVulnerability>? PackageVulnerabilities { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public DbSet<CatalogEntry>? CatalogLeafs { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public DbSet<Organization>? Organizations { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public DbSet<OrganizationMember>? OrganizationMembers { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public DbSet<PackageOwner>? PackageOwners { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public DbSet<ApiKey>? ApiKeys { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
    public DbSet<ApiKeyRepository>? ApiKeyRepositories { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public DbSet<PackageValidation>? PackageValidations { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public DbSet<AbuseReport>? AbuseReports { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public DbSet<PackageSymbol>? PackageSymbols { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public DbSet<Repository>? Repositories { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public DbSet<Project>? Projects { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public DbSet<Group>? Groups { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public DbSet<GroupMember>? GroupMembers { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public DbSet<PermissionTarget>? PermissionTargets { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public DbSet<PermissionGrant>? PermissionGrants { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public DbSet<ProjectMember>? ProjectMembers { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public DbSet<AccessAuditEntry>? AccessAuditEntries { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public DbSet<BaGetUser> Users { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public bool SupportsLimitInSubqueries => throw new NotImplementedException();

    public bool IsUniqueConstraintViolationException(DbUpdateException exception)
    {
        throw new NotImplementedException();
    }

    public Task RunMigrationsAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public Task BeginKeyedInsertAsync(IEntityType table, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task EndKeyedInsertAsync(IEntityType table, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}