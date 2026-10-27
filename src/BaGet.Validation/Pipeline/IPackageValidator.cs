using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Packaging;
using NuGet.Versioning;

namespace BaGet.Validation.Pipeline;

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
