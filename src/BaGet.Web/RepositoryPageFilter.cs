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
/// caller must be allowed to read the repository the request resolved to (<see cref="IRepositoryAccess"/>), else the
/// page answers 404, the same answer as an unknown repository, so nothing says that a private one exists. An access
/// model plugs in by replacing <see cref="IRepositoryAccess"/>; no page repeats the check.
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

            if (!await access.CanReadAsync(context.HttpContext.User, repository, context.HttpContext.RequestAborted))
            {
                context.Result = new NotFoundResult();
                return;
            }
        }

        await next();
    }
}
