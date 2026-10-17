using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace BaGet.Core
{
    public interface IContext
    {
        DatabaseFacade Database { get; }

        DbSet<Package> Packages { get; set; }

        /// <summary>
        /// The dependencies declared by packages. A dependency belongs to the package that
        /// declares it, but the relationship is optional in the database, so deleting a
        /// package does not delete them.
        /// </summary>
        DbSet<PackageDependency> PackageDependencies { get; set; }

        /// <summary>
        /// The deprecations of package versions.
        /// </summary>
        DbSet<PackageDeprecation> PackageDeprecations { get; set; }

        /// <summary>
        /// The known vulnerabilities affecting package versions.
        /// </summary>
        DbSet<PackageVulnerability> PackageVulnerabilities { get; set; }

        DbSet<CatalogEntry> CatalogLeafs { get; set; }

        DbSet<Organization> Organizations { get; set; }

        DbSet<OrganizationMember> OrganizationMembers { get; set; }

        DbSet<PackageOwner> PackageOwners { get; set; }

        DbSet<ApiKey> ApiKeys { get; set; }

        DbSet<PackageValidation> PackageValidations { get; set; }

        DbSet<AbuseReport> AbuseReports { get; set; }

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
    }
}
