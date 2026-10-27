using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BaGet.Identity.Application.Screens;

/// <summary>The answer of the <see cref="ScreenGate"/>.</summary>
public enum GateOutcome
{
    /// <summary>The caller may use the screen.</summary>
    Allowed,

    /// <summary>Accounts are disabled: there is nothing to show (404).</summary>
    AccountsOff,

    /// <summary>The screen does not exist for this caller (404): no mail configured, not an administrator, not a project administrator.</summary>
    NotFound,

    /// <summary>The caller must sign in.</summary>
    SignIn,

    /// <summary>The caller is known but this credential may not do it (a screen-only twin over HTTP, a key without the administration scope).</summary>
    Forbidden,

    /// <summary>The database has no access model (Azure Table) and the caller is an administrator: the screen says so.</summary>
    AccessUnavailable,
}

/// <summary>Who may use an administration screen.</summary>
public enum GateRole
{
    /// <summary>Anybody the other requirements let in.</summary>
    Anyone,

    /// <summary>Server administrators.</summary>
    Administrator,

    /// <summary>
    /// The access screens: server administrators, or whoever <see cref="GateRequest.AlsoAllow"/> admits (a project's administrators; a key only when it carries <see cref="ApiKeyScopes.Admin"/> and no repository limit);
    /// 503 on a write while the server is read-only; an administrator is sent to the "unavailable" screen where there is no access model.
    /// </summary>
    AccessAdministrator,
}

/// <summary>What a screen requires of its caller, in the order the gate checks it.</summary>
/// <param name="RequiresSignIn">The caller must be signed in (else <see cref="GateOutcome.SignIn"/>).</param>
/// <param name="RequiresEmail">Mail must be configured (else <see cref="GateOutcome.NotFound"/>).</param>
/// <param name="Role">Who may use the screen.</param>
/// <param name="IsWrite">The request changes something: the read-only <see cref="MaintenanceGate"/> refuses it (<see cref="MaintenanceException"/>, 503) once the role is settled.</param>
/// <param name="KeyCaller">The API key behind the request, or null for a browser session (or an anonymous visitor).</param>
/// <param name="CookieOnly">The screen changes or reveals credentials: an API key never opens it (<see cref="GateOutcome.Forbidden"/>).</param>
/// <param name="AlsoAllow">For <see cref="GateRole.AccessAdministrator"/>: admits a caller who is not a server administrator (a project administrator).</param>
public sealed record GateRequest(
    bool RequiresSignIn = false,
    bool RequiresEmail = false,
    GateRole Role = GateRole.Anyone,
    bool IsWrite = false,
    BaGetPrincipal? KeyCaller = null,
    bool CookieOnly = false,
    Func<CancellationToken, Task<bool>>? AlsoAllow = null)
{
    /// <summary>
    /// The request reached the screen over HTTP as a JSON twin (<c>/api/ui</c>), where the only credential is an API key: a <see cref="CookieOnly"/> screen is then
    /// unreachable (<see cref="GateOutcome.Forbidden"/>, "use the web screen") for any caller, and a caller who is not signed in is told how to authenticate.
    /// The in-process call of a view controller leaves it false.
    /// </summary>
    public bool OverHttp { get; init; }
}

/// <summary>The gate's decision and what it learned on the way.</summary>
/// <param name="Outcome">Whether the screen may run, and if not why.</param>
/// <param name="IsServerAdministrator">Whether the caller is a server administrator (only evaluated for the roles that need it).</param>
public readonly record struct GateResult(GateOutcome Outcome, bool IsServerAdministrator)
{
    /// <summary>Why the screen was refused, in words for an API client (null when there is nothing to add).</summary>
    public string? Message { get; init; }

    /// <summary>Whether the screen may run.</summary>
    public bool IsAllowed => Outcome == GateOutcome.Allowed;
}

/// <summary>
/// The single place that decides whether a caller may use a screen of the account and administration area: accounts enabled, mail
/// configured, signed in, administrator (or project administrator), the access model present, the server writable. The view
/// controller bases and the JSON API bases both ask it, in the same order, so the two ways to reach a screen cannot disagree. It
/// returns a <see cref="GateOutcome"/>; each side turns it into its own answer (a 404 page or a JSON 404, a sign-in redirect or a
/// 401). Resolved per request: the services it needs exist only while accounts are enabled.
/// </summary>
public sealed class ScreenGate
{
    private readonly IOptions<AccountsOptions> _accounts;
    private readonly IOptions<EmailOptions> _email;
    private readonly IServiceProvider _services;

    /// <summary>Creates the gate.</summary>
    public ScreenGate(IOptions<AccountsOptions> accounts, IOptions<EmailOptions> email, IServiceProvider services)
    {
        _accounts = accounts ?? throw new ArgumentNullException(nameof(accounts));
        _email = email ?? throw new ArgumentNullException(nameof(email));
        _services = services ?? throw new ArgumentNullException(nameof(services));
    }

    /// <summary>Whether accounts are enabled.</summary>
    public bool AccountsEnabled => _accounts.Value.Enabled;

    /// <summary>Whether outgoing mail is configured.</summary>
    public bool EmailConfigured => _email.Value.IsConfigured;

    /// <summary>
    /// The synchronous part of the gate: accounts enabled, mail configured when required, the screen-only rule, signed in when required.
    /// </summary>
    public GateOutcome CheckAccount(ClaimsPrincipal? user, GateRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (!AccountsEnabled) return GateOutcome.AccountsOff;
        if (request.RequiresEmail && !EmailConfigured) return GateOutcome.NotFound;
        if (request.CookieOnly && (request.KeyCaller != null || request.OverHttp)) return GateOutcome.Forbidden;
        if (request.RequiresSignIn && user?.Identity?.IsAuthenticated != true) return GateOutcome.SignIn;

        return GateOutcome.Allowed;
    }

    /// <summary>Whether the caller is a server administrator: the session's user, or the key of an administrator (see <see cref="IsAdministratorKey"/>).</summary>
    public async Task<bool> IsAdministratorAsync(ClaimsPrincipal? user, BaGetPrincipal? keyCaller, CancellationToken cancellationToken)
    {
        if (keyCaller != null) return IsAdministratorKey(keyCaller);
        if (user == null) return false;

        return await _services.GetRequiredService<AdministratorService>().IsAdministratorAsync(user, cancellationToken);
    }

    /// <summary>
    /// Whether an API key may use the administration screens: the key of an administrator that carries <see cref="ApiKeyScopes.Admin"/> and is not limited
    /// to repositories. The global <c>ApiKey</c> (the superuser) is refused here, unlike on the administration API (<c>/api/v1</c>): it has no owner, and the
    /// screens act as the signed-in user (an open server authenticates everybody as the superuser, which must never open the administration anyway).
    /// </summary>
    public bool IsAdministratorKey(BaGetPrincipal key)
    {
        if (key == null) throw new ArgumentNullException(nameof(key));

        // The global key has no owner: the use cases that need the current user could not act for it. The screens want an administrator's key.
        if (key.IsSuperuser) return false;

        return key.IsAdministrator && key.HasScope(ApiKeyScopes.Admin) && key.RepositoryKeys == null;
    }

    /// <summary>The whole gate for a screen.</summary>
    public async Task<GateResult> CheckAsync(ClaimsPrincipal? user, GateRequest request, CancellationToken cancellationToken)
    {
        var account = CheckAccount(user, request);
        if (account != GateOutcome.Allowed) return new GateResult(account, false) { Message = MessageFor(account, request) };

        if (request.Role == GateRole.Anyone) return new GateResult(GateOutcome.Allowed, false);

        var isAdministrator = await IsAdministratorAsync(user, request.KeyCaller, cancellationToken);

        if (request.Role == GateRole.Administrator)
        {
            return Decide(isAdministrator, request);
        }

        // The access screens need a relational database: an administrator is told so, anyone else sees nothing.
        if (_services.GetRequiredService<IContext>() is NullContext)
        {
            return new GateResult(isAdministrator ? GateOutcome.AccessUnavailable : GateOutcome.NotFound, isAdministrator);
        }

        if (!isAdministrator && (request.AlsoAllow == null || !CanDelegate(request.KeyCaller) || !await request.AlsoAllow(cancellationToken)))
        {
            return Decide(false, request);
        }

        if (request.IsWrite) _services.GetService<MaintenanceGate>()?.ThrowIfClosed();

        return new GateResult(GateOutcome.Allowed, isAdministrator);
    }

    // A key reaches the access screens only with the administration scope and no repository limit, as for the administration API
    // (a project administrator's other keys, e.g. package:push or limited to one repository, never administer); a session needs nothing more.
    private static bool CanDelegate(BaGetPrincipal? key) => key == null || (!key.IsSuperuser && key.HasScope(ApiKeyScopes.Admin) && key.RepositoryKeys == null);

    // A key that lacks the administration scope is told so (403); a session that is not an administrator learns nothing (404).
    private static GateOutcome Refusal(GateRequest request) => request.KeyCaller != null ? GateOutcome.Forbidden : GateOutcome.NotFound;

    /// <summary>The words an API client gets for a refusal of the gate (null where the outcome says it all).</summary>
    public const string ScreenOnlyMessage = "This screen is only available in the web UI (it changes or reveals credentials or ownership, which an API key never does): use the web screen.";

    /// <summary>The words for a caller of <c>/api/ui</c> who is not authenticated.</summary>
    public const string KeyRequiredMessage = "Authenticate with an API key: 'Authorization: Bearer <key>' or the 'X-NuGet-ApiKey' header (the browser session is not accepted on /api/ui).";

    /// <summary>The words for the global <c>ApiKey</c> on an administration screen.</summary>
    public const string GlobalKeyMessage = "The global ApiKey cannot use the administration screens: use an administrator's API key with the admin:api scope and no repository limit.";

    /// <summary>The words for a key that is not an administrator's, lacks the admin:api scope or is limited to repositories.</summary>
    public const string AdminKeyMessage = "The administration screens need an administrator's API key with the admin:api scope and no repository limit.";

    private static string? MessageFor(GateOutcome outcome, GateRequest request)
        => outcome switch
        {
            GateOutcome.Forbidden when request.CookieOnly => ScreenOnlyMessage,
            GateOutcome.SignIn when request.OverHttp => KeyRequiredMessage,
            _ => null,
        };

    private static GateResult Decide(bool isAdministrator, GateRequest request)
    {
        if (isAdministrator) return new GateResult(GateOutcome.Allowed, true);

        var outcome = Refusal(request);
        var message = outcome != GateOutcome.Forbidden ? null : request.KeyCaller!.IsSuperuser ? GlobalKeyMessage : AdminKeyMessage;

        return new GateResult(outcome, false) { Message = message };
    }
}
