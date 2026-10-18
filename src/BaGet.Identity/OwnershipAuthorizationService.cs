using System;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.Extensions.Logging;

namespace BaGet.Identity;

/// <summary>
/// Package ownership rules, as nuget.org applies them:
/// <list type="bullet">
/// <item>The superuser may do anything.</item>
/// <item>A key acts only within its scopes and its package glob.</item>
/// <item>An administrator skips ownership, never the key's own limits (scopes and glob): a
/// push-only key an administrator made for CI stays push-only. It may push a version of,
/// or unlist, any existing ID, ownerless ones included; a new ID still needs
/// <see cref="ApiKeyScopes.Push"/>.</item>
/// <item>Pushing a new package ID needs <see cref="ApiKeyScopes.Push"/>, and makes the
/// pusher its owner.</item>
/// <item>Anything else needs the user to own the ID, directly or through an
/// organization.</item>
/// <item>An ID that already exists but has no owners predates accounts, and only the
/// superuser may change it, so nothing becomes inaccessible on upgrade.</item>
/// </list>
/// </summary>
public class OwnershipAuthorizationService : IPackageAuthorizationService
{
    private readonly PackageOwnershipService _ownership;
    private readonly IPackageDatabase _packages;
    private readonly ILogger<OwnershipAuthorizationService> _logger;

    public OwnershipAuthorizationService(
        PackageOwnershipService ownership,
        IPackageDatabase packages,
        ILogger<OwnershipAuthorizationService> logger)
    {
        _ownership = ownership ?? throw new ArgumentNullException(nameof(ownership));
        _packages = packages ?? throw new ArgumentNullException(nameof(packages));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> AuthorizeAsync(
        BaGetPrincipal principal,
        PackageAction action,
        string packageId,
        CancellationToken cancellationToken)
    {
        if (principal == null) return false;
        if (principal.IsSuperuser) return true;
        if (!principal.MatchesPackage(packageId)) return false;

        // An administrator skips ownership, never the key's own limits: a push-only key an
        // administrator made for CI stays push-only.
        if (principal.IsAdministrator)
        {
            switch (action)
            {
                case PackageAction.Push:
                    var exists = await _packages.ExistsAsync(packageId, cancellationToken)
                                 || await _ownership.HasOwnersAsync(packageId, cancellationToken);

                    return exists
                        ? principal.HasScope(ApiKeyScopes.Push) || principal.HasScope(ApiKeyScopes.PushVersion)
                        : principal.HasScope(ApiKeyScopes.Push);

                case PackageAction.Manage:
                    return principal.HasScope(ApiKeyScopes.Unlist);

                default:
                    return false;
            }
        }

        var isOwner = await _ownership.IsOwnerAsync(principal.UserId, principal.UserName, packageId, cancellationToken);

        switch (action)
        {
            case PackageAction.Push:
                if (isOwner)
                {
                    return principal.HasScope(ApiKeyScopes.Push) || principal.HasScope(ApiKeyScopes.PushVersion);
                }

                // A new ID may be claimed; an existing one without owners may not.
                return principal.HasScope(ApiKeyScopes.Push)
                       && !await _ownership.HasOwnersAsync(packageId, cancellationToken)
                       && !await _packages.ExistsAsync(packageId, cancellationToken);

            case PackageAction.Manage:
                return isOwner && principal.HasScope(ApiKeyScopes.Unlist);

            default:
                return false;
        }
    }

    public async Task OnPushedAsync(BaGetPrincipal principal, string packageId, bool isNewId, CancellationToken cancellationToken)
    {
        // Only a new ID is claimed: an administrator may push to an existing ownerless one,
        // and that must leave it ownerless.
        if (!isNewId || principal == null || principal.IsSuperuser || packageId == null) return;
        if (await _ownership.HasOwnersAsync(packageId, cancellationToken)) return;

        // Two users pushing the same new ID at once may both become owners. That is
        // safe, if surprising: each could only have done so with the push scope.
        var result = await _ownership.AddOwnerAsync(packageId, principal.UserName, cancellationToken);

        _logger.LogInformation(
            "{UserName} pushed new package {PackageId} and became its owner: {Result}",
            principal.UserName,
            packageId,
            result);
    }
}