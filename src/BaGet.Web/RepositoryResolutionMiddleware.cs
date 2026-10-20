using System;
using System.Net;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace BaGet.Web;

/// <summary>
/// Selects the repository of the request: the <c>{repo}</c> route value of an <c>/r/{repo}/</c> route, else the
/// default repository (the scoped <see cref="IRepositoryContext"/> starts there). A name that matches no repository
/// is answered 404 right here; it never falls back to the default repository, which would show or modify the wrong
/// feed. Must run after routing (it reads the matched endpoint's route values) and before anything queries the
/// database, so authentication follows it.
/// </summary>
public class RepositoryResolutionMiddleware
{
    private readonly RequestDelegate _next;

    public RepositoryResolutionMiddleware(RequestDelegate next)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
    }

    public async Task Invoke(HttpContext context, IRepositoryContext repository, IRepositoryService repositories)
    {
        if (context.GetRouteValue(Routes.RepositoryRouteKey) is string name)
        {
            // Names are lowercase (RepositoryNames); URLs are matched case-insensitively like the rest of the feed.
            var found = await repositories.FindAsync(name.ToLowerInvariant(), context.RequestAborted);

            // Only NuGet repositories speak this protocol; a repository of another format has its own routes.
            if (found == null || found.Format != RepositoryFormat.NuGet)
            {
                context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                context.Response.ContentType = "text/plain; charset=utf-8";
                await context.Response.WriteAsync("There is no such repository.", context.RequestAborted);
                return;
            }

            repository.Use(found);
        }

        await _next(context);
    }
}
