using BaGet.Core;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BaGet.Identity
{
    public static class AccountsServiceCollectionExtensions
    {
        public const string OpenIdConnectScheme = "oidc";

        /// <summary>
        /// Add accounts if "Accounts:Enabled" is set. Otherwise only an empty authentication
        /// setup is added, so the pipeline's authentication middleware has nothing to do.
        /// </summary>
        public static IServiceCollection AddBaGetAccounts(this IServiceCollection services, IConfiguration configuration)
        {
            var options = configuration.GetSection(nameof(BaGetOptions.Accounts)).Get<AccountsOptions>()
                ?? new AccountsOptions();

            if (!options.Enabled)
            {
                services.AddAuthentication();
                return services;
            }

            services.TryAddScoped<ApiKeyService>();
            services.TryAddScoped<PackageOwnershipService>();
            services.TryAddScoped<SessionPrincipalFactory>();
            services.TryAddTransient<ApiKeyAuthenticationService>();
            services.Replace(ServiceDescriptor.Transient<Core.IAuthenticationService, AccountAuthenticationService>());
            services.Replace(ServiceDescriptor.Transient<IPackageAuthorizationService, OwnershipAuthorizationService>());

            // The context is chosen per database provider at runtime, so Identity's EF stores
            // are given whichever one was resolved rather than a context type.
            services.TryAddScoped<IUserStore<BaGetUser>>(provider =>
                new UserStore<BaGetUser, IdentityRole, DbContext, string>(
                    (DbContext)provider.GetRequiredService<IContext>()));

            services
                .AddIdentityCore<BaGetUser>(identity =>
                {
                    identity.User.RequireUniqueEmail = false;
                    identity.Lockout.AllowedForNewUsers = true;
                })
                .AddSignInManager()
                .AddDefaultTokenProviders();

            var authentication = services.AddAuthentication(IdentityConstants.ApplicationScheme);
            authentication.AddIdentityCookies(cookies => cookies.ApplicationCookie.Configure(cookie =>
            {
                cookie.LoginPath = "/account/login";
                cookie.LogoutPath = "/account/logout";
                cookie.AccessDeniedPath = "/account/login";
            }));

            if (options.OpenIdConnect.IsConfigured)
            {
                authentication.AddOpenIdConnect(OpenIdConnectScheme, options.OpenIdConnect.DisplayName, oidc =>
                {
                    oidc.SignInScheme = IdentityConstants.ExternalScheme;
                    oidc.Authority = options.OpenIdConnect.Authority;
                    oidc.ClientId = options.OpenIdConnect.ClientId;
                    oidc.ClientSecret = options.OpenIdConnect.ClientSecret;
                    oidc.ResponseType = "code";
                    oidc.Scope.Add("profile");
                    oidc.Scope.Add("email");
                });
            }

            return services;
        }
    }
}
