using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NuGet.Versioning;

namespace BaGet.Core
{
    public class PackageDatabase : IPackageDatabase
    {
        private readonly IContext _context;

        public PackageDatabase(IContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<PackageAddResult> AddAsync(Package package, CancellationToken cancellationToken)
        {
            try
            {
                _context.Packages.Add(package);

                await _context.SaveChangesAsync(cancellationToken);

                return PackageAddResult.Success;
            }
            catch (DbUpdateException e)
                when (_context.IsUniqueConstraintViolationException(e))
            {
                return PackageAddResult.PackageAlreadyExists;
            }
        }

        public async Task<bool> ExistsAsync(string id, CancellationToken cancellationToken)
        {
            return await _context
                .Packages
                .Where(p => p.Id == id)
                .AnyAsync(cancellationToken);
        }

        public async Task<bool> ExistsAsync(string id, NuGetVersion version, CancellationToken cancellationToken)
        {
            return await _context
                .Packages
                .Where(p => p.Id == id)
                .Where(p => p.NormalizedVersionString == version.ToNormalizedString())
                .AnyAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<Package>> FindAsync(string id, bool includeUnlisted, CancellationToken cancellationToken)
        {
            var query = _context.Packages
                .Include(p => p.Dependencies)
                .Include(p => p.PackageTypes)
                .Include(p => p.TargetFrameworks)
                .Include(p => p.Deprecation)
                .Include(p => p.Vulnerabilities)
                .AsSplitQuery()
                .Where(p => p.Id == id);

            if (!includeUnlisted)
            {
                query = query.Where(p => p.Listed);
            }

            return (await query.ToListAsync(cancellationToken)).AsReadOnly();
        }

        public Task<Package> FindOrNullAsync(
            string id,
            NuGetVersion version,
            bool includeUnlisted,
            CancellationToken cancellationToken)
        {
            var query = _context.Packages
                .Include(p => p.Dependencies)
                .Include(p => p.TargetFrameworks)
                .Include(p => p.Deprecation)
                .Include(p => p.Vulnerabilities)
                .AsSplitQuery()
                .Where(p => p.Id == id)
                .Where(p => p.NormalizedVersionString == version.ToNormalizedString());

            if (!includeUnlisted)
            {
                query = query.Where(p => p.Listed);
            }

            return query.FirstOrDefaultAsync(cancellationToken);
        }

        public Task<bool> UnlistPackageAsync(string id, NuGetVersion version, CancellationToken cancellationToken)
        {
            return TryUpdatePackageAsync(id, version, p => p.Listed = false, cancellationToken);
        }

        public Task<bool> RelistPackageAsync(string id, NuGetVersion version, CancellationToken cancellationToken)
        {
            return TryUpdatePackageAsync(id, version, p => p.Listed = true, cancellationToken);
        }

        public async Task AddDownloadAsync(string id, NuGetVersion version, CancellationToken cancellationToken)
        {
            await TryUpdatePackageAsync(id, version, p => p.Downloads += 1, cancellationToken);
        }

        public async Task<bool> HardDeletePackageAsync(string id, NuGetVersion version, CancellationToken cancellationToken)
        {
            var package = await _context.Packages
                .Where(p => p.Id == id)
                .Where(p => p.NormalizedVersionString == version.ToNormalizedString())
                .Include(p => p.Dependencies)
                .Include(p => p.TargetFrameworks)
                .Include(p => p.Deprecation)
                .Include(p => p.Vulnerabilities)
                .AsSplitQuery()
                .FirstOrDefaultAsync(cancellationToken);

            if (package == null)
            {
                return false;
            }

            // The package to dependency relationship is optional in the database, so EF would
            // null the foreign key and leave these rows behind rather than delete them.
            _context.PackageDependencies.RemoveRange(package.Dependencies);

            // The same holds for a package's deprecation and vulnerabilities.
            if (package.Deprecation != null)
            {
                _context.PackageDeprecations.Remove(package.Deprecation);
            }

            _context.PackageVulnerabilities.RemoveRange(package.Vulnerabilities);
            _context.Packages.Remove(package);
            await _context.SaveChangesAsync(cancellationToken);

            return true;
        }

        public async Task<bool> SetDeprecationAsync(
            string id,
            NuGetVersion version,
            PackageDeprecation deprecation,
            CancellationToken cancellationToken)
        {
            if (deprecation == null) throw new ArgumentNullException(nameof(deprecation));

            var package = await FindForUpdateAsync(id, version, cancellationToken);
            if (package == null)
            {
                return false;
            }

            // A version has at most one deprecation, so setting one replaces the last.
            if (package.Deprecation != null)
            {
                _context.PackageDeprecations.Remove(package.Deprecation);
            }

            package.Deprecation = deprecation;
            await _context.SaveChangesAsync(cancellationToken);

            return true;
        }

        public async Task<bool> RemoveDeprecationAsync(
            string id,
            NuGetVersion version,
            CancellationToken cancellationToken)
        {
            var package = await FindForUpdateAsync(id, version, cancellationToken);
            if (package == null)
            {
                return false;
            }

            if (package.Deprecation != null)
            {
                _context.PackageDeprecations.Remove(package.Deprecation);
                await _context.SaveChangesAsync(cancellationToken);
            }

            return true;
        }

        public async Task<bool> SetVulnerabilitiesAsync(
            string id,
            NuGetVersion version,
            IReadOnlyList<PackageVulnerability> vulnerabilities,
            CancellationToken cancellationToken)
        {
            if (vulnerabilities == null) throw new ArgumentNullException(nameof(vulnerabilities));

            var package = await FindForUpdateAsync(id, version, cancellationToken);
            if (package == null)
            {
                return false;
            }

            // The list replaces whatever the version had, so an empty list clears it.
            _context.PackageVulnerabilities.RemoveRange(package.Vulnerabilities);
            package.Vulnerabilities = vulnerabilities.ToList();
            await _context.SaveChangesAsync(cancellationToken);

            return true;
        }

        public async Task<IReadOnlyList<PackageVulnerability>> GetAllVulnerabilitiesAsync(
            CancellationToken cancellationToken)
        {
            return await _context.PackageVulnerabilities
                .Include(v => v.Package)
                .Where(v => v.Package != null)
                .AsNoTracking()
                .ToListAsync(cancellationToken);
        }

        private Task<Package> FindForUpdateAsync(
            string id,
            NuGetVersion version,
            CancellationToken cancellationToken)
        {
            return _context.Packages
                .Include(p => p.Deprecation)
                .Include(p => p.Vulnerabilities)
                .AsSplitQuery()
                .Where(p => p.Id == id)
                .Where(p => p.NormalizedVersionString == version.ToNormalizedString())
                .FirstOrDefaultAsync(cancellationToken);
        }

        private async Task<bool> TryUpdatePackageAsync(
            string id,
            NuGetVersion version,
            Action<Package> action,
            CancellationToken cancellationToken)
        {
            var package = await _context.Packages
                .Where(p => p.Id == id)
                .Where(p => p.NormalizedVersionString == version.ToNormalizedString())
                .FirstOrDefaultAsync();

            if (package != null)
            {
                action(package);
                await _context.SaveChangesAsync(cancellationToken);

                return true;
            }

            return false;
        }
    }
}
