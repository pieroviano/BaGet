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
    /// <summary>A read route: checked for every HTTP method, since its action may answer any of them.</summary>
    public static readonly RepositoryRouteMetadata Instance = new(writesOnly: false, authorizesItself: false, RepositoryFormat.NuGet);

    private static readonly RepositoryRouteMetadata WriteOnly = new(writesOnly: true, authorizesItself: false, RepositoryFormat.NuGet);
    private static readonly RepositoryRouteMetadata SelfAuthorized = new(writesOnly: false, authorizesItself: true, RepositoryFormat.NuGet);

    private RepositoryRouteMetadata(bool writesOnly, bool authorizesItself, RepositoryFormat format)
    {
        WritesOnly = writesOnly;
        AuthorizesItself = authorizesItself;
        Format = format;
    }

    /// <summary>
    /// The format whose protocol the route speaks (<see cref="IFormatModule"/>). <see cref="RepositoryResolutionMiddleware"/>
    /// answers 404 when the request's repository has another format, so an npm route never reaches a NuGet repository nor the
    /// reverse. Endpoints without this marker (the Razor Pages) are NuGet's.
    /// </summary>
    public RepositoryFormat Format { get; }

    /// <summary>
    /// True for a route constrained to write methods (publish, symbol upload, delete, relist, deprecation and
    /// vulnerability PUT/DELETE): its callers authenticate with an API key and are authorized by
    /// <see cref="IPackageAuthorizationService"/>, so the read check skips it for those methods (a <c>GET</c> or
    /// <c>HEAD</c> to it is still checked).
    /// </summary>
    public bool WritesOnly { get; }

    /// <summary>
    /// True for a <c>GET</c> route that exists for the pusher and authorizes it itself with an API key per repository
    /// and package: the validation status of a submission (<c>ValidationStatusController</c>, Push or Manage on the
    /// package). A pusher holding Deploy without Read (a drop box) must see what became of its push, and the route
    /// reveals nothing to anyone who may not push that package, so the read check skips it on purpose.
    /// </summary>
    public bool AuthorizesItself { get; }

    internal static RepositoryRouteMetadata For(bool writesOnly, bool authorizesItself = false, RepositoryFormat format = RepositoryFormat.NuGet)
        => format != RepositoryFormat.NuGet ? new RepositoryRouteMetadata(writesOnly, authorizesItself, format)
        : authorizesItself ? SelfAuthorized : writesOnly ? WriteOnly : Instance;
}

/// <summary>
/// The read check of every protocol route (service index, search, autocomplete, registration, content, V2, catalog,
/// symbol downloads, vulnerabilities, signatures; whatever the HTTP method, since an action not restricted to <c>GET</c>
/// also answers <c>POST</c>, <c>PUT</c> or <c>OPTIONS</c>), under <c>/</c> and <c>/r/{repo}/</c>:
/// the caller needs <see cref="AccessAction.Read"/> on the request's repository, and on the package for routes that name
/// one. An anonymous caller is answered <c>401</c> with a Basic challenge, which makes NuGet clients send credentials;
/// an authenticated one without access gets <c>404</c>, the same answer as an unknown repository or package, so nothing
/// says that a private one exists. Routes that only accept writes (<c>PUT</c>, <c>POST</c>, <c>DELETE</c>; <see cref="RepositoryRouteMetadata.WritesOnly"/>)
/// and the validation status (<see cref="RepositoryRouteMetadata.AuthorizesItself"/>) authenticate with an API key and are authorized by <see cref="IPackageAuthorizationService"/>. Runs before model binding, as a resource filter, and is
/// registered with MVC itself, so a host cannot forget it.
/// </summary>
public class RepositoryReadFilter : IAsyncResourceFilter
{
    /// <inheritdoc/>
    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        var http = context.HttpContext;
        var route = http.GetEndpoint()?.Metadata.GetMetadata<RepositoryRouteMetadata>();
        if (route == null
            || route.AuthorizesItself
            || (route.WritesOnly && !(HttpMethods.IsGet(http.Request.Method) || HttpMethods.IsHead(http.Request.Method))))
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
