using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.Pages.Account;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace BaGet.Identity.Pages.Admin;

/// <summary>The result of previewing repository and package patterns.</summary>
/// <param name="Repositories">The NuGet repositories whose names match the repository patterns.</param>
/// <param name="TestPackage">The package ID typed in the test box, if any.</param>
/// <param name="Test">Whether the target would apply to <paramref name="TestPackage"/>, and why.</param>
public sealed record PatternPreview(IReadOnlyList<string> Repositories, string? TestPackage, PatternTest? Test);

/// <summary>
/// Base of the access-control pages (groups, permission targets, projects, audit): signed in, accounts enabled, an
/// administrator, unless a page widens it to a project's administrators (<see cref="AuthorizeAsync"/>); anyone else
/// gets 404. Needs a relational database: where there is no access model (Azure Table) every page sends to
/// <c>/admin/access-unavailable</c>, which says so. Every POST is refused with 503 while the
/// <see cref="MaintenanceGate"/> is closed, and changes go through <see cref="IAccessAdministration"/>, which audits
/// them and invalidates the evaluator's cache.
/// </summary>
public abstract class AccessPageModel : AdminPageModel
{
    /// <summary>Outcome of the last action.</summary>
    [TempData]
    public string? Message { get; set; }

    /// <summary>Why the last action was refused.</summary>
    [TempData]
    public string? Error { get; set; }

    /// <summary>Whether the signed-in user is a server administrator (rather than only a project administrator).</summary>
    public bool IsServerAdministrator { get; private set; }

    /// <summary>The signed-in user's id.</summary>
    protected string? CurrentUserId { get; private set; }

    /// <summary>Whether the signed in user has a password to confirm with.</summary>
    public bool HasPassword { get; private set; } = true;

    /// <summary>For an account without a password: it signed in again with its provider a moment ago.</summary>
    public bool Reauthenticated { get; private set; }

    /// <summary>Whether an external provider is available to sign in again with.</summary>
    public bool CanReauthenticate => Accounts.OpenIdConnect.IsConfigured;

    /// <summary>The access model's administration, resolved per request.</summary>
    protected IAccessAdministration AccessAdmin => HttpContext.RequestServices.GetRequiredService<IAccessAdministration>();

    /// <summary>The access evaluator, resolved per request.</summary>
    protected IAccessEvaluator Evaluator => HttpContext.RequestServices.GetRequiredService<IAccessEvaluator>();

    /// <summary>The user name recorded in the audit log.</summary>
    protected string? Actor => User.Identity?.Name;

    /// <summary>The model of the password confirmation partial, with <paramref name="id"/> for the input.</summary>
    public ConfirmPasswordView Confirmation(string id)
        => new ConfirmPasswordView(id, HasPassword, Reauthenticated, CanReauthenticate, Request.Path + Request.QueryString);

    /// <summary>
    /// Whether the signed-in user may use this page. The default is server administrators; project pages also admit
    /// the administrators of the project they act on.
    /// </summary>
    protected virtual async Task<bool> AuthorizeAsync(CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        return IsServerAdministrator;
    }

    /// <inheritdoc />
    public override async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        // Not AdminPageModel's own check: a project administrator is admitted by AuthorizeAsync.
        OnPageHandlerExecuting(context);
        if (context.Result != null) return;

        var cancellationToken = HttpContext.RequestAborted;
        IsServerAdministrator = await Administrators.IsAdministratorAsync(User, cancellationToken);
        CurrentUserId = UserManager.GetUserId(User);

        if (HttpContext.RequestServices.GetRequiredService<IContext>() is NullContext)
        {
            context.Result = IsServerAdministrator ? RedirectToPage("/Admin/AccessUnavailable") : NotFound();
            return;
        }

        if (!await AuthorizeAsync(cancellationToken))
        {
            context.Result = NotFound();
            return;
        }

        if (HttpMethods.IsPost(Request.Method))
        {
            HttpContext.RequestServices.GetService<MaintenanceGate>()?.ThrowIfClosed();
        }

        var current = await UserManager.GetUserAsync(User);
        HasPassword = current == null || await UserManager.HasPasswordAsync(current);
        Reauthenticated = !HasPassword && await HasReauthenticatedRecentlyAsync();

        OnPageHandlerExecuted(await next());
    }

    /// <summary>
    /// Runs one change: asks for the password when <paramref name="confirm"/>, turns an
    /// <see cref="AccessModelException"/> into <see cref="Error"/>, and redirects to <paramref name="redirect"/>.
    /// </summary>
    /// <param name="password">What the user typed.</param>
    /// <param name="confirm">Whether the change asks for the password (grants, memberships, deletions).</param>
    /// <param name="change">The change; returns the message to show.</param>
    /// <param name="redirect">Where to go afterwards.</param>
    protected async Task<IActionResult> ActAsync(string? password, bool confirm, Func<Task<string>> change, Func<IActionResult> redirect)
    {
        if (confirm)
        {
            var current = await UserManager.GetUserAsync(User);
            if (current == null) return Challenge();

            if (!await IsConfirmedAsync(current, password))
            {
                Error = "The confirmation is incorrect, so nothing was changed.";
                return redirect();
            }
        }

        try
        {
            Message = await change();
        }
        catch (AccessModelException e)
        {
            Error = e.Message;
        }

        return redirect();
    }

    /// <summary>
    /// What a set of patterns selects, for the pattern editors: the repositories whose names match the repository
    /// patterns, and, when a package ID was typed in the test box, whether the target would apply to it.
    /// </summary>
    protected async Task<PatternPreview> PreviewAsync(
        string[] repositoryPatterns,
        string[] includePatterns,
        string[] excludePatterns,
        string? testPackage,
        CancellationToken cancellationToken)
    {
        var all = await HttpContext.RequestServices.GetRequiredService<IRepositoryService>().ListAsync(cancellationToken);
        var names = all
            .Where(r => r.Format == RepositoryFormat.NuGet && AccessPatterns.IsMatch(repositoryPatterns, r.Name))
            .Select(r => r.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();
        var package = testPackage?.Trim();

        return new PatternPreview(
            names,
            string.IsNullOrEmpty(package) ? null : package,
            string.IsNullOrEmpty(package) ? null : AccessPatterns.Test(includePatterns, excludePatterns, package));
    }

    /// <summary>Parses the ticked action boxes (names of <see cref="AccessAction"/> values).</summary>
    protected static AccessAction ParseActions(IEnumerable<string>? names)
    {
        var actions = AccessAction.None;
        foreach (var name in names ?? Array.Empty<string>())
        {
            if (Enum.TryParse<AccessAction>(name, ignoreCase: true, out var action) && action != AccessAction.All) actions |= action;
        }

        return actions;
    }

    /// <summary>The single actions, in display order.</summary>
    public static readonly AccessAction[] AllActions =
    {
        AccessAction.Read, AccessAction.Annotate, AccessAction.Deploy, AccessAction.Delete, AccessAction.Manage,
    };

    /// <summary>The actions in <paramref name="actions"/> as words, or "none".</summary>
    public static string Words(AccessAction actions)
        => actions == AccessAction.None ? "none" : string.Join(", ", AllActions.Where(a => (actions & a) == a));

    /// <summary>Patterns one per line, the way the editors show them.</summary>
    public static string Lines(IEnumerable<string> patterns) => string.Join("\n", patterns);

    /// <summary>The package filter and repository patterns as one line a person can read back.</summary>
    public static string Join(IEnumerable<string> patterns) => string.Join(", ", patterns);

}
