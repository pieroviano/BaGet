using System;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace BaGet.Identity
{
    /// <summary>
    /// The global API key, as before, plus users' own keys.
    /// </summary>
    public class AccountAuthenticationService : Core.IAuthenticationService
    {
        private readonly ApiKeyAuthenticationService _globalKey;
        private readonly ApiKeyService _apiKeys;
        private readonly UserManager<BaGetUser> _users;
        private readonly IOptionsSnapshot<BaGetOptions> _options;

        public AccountAuthenticationService(
            ApiKeyAuthenticationService globalKey,
            ApiKeyService apiKeys,
            UserManager<BaGetUser> users,
            IOptionsSnapshot<BaGetOptions> options)
        {
            _globalKey = globalKey ?? throw new ArgumentNullException(nameof(globalKey));
            _apiKeys = apiKeys ?? throw new ArgumentNullException(nameof(apiKeys));
            _users = users ?? throw new ArgumentNullException(nameof(users));
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public async Task<bool> AuthenticateAsync(string apiKey, CancellationToken cancellationToken)
            => await AuthenticatePrincipalAsync(apiKey, cancellationToken) != null;

        public async Task<BaGetPrincipal> AuthenticatePrincipalAsync(string apiKey, CancellationToken cancellationToken)
        {
            // An open server, and the global key, stay exactly as they were: the superuser.
            if (string.IsNullOrEmpty(_options.Value.ApiKey)
                || await _globalKey.AuthenticateAsync(apiKey, cancellationToken))
            {
                return BaGetPrincipal.Superuser;
            }

            var key = await _apiKeys.FindValidOrNullAsync(apiKey, cancellationToken);
            if (key == null) return null;

            var user = await _users.FindByIdAsync(key.UserId);
            if (user == null) return null;

            return new BaGetPrincipal(
                user.Id,
                user.UserName,
                isSuperuser: false,
                key.Scopes,
                key.PackageGlobPattern);
        }
    }
}
