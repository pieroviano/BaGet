using System.Linq;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Constraints;

namespace BaGet;

/// <summary>
/// Maps every endpoint. Each protocol route (everything but the Razor Pages) is registered twice: at its plain
/// path, which serves the <c>default</c> repository, and under <c>r/{repo}/</c>, which serves the named one
/// (<see cref="Routes.RepositoryPathPrefix"/>). <see cref="RepositoryResolutionMiddleware"/> turns the route value
/// into <see cref="BaGet.Core.IRepositoryContext"/>, and <see cref="BaGetUrlGenerator"/> picks the matching variant.
/// </summary>
public class BaGetEndpointBuilder
{
    /// <summary>
    /// The one place that registers a protocol route, so no endpoint can exist for the default repository only.
    /// <paramref name="format"/> is the format whose protocol the route speaks (its <see cref="IFormatModule"/>;
    /// the pattern starts with the module's <see cref="IFormatModule.RoutePrefix"/> for a format other than NuGet): a request
    /// for it in a repository of another format is answered 404.
    /// </summary>
    public static void MapRoute(
        IEndpointRouteBuilder endpoints,
        string name,
        string pattern,
        object defaults,
        object? constraints = null,
        bool authorizesItself = false,
        RepositoryFormat format = RepositoryFormat.NuGet)
    {
        // The marker is what RepositoryReadFilter looks for: every route registered here needs Read access, except a
        // route that is constrained to write methods only (and then only for those methods).
        var metadata = RepositoryRouteMetadata.For(WritesOnly(constraints), authorizesItself, format);
        endpoints.MapControllerRoute(name, pattern, defaults, constraints).WithMetadata(metadata);
        endpoints.MapControllerRoute(
            Routes.RepositoryRouteName(name),
            Routes.RepositoryPathPrefix + pattern,
            defaults,
            constraints).WithMetadata(metadata);
    }

    /// <summary>
    /// Whether the <c>httpMethod</c> constraint admits write methods (PUT, POST, PATCH, DELETE) only. An unconstrained
    /// route, or one that admits GET or HEAD, is a read route whatever method the caller sends.
    /// </summary>
    private static bool WritesOnly(object? constraints)
    {
        if (constraints == null) return false;

        var values = new RouteValueDictionary(constraints);
        if (!values.TryGetValue("httpMethod", out var constraint) || constraint is not HttpMethodRouteConstraint method) return false;

        return method.AllowedMethods.Count > 0
            && method.AllowedMethods.All(m => !HttpMethods.IsGet(m) && !HttpMethods.IsHead(m)
                && !HttpMethods.IsOptions(m) && !HttpMethods.IsTrace(m));
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapRazorPages();

        MapServiceIndexRoutes(endpoints);
        MapPackagePublishRoutes(endpoints);
        MapSymbolRoutes(endpoints);
        MapSearchRoutes(endpoints);
        MapPackageMetadataRoutes(endpoints);
        MapPackageContentRoutes(endpoints);
        MapV2FeedRoutes(endpoints);
        MapPackageMetadataAdminRoutes(endpoints);
        MapVulnerabilityRoutes(endpoints);
        MapCatalogRoutes(endpoints);
        MapValidationRoutes(endpoints);
    }

    /// <summary>
    /// Deprecation and vulnerability metadata for a package version. Writes only, so they
    /// share the authorisation of the publish endpoints.
    /// </summary>
    public void MapPackageMetadataAdminRoutes(IEndpointRouteBuilder endpoints)
    {
        MapRoute(
            endpoints,
            name: Routes.SetDeprecationRouteName,
            pattern: "v3/package/{id}/{version}/deprecation",
            defaults: new { controller = "PackageMetadataAdmin", action = "SetDeprecation" },
            constraints: new { httpMethod = new HttpMethodRouteConstraint("PUT") });

        MapRoute(
            endpoints,
            name: Routes.RemoveDeprecationRouteName,
            pattern: "v3/package/{id}/{version}/deprecation",
            defaults: new { controller = "PackageMetadataAdmin", action = "RemoveDeprecation" },
            constraints: new { httpMethod = new HttpMethodRouteConstraint("DELETE") });

        MapRoute(
            endpoints,
            name: Routes.SetVulnerabilitiesRouteName,
            pattern: "v3/package/{id}/{version}/vulnerabilities",
            defaults: new { controller = "PackageMetadataAdmin", action = "SetVulnerabilities" },
            constraints: new { httpMethod = new HttpMethodRouteConstraint("PUT") });
    }

    public void MapValidationRoutes(IEndpointRouteBuilder endpoints)
    {
        MapRoute(
            endpoints,
            name: Routes.RepositorySignaturesRouteName,
            pattern: "v3/repository-signatures/index.json",
            defaults: new { controller = "RepositorySignatures", action = "Index" },
            constraints: new { httpMethod = new HttpMethodRouteConstraint("GET") });

        MapRoute(
            endpoints,
            name: Routes.RepositorySigningCertificateRouteName,
            pattern: "v3/repository-signatures/{fingerprint}.crt",
            defaults: new { controller = "RepositorySignatures", action = "Certificate" },
            constraints: new { httpMethod = new HttpMethodRouteConstraint("GET") });

        MapRoute(
            endpoints,
            name: Routes.ValidationStatusRouteName,
            pattern: "v3/package/{id}/{version}/validation",
            defaults: new { controller = "ValidationStatus", action = "Get" },
            constraints: new { httpMethod = new HttpMethodRouteConstraint("GET") },
            authorizesItself: true);
    }

    public void MapCatalogRoutes(IEndpointRouteBuilder endpoints)
    {
        MapRoute(
            endpoints,
            name: Routes.CatalogIndexRouteName,
            pattern: "v3/catalog/index.json",
            defaults: new { controller = "Catalog", action = "Index" },
            constraints: new { httpMethod = new HttpMethodRouteConstraint("GET") });

        MapRoute(
            endpoints,
            name: Routes.CatalogPageRouteName,
            pattern: "v3/catalog/page{pageIndex:int}.json",
            defaults: new { controller = "Catalog", action = "Page" },
            constraints: new { httpMethod = new HttpMethodRouteConstraint("GET") });

        MapRoute(
            endpoints,
            name: Routes.CatalogLeafRouteName,
            pattern: "v3/catalog/data/{commitId}.json",
            defaults: new { controller = "Catalog", action = "Leaf" },
            constraints: new { httpMethod = new HttpMethodRouteConstraint("GET") });
    }

    public void MapVulnerabilityRoutes(IEndpointRouteBuilder endpoints)
    {
        MapRoute(
            endpoints,
            name: Routes.VulnerabilityIndexRouteName,
            pattern: "v3/vulnerabilities/index.json",
            defaults: new { controller = "Vulnerability", action = "Index" },
            constraints: new { httpMethod = new HttpMethodRouteConstraint("GET") });

        MapRoute(
            endpoints,
            name: Routes.VulnerabilityPageRouteName,
            pattern: "v3/vulnerabilities/{pageName}.json",
            defaults: new { controller = "Vulnerability", action = "Page" },
            constraints: new { httpMethod = new HttpMethodRouteConstraint("GET") });
    }

    /// <summary>
    /// The V2 (OData) feed. The routes are always mapped; the controller itself
    /// answers 404 for every action when "Feeds:EnableV2" is off, so turning the feed
    /// off does not change which routes exist.
    /// </summary>
    public void MapV2FeedRoutes(IEndpointRouteBuilder endpoints)
    {
        MapRoute(
            endpoints,
            name: Routes.V2RootRouteName,
            pattern: "api/v2",
            defaults: new { controller = "V2Feed", action = "Root" },
            constraints: new { httpMethod = new HttpMethodRouteConstraint("GET") });

        MapRoute(
            endpoints,
            name: Routes.V2MetadataRouteName,
            pattern: "api/v2/$metadata",
            defaults: new { controller = "V2Feed", action = "Metadata" },
            constraints: new { httpMethod = new HttpMethodRouteConstraint("GET") });

        MapRoute(
            endpoints,
            name: Routes.V2FindPackagesByIdRouteName,
            pattern: "api/v2/FindPackagesById()",
            defaults: new { controller = "V2Feed", action = "FindPackagesById" },
            constraints: new { httpMethod = new HttpMethodRouteConstraint("GET") });

        MapRoute(
            endpoints,
            name: Routes.V2SearchRouteName,
            pattern: "api/v2/Search()",
            defaults: new { controller = "V2Feed", action = "Search" },
            constraints: new { httpMethod = new HttpMethodRouteConstraint("GET") });

        // The keyed form carries its values inside the segment, so it is matched by a
        // literal pattern per argument order rather than by route values.
        MapRoute(
            endpoints,
            name: Routes.V2PackagesByKeyRouteName,
            pattern: "api/v2/Packages(Id={id},Version={version})",
            defaults: new { controller = "V2Feed", action = "PackagesByKey" },
            constraints: new { httpMethod = new HttpMethodRouteConstraint("GET") });

        MapRoute(
            endpoints,
            name: Routes.V2PackagesRouteName,
            pattern: "api/v2/Packages()",
            defaults: new { controller = "V2Feed", action = "Packages" },
            constraints: new { httpMethod = new HttpMethodRouteConstraint("GET") });
    }

    public void MapServiceIndexRoutes(IEndpointRouteBuilder endpoints)
    {
        MapRoute(
            endpoints,
            name: Routes.IndexRouteName,
            pattern: "v3/index.json",
            defaults: new { controller = "ServiceIndex", action = "Get" });
    }

    public void MapPackagePublishRoutes(IEndpointRouteBuilder endpoints)
    {
        MapRoute(
            endpoints,
            name: Routes.UploadPackageRouteName,
            pattern: "api/v2/package",
            defaults: new { controller = "PackagePublish", action = "Upload" },
            constraints: new { httpMethod = new HttpMethodRouteConstraint("PUT") });

        MapRoute(
            endpoints,
            name: Routes.DeleteRouteName,
            pattern: "api/v2/package/{id}/{version}",
            defaults: new { controller = "PackagePublish", action = "Delete" },
            constraints: new { httpMethod = new HttpMethodRouteConstraint("DELETE") });

        MapRoute(
            endpoints,
            name: Routes.RelistRouteName,
            pattern: "api/v2/package/{id}/{version}",
            defaults: new { controller = "PackagePublish", action = "Relist" },
            constraints: new { httpMethod = new HttpMethodRouteConstraint("POST") });
    }

    public void MapSymbolRoutes(IEndpointRouteBuilder endpoints)
    {
        MapRoute(
            endpoints,
            name: Routes.UploadSymbolRouteName,
            pattern: "api/v2/symbol",
            defaults: new { controller = "Symbol", action = "Upload" },
            constraints: new { httpMethod = new HttpMethodRouteConstraint("PUT") });

        MapRoute(
            endpoints,
            name: Routes.SymbolDownloadRouteName,
            pattern: "api/download/symbols/{file}/{key}/{file2}",
            defaults: new { controller = "Symbol", action = "Get" });

        MapRoute(
            endpoints,
            name: Routes.PrefixedSymbolDownloadRouteName,
            pattern: "api/download/symbols/{prefix}/{file}/{key}/{file2}",
            defaults: new { controller = "Symbol", action = "Get" });
    }

    public void MapSearchRoutes(IEndpointRouteBuilder endpoints)
    {
        MapRoute(
            endpoints,
            name: Routes.SearchRouteName,
            pattern: "v3/search",
            defaults: new { controller = "Search", action = "Search" });

        MapRoute(
            endpoints,
            name: Routes.AutocompleteRouteName,
            pattern: "v3/autocomplete",
            defaults: new { controller = "Search", action = "Autocomplete" });

        // This is an unofficial API to find packages that depend on a given package.
        MapRoute(
            endpoints,
            name: Routes.DependentsRouteName,
            pattern: "v3/dependents",
            defaults: new { controller = "Search", action = "Dependents" });
    }

    public void MapPackageMetadataRoutes(IEndpointRouteBuilder endpoints)
    {
        MapRoute(
            endpoints,
            name: Routes.RegistrationIndexRouteName,
            pattern: "v3/registration/{id}/index.json",
            defaults: new { controller = "PackageMetadata", action = "RegistrationIndex" });

        MapRoute(
            endpoints,
            name: Routes.RegistrationLeafRouteName,
            pattern: "v3/registration/{id}/{version}.json",
            defaults: new { controller = "PackageMetadata", action = "RegistrationLeaf" });
    }

    public void MapPackageContentRoutes(IEndpointRouteBuilder endpoints)
    {
        MapRoute(
            endpoints,
            name: Routes.PackageVersionsRouteName,
            pattern: "v3/package/{id}/index.json",
            defaults: new { controller = "PackageContent", action = "GetPackageVersions" });

        MapRoute(
            endpoints,
            name: Routes.PackageDownloadRouteName,
            pattern: "v3/package/{id}/{version}/{idVersion}.nupkg",
            defaults: new { controller = "PackageContent", action = "DownloadPackage" });

        MapRoute(
            endpoints,
            name: Routes.PackageDownloadManifestRouteName,
            pattern: "v3/package/{id}/{version}/{id2}.nuspec",
            defaults: new { controller = "PackageContent", action = "DownloadNuspec" });

        MapRoute(
            endpoints,
            name: Routes.PackageDownloadReadmeRouteName,
            pattern: "v3/package/{id}/{version}/readme",
            defaults: new { controller = "PackageContent", action = "DownloadReadme" });

        MapRoute(
            endpoints,
            name: Routes.PackageDownloadIconRouteName,
            pattern: "v3/package/{id}/{version}/icon",
            defaults: new { controller = "PackageContent", action = "DownloadIcon" });
    }
}