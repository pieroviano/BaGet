using System;
using System.Globalization;
using BaGet.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using NuGet.Versioning;

namespace BaGet.Web
{
    // The URLs below are built from the incoming request's "Host" header, which the client
    // controls. The header is validated against the configured allow-list by ASP.NET Core's
    // host filtering middleware instead of here: see BaGetOptions.AllowedHosts, which BaGet's
    // Startup binds to HostFilteringOptions and enforces with UseHostFiltering.
    //
    // Validating in this generator would be both too late and too narrow. Too late, because
    // by the time a URL is generated the request has already been handled on a host BaGet
    // does not serve, and there is no correct URL left to return. Too narrow, because the
    // same header is read by routing, by LinkGenerator below and by anything else holding
    // the HttpContext; only rejecting the request protects all of them at once.
    public class BaGetUrlGenerator : IUrlGenerator
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly LinkGenerator _linkGenerator;

        public BaGetUrlGenerator(IHttpContextAccessor httpContextAccessor, LinkGenerator linkGenerator)
        {
            _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
            _linkGenerator = linkGenerator ?? throw new ArgumentNullException(nameof(linkGenerator));
        }

        public string GetServiceIndexUrl()
        {
            return _linkGenerator.GetUriByRouteValues(
                _httpContextAccessor.HttpContext,
                Routes.IndexRouteName,
                values: null);
        }

        public string GetPackageContentResourceUrl()
        {
            return AbsoluteUrl("v3/package");
        }

        public string GetPackageMetadataResourceUrl()
        {
            return AbsoluteUrl("v3/registration");
        }

        public string GetPackagePublishResourceUrl()
        {
            return _linkGenerator.GetUriByRouteValues(
                _httpContextAccessor.HttpContext,
                Routes.UploadPackageRouteName,
                values: null);
        }

        public string GetSymbolPublishResourceUrl()
        {
            return _linkGenerator.GetUriByRouteValues(
                _httpContextAccessor.HttpContext,
                Routes.UploadSymbolRouteName,
                values: null);
        }

        public string GetSearchResourceUrl()
        {
            return _linkGenerator.GetUriByRouteValues(
                _httpContextAccessor.HttpContext,
                Routes.SearchRouteName,
                values: null);
        }

        public string GetAutocompleteResourceUrl()
        {
            return _linkGenerator.GetUriByRouteValues(
                _httpContextAccessor.HttpContext,
                Routes.AutocompleteRouteName,
                values: null);
        }

        public string GetRegistrationIndexUrl(string id)
        {
            return _linkGenerator.GetUriByRouteValues(
                _httpContextAccessor.HttpContext,
                Routes.RegistrationIndexRouteName,
                values: new { Id = id.ToLowerInvariant() });
        }

        public string GetRegistrationPageUrl(string id, NuGetVersion lower, NuGetVersion upper)
        {
            // BaGet does not support paging the registration resource.
            throw new NotImplementedException();
        }

        public string GetRegistrationLeafUrl(string id, NuGetVersion version)
        {
            return _linkGenerator.GetUriByRouteValues(
                _httpContextAccessor.HttpContext,
                Routes.RegistrationLeafRouteName,
                values: new
                {
                    Id = id.ToLowerInvariant(),
                    Version = version.ToNormalizedString().ToLowerInvariant(),
                });
        }

        public string GetPackageVersionsUrl(string id)
        {
            return _linkGenerator.GetUriByRouteValues(
                _httpContextAccessor.HttpContext,
                Routes.PackageVersionsRouteName,
                values: new { Id = id.ToLowerInvariant() });
        }

        public string GetPackageDownloadUrl(string id, NuGetVersion version)
        {
            id = id.ToLowerInvariant();
            var versionString = version.ToNormalizedString().ToLowerInvariant();

            return _linkGenerator.GetUriByRouteValues(
                _httpContextAccessor.HttpContext,
                Routes.PackageDownloadRouteName,
                values: new
                {
                    Id = id,
                    Version = versionString,
                    IdVersion = $"{id}.{versionString}"
                });
        }

        public string GetPackageManifestDownloadUrl(string id, NuGetVersion version)
        {
            id = id.ToLowerInvariant();
            var versionString = version.ToNormalizedString().ToLowerInvariant();

            return _linkGenerator.GetUriByRouteValues(
                _httpContextAccessor.HttpContext,
                Routes.PackageDownloadRouteName,
                values: new
                {
                    Id = id,
                    Version = versionString,
                    Id2 = id,
                });
        }

        public string GetPackageIconDownloadUrl(string id, NuGetVersion version)
        {
            id = id.ToLowerInvariant();
            var versionString = version.ToNormalizedString().ToLowerInvariant();

            return _linkGenerator.GetUriByRouteValues(
                _httpContextAccessor.HttpContext,
                Routes.PackageDownloadIconRouteName,
                values: new
                {
                    Id = id,
                    Version = versionString
                });
        }

        public string GetV2FeedUrl()
        {
            return _linkGenerator.GetUriByRouteValues(
                _httpContextAccessor.HttpContext,
                Routes.V2RootRouteName,
                values: null);
        }

        // These two are templates for the client to expand, not URLs for a specific
        // package, so the placeholders are returned verbatim. Routing them through
        // LinkGenerator would percent-encode the braces.
        public string GetPackageDetailsUriTemplate()
        {
            return AbsoluteUrl("packages/{id}/{version}");
        }

        public string GetVulnerabilityIndexUrl()
        {
            return AbsoluteUrl("v3/vulnerabilities/index.json");
        }

        public string GetVulnerabilityPageUrl(string pageName)
        {
            return AbsoluteUrl($"v3/vulnerabilities/{Uri.EscapeDataString(pageName)}.json");
        }

        public string GetCatalogIndexUrl()
        {
            return AbsoluteUrl("v3/catalog/index.json");
        }

        public string GetCatalogPageUrl(int pageIndex)
        {
            return AbsoluteUrl($"v3/catalog/page{pageIndex.ToString(CultureInfo.InvariantCulture)}.json");
        }

        public string GetCatalogLeafUrl(string commitId)
        {
            return AbsoluteUrl($"v3/catalog/data/{Uri.EscapeDataString(commitId)}.json");
        }

        public string GetRepositorySignaturesUrl()
        {
            return AbsoluteUrl("v3/repository-signatures/index.json");
        }

        public string GetRepositorySigningCertificateUrl(string fingerprint)
        {
            return AbsoluteUrl($"v3/repository-signatures/{Uri.EscapeDataString(fingerprint)}.crt");
        }

        public string GetReportAbuseUriTemplate()
        {
            return AbsoluteUrl("packages/{id}/{version}/report-abuse");
        }

        private string AbsoluteUrl(string relativePath)
        {
            var request = _httpContextAccessor.HttpContext.Request;

            return string.Concat(
                request.Scheme,
                "://",
                request.Host.ToUriComponent(),
                request.PathBase.ToUriComponent(),
                "/",
                relativePath);
        }
    }
}
