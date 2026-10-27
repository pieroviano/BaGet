using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using BaGet.Core;
using BaGet.Identity.Application.Services;

namespace BaGet.Identity.Application.Models;

/// <summary>What the account settings screen shows (change email, change password, delete the account).</summary>
public sealed class SettingsViewModel
{
    /// <summary>The account's email address.</summary>
    public string? Email { get; set; }

    /// <summary>Whether the address is confirmed.</summary>
    public bool EmailConfirmed { get; set; }

    /// <summary>Whether the account has a password (else it signs in through a provider).</summary>
    public bool HasPassword { get; set; } = true;

    /// <summary>For an account without a password: it signed in again with its provider a moment ago.</summary>
    public bool Reauthenticated { get; set; }

    /// <summary>Whether an external provider is configured to sign in again with.</summary>
    public bool CanReauthenticate { get; set; }

    /// <summary>Whether mail is configured.</summary>
    public bool CanSendEmail { get; set; }

    /// <summary>The success message to show.</summary>
    public string? Message { get; set; }

    /// <summary>The refusal to show.</summary>
    public string? Error { get; set; }

    /// <summary>Why the account cannot be deleted yet, when it cannot.</summary>
    public DeletionBlockers? Blockers { get; set; }
}

/// <summary>What the two-factor authentication screen shows.</summary>
public sealed class TwoFactorViewModel
{
    /// <summary>Whether two-factor authentication is on.</summary>
    public bool TwoFactorEnabled { get; set; }

    /// <summary>The key to type into an authenticator that cannot scan the QR code.</summary>
    public string? SharedKey { get; set; }

    /// <summary>The QR code of the setup link, an SVG built from the user's own key.</summary>
    public string? QrCodeSvg { get; set; }

    /// <summary>For an account without a password: it signed in again with its provider a moment ago.</summary>
    public bool Reauthenticated { get; set; }

    /// <summary>Whether an external provider is configured to sign in again with.</summary>
    public bool CanReauthenticate { get; set; }

    /// <summary>How many recovery codes are unused.</summary>
    public int RecoveryCodesLeft { get; set; }

    /// <summary>Whether the account has a password (else it signs in through a provider).</summary>
    public bool HasPassword { get; set; } = true;

    /// <summary>The new recovery codes. Shown once, when two-factor authentication is turned on.</summary>
    public IReadOnlyList<string> RecoveryCodes { get; set; } = Array.Empty<string>();

    /// <summary>The refusal to show.</summary>
    public string? Error { get; set; }

    /// <summary>The success message to show.</summary>
    public string? Message { get; set; }
}

/// <summary>What the new-key form posts (and the body of <c>POST /api/ui/account/apikeys</c>).</summary>
public class ApiKeyForm
{
    /// <summary>What the key is for.</summary>
    [StringLength(AbstractContext<Microsoft.EntityFrameworkCore.DbContext>.MaxApiKeyDescriptionLength)]
    public string? Description { get; set; }

    /// <summary>The scopes of the new key.</summary>
    public List<string> Scopes { get; set; } = new List<string>();

    /// <summary>The package IDs the key applies to.</summary>
    [Display(Name = "Package glob pattern")]
    public string PackageGlobPattern { get; set; } = "*";

    /// <summary>Days until the key expires; none = never.</summary>
    [Range(1, 3650)]
    [Display(Name = "Expires after (days)")]
    public int? ExpiresInDays { get; set; } = 365;

    /// <summary>The keys of the repositories the new key is limited to; none (the default) means every repository.</summary>
    public List<int> RepositoryKeys { get; set; } = new List<int>();
}

/// <summary>One API key as the screen lists it: never the key itself or its hash (only the creation response shows a new key, once).</summary>
public sealed class AccountApiKey
{
    /// <summary>The key's id (the number the revoke button posts).</summary>
    public int Key { get; set; }

    /// <summary>What the key is for.</summary>
    public string? Description { get; set; }

    /// <summary>What the key may do.</summary>
    public IReadOnlyList<string> Scopes { get; set; } = Array.Empty<string>();

    /// <summary>The package IDs the key applies to.</summary>
    public string? PackageGlobPattern { get; set; }

    /// <summary>When it was created.</summary>
    public DateTime Created { get; set; }

    /// <summary>When it expires; null = never.</summary>
    public DateTime? Expires { get; set; }

    /// <summary>The listing row of <paramref name="key"/>.</summary>
    public static AccountApiKey From(ApiKey key) => new()
    {
        Key = key.Key,
        Description = key.Description,
        Scopes = key.Scopes ?? Array.Empty<string>(),
        PackageGlobPattern = key.PackageGlobPattern,
        Created = key.Created,
        Expires = key.Expires,
    };
}

/// <summary>
/// The API keys screen: the new-key form's fields it repeats and the user's keys. A request binds <see cref="ApiKeyForm"/>, never this type,
/// so the outputs cannot be set from outside.
/// </summary>
public sealed class ApiKeysViewModel : ApiKeyForm
{
    /// <summary>
    /// The repositories offered in the picker: those the user can read. Empty (picker hidden) where the server holds
    /// the default repository only.
    /// </summary>
    public IReadOnlyList<RepositoryInfo> Repositories { get; set; } = Array.Empty<RepositoryInfo>();

    /// <summary>The user's keys.</summary>
    public IReadOnlyList<AccountApiKey> Keys { get; set; } = Array.Empty<AccountApiKey>();

    /// <summary>The names of the repositories each key (by key id) is limited to.</summary>
    public IReadOnlyDictionary<int, IReadOnlyList<string>> KeyRepositories { get; set; } = new Dictionary<int, IReadOnlyList<string>>();

    /// <summary>The key just created. Shown once: only its hash is kept.</summary>
    public string? CreatedKey { get; set; }

    /// <summary>
    /// The scopes the new-key form offers: the package scopes, plus <see cref="ApiKeyScopes.Admin"/> (the REST API under
    /// <c>/api/v1</c>) for an administrator.
    /// </summary>
    public IReadOnlyList<string> AllScopes { get; set; } = ApiKeyScopes.All;

    /// <summary>Where the key may be used: the repositories it is limited to, or "all repositories".</summary>
    public string DescribeRepositories(AccountApiKey key)
        => ApiKeyService.DescribeRepositories(KeyRepositories.GetValueOrDefault(key.Key));

    /// <summary>What the key can do and on which package IDs.</summary>
    public static string Describe(AccountApiKey key)
        => string.Join(", ", key.Scopes) + " on " + key.PackageGlobPattern;
}
