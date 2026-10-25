using System.Threading.Tasks;
using BaGet.Identity.ViewModels;
using Microsoft.AspNetCore.Mvc;
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

    /// <summary>
    /// Carries the outcome of an action across a redirect, as <c>[TempData] Message</c> / <c>[TempData] Error</c> did on the page
    /// models: a value the action just set is stored for the next request; otherwise the model shows (and consumes) what the previous
    /// request stored. Call it once per action, after the workflow ran and before returning.
    /// </summary>
    [NonAction]
    protected void Flash(IFlashMessage model)
    {
        if (model.Message != null) TempData["Message"] = model.Message;
        else model.Message = TempData["Message"] as string;
    }

    /// <summary><see cref="Flash(IFlashMessage)"/> for a screen that also flashes an error.</summary>
    [NonAction]
    protected void Flash(IFlashModel model)
    {
        Flash((IFlashMessage)model);

        if (model.Error != null) TempData["Error"] = model.Error;
        else model.Error = TempData["Error"] as string;
    }
}
