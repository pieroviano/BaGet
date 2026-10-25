using System;
using System.Linq;
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
/// The logic of an access-control screen (groups, permission targets, projects, audit), independent of the programming model: the
/// controller (<c>AccessControllerBase</c>) and the obsolete page-model shim (<c>AccessPageModel</c>) of the same screen both build
/// one workflow per request and call it. The base class that runs first has already checked who may use the screen (administrator,
/// or a project administrator, see <see cref="AccessAuthorization"/>), refused a write while the read-only gate is closed
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

    /// <summary>Copies the outcome of a workflow run to the page model's own <c>Message</c> / <c>Error</c> (the shims).</summary>
    public static void CopyOutcome(AccessPageModel page, IFlashModel model)
    {
        if (model.Message != null) page.Message = model.Message;
        if (model.Error != null) page.Error = model.Error;
    }
}

/// <summary>
/// Who may open an access screen beyond the server administrators: the administrators of the project the screen acts on. One place
/// for the controllers and the page-model shims, so they cannot differ.
/// </summary>
internal static class AccessAuthorization
{
    /// <summary>The projects list: server administrators, and anyone who administers at least one project.</summary>
    public static async Task<bool> ProjectsAsync(IAccessAdministration access, bool isServerAdministrator, string? userId, CancellationToken cancellationToken)
        => isServerAdministrator || (await AdministeredAsync(access, userId, cancellationToken)).Count > 0;

    /// <summary>A project screen: server administrators, and the project's own administrators (the project is the <c>name</c> route value).</summary>
    public static async Task<bool> ProjectAsync(IAccessAdministration access, bool isServerAdministrator, string? userId, string? projectName, CancellationToken cancellationToken)
    {
        if (isServerAdministrator) return true;

        return !string.IsNullOrEmpty(projectName) && userId != null && await access.IsProjectAdminAsync(userId, projectName, cancellationToken);
    }

    /// <summary>A permission target screen: server administrators, and the administrators of the target's project for a project target.</summary>
    public static async Task<bool> TargetAsync(IAccessAdministration access, bool isServerAdministrator, string? userId, string? targetName, CancellationToken cancellationToken)
    {
        if (isServerAdministrator) return true;
        if (string.IsNullOrEmpty(targetName) || userId == null) return false;

        var target = await access.GetTargetAsync(targetName, cancellationToken);

        return target?.Target.ProjectName != null && await access.IsProjectAdminAsync(userId, target.Target.ProjectName, cancellationToken);
    }

    /// <summary>The names of the projects the user administers.</summary>
    public static async Task<System.Collections.Generic.IReadOnlyList<string>> AdministeredAsync(IAccessAdministration access, string? userId, CancellationToken cancellationToken)
        => userId == null
            ? Array.Empty<string>()
            : (await access.ProjectRolesOfUserAsync(userId, cancellationToken))
                .Where(r => r.Role == ProjectRole.Admin).Select(r => r.Project).Distinct(StringComparer.Ordinal).ToList();
}
