using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace BaGet.Core;

/// <summary>
/// Who an API key authenticated, and what it may do.
/// </summary>
public sealed class BaGetPrincipal
{
    /// <summary>
    /// The server's global API key, or any caller when no key is configured: it may do
    /// anything to any package.
    /// </summary>
    public static readonly BaGetPrincipal Superuser = new BaGetPrincipal(
        userId: null,
        userName: null,
        isSuperuser: true,
        scopes: ApiKeyScopes.All,
        packageGlobPattern: "*");

    public BaGetPrincipal(
        string userId,
        string userName,
        bool isSuperuser,
        IReadOnlyList<string> scopes,
        string packageGlobPattern)
    {
        UserId = userId;
        UserName = userName;
        IsSuperuser = isSuperuser;
        Scopes = scopes ?? Array.Empty<string>();
        PackageGlobPattern = string.IsNullOrEmpty(packageGlobPattern) ? "*" : packageGlobPattern;
    }

    public string UserId { get; }

    public string UserName { get; }

    public bool IsSuperuser { get; }

    public IReadOnlyList<string> Scopes { get; }

    public string PackageGlobPattern { get; }

    public bool HasScope(string scope) => IsSuperuser || Scopes.Contains(scope, StringComparer.Ordinal);

    public bool MatchesPackage(string packageId) => IsSuperuser || GlobMatches(PackageGlobPattern, packageId);

    /// <summary>
    /// Match a package ID against a glob in which "*" stands for any run of characters.
    /// Package IDs are case insensitive, so the match is too.
    /// </summary>
    internal static bool GlobMatches(string pattern, string packageId)
    {
        if (packageId == null) return false;

        var regex = "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$";

        return Regex.IsMatch(packageId, regex, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }
}