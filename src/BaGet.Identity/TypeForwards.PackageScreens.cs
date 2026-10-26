using System.Runtime.CompilerServices;

// View models of the package-management screens (Npm/Maven/Docker manage, contact owners, validation status, scan) moved to
// BaGet.Identity.Application as the JSON models of their /api/ui twins; name and namespace are unchanged.
[assembly: TypeForwardedTo(typeof(BaGet.Identity.ViewModels.ContactOwnersViewModel))]
[assembly: TypeForwardedTo(typeof(BaGet.Identity.ViewModels.DockerManageViewModel))]
[assembly: TypeForwardedTo(typeof(BaGet.Identity.ViewModels.MavenManageViewModel))]
[assembly: TypeForwardedTo(typeof(BaGet.Identity.ViewModels.NpmManageViewModel))]
[assembly: TypeForwardedTo(typeof(BaGet.Identity.ViewModels.ScanViewModel))]
[assembly: TypeForwardedTo(typeof(BaGet.Identity.ViewModels.ValidationStatusViewModel))]
[assembly: TypeForwardedTo(typeof(BaGet.Identity.ViewModels.ValidationSubmission))]
