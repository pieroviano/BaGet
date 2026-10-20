using BaGet.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace BaGet.Identity.Pages.Admin;

/// <summary>
/// The feed (service index) address of a repository, as shown to administrators to paste into a client:
/// <c>/v3/index.json</c> for the default repository, <c>/r/{name}/v3/index.json</c> for the others.
/// </summary>
internal static class RepositoryFeedUrl
{
    private const string ServiceIndexPath = "v3/index.json";

    /// <summary>
    /// Builds the address from the server's own service index URL (so a configured public URL or path base is honoured),
    /// falling back to the request.
    /// </summary>
    public static string For(HttpContext http, string repositoryName)
    {
        var root = http.RequestServices.GetService<IUrlGenerator>()?.GetServiceIndexUrl();
        root = string.IsNullOrEmpty(root)
            ? $"{http.Request.Scheme}://{http.Request.Host}{http.Request.PathBase}/{ServiceIndexPath}"
            : root;

        var prefix = root.EndsWith(ServiceIndexPath) ? root.Substring(0, root.Length - ServiceIndexPath.Length) : root.TrimEnd('/') + "/";

        return repositoryName == Repository.DefaultName
            ? prefix + ServiceIndexPath
            : $"{prefix}r/{repositoryName}/{ServiceIndexPath}";
    }
}
