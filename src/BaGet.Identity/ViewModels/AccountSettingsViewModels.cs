using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using BaGet.Core;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace BaGet.Identity.ViewModels;

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

/// <summary>The API keys screen: the new-key form and the user's keys. Outputs are never bound from the request.</summary>
public sealed class ApiKeysViewModel
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

    /// <summary>
    /// The repositories offered in the picker: those the user can read. Empty (picker hidden) where the server holds
    /// the default repository only.
    /// </summary>
    [BindNever, ValidateNever]
    public IReadOnlyList<RepositoryInfo> Repositories { get; set; } = Array.Empty<RepositoryInfo>();

    /// <summary>The user's keys.</summary>
    [BindNever, ValidateNever]
    public IReadOnlyList<ApiKey> Keys { get; set; } = Array.Empty<ApiKey>();

    /// <summary>The names of the repositories each key (by key id) is limited to.</summary>
    [BindNever, ValidateNever]
    public IReadOnlyDictionary<int, IReadOnlyList<string>> KeyRepositories { get; set; } = new Dictionary<int, IReadOnlyList<string>>();

    /// <summary>The key just created. Shown once: only its hash is kept.</summary>
    [BindNever, ValidateNever]
    public string? CreatedKey { get; set; }

    /// <summary>
    /// The scopes the new-key form offers: the package scopes, plus <see cref="ApiKeyScopes.Admin"/> (the REST API under
    /// <c>/api/v1</c>) for an administrator.
    /// </summary>
    [BindNever, ValidateNever]
    public IReadOnlyList<string> AllScopes { get; set; } = ApiKeyScopes.All;

    /// <summary>Where the key may be used: the repositories it is limited to, or "all repositories".</summary>
    public string DescribeRepositories(ApiKey key)
        => ApiKeyService.DescribeRepositories(KeyRepositories.GetValueOrDefault(key.Key));

    /// <summary>What the key can do and on which package IDs.</summary>
    public static string Describe(ApiKey key)
        => string.Join(", ", key.Scopes ?? Array.Empty<string>()) + " on " + key.PackageGlobPattern;
}
