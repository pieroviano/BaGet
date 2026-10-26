using System;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.Mvc;
using BaGet.Identity.Pages.Admin;
using BaGet.Identity.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace BaGet.Identity.Workflows;

/// <summary>
/// The logic of an access-control screen (groups, permission targets, projects, audit), the controller (<c>AccessControllerBase</c>) builds one workflow per request and calls it. The base class that runs first has already checked who may use the screen (administrator,
/// or a project administrator, see <see cref="BaGet.Identity.Application.AccessAuthorization"/>), refused a write while the read-only gate is closed
/// (<c>ThrowIfClosed</c>) and told the workflow who the caller is (<see cref="IsServerAdministrator"/>, <see cref="CurrentUserId"/>);
/// everything else (password confirmation, the 404 and administrator-only rules of each action, audit through
/// <see cref="IAccessAdministration"/>) is here. A handler returns the result the page handler returned, except that <c>Page()</c>
/// is <c>null</c> ("render the screen").
/// </summary>
internal abstract class AccessWorkflow : AdminWorkflow
{
    protected AccessWorkflow(HttpContext httpContext, IUrlHelper url) : base(httpContext, url)
    {
    }

    /// <summary>Whether the signed-in user is a server administrator (rather than only a project administrator).</summary>
    public bool IsServerAdministrator { get; set; }

    /// <summary>The signed-in user's id.</summary>
    public string? CurrentUserId { get; set; }

    /// <summary>The access model's administration.</summary>
    protected IAccessAdministration AccessAdmin => Services.GetRequiredService<IAccessAdministration>();

    /// <summary>The user name recorded in the audit log.</summary>
    protected string? Actor => User.Identity?.Name;

    /// <summary>Fills what the confirmation partial needs; the page to come back to is the current URL.</summary>
    protected Task LoadConfirmationAsync(ConfirmationModel model)
        => LoadConfirmationAsync(model, Http.Request.Path + Http.Request.QueryString);

    /// <summary>
    /// Runs one change: asks for the password when <paramref name="confirm"/>, turns an <see cref="AccessModelException"/> into the
    /// model's <see cref="IFlashMessage"/> error, and redirects to <paramref name="redirect"/>.
    /// </summary>
    protected async Task<IActionResult> ActAsync(IFlashModel model, string? password, bool confirm, Func<Task<string>> change, Func<IActionResult> redirect)
    {
        if (confirm)
        {
            var current = await UserManager.GetUserAsync(User);
            if (current == null) return new ChallengeResult();

            if (!await IsConfirmedAsync(current, password))
            {
                model.Error = "The confirmation is incorrect, so nothing was changed.";
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

    /// <summary>What a set of patterns selects, for the pattern editors.</summary>
    protected Task<PatternPreview> PreviewAsync(string[] repositoryPatterns, string[] includePatterns, string[] excludePatterns, string? testPackage, CancellationToken cancellationToken)
        => AccessSupport.PreviewAsync(Services, repositoryPatterns, includePatterns, excludePatterns, testPackage, cancellationToken);
}
