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
    /// <summary>
    /// <see cref="Microsoft.AspNetCore.Http.HttpContext.Items"/> key holding the virtual repository the request asked
    /// for, when it was redirected to that repository's deploy repository (the Upload page); absent otherwise.
    /// </summary>
    public const string RequestedVirtualItemKey = "BaGet.RequestedVirtualRepository";

    // Pages that read or change what one repository owns (owners, abuse reports, per-repository statistics, package
    // management): a virtual repository has none of its own, so they answer 404 there. Its packages are managed in the
    // member that holds them.
    private static readonly string[] NotOnVirtual =
    {
        "/Statistics",
        "/ReportAbuse",
        "/Packages/Manage",
        "/Packages/NpmManage",
        "/Packages/ContactOwners",
    };

    // Pages whose every action a remote npm repository refuses (publish, tags, deprecate and unpublish are 403 there; the cache is
    // managed on the administration page): they answer 404 instead of offering what cannot be done.
    private static readonly string[] NotOnRemote =
    {
        "/Packages/NpmManage",
    };

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
            if (repository.Format == RepositoryFormat.Npm && !string.IsNullOrEmpty(packageId))
            {
                // The access patterns see the decoded lower-case name, so @acme%2fSecret cannot reach a page that
                // the pattern for @acme/secret hides.
                if (!NpmNames.TryParse(packageId, out var npmName, out _))
                {
                    context.Result = new NotFoundResult();
                    return;
                }

                packageId = npmName;
            }

            // The upload pages show no package data of the repository, so they need the right to publish
            // (IRepositoryAccess.CanWriteAsync), not Read: a Deploy-only drop box is usable.
            var isUpload = string.Equals(page.ViewEnginePath, "/Upload", StringComparison.OrdinalIgnoreCase)
                || string.Equals(page.ViewEnginePath, "/NpmUpload", StringComparison.OrdinalIgnoreCase);
            var allowed = isUpload
                ? await access.CanWriteAsync(context.HttpContext.User, repository, context.HttpContext.RequestAborted)
                : string.IsNullOrEmpty(packageId)
                ? await access.CanReadAsync(context.HttpContext.User, repository, context.HttpContext.RequestAborted)
                : await access.CanReadPackageAsync(context.HttpContext.User, repository, packageId, context.HttpContext.RequestAborted);

            if (allowed && repository.Format != RepositoryPageRouteConvention.FormatOf(page.ViewEnginePath))
            {
                // The page belongs to another format. The front page of a repository is where its address (/r/name/) and the
                // header lead, so it sends the visitor to the format's own browse page; anything else is not there.
                var browse = string.Equals(page.ViewEnginePath, "/Index", StringComparison.OrdinalIgnoreCase)
                    ? services.GetRequiredService<FormatRegistry>().Find(repository.Format)?.RepositoryLinks(repository)
                        .FirstOrDefault(l => l.Id == "browse" && l.Page != null)
                    : null;
                context.Result = browse == null
                    ? new NotFoundResult()
                    : new RedirectToPageResult(browse.Page, new { repo = repository.IsDefault ? null : repository.Name });
                return;
            }

            if (allowed && repository.Kind == RepositoryKind.Remote && NotOnRemote.Contains(page.ViewEnginePath, StringComparer.OrdinalIgnoreCase))
            {
                context.Result = new NotFoundResult();
                return;
            }

            if (allowed && repository.Kind == RepositoryKind.Virtual)
            {
                if (NotOnVirtual.Contains(page.ViewEnginePath, StringComparer.OrdinalIgnoreCase))
                {
                    context.Result = new NotFoundResult();
                    return;
                }

                // An upload to a virtual repository is an upload to the repository it deploys to: from here on the request
                // works in that one (staging, ownership, publishing), exactly as the push endpoint does.
                if (isUpload)
                {
                    var target = await services.GetRequiredService<IVirtualRepositories>()
                        .DeployTargetAsync(repository, context.HttpContext.RequestAborted);
                    if (target == null)
                    {
                        context.Result = new NotFoundResult();
                        return;
                    }

                    context.HttpContext.Items[RequestedVirtualItemKey] = repository;
                    services.GetRequiredService<IRepositoryContext>().Use(target);
                }
            }

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
