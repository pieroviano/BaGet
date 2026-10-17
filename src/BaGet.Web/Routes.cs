namespace BaGet.Web
{
    public class Routes
    {
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
    }
}
