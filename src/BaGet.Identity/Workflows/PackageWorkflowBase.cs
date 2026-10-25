using System;
using System.Security.Claims;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BaGet.Identity.Workflows;

/// <summary>
/// The logic of a package-management screen (<c>Packages/*</c>), independent of the programming model: the MVC controller that
/// serves the screen and the obsolete page model that used to (a shim kept for the public API) both run the same workflow, so
/// the two cannot diverge. A workflow lives for one request and is built from its <see cref="HttpContext"/>; services are resolved
/// per request because they exist only while accounts are enabled.
/// </summary>
/// <remarks>
/// A step that ends the request returns an <see cref="IActionResult"/>. "Show the page again" is the <see cref="Show"/> marker,
/// which the caller turns into its own view (<c>View(name, model)</c> or <c>Page()</c>); every other result is a framework result
/// (404, 403, a challenge, a redirect to a page name) that works the same in both models.
/// </remarks>
internal abstract class PackageWorkflowBase
{
    /// <summary>Marks "render the screen" in a workflow result; see <see cref="Show"/>.</summary>
    internal sealed class ShowView : IActionResult
    {
        internal static readonly ShowView Instance = new();

        public Task ExecuteResultAsync(ActionContext context)
            => throw new InvalidOperationException("A workflow result must be resolved to a view by the caller.");
    }

    /// <summary>Creates the workflow of one request.</summary>
    protected PackageWorkflowBase(HttpContext httpContext)
    {
        HttpContext = httpContext ?? throw new ArgumentNullException(nameof(httpContext));
    }

    /// <summary>The request.</summary>
    protected HttpContext HttpContext { get; }

    /// <summary>The signed-in user.</summary>
    protected ClaimsPrincipal User => HttpContext.User;

    /// <summary>The accounts options.</summary>
    protected AccountsOptions Accounts => HttpContext.RequestServices.GetRequiredService<IOptions<AccountsOptions>>().Value;

    /// <summary>Users, passwords, tokens.</summary>
    protected UserManager<BaGetUser> UserManager => HttpContext.RequestServices.GetRequiredService<UserManager<BaGetUser>>();

    /// <summary>Package and organization ownership.</summary>
    protected PackageOwnershipService Ownership => HttpContext.RequestServices.GetRequiredService<PackageOwnershipService>();

    /// <summary>Whether an external provider is available to sign in again with.</summary>
    public bool CanReauthenticate => Accounts.OpenIdConnect.IsConfigured;

    /// <summary>Whether the server refuses writes (<see cref="BaGetOptions.IsReadOnlyMode"/>).</summary>
    protected bool IsReadOnlyMode
        => HttpContext.RequestServices.GetRequiredService<IOptionsSnapshot<BaGetOptions>>().Value.IsReadOnlyMode;

    /// <summary>The result that means "render the screen".</summary>
    public static IActionResult Show => ShowView.Instance;

    /// <summary>Whether a result is the <see cref="Show"/> marker.</summary>
    public static bool IsShow(IActionResult result) => result is ShowView;

    /// <summary>A 404.</summary>
    protected static IActionResult NotFound() => new NotFoundResult();

    /// <summary>A bare status code.</summary>
    protected static IActionResult StatusCode(int status) => new StatusCodeResult(status);

    /// <summary>A challenge with the default scheme (sends the visitor to sign in).</summary>
    protected static IActionResult Challenge() => new ChallengeResult();

    /// <summary>A redirect to a page by its name (resolved to the page or to the controller action that replaced it).</summary>
    protected static IActionResult RedirectToPage(string pageName) => new RedirectToPageResult(pageName);

    /// <summary>The sensitive-action check: the password, or a recent sign-in with the external provider.</summary>
    protected Task<bool> IsConfirmedAsync(BaGetUser? user, string? password)
        => AccountSupport.IsConfirmedAsync(UserManager, HttpContext, user, password);

    /// <summary>Whether the session re-authenticated with the external provider recently.</summary>
    protected Task<bool> HasReauthenticatedRecentlyAsync()
        => AccountSupport.HasReauthenticatedRecentlyAsync(HttpContext);

    /// <summary>The session's principal for the format services (null when it cannot be built).</summary>
    protected async Task<BaGetPrincipal?> CallerAsync()
        => await HttpContext.RequestServices.GetRequiredService<ISessionPrincipalFactory>().CreateAsync(User);

    /// <summary>
    /// The message of a package-management screen's owner change, shared by the management screens
    /// (<c>unit</c> is "package", "artifact" ...).
    /// </summary>
    protected static string Describe(OwnerChangeResult result, string unit) => result switch
    {
        OwnerChangeResult.UnknownAccount => "There is no such user or organization.",
        OwnerChangeResult.AlreadyOwner => $"That account already owns the {unit}.",
        OwnerChangeResult.NotOwner => "That account is not an owner.",
        OwnerChangeResult.LastOwner => $"{(unit == "package" ? "A" : "An")} {unit} keeps at least one owner.",
        _ => result.ToString(),
    };
}
