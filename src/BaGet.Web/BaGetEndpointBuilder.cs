using System.Threading.Tasks;
using BaGet.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Constraints;

namespace BaGet
{
    public class BaGetEndpointBuilder
    {
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
        }

        /// <summary>
        /// Deprecation and vulnerability metadata for a package version. Writes only, so they
        /// share the authorisation of the publish endpoints.
        /// </summary>
        public void MapPackageMetadataAdminRoutes(IEndpointRouteBuilder endpoints)
        {
            endpoints.MapControllerRoute(
                name: Routes.SetDeprecationRouteName,
                pattern: "v3/package/{id}/{version}/deprecation",
                defaults: new { controller = "PackageMetadataAdmin", action = "SetDeprecation" },
                constraints: new { httpMethod = new HttpMethodRouteConstraint("PUT") });

            endpoints.MapControllerRoute(
                name: Routes.RemoveDeprecationRouteName,
                pattern: "v3/package/{id}/{version}/deprecation",
                defaults: new { controller = "PackageMetadataAdmin", action = "RemoveDeprecation" },
                constraints: new { httpMethod = new HttpMethodRouteConstraint("DELETE") });

            endpoints.MapControllerRoute(
                name: Routes.SetVulnerabilitiesRouteName,
                pattern: "v3/package/{id}/{version}/vulnerabilities",
                defaults: new { controller = "PackageMetadataAdmin", action = "SetVulnerabilities" },
                constraints: new { httpMethod = new HttpMethodRouteConstraint("PUT") });
        }

        public void MapCatalogRoutes(IEndpointRouteBuilder endpoints)
        {
            endpoints.MapControllerRoute(
                name: Routes.CatalogIndexRouteName,
                pattern: "v3/catalog/index.json",
                defaults: new { controller = "Catalog", action = "Index" },
                constraints: new { httpMethod = new HttpMethodRouteConstraint("GET") });

            endpoints.MapControllerRoute(
                name: Routes.CatalogPageRouteName,
                pattern: "v3/catalog/page{pageIndex:int}.json",
                defaults: new { controller = "Catalog", action = "Page" },
                constraints: new { httpMethod = new HttpMethodRouteConstraint("GET") });

            endpoints.MapControllerRoute(
                name: Routes.CatalogLeafRouteName,
                pattern: "v3/catalog/data/{commitId}.json",
                defaults: new { controller = "Catalog", action = "Leaf" },
                constraints: new { httpMethod = new HttpMethodRouteConstraint("GET") });
        }

        public void MapVulnerabilityRoutes(IEndpointRouteBuilder endpoints)
        {
            endpoints.MapControllerRoute(
                name: Routes.VulnerabilityIndexRouteName,
                pattern: "v3/vulnerabilities/index.json",
                defaults: new { controller = "Vulnerability", action = "Index" },
                constraints: new { httpMethod = new HttpMethodRouteConstraint("GET") });

            endpoints.MapControllerRoute(
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
            endpoints.MapControllerRoute(
                name: Routes.V2RootRouteName,
                pattern: "api/v2",
                defaults: new { controller = "V2Feed", action = "Root" },
                constraints: new { httpMethod = new HttpMethodRouteConstraint("GET") });

            endpoints.MapControllerRoute(
                name: Routes.V2MetadataRouteName,
                pattern: "api/v2/$metadata",
                defaults: new { controller = "V2Feed", action = "Metadata" },
                constraints: new { httpMethod = new HttpMethodRouteConstraint("GET") });

            endpoints.MapControllerRoute(
                name: Routes.V2FindPackagesByIdRouteName,
                pattern: "api/v2/FindPackagesById()",
                defaults: new { controller = "V2Feed", action = "FindPackagesById" },
                constraints: new { httpMethod = new HttpMethodRouteConstraint("GET") });

            endpoints.MapControllerRoute(
                name: Routes.V2SearchRouteName,
                pattern: "api/v2/Search()",
                defaults: new { controller = "V2Feed", action = "Search" },
                constraints: new { httpMethod = new HttpMethodRouteConstraint("GET") });

            // The keyed form carries its values inside the segment, so it is matched by a
            // literal pattern per argument order rather than by route values.
            endpoints.MapControllerRoute(
                name: Routes.V2PackagesByKeyRouteName,
                pattern: "api/v2/Packages(Id={id},Version={version})",
                defaults: new { controller = "V2Feed", action = "PackagesByKey" },
                constraints: new { httpMethod = new HttpMethodRouteConstraint("GET") });

            endpoints.MapControllerRoute(
                name: Routes.V2PackagesRouteName,
                pattern: "api/v2/Packages()",
                defaults: new { controller = "V2Feed", action = "Packages" },
                constraints: new { httpMethod = new HttpMethodRouteConstraint("GET") });
        }

        public void MapServiceIndexRoutes(IEndpointRouteBuilder endpoints)
        {
            endpoints.MapControllerRoute(
                name: Routes.IndexRouteName,
                pattern: "v3/index.json",
                defaults: new { controller = "ServiceIndex", action = "Get" });
        }

        public void MapPackagePublishRoutes(IEndpointRouteBuilder endpoints)
        {
            endpoints.MapControllerRoute(
                name: Routes.UploadPackageRouteName,
                pattern: "api/v2/package",
                defaults: new { controller = "PackagePublish", action = "Upload" },
                constraints: new { httpMethod = new HttpMethodRouteConstraint("PUT") });

            endpoints.MapControllerRoute(
                name: Routes.DeleteRouteName,
                pattern: "api/v2/package/{id}/{version}",
                defaults: new { controller = "PackagePublish", action = "Delete" },
                constraints: new { httpMethod = new HttpMethodRouteConstraint("DELETE") });

            endpoints.MapControllerRoute(
                name: Routes.RelistRouteName,
                pattern: "api/v2/package/{id}/{version}",
                defaults: new { controller = "PackagePublish", action = "Relist" },
                constraints: new { httpMethod = new HttpMethodRouteConstraint("POST") });
        }

        public void MapSymbolRoutes(IEndpointRouteBuilder endpoints)
        {
            endpoints.MapControllerRoute(
                name: Routes.UploadSymbolRouteName,
                pattern: "api/v2/symbol",
                defaults: new { controller = "Symbol", action = "Upload" },
                constraints: new { httpMethod = new HttpMethodRouteConstraint("PUT") });

            endpoints.MapControllerRoute(
                name: Routes.SymbolDownloadRouteName,
                pattern: "api/download/symbols/{file}/{key}/{file2}",
                defaults: new { controller = "Symbol", action = "Get" });

            endpoints.MapControllerRoute(
                name: Routes.PrefixedSymbolDownloadRouteName,
                pattern: "api/download/symbols/{prefix}/{file}/{key}/{file2}",
                defaults: new { controller = "Symbol", action = "Get" });
        }

        public void MapSearchRoutes(IEndpointRouteBuilder endpoints)
        {
            endpoints.MapControllerRoute(
                name: Routes.SearchRouteName,
                pattern: "v3/search",
                defaults: new { controller = "Search", action = "Search" });

            endpoints.MapControllerRoute(
                name: Routes.AutocompleteRouteName,
                pattern: "v3/autocomplete",
                defaults: new { controller = "Search", action = "Autocomplete" });

            // This is an unofficial API to find packages that depend on a given package.
            endpoints.MapControllerRoute(
                name: Routes.DependentsRouteName,
                pattern: "v3/dependents",
                defaults: new { controller = "Search", action = "Dependents" });
        }

        public void MapPackageMetadataRoutes(IEndpointRouteBuilder endpoints)
        {
            endpoints.MapControllerRoute(
               name: Routes.RegistrationIndexRouteName,
               pattern: "v3/registration/{id}/index.json",
               defaults: new { controller = "PackageMetadata", action = "RegistrationIndex" });

            endpoints.MapControllerRoute(
                name: Routes.RegistrationLeafRouteName,
                pattern: "v3/registration/{id}/{version}.json",
                defaults: new { controller = "PackageMetadata", action = "RegistrationLeaf" });
        }

        public void MapPackageContentRoutes(IEndpointRouteBuilder endpoints)
        {
            endpoints.MapControllerRoute(
                name: Routes.PackageVersionsRouteName,
                pattern: "v3/package/{id}/index.json",
                defaults: new { controller = "PackageContent", action = "GetPackageVersions" });

            endpoints.MapControllerRoute(
                name: Routes.PackageDownloadRouteName,
                pattern: "v3/package/{id}/{version}/{idVersion}.nupkg",
                defaults: new { controller = "PackageContent", action = "DownloadPackage" });

            endpoints.MapControllerRoute(
                name: Routes.PackageDownloadManifestRouteName,
                pattern: "v3/package/{id}/{version}/{id2}.nuspec",
                defaults: new { controller = "PackageContent", action = "DownloadNuspec" });

            endpoints.MapControllerRoute(
                name: Routes.PackageDownloadReadmeRouteName,
                pattern: "v3/package/{id}/{version}/readme",
                defaults: new { controller = "PackageContent", action = "DownloadReadme" });

            endpoints.MapControllerRoute(
                name: Routes.PackageDownloadIconRouteName,
                pattern: "v3/package/{id}/{version}/icon",
                defaults: new { controller = "PackageContent", action = "DownloadIcon" });
        }
    }
}
