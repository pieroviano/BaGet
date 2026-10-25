using System.Linq;
using System.Threading.Tasks;
using BaGet.Identity.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace BaGet.Web;

/// <summary>
/// The repository check of the controller actions that replace Razor Pages: the counterpart of <see cref="RepositoryPageFilter"/>
/// for an action marked <see cref="RepositoryPageAttribute"/> (on the action or its controller). It runs the same
/// <see cref="RepositoryPageGate"/> with the rule the attribute states (format, upload, not on virtual / remote, front page), so a
/// page answers the same whether it is a page or an action. Registered with MVC itself, so a host cannot forget it, and inert for
/// every action without the attribute.
/// <para>
/// Order: global filters run after the controller's own <c>OnActionExecuting</c>/<c>OnActionExecutionAsync</c> override (the
/// controller is a filter of order <c>int.MinValue</c>), exactly as the global page filter ran after the page model's
/// <c>OnPageHandlerExecuting</c>; a controller check that must precede the repository check (a model check) therefore does.
/// </para>
/// </summary>
public class RepositoryActionFilter : IAsyncActionFilter
{
    /// <inheritdoc/>
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var attribute = context.ActionDescriptor.EndpointMetadata.OfType<RepositoryPageAttribute>().LastOrDefault();
        if (attribute != null)
        {
            var rule = new RepositoryPageRule(
                Format: attribute.AnyFormat ? null : attribute.Format,
                Upload: attribute.Upload,
                NotOnVirtual: attribute.NotOnVirtual,
                NotOnRemote: attribute.NotOnRemote,
                FrontPage: attribute.FrontPage);

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
