using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace BaGet.Core;

public interface IContext
{
    DatabaseFacade Database { get; }

    DbSet<Package>? Packages { get; set; }

    /// <summary>
    /// The dependencies declared by packages. A dependency belongs to the package that
    /// declares it, but the relationship is optional in the database, so deleting a
    /// package does not delete them.
    /// </summary>
    DbSet<PackageDependency>? PackageDependencies { get; set; }

    /// <summary>
    /// The deprecations of package versions.
    /// </summary>
    DbSet<PackageDeprecation>? PackageDeprecations { get; set; }

    /// <summary>
    /// The known vulnerabilities affecting package versions.
    /// </summary>
    DbSet<PackageVulnerability>? PackageVulnerabilities { get; set; }

    DbSet<CatalogEntry>? CatalogLeafs { get; set; }

    DbSet<Organization>? Organizations { get; set; }

    DbSet<OrganizationMember>? OrganizationMembers { get; set; }

    DbSet<PackageOwner>? PackageOwners { get; set; }

    DbSet<ApiKey>? ApiKeys { get; set; }

    DbSet<PackageValidation>? PackageValidations { get; set; }

    DbSet<AbuseReport>? AbuseReports { get; set; }

    /// <summary>
    /// The repositories. Not repository-scoped: it lists them all.
    /// </summary>
    DbSet<Repository>? Repositories { get; set; }

    DbSet<Project>? Projects { get; set; }

    /// <summary>The access model: groups, their members, permission targets and grants, project members and the audit log.</summary>
    DbSet<Group>? Groups { get; set; }

    DbSet<GroupMember>? GroupMembers { get; set; }

    DbSet<PermissionTarget>? PermissionTargets { get; set; }

    DbSet<PermissionGrant>? PermissionGrants { get; set; }

    DbSet<ProjectMember>? ProjectMembers { get; set; }

    DbSet<AccessAuditEntry>? AccessAuditEntries { get; set; }

    /// <summary>
    /// Identity's users. Declared here so that account services can query them through
    /// the same context as everything else.
    /// </summary>
    DbSet<BaGetUser> Users { get; set; }

    /// <summary>
    /// Check whether a <see cref="DbUpdateException"/> is due to a SQL unique constraint violation.
    /// </summary>
    /// <param name="exception">The exception to inspect.</param>
    /// <returns>Whether the exception was caused to SQL unique constraint violation.</returns>
    bool IsUniqueConstraintViolationException(DbUpdateException exception);

    /// <summary>
    /// Whether this database engine supports LINQ "Take" in subqueries.
    /// </summary>
    bool SupportsLimitInSubqueries { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Applies any pending migrations for the context to the database.
    /// Creates the database if it does not already exist.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the task.</param>
    /// <returns>A task that completes once migrations are applied.</returns>
    Task RunMigrationsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Prepares the database for inserting rows of <paramref name="table"/> with explicit values for
    /// their generated (identity/serial/auto-increment) key, as a data copy does. Call it before the
    /// inserts and <see cref="EndKeyedInsertAsync"/> after them, for one table at a time.
    /// </summary>
    /// <remarks>
    /// Providers that need it (SQL Server) hold the context's connection open between the two calls,
    /// because the setting is per connection; the caller must therefore call both, on the same context.
    /// Providers whose engine needs nothing (SQLite, MySQL) do nothing.
    /// </remarks>
    /// <param name="table">The EF entity type about to receive explicit keys.</param>
    /// <param name="cancellationToken">A token to cancel the task.</param>
    Task BeginKeyedInsertAsync(IEntityType table, CancellationToken cancellationToken);

    /// <summary>
    /// Completes the explicit-key inserts started by <see cref="BeginKeyedInsertAsync"/>, so that keys
    /// generated afterwards continue after the highest copied key (PostgreSQL resets its sequence here).
    /// </summary>
    /// <param name="table">The EF entity type whose rows were inserted.</param>
    /// <param name="cancellationToken">A token to cancel the task.</param>
    Task EndKeyedInsertAsync(IEntityType table, CancellationToken cancellationToken);
}
