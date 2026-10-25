using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BaGet.Identity.Application;

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

    /// <summary>The caller is known but this credential may not do it (an API key on a cookie-only screen, a key without the administration scope).</summary>
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
    /// The access screens: server administrators, or whoever <see cref="GateRequest.AlsoAllow"/> admits (a project's administrators);
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
    Func<CancellationToken, Task<bool>>? AlsoAllow = null);

/// <summary>The gate's decision and what it learned on the way.</summary>
/// <param name="Outcome">Whether the screen may run, and if not why.</param>
/// <param name="IsServerAdministrator">Whether the caller is a server administrator (only evaluated for the roles that need it).</param>
public readonly record struct GateResult(GateOutcome Outcome, bool IsServerAdministrator)
{
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
    /// The synchronous part of the gate: accounts enabled, mail configured when required, the cookie-only rule, signed in when required.
    /// </summary>
    public GateOutcome CheckAccount(ClaimsPrincipal? user, GateRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (!AccountsEnabled) return GateOutcome.AccountsOff;
        if (request.RequiresEmail && !EmailConfigured) return GateOutcome.NotFound;
        if (request.CookieOnly && request.KeyCaller != null) return GateOutcome.Forbidden;
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
    /// Whether an API key may administer the server, as for the administration API: the global key (only when one is configured; an
    /// open server authenticates everybody as the superuser, which must never open the administration), or the key of an
    /// administrator that carries <see cref="ApiKeyScopes.Admin"/> and is not limited to repositories.
    /// </summary>
    public bool IsAdministratorKey(BaGetPrincipal key)
    {
        if (key == null) throw new ArgumentNullException(nameof(key));

        if (key.IsSuperuser)
        {
            return !string.IsNullOrEmpty(_services.GetRequiredService<IOptionsSnapshot<BaGetOptions>>().Value.ApiKey);
        }

        return key.IsAdministrator && key.HasScope(ApiKeyScopes.Admin) && key.RepositoryKeys == null;
    }

    /// <summary>The whole gate for a screen.</summary>
    public async Task<GateResult> CheckAsync(ClaimsPrincipal? user, GateRequest request, CancellationToken cancellationToken)
    {
        var account = CheckAccount(user, request);
        if (account != GateOutcome.Allowed) return new GateResult(account, false);

        if (request.Role == GateRole.Anyone) return new GateResult(GateOutcome.Allowed, false);

        var isAdministrator = await IsAdministratorAsync(user, request.KeyCaller, cancellationToken);

        if (request.Role == GateRole.Administrator)
        {
            return new GateResult(isAdministrator ? GateOutcome.Allowed : Refusal(request), isAdministrator);
        }

        // The access screens need a relational database: an administrator is told so, anyone else sees nothing.
        if (_services.GetRequiredService<IContext>() is NullContext)
        {
            return new GateResult(isAdministrator ? GateOutcome.AccessUnavailable : GateOutcome.NotFound, isAdministrator);
        }

        if (!isAdministrator && (request.AlsoAllow == null || !await request.AlsoAllow(cancellationToken)))
        {
            return new GateResult(Refusal(request), isAdministrator);
        }

        if (request.IsWrite) _services.GetService<MaintenanceGate>()?.ThrowIfClosed();

        return new GateResult(GateOutcome.Allowed, isAdministrator);
    }

    // A key that lacks the administration scope is told so (403); a session that is not an administrator learns nothing (404).
    private static GateOutcome Refusal(GateRequest request) => request.KeyCaller != null ? GateOutcome.Forbidden : GateOutcome.NotFound;
}
