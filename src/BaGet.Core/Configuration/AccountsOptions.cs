namespace BaGet.Core;

/// <summary>
/// User accounts, package owners and per-user API keys.
/// </summary>
[RestartRequired]
public class AccountsOptions
{
    /// <summary>
    /// Off by default, so that an upgraded server behaves exactly as before: only the
    /// global <see cref="BaGetOptions.ApiKey"/> authenticates.
    /// </summary>
    /// <remarks>
    /// While this is on, a caller must prove who they are: the global
    /// <see cref="BaGetOptions.ApiKey"/> if one is configured, otherwise a user's own key.
    /// An unset <see cref="BaGetOptions.ApiKey"/> means there is no global key, not that
    /// the server is open - otherwise ownership, which is the point of accounts, could be
    /// bypassed by sending no key at all.
    /// </remarks>
    public bool Enabled { get; set; }

    /// <summary>
    /// Whether anyone may create a local account from the sign-in page.
    /// </summary>
    public bool AllowRegistration { get; set; } = true;

    /// <summary>
    /// Whether an account created by self-service (the registration page, or the first sign-in through the external
    /// provider) stays disabled until an administrator approves it on the Users page. Accounts an administrator
    /// creates are never pending. Off by default: new accounts sign in at once, as before.
    /// </summary>
    public bool RequireApproval { get; set; }

    /// <summary>
    /// User names promoted to administrator at startup, separated by semicolons. Applied
    /// only while the server has no administrator, and only to accounts that already exist,
    /// so it bootstraps the first administrator and nothing more: a listed name registered
    /// later is not promoted, and a demotion is not reversed. See also the "account promote" command.
    /// </summary>
    [RestartRequired]
    public string? InitialAdministrators { get; set; }

    /// <summary>
    /// Whether a server with no users at all creates the administrator "Admin" at startup, with a
    /// well-known default password that must be changed at the first sign-in. Turn it off to bootstrap
    /// only through <see cref="InitialAdministrators"/> or the "account promote" command.
    /// </summary>
    [RestartRequired]
    public bool CreateDefaultAdministrator { get; set; } = true;

    /// <summary>
    /// An external OpenID Connect provider, such as Microsoft Entra ID. Unused unless
    /// <see cref="AccountsOpenIdConnectOptions.Authority"/> is set.
    /// </summary>
    public AccountsOpenIdConnectOptions OpenIdConnect { get; set; } = new AccountsOpenIdConnectOptions();
}

public class AccountsOpenIdConnectOptions
{
    public string? Authority { get; set; }

    public string? ClientId { get; set; }

    [SensitiveSetting]
    public string? ClientSecret { get; set; }

    /// <summary>
    /// The provider's name on the sign-in button.
    /// </summary>
    public string DisplayName { get; set; } = "OpenID Connect";

    public bool IsConfigured => !string.IsNullOrEmpty(Authority) && !string.IsNullOrEmpty(ClientId);
}