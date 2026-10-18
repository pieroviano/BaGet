using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.EntityFrameworkCore;

namespace BaGet.Identity;

/// <summary>
/// Creates, lists and revokes users' API keys. A key is shown once, when it is created;
/// only its hash is stored.
/// </summary>
public class ApiKeyService
{
    /// <summary>
    /// Marks a BaGet key, so a leaked one is recognisable by secret scanners.
    /// </summary>
    internal const string KeyPrefix = "bgt_";

    private readonly IContext _context;
    private readonly SystemTime _time;

    public ApiKeyService(IContext context, SystemTime time)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _time = time ?? throw new ArgumentNullException(nameof(time));
    }

    /// <summary>
    /// Create a key and return it in plain text. It cannot be recovered afterwards.
    /// </summary>
    public async Task<string> CreateAsync(
        string? userId,
        string? description,
        IReadOnlyList<string>? scopes,
        string packageGlobPattern,
        DateTime? expires,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(userId)) throw new ArgumentException("A user is required.", nameof(userId));

        var unknown = scopes?.FirstOrDefault(s => !ApiKeyScopes.All.Contains(s, StringComparer.Ordinal));
        if (scopes == null || scopes.Count == 0 || unknown != null)
        {
            throw new ArgumentException($"Scopes must be some of: {string.Join(", ", ApiKeyScopes.All)}.", nameof(scopes));
        }

        var key = KeyPrefix + Base64Url(RandomNumberGenerator.GetBytes(32));

        _context.ApiKeys?.Add(new ApiKey
        {
            UserId = userId,
            Description = description,
            HashedKey = Hash(key),
            Scopes = scopes.Distinct(StringComparer.Ordinal).ToArray(),
            PackageGlobPattern = string.IsNullOrWhiteSpace(packageGlobPattern) ? "*" : packageGlobPattern.Trim(),
            Created = _time.UtcNow,
            Expires = expires?.ToUniversalTime(),
        });

        await _context.SaveChangesAsync(cancellationToken);

        return key;
    }

    public async Task<IReadOnlyList<ApiKey>> ListAsync(string? userId, CancellationToken cancellationToken)
    {
        #pragma warning disable CS8604
        return await _context.ApiKeys
            .AsNoTracking()
            .Where(k => k.UserId == userId)
            .OrderBy(k => k.Key)
            .ToListAsync(cancellationToken);
        #pragma warning restore CS8604
    }

    /// <summary>
    /// Revoke one of the user's keys. False if the user has no such key.
    /// </summary>
    public async Task<bool> RevokeAsync(string? userId, int key, CancellationToken cancellationToken)
    {
        #pragma warning disable CS8602
        var apiKey = await _context.ApiKeys?.FirstOrDefaultAsync(k => k.Key == key && k.UserId == userId, cancellationToken);
        #pragma warning restore CS8602
        if (apiKey == null) return false;

        _context.ApiKeys?.Remove(apiKey);
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }

    /// <summary>
    /// The stored key that matches, if it exists and has not expired.
    /// </summary>
    public async Task<ApiKey?> FindValidOrNullAsync(string? key, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(key) || !key.StartsWith(KeyPrefix, StringComparison.Ordinal)) return null;

        var hash = Hash(key);
        #pragma warning disable CS8604
        var apiKey = await _context.ApiKeys
            .AsNoTracking()
            .FirstOrDefaultAsync(k => k.HashedKey == hash, cancellationToken);
        #pragma warning restore CS8604

        if (apiKey == null) return null;
        if (apiKey.Expires.HasValue && DateTime.SpecifyKind(apiKey.Expires.Value, DateTimeKind.Utc) <= _time.UtcNow) return null;

        return apiKey;
    }

    /// <summary>
    /// A key is 256 random bits, so a fast unsalted hash is enough: there is nothing
    /// to brute force, and the lookup by hash must be exact.
    /// </summary>
    internal static string Hash(string key)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(key));

        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string Base64Url(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}