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
/// package (<c>{id}</c>) is checked against that package too (<see cref="IRepositoryAccess.CanReadPackageAsync"/>). The check
/// itself is <see cref="RepositoryPageGate"/>, shared with <see cref="RepositoryActionFilter"/> (the controller actions that
/// replace pages), so the two programming models answer alike.
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
        "/Packages/MavenManage",
        "/Packages/DockerManage",
        "/Packages/ContactOwners",
        "/Packages/ValidationStatus",
        "/Packages/Scan",
    };

    // Pages whose every action a remote npm repository refuses (publish, tags, deprecate and unpublish are 403 there; the cache is
    // managed on the administration page): they answer 404 instead of offering what cannot be done.
    private static readonly string[] NotOnRemote =
    {
        "/Packages/NpmManage",
        "/Packages/MavenManage",
    };

    // The upload pages need the right to publish, not Read, and continue in a virtual repository's deploy repository.
    private static readonly string[] UploadPages = { "/Upload", "/NpmUpload", "/MavenUpload" };

    /// <inheritdoc/>
    public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context) => Task.CompletedTask;

    /// <inheritdoc/>
    public async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        if (context.ActionDescriptor is PageActionDescriptor page
            && RepositoryPageRouteConvention.RepositoryPages.Contains(page.ViewEnginePath, StringComparer.OrdinalIgnoreCase))
        {
            var path = page.ViewEnginePath;
            var rule = new RepositoryPageRule(
                Format: RepositoryPageRouteConvention.FormatOf(path),
                Upload: UploadPages.Contains(path, StringComparer.OrdinalIgnoreCase),
                NotOnVirtual: NotOnVirtual.Contains(path, StringComparer.OrdinalIgnoreCase),
                NotOnRemote: NotOnRemote.Contains(path, StringComparer.OrdinalIgnoreCase),
                FrontPage: string.Equals(path, "/Index", StringComparison.OrdinalIgnoreCase));

            var result = await RepositoryPageGate.CheckAsync(context.HttpContext, context.RouteData, rule);
            if (result != null)
            {
                context.Result = result;
                return;
            }
        }

        await next();
    }
}
