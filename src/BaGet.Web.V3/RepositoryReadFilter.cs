using System;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Web.V3.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace BaGet.Web.V3;

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

    private RepositoryRouteMetadata(bool writesOnly, bool authorizesItself, RepositoryFormat format, bool writesContent = true, bool resolvesRepositoryItself = false)
    {
        ResolvesRepositoryItself = resolvesRepositoryItself;
        WritesContent = writesContent;
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

    /// <summary>
    /// False for a route that does not change the repository's content though it is a write method: the credential calls
    /// (npm's login and logout). <see cref="RepositoryResolutionMiddleware"/> leaves such a request on the repository it
    /// addressed instead of moving it to a virtual repository's deploy repository, so a token issued by a login on a virtual
    /// repository is a token of that virtual repository.
    /// </summary>
    public bool WritesContent { get; }

    /// <summary>
    /// True for a route that finds its repository itself and answers an unknown or forbidden one in its own protocol (the OCI
    /// registry: <c>/v2/{repo}/…</c> must answer <c>401</c> with a token challenge, not a plain <c>404</c>, so that a client
    /// cannot tell a private repository from one that does not exist). <see cref="RepositoryResolutionMiddleware"/> then leaves
    /// the repository alone; the route's own filter calls <see cref="BaGet.Core.IRepositoryContext.Use"/> once it has authorized.
    /// Such a route also <see cref="AuthorizesItself"/>.
    /// </summary>
    public bool ResolvesRepositoryItself { get; }

    internal static RepositoryRouteMetadata For(bool writesOnly, bool authorizesItself = false, RepositoryFormat format = RepositoryFormat.NuGet, bool writesContent = true, bool resolvesRepositoryItself = false)
        => format != RepositoryFormat.NuGet || !writesContent || resolvesRepositoryItself ? new RepositoryRouteMetadata(writesOnly, authorizesItself || resolvesRepositoryItself, format, writesContent, resolvesRepositoryItself)
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
        Func<Task<string?>>? resolveName = null;
        if (route.Format == RepositoryFormat.Npm)
        {
            // npm routes name the package in "name" (possibly @scope%2fname) or "scope" and "pkg"; the access patterns see the
            // decoded name, so a route cannot reach a package that its pattern would hide by writing the name differently.
            var raw = NpmRouteNames.FromRouteValues(context.RouteData.Values);
            if (raw != null)
            {
                if (!NpmNames.TryParseLookup(raw, out var normalizedName, out _))
                {
                    context.Result = new NotFoundResult();
                    return;
                }

                packageId = normalizedName;
            }
        }

        if (route.Format == RepositoryFormat.Maven)
        {
            // A Maven route names the artifact through its path: the access patterns see group:artifact, which the repository
            // works out from what is stored (metadata can name an artifact or one of its versions).
            var requested = MavenNames.ParsePath(context.RouteData.Values.TryGetValue("path", out var raw) ? raw as string : null);
            if (requested == null)
            {
                context.Result = new NotFoundResult();
                return;
            }

            // Looked up only once the repository itself may be read: a remote repository asks its upstream to tell a version's metadata
            // from an artifact's, which nobody who may not read the repository should be able to make it do.
            var maven = services.GetRequiredService<IMavenRepository>();
            resolveName = async () => (await maven.ResolveAsync(requested, http.RequestAborted))?.Name;
        }

        async Task<bool> MayReadAsync(BaGetPrincipal? caller)
        {
            if (!await evaluator.IsAllowedAsync(caller, repository, packageId: null, AccessAction.Read, http.RequestAborted)) return false;

            if (resolveName != null)
            {
                packageId = await resolveName();
                resolveName = null;
            }

            return packageId == null
                || await evaluator.IsAllowedAsync(caller, repository, packageId, AccessAction.Read, http.RequestAborted);
        }

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
            // npm answers a 401 with E401 and never retries; its clients send Bearer, NuGet's send Basic.
            http.Response.Headers.WWWAuthenticate = route.Format == RepositoryFormat.Npm
                ? new Microsoft.Extensions.Primitives.StringValues(new[] { BasicCredentials.BearerChallenge, BasicCredentials.Challenge })
                : BasicCredentials.Challenge;
            context.Result = new UnauthorizedResult();
        }
        else
        {
            context.Result = new NotFoundResult();
        }
    }
}
