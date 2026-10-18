using System.Threading.Tasks;
using BaGet.Identity.Pages.Account;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace BaGet.Identity.Pages.Admin;

/// <summary>
/// Base of the administrator pages: signed in, accounts enabled (see <see cref="AccountPageModel"/>),
/// and an administrator. Anyone else gets 404, so the pages' existence is not disclosed.
/// </summary>
public abstract class AdminPageModel : AccountPageModel
{
    /// <inheritdoc />
    protected override bool RequiresSignIn => true;

    /// <summary>The administrator service, resolved per request: it exists only while accounts are enabled.</summary>
    protected AdministratorService Administrators
        => HttpContext.RequestServices.GetRequiredService<AdministratorService>();

    /// <inheritdoc />
    public override async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        OnPageHandlerExecuting(context);
        if (context.Result != null) return;

        if (!await Administrators.IsAdministratorAsync(User, HttpContext.RequestAborted))
        {
            context.Result = NotFound();
            return;
        }

        OnPageHandlerExecuted(await next());
    }
}
