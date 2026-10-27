using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Packaging;
using NuGet.Versioning;

namespace BaGet.Validation.Pipeline;

public sealed class PackageValidatorResult
{
    public static readonly PackageValidatorResult Valid = new PackageValidatorResult(true, null);

    private PackageValidatorResult(bool isValid, string? message)
    {
        IsValid = isValid;
        Message = message;
    }

    public bool IsValid { get; }

    /// <summary>
    /// Why the package is invalid. Shown to the package's owners.
    /// </summary>
    public string? Message { get; }

    public static PackageValidatorResult Invalid(string message)
        => new PackageValidatorResult(false, message ?? throw new ArgumentNullException(nameof(message)));
}