using System;
using System.Net;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace BaGet.Web;

/// <summary>
/// Selects the repository of the request: the <c>{repo}</c> route value of an <c>/r/{repo}/</c> route, else the
/// default repository (the scoped <see cref="IRepositoryContext"/> starts there). A name that matches no repository
/// is answered 404 right here; it never falls back to the default repository, which would show or modify the wrong
/// feed. A route speaks one format's protocol (<see cref="RepositoryRouteMetadata.Format"/>; Razor Pages and unmarked
/// endpoints are NuGet's); it is answered 404 when the repository has another format, the <c>default</c> repository (always NuGet)
/// included. Must run after routing (it reads the matched endpoint's route values) and before anything queries the
/// database, so authentication follows it.
/// </summary>
public class RepositoryResolutionMiddleware
{
    private readonly RequestDelegate _next;

    public RepositoryResolutionMiddleware(RequestDelegate next)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
    }

    public async Task Invoke(
        HttpContext context,
        IRepositoryContext repository,
        IRepositoryService repositories,
        IRequestCaller caller)
    {
        // Who is asking, for code in Core that filters by the caller's rights (a virtual repository reads only the members
        // the caller may read). Credentials are read when something asks, after authentication has run.
        caller.Use(async cancellationToken =>
        {
            var resolver = context.RequestServices.GetService<RequestPrincipalResolver>();

            return resolver == null ? null : await resolver.ResolveAsync(context, cancellationToken);
        });

        // The format the matched route speaks; pages and endpoints a host maps itself are NuGet's.
        var format = context.GetEndpoint()?.Metadata.GetMetadata<RepositoryRouteMetadata>()?.Format ?? RepositoryFormat.NuGet;

        if (context.GetRouteValue(Routes.RepositoryRouteKey) is string name)
        {
            // Names are lowercase (RepositoryNames); URLs are matched case-insensitively like the rest of the feed.
            var found = await repositories.FindAsync(name.ToLowerInvariant(), context.RequestAborted);

            // Only a repository of the route's format speaks this protocol; a repository of another format has its own routes.
            if (found == null || found.Format != format)
            {
                context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                context.Response.ContentType = "text/plain; charset=utf-8";
                await context.Response.WriteAsync("There is no such repository.", context.RequestAborted);
                return;
            }

            repository.Use(found);

            if (found.Kind == RepositoryKind.Virtual && await RedirectWriteAsync(context, repository, found))
            {
                return;
            }
        }

        else if (format != repository.Current.Format)
        {
            // The plain routes serve the default repository, which is NuGet: another format's routes are not there.
            context.Response.StatusCode = (int)HttpStatusCode.NotFound;
            context.Response.ContentType = "text/plain; charset=utf-8";
            await context.Response.WriteAsync("There is no such repository.", context.RequestAborted);
            return;
        }

        await _next(context);
    }

    // A write to a virtual repository (a push, an unlist, a deprecation, the status of a submission) goes to its deploy
    // repository: the request works there from now on, so the access model, ownership and the indexing all apply to that
    // repository. Returns true when the request was answered here (the virtual repository has no deploy repository).
    private async Task<bool> RedirectWriteAsync(HttpContext context, IRepositoryContext repository, RepositoryInfo virtualRepository)
    {
        var route = context.GetEndpoint()?.Metadata.GetMetadata<RepositoryRouteMetadata>();
        var isWrite = route != null
            && (route.AuthorizesItself
                || (route.WritesOnly && !(HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method))));
        if (!isWrite) return false;

        var target = await context.RequestServices.GetRequiredService<IVirtualRepositories>()
            .DeployTargetAsync(virtualRepository, context.RequestAborted);
        if (target == null)
        {
            context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
            context.Response.ContentType = "text/plain; charset=utf-8";
            await context.Response.WriteAsync("This virtual repository has no deploy repository, so it takes no uploads.", context.RequestAborted);

            return true;
        }

        repository.Use(target);
        await _next(context);

        return true;
    }
}
