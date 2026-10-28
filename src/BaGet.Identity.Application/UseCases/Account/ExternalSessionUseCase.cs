using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.Application.Models.Session;
using BaGet.Identity.Application.Screens;
using BaGet.Identity.Application.Security;
using Microsoft.AspNetCore.Identity;

namespace BaGet.Identity.Application.UseCases.Account;

/// <summary>
/// The external provider (OpenID Connect) sign-in for a client that holds tokens: the same rules as the sign-in page (<see cref="ExternalSignInUseCase"/>
/// decides, creating the account the first time, honouring lockout, approval and the confirmed email), but the answer is a pair of tokens (or the ticket
/// for the second factor) instead of a cookie. Browser-only, like the page: the provider redirects the browser, so the controller sends the challenge and
/// returns the tokens in the URL fragment of an address <see cref="SessionReturnUrls"/> allowed.
/// </summary>
public sealed class ExternalSessionUseCase
{
    private readonly ExternalSignInUseCase _external;
    private readonly SessionUseCase _sessions;
    private readonly SignInManager<BaGetUser> _signIn;
    private readonly UserManager<BaGetUser> _users;
    private readonly SystemTime _time;

    /// <summary>Creates the use case.</summary>
    public ExternalSessionUseCase(ExternalSignInUseCase external, SessionUseCase sessions, SignInManager<BaGetUser> signIn, UserManager<BaGetUser> users, SystemTime time)
    {
        _external = external ?? throw new ArgumentNullException(nameof(external));
        _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
        _signIn = signIn ?? throw new ArgumentNullException(nameof(signIn));
        _users = users ?? throw new ArgumentNullException(nameof(users));
        _time = time ?? throw new ArgumentNullException(nameof(time));
    }

    /// <summary>The challenge to send to the provider, or null when none is configured.</summary>
    /// <param name="callbackUrl">The address of the callback of this API.</param>
    public ExternalChallenge? Start(string callbackUrl) => _external.StartSignIn(callbackUrl);

    /// <summary>The provider answered: tokens, the second-factor ticket, a notice (account created but not usable yet), or the reason it was refused.</summary>
    public async Task<ScreenResult<SessionModel>> CompleteAsync(CancellationToken cancellationToken)
    {
        var info = await _signIn.GetExternalLoginInfoAsync();
        var login = await _external.CompleteSignInAsync(null, cancellationToken);

        switch (login.Status)
        {
            case ScreenStatus.Invalid:
                return ScreenResult<SessionModel>.Invalid(new SessionModel { Notice = null }, login.Errors);

            case ScreenStatus.Ok:
                // The account was created but cannot sign in yet (approval, confirmation link): there is no session to issue.
                return ScreenResult<SessionModel>.Ok(new SessionModel { Notice = login.Model?.Notice });
        }

        var user = info == null ? null : await _users.FindByLoginAsync(info.LoginProvider, info.ProviderKey);

        // The page's cookie (or its half-signed-in two-factor cookie) was set on the way: this client has none, so drop it.
        await _signIn.SignOutAsync();
        if (user == null) return ScreenResult<SessionModel>.SignIn("The external sign-in failed.");

        return await _sessions.CompleteAsync(user, _time.UtcNow);
    }
}
