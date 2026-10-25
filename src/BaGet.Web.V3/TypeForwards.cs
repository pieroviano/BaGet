using System.Runtime.CompilerServices;

// Types that moved to BaGet.Identity (the JSON UI API needs them and BaGet.Web.V3 depends on it) or to BaGet.Web.Application keep their name and namespace,
// so binaries compiled against BaGet.Web.V3 keep working.
[assembly: TypeForwardedTo(typeof(BaGet.Web.RequestPrincipalResolver))]
[assembly: TypeForwardedTo(typeof(BaGet.Web.BasicCredentials))]
[assembly: TypeForwardedTo(typeof(BaGet.Web.ReadmeMarkdown))]
