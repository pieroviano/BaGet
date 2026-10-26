using System.Runtime.CompilerServices;

// Types that moved to BaGet.Identity (the JSON UI API needs them and BaGet.Web.V3 depends on it) or to BaGet.Web.Application keep their name and namespace,
// so binaries compiled against BaGet.Web.V3 keep working.
[assembly: TypeForwardedTo(typeof(BaGet.Web.RequestPrincipalResolver))]
[assembly: TypeForwardedTo(typeof(BaGet.Web.BasicCredentials))]
[assembly: TypeForwardedTo(typeof(BaGet.Web.ReadmeMarkdown))]

// The format and upload screens' data (view models, which are the JSON DTOs of the UI API) and the presentation helpers they use.
[assembly: TypeForwardedTo(typeof(BaGet.Web.DockerSnippets))]
[assembly: TypeForwardedTo(typeof(BaGet.Web.PackageDependencies))]
[assembly: TypeForwardedTo(typeof(BaGet.Web.DependencyGroupModel))]
[assembly: TypeForwardedTo(typeof(BaGet.Web.DependencyModel))]
[assembly: TypeForwardedTo(typeof(BaGet.Web.InstallSnippet))]
[assembly: TypeForwardedTo(typeof(BaGet.Web.DockerImagesViewModel))]
[assembly: TypeForwardedTo(typeof(BaGet.Web.DockerImageViewModel))]
[assembly: TypeForwardedTo(typeof(BaGet.Web.MavenArtifactsViewModel))]
[assembly: TypeForwardedTo(typeof(BaGet.Web.MavenArtifactViewModel))]
[assembly: TypeForwardedTo(typeof(BaGet.Web.MavenUploadViewModel))]
[assembly: TypeForwardedTo(typeof(BaGet.Web.NpmPackagesViewModel))]
[assembly: TypeForwardedTo(typeof(BaGet.Web.NpmPackageViewModel))]
[assembly: TypeForwardedTo(typeof(BaGet.Web.NpmUploadViewModel))]
[assembly: TypeForwardedTo(typeof(BaGet.Web.UploadPreview))]
[assembly: TypeForwardedTo(typeof(BaGet.Web.UploadViewModel))]
