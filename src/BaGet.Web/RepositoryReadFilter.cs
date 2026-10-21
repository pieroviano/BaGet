using System;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace BaGet.Web;

/// <summary>
/// Marks the endpoints <see cref="BaGetEndpointBuilder"/> registers through its single <c>MapRoute</c>: the protocol
/// routes of a repository, which <see cref="RepositoryReadFilter"/> protects. A controller a host maps itself carries no
/// marker and is left alone.
/// </summary>
public sealed class RepositoryRouteMetadata
{
    public static readonly RepositoryRouteMetadata Instance = new();

    private RepositoryRouteMetadata() { }
}

/// <summary>
/// The read check of every protocol route (service index, search, autocomplete, registration, content, V2, catalog,
/// symbol downloads, vulnerabilities, signatures; <c>GET</c> and <c>HEAD</c>), under <c>/</c> and <c>/r/{repo}/</c>:
/// the caller needs <see cref="AccessAction.Read"/> on the request's repository, and on the package for routes that name
/// one. An anonymous caller is answered <c>401</c> with a Basic challenge, which makes NuGet clients send credentials;
/// an authenticated one without access gets <c>404</c>, the same answer as an unknown repository or package, so nothing
/// says that a private one exists. Writes (<c>PUT</c>, <c>POST</c>, <c>DELETE</c>) authenticate with an API key and are
/// authorized by <see cref="IPackageAuthorizationService"/>. Runs before model binding, as a resource filter, and is
/// registered with MVC itself, so a host cannot forget it.
/// </summary>
public class RepositoryReadFilter : IAsyncResourceFilter
{
    /// <inheritdoc/>
    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        var http = context.HttpContext;
        if (http.GetEndpoint()?.Metadata.GetMetadata<RepositoryRouteMetadata>() == null
            || !(HttpMethods.IsGet(http.Request.Method) || HttpMethods.IsHead(http.Request.Method)))
        {
            await next();
            return;
        }

        var services = http.RequestServices;
        var evaluator = services.GetRequiredService<IAccessEvaluator>();
        var repository = services.GetRequiredService<IRepositoryContext>().Current;
        var packageId = context.RouteData.Values.TryGetValue("id", out var id) ? id as string : null;

        async Task<bool> MayReadAsync(BaGetPrincipal? caller)
            => await evaluator.IsAllowedAsync(caller, repository, packageId: null, AccessAction.Read, http.RequestAborted)
               && (packageId == null
                   || await evaluator.IsAllowedAsync(caller, repository, packageId, AccessAction.Read, http.RequestAborted));

        // Open to everyone is the common case (and the only one before the access model): answered without looking at
        // the credentials, so a public feed costs no API key lookup even for a client that sends them.
        if (await MayReadAsync(null))
        {
            await next();
            return;
        }

        var principal = await services.GetRequiredService<RequestPrincipalResolver>().ResolveAsync(http, http.RequestAborted);
        if (principal != null && await MayReadAsync(principal))
        {
            await next();
            return;
        }

        if (principal == null)
        {
            http.Response.Headers.WWWAuthenticate = BasicCredentials.Challenge;
            context.Result = new UnauthorizedResult();
        }
        else
        {
            context.Result = new NotFoundResult();
        }
    }
}
