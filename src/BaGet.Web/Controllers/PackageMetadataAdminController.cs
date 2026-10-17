using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NuGet.Versioning;

namespace BaGet.Web;

/// <summary>
/// Sets a package version's deprecation and vulnerability metadata. These are writes, so
/// they are authorised exactly like push and delete.
/// </summary>
public class PackageMetadataAdminController : Controller
{
    private readonly IAuthenticationService _authentication;
    private readonly IPackageAuthorizationService _authorization;
    private readonly IPackageDatabase _packages;
    private readonly IOptionsSnapshot<BaGetOptions> _options;
    private readonly PackageChangeNotifier _notifier;

    public PackageMetadataAdminController(
        IAuthenticationService authentication,
        IPackageDatabase packages,
        IOptionsSnapshot<BaGetOptions> options,
        PackageChangeNotifier notifier,
        IPackageAuthorizationService authorization)
    {
        _authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
        _notifier = notifier ?? throw new ArgumentNullException(nameof(notifier));
        _authentication = authentication ?? throw new ArgumentNullException(nameof(authentication));
        _packages = packages ?? throw new ArgumentNullException(nameof(packages));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<IActionResult> SetDeprecation(
        string id,
        string version,
        [FromBody] DeprecationRequest request,
        CancellationToken cancellationToken)
    {
        var denied = await AuthorizeAsync(id, cancellationToken);
        if (denied != null) return denied;

        if (!NuGetVersion.TryParse(version, out var nugetVersion))
        {
            return NotFound();
        }

        var error = Validate(request);
        if (error != null)
        {
            return BadRequest(error);
        }

        var deprecation = new PackageDeprecation
        {
            Reasons = request.Reasons.Distinct(StringComparer.Ordinal).ToArray(),
            Message = request.Message,
            AlternatePackageId = request.AlternatePackageId,
            AlternateVersionRange = request.AlternatePackageId == null ? null : request.AlternateVersionRange,
        };

        return await RunAsync(id, nugetVersion, () => _packages.SetDeprecationAsync(id, nugetVersion, deprecation, cancellationToken), cancellationToken);
    }

    public async Task<IActionResult> RemoveDeprecation(
        string id,
        string version,
        CancellationToken cancellationToken)
    {
        var denied = await AuthorizeAsync(id, cancellationToken);
        if (denied != null) return denied;

        if (!NuGetVersion.TryParse(version, out var nugetVersion))
        {
            return NotFound();
        }

        return await RunAsync(id, nugetVersion, () => _packages.RemoveDeprecationAsync(id, nugetVersion, cancellationToken), cancellationToken);
    }

    public async Task<IActionResult> SetVulnerabilities(
        string id,
        string version,
        [FromBody] List<VulnerabilityRequest> request,
        CancellationToken cancellationToken)
    {
        var denied = await AuthorizeAsync(id, cancellationToken);
        if (denied != null) return denied;

        if (!NuGetVersion.TryParse(version, out var nugetVersion))
        {
            return NotFound();
        }

        var error = Validate(request);
        if (error != null)
        {
            return BadRequest(error);
        }

        var vulnerabilities = request
            .Select(v => new PackageVulnerability
            {
                AdvisoryUrl = v.AdvisoryUrl,
                Severity = v.Severity,
                VersionRange = v.VersionRange,
            })
            .ToList();

        return await RunAsync(id, nugetVersion, () => _packages.SetVulnerabilitiesAsync(id, nugetVersion, vulnerabilities, cancellationToken), cancellationToken);
    }

    internal static string Validate(DeprecationRequest request)
    {
        if (request?.Reasons == null || request.Reasons.Count == 0)
        {
            return "At least one deprecation reason is required.";
        }

        var unknown = request.Reasons.FirstOrDefault(r => !PackageDeprecation.KnownReasons.Contains(r, StringComparer.Ordinal));
        if (unknown != null)
        {
            return $"Unknown deprecation reason '{unknown}'. Use one of: {string.Join(", ", PackageDeprecation.KnownReasons)}.";
        }

        if (request.Message?.Length > AbstractContext<DbContext>.DefaultMaxStringLength)
        {
            return "The deprecation message is too long.";
        }

        if (request.AlternatePackageId == null && request.AlternateVersionRange != null)
        {
            return "An alternate version range needs an alternate package ID.";
        }

        if (request.AlternateVersionRange != null
            && request.AlternateVersionRange != "*"
            && !VersionRange.TryParse(request.AlternateVersionRange, out _))
        {
            return $"'{request.AlternateVersionRange}' is not a version range.";
        }

        return null;
    }

    internal static string Validate(IReadOnlyList<VulnerabilityRequest> request)
    {
        if (request == null)
        {
            return "A list of vulnerabilities is required; send an empty list to clear them.";
        }

        foreach (var vulnerability in request)
        {
            if (vulnerability == null)
            {
                return "A vulnerability cannot be null.";
            }

            if (!Uri.TryCreate(vulnerability.AdvisoryUrl, UriKind.Absolute, out var url)
                || (url.Scheme != Uri.UriSchemeHttps && url.Scheme != Uri.UriSchemeHttp))
            {
                return $"'{vulnerability.AdvisoryUrl}' is not an absolute http or https URL.";
            }

            // The protocol numbers severity 0 low, 1 moderate, 2 high, 3 critical.
            if (vulnerability.Severity < 0 || vulnerability.Severity > 3)
            {
                return $"Severity {vulnerability.Severity} is outside the range 0 to 3.";
            }

            if (vulnerability.VersionRange != null && !VersionRange.TryParse(vulnerability.VersionRange, out _))
            {
                return $"'{vulnerability.VersionRange}' is not a version range.";
            }
        }

        return null;
    }

    private async Task<IActionResult> AuthorizeAsync(string id, CancellationToken cancellationToken)
    {
        // The same rules as deleting or relisting a package.
        if (_options.Value.IsReadOnlyMode)
        {
            return Unauthorized();
        }

        var principal = await _authentication.AuthenticatePrincipalAsync(Request.GetApiKey(), cancellationToken);
        if (principal == null)
        {
            return Unauthorized();
        }

        if (!await _authorization.AuthorizeAsync(principal, PackageAction.Manage, id, cancellationToken))
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        return null;
    }

    private async Task<IActionResult> RunAsync(
        string id,
        NuGetVersion version,
        Func<Task<bool>> operation,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!await operation())
            {
                return NotFound();
            }

            await _notifier.PackageChangedAsync(id, version, cancellationToken);

            return NoContent();
        }
        catch (NotSupportedException e)
        {
            // The configured database cannot store this metadata.
            return StatusCode(StatusCodes.Status501NotImplemented, e.Message);
        }
    }
}

public class DeprecationRequest
{
    [JsonPropertyName("reasons")]
    public IReadOnlyList<string> Reasons { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; }

    [JsonPropertyName("alternatePackageId")]
    public string AlternatePackageId { get; set; }

    [JsonPropertyName("alternateVersionRange")]
    public string AlternateVersionRange { get; set; }
}

public class VulnerabilityRequest
{
    [JsonPropertyName("advisoryUrl")]
    public string AdvisoryUrl { get; set; }

    [JsonPropertyName("severity")]
    public int Severity { get; set; }

    [JsonPropertyName("versionRange")]
    public string VersionRange { get; set; }
}