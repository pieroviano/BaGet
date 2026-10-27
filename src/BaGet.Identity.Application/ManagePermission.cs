using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.Extensions.DependencyInjection;

namespace BaGet.Identity.Application;

/// <summary>
/// Whether a signed-in user holds the access model's <see cref="AccessAction.Manage"/> on a package in the current
/// repository: the permission half of the Manage page's rule. Ownership is the other half (D4 of the access plan: a
/// permission is necessary, ownership remains necessary); administrators hold every action.
/// </summary>
public static class ManagePermission
{
    /// <param name="services">The request's services.</param>
    /// <param name="user">The signed-in user.</param>
    /// <param name="packageId">The package ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public static async Task<bool> HeldAsync(IServiceProvider services, ClaimsPrincipal user, string? packageId, CancellationToken cancellationToken)
    {
        var sessions = services.GetService<ISessionPrincipalFactory>();
        var evaluator = services.GetService<IAccessEvaluator>();
        if (sessions == null || evaluator == null) return true;

        var principal = await sessions.CreateAsync(user).ConfigureAwait(false);
        var repository = services.GetRequiredService<IRepositoryContext>().Current;

        return await evaluator.IsAllowedAsync(principal, repository, packageId, AccessAction.Manage, cancellationToken).ConfigureAwait(false);
    }
}
