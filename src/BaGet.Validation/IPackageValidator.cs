using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Packaging;
using NuGet.Versioning;

namespace BaGet.Validation
{
    /// <summary>
    /// Decides whether a pushed package may be served. Register any number, for example a
    /// malware scanner; every one must pass. A validator that throws fails the package.
    /// </summary>
    public interface IPackageValidator
    {
        /// <summary>
        /// Recorded as the validation step's type, so it must be stable and unique.
        /// </summary>
        string Name { get; }

        Task<PackageValidatorResult> ValidateAsync(PackageValidationContext context, CancellationToken cancellationToken);
    }

    public sealed class PackageValidationContext
    {
        public PackageValidationContext(string packageId, NuGetVersion packageVersion, Stream package, PackageArchiveReader reader)
        {
            PackageId = packageId;
            PackageVersion = packageVersion;
            Package = package;
            Reader = reader;
        }

        public string PackageId { get; }

        public NuGetVersion PackageVersion { get; }

        /// <summary>
        /// The raw package. Seekable; a validator must not dispose it.
        /// </summary>
        public Stream Package { get; }

        /// <summary>
        /// A reader over <see cref="Package"/>. A validator must not dispose it.
        /// </summary>
        public PackageArchiveReader Reader { get; }
    }

    public sealed class PackageValidatorResult
    {
        public static readonly PackageValidatorResult Valid = new PackageValidatorResult(true, null);

        private PackageValidatorResult(bool isValid, string message)
        {
            IsValid = isValid;
            Message = message;
        }

        public bool IsValid { get; }

        /// <summary>
        /// Why the package is invalid. Shown to the package's owners.
        /// </summary>
        public string Message { get; }

        public static PackageValidatorResult Invalid(string message)
            => new PackageValidatorResult(false, message ?? throw new ArgumentNullException(nameof(message)));
    }
}
