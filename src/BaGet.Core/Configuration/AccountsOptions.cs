namespace BaGet.Core
{
    /// <summary>
    /// User accounts, package owners and per-user API keys.
    /// </summary>
    public class AccountsOptions
    {
        /// <summary>
        /// Off by default, so that an upgraded server behaves exactly as before: only the
        /// global <see cref="BaGetOptions.ApiKey"/> authenticates.
        /// </summary>
        /// <remarks>
        /// Per-user keys only restrict anything when <see cref="BaGetOptions.ApiKey"/> is set.
        /// Without it the server is open, and every caller remains the superuser.
        /// </remarks>
        public bool Enabled { get; set; }

        /// <summary>
        /// Whether anyone may create a local account from the sign-in page.
        /// </summary>
        public bool AllowRegistration { get; set; } = true;

        /// <summary>
        /// An external OpenID Connect provider, such as Microsoft Entra ID. Unused unless
        /// <see cref="AccountsOpenIdConnectOptions.Authority"/> is set.
        /// </summary>
        public AccountsOpenIdConnectOptions OpenIdConnect { get; set; } = new AccountsOpenIdConnectOptions();
    }

    public class AccountsOpenIdConnectOptions
    {
        public string Authority { get; set; }

        public string ClientId { get; set; }

        public string ClientSecret { get; set; }

        /// <summary>
        /// The provider's name on the sign-in button.
        /// </summary>
        public string DisplayName { get; set; } = "OpenID Connect";

        public bool IsConfigured => !string.IsNullOrEmpty(Authority) && !string.IsNullOrEmpty(ClientId);
    }
}
