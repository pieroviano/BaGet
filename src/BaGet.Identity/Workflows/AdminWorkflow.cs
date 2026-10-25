using System;
using System.Security.Claims;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.Mvc;
using BaGet.Identity.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BaGet.Identity.Workflows;

/// <summary>
/// The logic of an administrator screen, independent of the programming model: the controller (<c>AdminControllerBase</c>) and the
/// obsolete page-model shim (<c>AdminPageModel</c>) of the same screen both build one workflow per request and call it, so the rules
/// cannot diverge. It is created by hand with the request's <see cref="HttpContext"/> and URL helper (a page model's public
/// constructor cannot take services); its services are resolved per request, as the base classes do, because they exist only while
/// accounts are enabled.
/// <para>
/// A handler returns the result the page model's handler returned, except that <c>Page()</c> is <c>null</c> ("render the screen"):
/// the caller turns null into <c>View(...)</c> or <c>Page()</c>. Every gate (<c>ThrowIfClosed</c>, password confirmation, 404 rules)
/// is in the workflow, so a caller cannot skip it. The administrator check itself stays in the base class that runs first.
/// </para>
/// </summary>
internal abstract class AdminWorkflow
{
    /// <summary>Creates the workflow for one request.</summary>
    protected AdminWorkflow(HttpContext httpContext, IUrlHelper url)
    {
        Http = httpContext ?? throw new ArgumentNullException(nameof(httpContext));
        Url = url ?? throw new ArgumentNullException(nameof(url));
    }

    /// <summary>The request.</summary>
    protected HttpContext Http { get; }

    /// <summary>The URL helper of the screen (resolves legacy page names to actions, keeps the repository).</summary>
    protected IUrlHelper Url { get; }

    /// <summary>The request's services.</summary>
    protected IServiceProvider Services => Http.RequestServices;

    /// <summary>The signed in user.</summary>
    protected ClaimsPrincipal User => Http.User;

    /// <summary>The accounts options.</summary>
    protected AccountsOptions Accounts => Services.GetRequiredService<IOptions<AccountsOptions>>().Value;

    /// <summary>Users, passwords, tokens.</summary>
    protected UserManager<BaGetUser> UserManager => Services.GetRequiredService<UserManager<BaGetUser>>();

    /// <summary>Package and organization ownership.</summary>
    protected PackageOwnershipService Ownership => Services.GetRequiredService<PackageOwnershipService>();

    /// <summary>API keys.</summary>
    protected ApiKeyService ApiKeys => Services.GetRequiredService<ApiKeyService>();

    /// <summary>The administrator service.</summary>
    protected AdministratorService Administrators => Services.GetRequiredService<AdministratorService>();

    /// <summary>Whether outgoing mail is configured.</summary>
    protected bool EmailConfigured => Services.GetRequiredService<IOptions<EmailOptions>>().Value.IsConfigured;

    /// <summary>Refuses (503) a write while the read-only gate is closed.</summary>
    protected void ThrowIfClosed() => Services.GetService<MaintenanceGate>()?.ThrowIfClosed();

    /// <summary>The check behind every sensitive action: the password, or a recent provider sign-in.</summary>
    protected Task<bool> IsConfirmedAsync(BaGetUser? user, string? password)
        => AccountSupport.IsConfirmedAsync(UserManager, Http, user, password);

    /// <summary>An absolute link to an account page for an email (<see cref="AccountSupport.PageLink"/>).</summary>
    protected string PageLink(string page, object values) => AccountSupport.PageLink(Http, Url, page, values);

    /// <summary>The messages of an identity failure as one sentence.</summary>
    protected static string Describe(IdentityResult result) => AccountSupport.Describe(result);

    /// <summary>Fills what the confirmation partial needs: a password to type, else a recent provider sign-in.</summary>
    protected async Task LoadConfirmationAsync(ConfirmationModel model, string? returnPage = null)
    {
        var current = await UserManager.GetUserAsync(User);
        model.HasPassword = current == null || await UserManager.HasPasswordAsync(current);
        model.Reauthenticated = !model.HasPassword && await AccountSupport.HasReauthenticatedRecentlyAsync(Http);
        model.CanReauthenticate = Accounts.OpenIdConnect.IsConfigured;
        if (returnPage != null) model.ReturnPage = returnPage;
    }

    /// <summary>A redirect to a screen by its page name; resolved by the legacy page mapping, so the URL is the page's.</summary>
    protected static IActionResult RedirectToScreen(string page, object? values = null, string? fragment = null)
        => new RedirectToPageResult(page, pageHandler: null, routeValues: values, fragment: fragment);
}
