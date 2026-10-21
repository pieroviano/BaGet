using System;
using System.Linq;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace BaGet.Web;

/// <summary>
/// The single check every repository page passes (<see cref="RepositoryPageRouteConvention.RepositoryPages"/>): the
/// caller must be allowed to read the repository (the upload page: to publish to it) the request resolved to (<see cref="IRepositoryAccess"/>), else the
/// page answers 404 to a signed-in user (the same answer as an unknown repository, so nothing says that a private one
/// exists) and sends a visitor who is not signed in to sign in, when the host has accounts. <see cref="IRepositoryAccess"/>
/// is backed by the access model (<see cref="IAccessEvaluator"/>); no page repeats the check. A page whose route names a
/// package (<c>{id}</c>) is checked against that package too (<see cref="IRepositoryAccess.CanReadPackageAsync"/>).
/// </summary>
public class RepositoryPageFilter : IAsyncPageFilter
{
    /// <inheritdoc/>
    public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context) => Task.CompletedTask;

    /// <inheritdoc/>
    public async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        if (context.ActionDescriptor is PageActionDescriptor page
            && RepositoryPageRouteConvention.RepositoryPages.Contains(page.ViewEnginePath, StringComparer.OrdinalIgnoreCase))
        {
            var services = context.HttpContext.RequestServices;
            var repository = services.GetRequiredService<IRepositoryContext>().Current;
            var access = services.GetRequiredService<IRepositoryAccess>();

            // A page of one package ({id} in its route) also needs Read on that package, so the include/exclude
            // patterns of the caller's grants hide it as they hide its registration and content.
            var packageId = context.RouteData.Values.TryGetValue("id", out var id) ? id as string : null;
            // The upload page shows no package data of the repository, so it needs the right to publish
            // (IRepositoryAccess.CanWriteAsync), not Read: a Deploy-only drop box is usable.
            var allowed = string.Equals(page.ViewEnginePath, "/Upload", StringComparison.OrdinalIgnoreCase)
                ? await access.CanWriteAsync(context.HttpContext.User, repository, context.HttpContext.RequestAborted)
                : string.IsNullOrEmpty(packageId)
                ? await access.CanReadAsync(context.HttpContext.User, repository, context.HttpContext.RequestAborted)
                : await access.CanReadPackageAsync(context.HttpContext.User, repository, packageId, context.HttpContext.RequestAborted);

            if (!allowed)
            {
                // A visitor who is not signed in is sent to sign in (when the host has accounts) - they may be allowed
                // once they are; a signed-in user without Read gets 404, so nothing says the repository exists.
                var schemes = services.GetRequiredService<Microsoft.AspNetCore.Authentication.IAuthenticationSchemeProvider>();
                context.Result = context.HttpContext.User.Identity?.IsAuthenticated != true
                    && await schemes.GetDefaultChallengeSchemeAsync() != null
                    ? new ChallengeResult()
                    : new NotFoundResult();
                return;
            }
        }

        await next();
    }
}
