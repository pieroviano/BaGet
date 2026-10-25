using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace BaGet.Identity.Mvc;

/// <summary>
/// Base of the administrator screens: the controller counterpart of <c>AdminPageModel</c>. Signed in, accounts enabled (see
/// <see cref="AccountControllerBase"/>), and an administrator. Anyone else gets 404, so the screens' existence is not disclosed
/// (an anonymous visitor is challenged to sign in first, as the pages did).
/// </summary>
public abstract class AdminControllerBase : AccountControllerBase
{
    /// <inheritdoc />
    protected override bool RequiresSignIn => true;

    /// <summary>The administrator service, resolved per request: it exists only while accounts are enabled.</summary>
    protected AdministratorService Administrators
        => HttpContext.RequestServices.GetRequiredService<AdministratorService>();

    /// <inheritdoc />
    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        OnActionExecuting(context);
        if (context.Result != null) return;

        if (!await Administrators.IsAdministratorAsync(User, HttpContext.RequestAborted))
        {
            context.Result = NotFound();
            return;
        }

        OnActionExecuted(await next());
    }
}
