namespace BaGet.Web;

public class Routes
{
    /// <summary>
    /// The route value that carries the repository name on the <c>/r/{repo}/</c> variant of a protocol route.
    /// </summary>
    public const string RepositoryRouteKey = "repo";

    /// <summary>
    /// The path segments in front of a non-default repository's routes.
    /// </summary>
    public const string RepositoryPathPrefix = "r/{" + RepositoryRouteKey + "}/";

    /// <summary>
    /// The name under which the <c>/r/{repo}/</c> variant of the route <paramref name="routeName"/> is registered;
    /// the unprefixed route keeps <paramref name="routeName"/> and serves the default repository.
    /// </summary>
    public static string RepositoryRouteName(string routeName) => "r:" + routeName;

    public const string IndexRouteName = "index";
    public const string UploadPackageRouteName = "upload-package";
    public const string UploadSymbolRouteName = "upload-symbol";
    public const string DeleteRouteName = "delete";
    public const string RelistRouteName = "relist";
    public const string SearchRouteName = "search";
    public const string AutocompleteRouteName = "autocomplete";
    public const string DependentsRouteName = "dependents";
    public const string RegistrationIndexRouteName = "registration-index";
    public const string RegistrationLeafRouteName = "registration-leaf";
    public const string PackageVersionsRouteName = "package-versions";
    public const string PackageDownloadRouteName = "package-download";
    public const string PackageDownloadManifestRouteName = "package-download-manifest";
    public const string PackageDownloadReadmeRouteName = "package-download-readme";
    public const string PackageDownloadIconRouteName = "package-download-icon";
    public const string SymbolDownloadRouteName = "symbol-download";
    public const string PrefixedSymbolDownloadRouteName = "prefixed-symbol-download";

    public const string V2RootRouteName = "v2-root";
    public const string V2MetadataRouteName = "v2-metadata";
    public const string V2FindPackagesByIdRouteName = "v2-find-packages-by-id";
    public const string V2PackagesRouteName = "v2-packages";
    public const string V2PackagesByKeyRouteName = "v2-packages-by-key";
    public const string V2SearchRouteName = "v2-search";

    public const string SetDeprecationRouteName = "set-deprecation";
    public const string RemoveDeprecationRouteName = "remove-deprecation";
    public const string SetVulnerabilitiesRouteName = "set-vulnerabilities";
    public const string VulnerabilityIndexRouteName = "vulnerability-index";
    public const string VulnerabilityPageRouteName = "vulnerability-page";
    public const string CatalogIndexRouteName = "catalog-index";
    public const string CatalogPageRouteName = "catalog-page";
    public const string CatalogLeafRouteName = "catalog-leaf";
    public const string RepositorySignaturesRouteName = "repository-signatures";
    public const string RepositorySigningCertificateRouteName = "repository-signing-certificate";
    public const string ValidationStatusRouteName = "validation-status";
}