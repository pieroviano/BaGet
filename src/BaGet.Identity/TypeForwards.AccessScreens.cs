using System.Runtime.CompilerServices;

// The view models of the access-control screens (groups, permission targets, projects, audit) moved with their use cases (the JSON DTOs of /api/ui); names and namespaces are kept.
[assembly: TypeForwardedTo(typeof(BaGet.Identity.ViewModels.AccessViewModel))]
[assembly: TypeForwardedTo(typeof(BaGet.Identity.ViewModels.GroupsViewModel))]
[assembly: TypeForwardedTo(typeof(BaGet.Identity.ViewModels.GroupDetailViewModel))]
[assembly: TypeForwardedTo(typeof(BaGet.Identity.ViewModels.PermissionsViewModel))]
[assembly: TypeForwardedTo(typeof(BaGet.Identity.ViewModels.PermissionDetailViewModel))]
[assembly: TypeForwardedTo(typeof(BaGet.Identity.ViewModels.ProjectsViewModel))]
[assembly: TypeForwardedTo(typeof(BaGet.Identity.ViewModels.ProjectDetailViewModel))]
[assembly: TypeForwardedTo(typeof(BaGet.Identity.ViewModels.AuditViewModel))]
[assembly: TypeForwardedTo(typeof(BaGet.Identity.Pages.Admin.PatternPreview))]
[assembly: TypeForwardedTo(typeof(BaGet.Identity.Pages.Admin.PatternFieldsView))]
