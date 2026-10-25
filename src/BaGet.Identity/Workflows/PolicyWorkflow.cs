using System;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Identity.Workflows;

/// <summary>
/// Base of the retention, scanning and replication workflows: the part of <c>AccessControllerBase</c> that a handler calls (<see cref="ActAsync"/>, the audit actor, the confirmation inputs). The checks that run before a handler (administrator,
/// no access model, <c>ThrowIfClosed</c> on a POST) stay in the controller base, which runs first.
/// <para>
/// A handler returns the action result, except that a screen to render is <c>null</c>.
/// </para>
/// </summary>
internal abstract class PolicyWorkflow : AdminWorkflow
{
    protected const string NotConfirmed = "The confirmation is incorrect, so nothing was changed.";

    protected PolicyWorkflow(HttpContext httpContext, IUrlHelper url) : base(httpContext, url)
    {
    }

    /// <summary>The user name recorded in the audit log.</summary>
    protected string? Actor => User.Identity?.Name;

    /// <summary>Fills the confirmation inputs of <paramref name="model"/> and tells the caller to render the screen.</summary>
    protected async Task<IActionResult?> ShowAsync(ConfirmationModel model)
    {
        await LoadConfirmationAsync(model, Http.Request.Path + Http.Request.QueryString);

        return null;
    }

    /// <summary>A redirect that fills the confirmation inputs first is never needed; this is the plain 404.</summary>
    protected static IActionResult NotFound() => new NotFoundResult();

    /// <summary>
    /// Runs one change: asks for the password when <paramref name="confirm"/>, turns an <see cref="AccessModelException"/> into
    /// <see cref="IFlashModel.Error"/>, and redirects to <paramref name="redirect"/>.
    /// </summary>
    protected async Task<IActionResult> ActAsync(IFlashModel model, string? password, bool confirm, Func<Task<string>> change, Func<IActionResult> redirect)
    {
        if (confirm)
        {
            var current = await UserManager.GetUserAsync(User);
            if (current == null) return new ChallengeResult();

            if (!await IsConfirmedAsync(current, password))
            {
                model.Error = NotConfirmed;
                return redirect();
            }
        }

        try
        {
            model.Message = await change();
        }
        catch (AccessModelException e)
        {
            model.Error = e.Message;
        }

        return redirect();
    }
}
