using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.Application.Models;
using BaGet.Identity.Application.Screens;
using Microsoft.AspNetCore.Identity;

namespace BaGet.Identity.Application.UseCases;

/// <summary>
/// Who is using an access-control screen: the signed-in account (or the key owner's) and whether it is a server administrator rather than only a
/// project administrator. The screen gate decided that before the use case runs; the use case only needs it for the rules inside a screen.
/// </summary>
/// <param name="Principal">The signed-in user (for an API key, the key's owner).</param>
/// <param name="IsServerAdministrator">Whether the caller is a server administrator.</param>
public sealed record AccessCaller(ClaimsPrincipal Principal, bool IsServerAdministrator);

/// <summary>
/// What the use cases of the access-control screens (groups, permission targets, projects, audit) share: the caller, the password confirmation of a
/// change, the audit actor and the conversion of an <see cref="AccessModelException"/> into the message shown on the screen. The gate that runs first
/// has already decided who may use the screen (an administrator, or a project's administrator, see <see cref="AccessAuthorization"/>); everything else
/// (password, the 404 and administrator-only rules of each action, the audit through <see cref="IAccessAdministration"/>) is in the use case.
/// </summary>
public abstract class AccessUseCaseBase : AdminUseCaseBase
{
    /// <summary>Creates the base.</summary>
    protected AccessUseCaseBase(
        UserManager<BaGetUser> users,
        PasswordConfirmation confirmation,
        IAccessAdministration access,
        IRepositoryService repositories,
        MaintenanceGate? gate)
        : base(users, confirmation, gate)
    {
        Access = access ?? throw new ArgumentNullException(nameof(access));
        Repositories = repositories ?? throw new ArgumentNullException(nameof(repositories));
    }

    /// <summary>The access model's administration.</summary>
    protected IAccessAdministration Access { get; }

    /// <summary>The repositories.</summary>
    protected IRepositoryService Repositories { get; }

    /// <summary>The user name recorded in the audit log.</summary>
    protected static string? Actor(AccessCaller caller) => caller.Principal.Identity?.Name;

    /// <summary>The caller's user id, or null (the global API key has no account).</summary>
    protected string? UserIdOf(AccessCaller caller) => Users.GetUserId(caller.Principal);

    /// <summary>Fills what the confirmation partial needs; the page to come back to after signing in again is the current request's URL.</summary>
    protected Task LoadConfirmationAsync(ConfirmationModel model, AccessCaller caller)
        => Confirmation.LoadForRequestAsync(model, caller.Principal);

    /// <summary>
    /// Runs one change: refuses it while the server is read-only, asks for the password when <paramref name="confirm"/>, turns an
    /// <see cref="AccessModelException"/> into the error shown, then answers with <paramref name="back"/> (given the message and the error).
    /// </summary>
    /// <param name="caller">Who changes.</param>
    /// <param name="password">What the user typed.</param>
    /// <param name="confirm">Whether the change asks for the password (grants, memberships, deletions).</param>
    /// <param name="change">The change; returns the message to show.</param>
    /// <param name="back">Where to go afterwards.</param>
    protected async Task<ScreenResult<T>> ActAsync<T>(
        AccessCaller caller,
        string? password,
        bool confirm,
        Func<Task<string>> change,
        Func<string?, string?, ScreenResult<T>> back)
    {
        ThrowIfClosed();

        if (confirm)
        {
            var current = await CurrentAsync(caller.Principal);
            if (current == null) return ScreenResult<T>.SignIn();

            if (!await Confirmation.IsConfirmedAsync(current, password)) return back(null, NotConfirmed);
        }

        string message;
        try
        {
            message = await change();
        }
        catch (AccessModelException e)
        {
            return back(null, e.Message);
        }

        return back(message, null);
    }

    /// <summary>What a set of patterns selects, for the pattern editors.</summary>
    protected Task<PatternPreview> PreviewAsync(string[] repositoryPatterns, string[] includePatterns, string[] excludePatterns, string? testPackage, CancellationToken cancellationToken)
        => AccessPatternSupport.PreviewAsync(Repositories, repositoryPatterns, includePatterns, excludePatterns, testPackage, cancellationToken);
}

/// <summary>
/// Who may open an access screen beyond the server administrators: the administrators of the project the screen acts on. One place for the view
/// controllers' and the JSON twins' gates.
/// </summary>
public static class AccessAuthorization
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
    public static async Task<IReadOnlyList<string>> AdministeredAsync(IAccessAdministration access, string? userId, CancellationToken cancellationToken)
        => userId == null
            ? Array.Empty<string>()
            : (await access.ProjectRolesOfUserAsync(userId, cancellationToken))
                .Where(r => r.Role == ProjectRole.Admin).Select(r => r.Project).Distinct(StringComparer.Ordinal).ToList();
}

/// <summary>The parts of the access screens that read and write pattern and action text; shared by the use cases and the controller bases.</summary>
public static class AccessPatternSupport
{
    /// <summary>The single actions, in display order.</summary>
    public static readonly AccessAction[] AllActions =
    {
        AccessAction.Read, AccessAction.Annotate, AccessAction.Deploy, AccessAction.Delete, AccessAction.Manage,
    };

    /// <summary>Parses the ticked action boxes (names of <see cref="AccessAction"/> values).</summary>
    public static AccessAction ParseActions(IEnumerable<string>? names)
    {
        var actions = AccessAction.None;
        foreach (var name in names ?? Array.Empty<string>())
        {
            if (Enum.TryParse<AccessAction>(name, ignoreCase: true, out var action) && action != AccessAction.All) actions |= action;
        }

        return actions;
    }

    /// <summary>The actions in <paramref name="actions"/> as words, or "none".</summary>
    public static string Words(AccessAction actions)
        => actions == AccessAction.None ? "none" : string.Join(", ", AllActions.Where(a => (actions & a) == a));

    /// <summary>Patterns one per line, the way the editors show them.</summary>
    public static string Lines(IEnumerable<string> patterns) => string.Join("\n", patterns);

    /// <summary>The package filter and repository patterns as one line a person can read back.</summary>
    public static string Join(IEnumerable<string> patterns) => string.Join(", ", patterns);

    /// <summary>What a set of patterns selects: the matching repository names and, for a typed package ID, whether it applies.</summary>
    public static async Task<PatternPreview> PreviewAsync(
        IRepositoryService repositories,
        string[] repositoryPatterns,
        string[] includePatterns,
        string[] excludePatterns,
        string? testPackage,
        CancellationToken cancellationToken)
    {
        var all = await repositories.ListAsync(cancellationToken);
        var names = all
            .Where(r => AccessPatterns.IsMatch(repositoryPatterns, r.Name))
            .Select(r => r.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();
        var package = testPackage?.Trim();

        return new PatternPreview(
            names,
            string.IsNullOrEmpty(package) ? null : package,
            string.IsNullOrEmpty(package) ? null : AccessPatterns.Test(includePatterns, excludePatterns, package));
    }
}
