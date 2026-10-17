using System.Threading;
using System.Threading.Tasks;

namespace BaGet.Core
{
    public enum PackageAction
    {
        /// <summary>
        /// Push a package or a symbol package.
        /// </summary>
        Push,

        /// <summary>
        /// Unlist, relist or delete a version, or edit its deprecation or vulnerabilities.
        /// </summary>
        Manage,
    }

    /// <summary>
    /// Decides whether an authenticated principal may act on a package ID.
    /// </summary>
    public interface IPackageAuthorizationService
    {
        Task<bool> AuthorizeAsync(
            BaGetPrincipal principal,
            PackageAction action,
            string packageId,
            CancellationToken cancellationToken);

        /// <summary>
        /// Called after a successful push, so that a user pushing a new package ID becomes
        /// its owner.
        /// </summary>
        Task OnPushedAsync(BaGetPrincipal principal, string packageId, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Without accounts every authenticated caller is the superuser, as it always was.
    /// </summary>
    public class AllowAuthenticatedAuthorizationService : IPackageAuthorizationService
    {
        public Task<bool> AuthorizeAsync(
            BaGetPrincipal principal,
            PackageAction action,
            string packageId,
            CancellationToken cancellationToken)
            => Task.FromResult(principal != null);

        public Task OnPushedAsync(BaGetPrincipal principal, string packageId, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
