using System;
using System.Security.Claims;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.Application.Models.Session;
using BaGet.Identity.Application.Screens;
using BaGet.Identity.Application.Services.Sessions;
using Microsoft.AspNetCore.Identity;

namespace BaGet.Identity.Application.UseCases.Account;

/// <summary>
/// Interactive sessions for a client that holds tokens instead of a cookie (a single-page application on any origin): sign in with the password
/// (and the second factor), refresh, sign out. The rules are the sign-in page's: lockout is honoured first, a wrong password is counted, an account
/// awaiting approval or with an unconfirmed email answers only to the right password, a wrong second-factor code is counted too. Tokens are
/// <see cref="SessionTokenService"/>'s; none of this touches the cookie.
/// </summary>
public sealed class SessionUseCase
{
    private const string BadCredentials = "The user name or password is incorrect.";
    private const string BadTicket = "The sign-in has expired or is not valid: sign in again.";
    private const string BadRefresh = "The refresh token has expired or is not valid: sign in again.";

    private readonly SignInManager<BaGetUser> _signIn;
    private readonly UserManager<BaGetUser> _users;
    private readonly SessionTokenService _tokens;
    private readonly SessionAuthenticator _authenticator;
    private readonly SystemTime _time;
    private readonly MaintenanceGate _gate;

    /// <summary>Creates the use case.</summary>
    public SessionUseCase(
        SignInManager<BaGetUser> signIn,
        UserManager<BaGetUser> users,
        SessionTokenService tokens,
        SessionAuthenticator authenticator,
        SystemTime time,
        MaintenanceGate gate)
    {
        _signIn = signIn ?? throw new ArgumentNullException(nameof(signIn));
        _users = users ?? throw new ArgumentNullException(nameof(users));
        _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
        _authenticator = authenticator ?? throw new ArgumentNullException(nameof(authenticator));
        _time = time ?? throw new ArgumentNullException(nameof(time));
        _gate = gate ?? throw new ArgumentNullException(nameof(gate));
    }

    /// <summary>The password step of a sign-in: tokens, or the ticket for the second factor when the account has one.</summary>
    public async Task<ScreenResult<SessionModel>> SignInAsync(SessionSignInForm? form)
    {
        var errors = ScreenValidator.Validate(form);
        if (errors.Count > 0) return ScreenResult<SessionModel>.Invalid(new SessionModel(), errors);
        if (!_tokens.IsAvailable) return Unavailable();

        // Like the page: only the user name finds the account.
        var user = await _users.FindByNameAsync(form!.UserName!);
        if (user == null)
        {
            // Spend the time of a password check, so the answer does not tell a missing account from a wrong password.
            await Task.Run(() => _users.PasswordHasher.HashPassword(new BaGetUser(), form.Password!));

            return ScreenResult<SessionModel>.SignIn(BadCredentials);
        }

        var result = await _signIn.CheckPasswordSignInAsync(user, form.Password!, lockoutOnFailure: true);
        if (result.IsLockedOut) return ScreenResult<SessionModel>.Forbidden(SignInUseCase.LockedMessage);
        if (result.IsNotAllowed) return ScreenResult<SessionModel>.Forbidden(user.PendingApproval ? SignInUseCase.PendingMessage : SignInUseCase.UnconfirmedMessage);
        if (!result.Succeeded) return ScreenResult<SessionModel>.SignIn(BadCredentials);

        return await CompleteAsync(user, _time.UtcNow);
    }

    /// <summary>The second factor, with a code of the authenticator app.</summary>
    public async Task<ScreenResult<SessionModel>> TwoFactorAsync(SessionTwoFactorForm? form)
    {
        var errors = ScreenValidator.Validate(form);
        if (errors.Count > 0) return ScreenResult<SessionModel>.Invalid(new SessionModel(), errors);

        var ticket = await _authenticator.AuthenticateAsync(SessionTokenKind.TwoFactor, form!.Ticket);
        if (ticket == null) return ScreenResult<SessionModel>.SignIn(BadTicket);

        var refusal = await CheckSecondFactorAllowedAsync(ticket.Account);
        if (refusal != null) return refusal;

        var code = form.Code!.Replace(" ", string.Empty).Replace("-", string.Empty);
        var valid = await _users.VerifyTwoFactorTokenAsync(ticket.Account, _users.Options.Tokens.AuthenticatorTokenProvider, code);

        return await FinishSecondFactorAsync(ticket, valid, "The code is not valid.");
    }

    /// <summary>The second factor, with a recovery code (each works once).</summary>
    public async Task<ScreenResult<SessionModel>> RecoveryCodeAsync(SessionRecoveryCodeForm? form)
    {
        var errors = ScreenValidator.Validate(form);
        if (errors.Count > 0) return ScreenResult<SessionModel>.Invalid(new SessionModel(), errors);

        var ticket = await _authenticator.AuthenticateAsync(SessionTokenKind.TwoFactor, form!.Ticket);
        if (ticket == null) return ScreenResult<SessionModel>.SignIn(BadTicket);

        var refusal = await CheckSecondFactorAllowedAsync(ticket.Account);
        if (refusal != null) return refusal;

        var redeemed = await _users.RedeemTwoFactorRecoveryCodeAsync(ticket.Account, form.RecoveryCode!.Replace(" ", string.Empty));

        return await FinishSecondFactorAsync(ticket, redeemed.Succeeded, "The recovery code is not valid, or was already used.");
    }

    /// <summary>
    /// Exchanges a refresh token for a new pair. The old refresh token stays valid until it expires (the tokens are stateless); the new pair keeps the
    /// original authentication time, so a refresh never counts as a fresh sign-in.
    /// </summary>
    public async Task<ScreenResult<SessionModel>> RefreshAsync(SessionRefreshForm? form)
    {
        var errors = ScreenValidator.Validate(form);
        if (errors.Count > 0) return ScreenResult<SessionModel>.Invalid(new SessionModel(), errors);

        var session = await _authenticator.AuthenticateAsync(SessionTokenKind.Refresh, form!.RefreshToken);
        if (session == null) return ScreenResult<SessionModel>.SignIn(BadRefresh);

        return ScreenResult<SessionModel>.Ok(await IssueAsync(session.Account, session.AuthenticatedAt));
    }

    /// <summary>
    /// Ends the session. Tokens cannot be taken back one by one, so by itself this revokes nothing (the client forgets them); with
    /// <see cref="SessionSignOutForm.Everywhere"/> the account's security stamp changes, which ends every token and cookie session of the account. The
    /// caller is the access token's user, or the owner of the refresh token in the form.
    /// </summary>
    public async Task<ScreenResult<EmptyScreen>> SignOutAsync(ClaimsPrincipal? caller, SessionSignOutForm? form)
    {
        if (form?.Everywhere != true) return ScreenResult<EmptyScreen>.Ok(EmptyScreen.Instance);

        var account = caller?.Identity?.IsAuthenticated == true ? await _users.GetUserAsync(caller) : null;
        if (account == null && !string.IsNullOrEmpty(form.RefreshToken))
        {
            account = (await _authenticator.AuthenticateAsync(SessionTokenKind.Refresh, form.RefreshToken))?.Account;
        }

        if (account == null) return ScreenResult<EmptyScreen>.SignIn("Send the access token or the refresh token of the session to end every session.");

        var updated = await _users.UpdateSecurityStampAsync(account);
        if (!updated.Succeeded) return ScreenResult<EmptyScreen>.Invalid(EmptyScreen.Instance, "The sessions could not be ended.");

        return ScreenResult<EmptyScreen>.Ok(EmptyScreen.Instance);
    }

    /// <summary>Finishes a sign-in whose first factor is proven (also the external provider's): the ticket for the second factor, or the tokens.</summary>
    internal async Task<ScreenResult<SessionModel>> CompleteAsync(BaGetUser user, DateTime authenticatedAt)
    {
        if (_users.SupportsUserTwoFactor && await _users.GetTwoFactorEnabledAsync(user))
        {
            var ticket = _tokens.Issue(SessionTokenKind.TwoFactor, user.Id, await StampOfAsync(user), authenticatedAt);

            return ScreenResult<SessionModel>.Ok(new SessionModel
            {
                TwoFactorRequired = true,
                TwoFactorTicket = ticket.Token,
                TwoFactorTicketExpiresAt = ticket.ExpiresAt,
                UserName = user.UserName,
            });
        }

        return ScreenResult<SessionModel>.Ok(await IssueAsync(user, authenticatedAt));
    }

    /// <summary>A pair of tokens for the account.</summary>
    internal async Task<SessionModel> IssueAsync(BaGetUser user, DateTime authenticatedAt)
    {
        var stamp = await StampOfAsync(user);
        var access = _tokens.Issue(SessionTokenKind.Access, user.Id, stamp, authenticatedAt);
        var refresh = _tokens.Issue(SessionTokenKind.Refresh, user.Id, stamp, authenticatedAt);

        return new SessionModel
        {
            AccessToken = access.Token,
            ExpiresAt = access.ExpiresAt,
            RefreshToken = refresh.Token,
            RefreshExpiresAt = refresh.ExpiresAt,
            UserName = user.UserName,
        };
    }

    private async Task<string> StampOfAsync(BaGetUser user)
        => _users.SupportsUserSecurityStamp ? await _users.GetSecurityStampAsync(user) ?? string.Empty : string.Empty;

    // A locked-out account is refused without looking at the code; one that cannot sign in (approval withdrawn, email) too.
    private async Task<ScreenResult<SessionModel>?> CheckSecondFactorAllowedAsync(BaGetUser user)
    {
        if (await _users.IsLockedOutAsync(user)) return ScreenResult<SessionModel>.Forbidden(SignInUseCase.LockedMessage);
        if (!await _signIn.CanSignInAsync(user)) return ScreenResult<SessionModel>.Forbidden(user.PendingApproval ? SignInUseCase.PendingMessage : SignInUseCase.UnconfirmedMessage);

        return null;
    }

    private async Task<ScreenResult<SessionModel>> FinishSecondFactorAsync(SessionAuthentication ticket, bool valid, string failure)
    {
        var user = ticket.Account;
        if (valid)
        {
            // Like the sign-in manager: the counter is reset, unless the read-only gate refuses the write.
            if (!_gate.IsClosed) await _users.ResetAccessFailedCountAsync(user);

            return ScreenResult<SessionModel>.Ok(await IssueAsync(user, ticket.AuthenticatedAt));
        }

        // A wrong code is a failed attempt, so codes cannot be guessed without limit.
        var counted = await _users.AccessFailedAsync(user);
        if (counted.Succeeded && await _users.IsLockedOutAsync(user)) return ScreenResult<SessionModel>.Forbidden(SignInUseCase.LockedMessage);

        return ScreenResult<SessionModel>.SignIn(failure);
    }

    private static ScreenResult<SessionModel> Unavailable()
        => ScreenResult<SessionModel>.Forbidden("Session tokens need ASP.NET Core data protection, which this host does not provide.");
}
