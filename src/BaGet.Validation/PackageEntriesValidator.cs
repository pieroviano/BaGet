using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.Extensions.Options;
using NuGet.Common;
using NuGet.Packaging.Signing;

namespace BaGet.Validation;

/// <summary>
/// Every entry's path must be safe to extract: none may escape the package's folder.
/// </summary>
public class PackageEntriesValidator : IPackageValidator
{
    public string Name => "PackageEntries";

    public async Task<PackageValidatorResult> ValidateAsync(PackageValidationContext context, CancellationToken cancellationToken)
    {
        try
        {
            await context.Reader.ValidatePackageEntriesAsync(cancellationToken);

            return PackageValidatorResult.Valid;
        }
        catch (NuGet.Packaging.Core.PackagingException e)
        {
            return PackageValidatorResult.Invalid(e.Message);
        }
    }
}
