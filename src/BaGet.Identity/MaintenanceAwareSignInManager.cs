using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BaGet.Identity;

/// <summary>
/// The sign-in manager, made to work while the <see cref="MaintenanceGate"/> is closed.
/// </summary>
/// <remarks>
/// A successful sign-in only needs to read the database, but Identity also writes the user on the way: it resets the
/// failed-attempt counter. With the gate closed that write is refused (503), which would keep an administrator who is
/// not signed in from reaching the Settings page after a database copy, where the status and the restart instructions
/// are. The rule is: while the gate is closed a sign-in completes without its incidental, non-essential writes (here, the
/// counter reset, which is retried by the next sign-in once the gate is open); every other write stays refused, a wrong
/// password included, because counting the failed attempt is a write.
/// </remarks>
public class MaintenanceAwareSignInManager : SignInManager<BaGetUser>
{
    private readonly MaintenanceGate _gate;

    public MaintenanceAwareSignInManager(
        UserManager<BaGetUser> userManager,
        IHttpContextAccessor contextAccessor,
        IUserClaimsPrincipalFactory<BaGetUser> claimsFactory,
        IOptions<IdentityOptions> optionsAccessor,
        ILogger<SignInManager<BaGetUser>> logger,
        IAuthenticationSchemeProvider schemes,
        IUserConfirmation<BaGetUser> confirmation,
        MaintenanceGate gate)
        : base(userManager, contextAccessor, claimsFactory, optionsAccessor, logger, schemes, confirmation)
    {
        _gate = gate;
    }

    /// <summary>Resets the failed-attempt counter, unless the gate is closed (a write it would refuse).</summary>
    protected override Task ResetLockout(BaGetUser user)
        => _gate.IsClosed ? Task.CompletedTask : base.ResetLockout(user);
}
