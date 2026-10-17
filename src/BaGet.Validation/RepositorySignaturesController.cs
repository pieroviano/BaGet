using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Protocol.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NuGet.Versioning;

namespace BaGet.Validation
{
    /// <summary>
    /// The RepositorySignatures/5.0.0 resource, and each submission's validation status.
    /// </summary>
    public class RepositorySignaturesController : Controller
    {
        // The OID the protocol keys a SHA-256 fingerprint by.
        internal const string Sha256Oid = "2.16.840.1.101.3.4.2.1";

        private readonly RepositorySigner _signer;
        private readonly IUrlGenerator _url;

        public RepositorySignaturesController(RepositorySigner signer, IUrlGenerator url)
        {
            _signer = signer ?? throw new ArgumentNullException(nameof(signer));
            _url = url ?? throw new ArgumentNullException(nameof(url));
        }

        [HttpGet]
        public IActionResult Index()
        {
            using var certificate = _signer.GetPublicCertificateOrNull();
            if (certificate == null) return NotFound();

            var fingerprint = Fingerprint(certificate.RawData);

            return Json(new RepositorySignaturesResponse
            {
                // Packages pushed before signing was configured, and mirrored ones, are not
                // repository signed, so a client must not insist that every package is.
                AllRepositorySigned = false,
                SigningCertificates = new[]
                {
                    new RepositorySigningCertificate
                    {
                        Fingerprints = new Dictionary<string, string> { [Sha256Oid] = fingerprint },
                        Subject = certificate.Subject,
                        Issuer = certificate.Issuer,
                        NotBefore = certificate.NotBefore.ToUniversalTime(),
                        NotAfter = certificate.NotAfter.ToUniversalTime(),
                        ContentUrl = _url.GetRepositorySigningCertificateUrl(fingerprint),
                    },
                },
            });
        }

        [HttpGet]
        public IActionResult Certificate(string fingerprint)
        {
            using var certificate = _signer.GetPublicCertificateOrNull();
            if (certificate == null || !string.Equals(Fingerprint(certificate.RawData), fingerprint, StringComparison.OrdinalIgnoreCase))
            {
                return NotFound();
            }

            return File(certificate.RawData, "application/pkix-cert");
        }

        internal static string Fingerprint(byte[] certificate)
            => Convert.ToHexString(SHA256.HashData(certificate)).ToLowerInvariant();
    }

    /// <summary>
    /// A package version's validation, for its owners: whether it is pending, available
    /// or quarantined, and why.
    /// </summary>
    public class ValidationStatusController : Controller
    {
        private readonly IContext _context;
        private readonly Core.IAuthenticationService _authentication;
        private readonly IPackageAuthorizationService _authorization;

        public ValidationStatusController(
            IContext context,
            Core.IAuthenticationService authentication,
            IPackageAuthorizationService authorization)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _authentication = authentication ?? throw new ArgumentNullException(nameof(authentication));
            _authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
        }

        [HttpGet]
        public async Task<IActionResult> Get(string id, string version, CancellationToken cancellationToken)
        {
            if (!NuGetVersion.TryParse(version, out var nugetVersion)) return NotFound();

            var principal = await _authentication.AuthenticatePrincipalAsync(Request.Headers["X-NuGet-ApiKey"], cancellationToken);
            if (principal == null) return Unauthorized();
            if (!await _authorization.AuthorizeAsync(principal, PackageAction.Push, id, cancellationToken)
                && !await _authorization.AuthorizeAsync(principal, PackageAction.Manage, id, cancellationToken))
            {
                return StatusCode(403);
            }

            if (_context is NullContext) return NotFound();

            var lowerId = id.ToLowerInvariant();
            var normalized = nugetVersion.ToNormalizedString();
            var steps = await _context.PackageValidations
                .AsNoTracking()
                .Where(v => v.PackageId.ToLower() == lowerId && v.PackageVersion == normalized)
                .OrderByDescending(v => v.Key)
                .ToListAsync(cancellationToken);

            if (steps.Count == 0) return NotFound();

            // The newest submission is the one that matters.
            var latest = steps[0].SubmissionId;
            var submission = steps.Where(s => s.SubmissionId == latest).OrderBy(s => s.Key).ToList();
            var index = submission.Single(s => s.Type == PackageValidation.IndexType);

            return Json(new ValidationStatusResponse
            {
                SubmissionId = latest,
                Status = index.State switch
                {
                    PackageValidationState.Succeeded => "Available",
                    PackageValidationState.Failed => "Quarantined",
                    _ => "Validating",
                },
                Steps = submission
                    .Select(s => new ValidationStep { Type = s.Type, State = s.State.ToString(), Message = s.Message })
                    .ToList(),
            });
        }
    }

    public class RepositorySignaturesResponse
    {
        [JsonPropertyName("allRepositorySigned")]
        public bool AllRepositorySigned { get; set; }

        [JsonPropertyName("signingCertificates")]
        public IReadOnlyList<RepositorySigningCertificate> SigningCertificates { get; set; }
    }

    public class RepositorySigningCertificate
    {
        [JsonPropertyName("fingerprints")]
        public IReadOnlyDictionary<string, string> Fingerprints { get; set; }

        [JsonPropertyName("subject")]
        public string Subject { get; set; }

        [JsonPropertyName("issuer")]
        public string Issuer { get; set; }

        [JsonPropertyName("notBefore")]
        public DateTime NotBefore { get; set; }

        [JsonPropertyName("notAfter")]
        public DateTime NotAfter { get; set; }

        [JsonPropertyName("contentUrl")]
        public string ContentUrl { get; set; }
    }

    public class ValidationStatusResponse
    {
        [JsonPropertyName("submissionId")]
        public string SubmissionId { get; set; }

        /// <summary>
        /// "Validating", "Available" or "Quarantined".
        /// </summary>
        [JsonPropertyName("status")]
        public string Status { get; set; }

        [JsonPropertyName("steps")]
        public IReadOnlyList<ValidationStep> Steps { get; set; }
    }

    public class ValidationStep
    {
        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("state")]
        public string State { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; }
    }

    /// <summary>
    /// Advertises RepositorySignatures/5.0.0 while a signing certificate is configured.
    /// </summary>
    public class RepositorySignaturesServiceIndexProvider : IServiceIndexResourceProvider
    {
        private readonly RepositorySigner _signer;
        private readonly IUrlGenerator _url;

        public RepositorySignaturesServiceIndexProvider(RepositorySigner signer, IUrlGenerator url)
        {
            _signer = signer ?? throw new ArgumentNullException(nameof(signer));
            _url = url ?? throw new ArgumentNullException(nameof(url));
        }

        public IEnumerable<ServiceIndexItem> GetResources()
        {
            if (!_signer.IsConfigured) yield break;

            yield return new ServiceIndexItem
            {
                ResourceUrl = _url.GetRepositorySignaturesUrl(),
                Type = "RepositorySignatures/5.0.0",
            };
        }
    }
}
