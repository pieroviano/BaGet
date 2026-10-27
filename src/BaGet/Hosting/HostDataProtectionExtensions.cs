using System.IO;
using BaGet.Core;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BaGet;

/// <summary>The data protection key ring of a single node.</summary>
internal static class HostDataProtectionExtensions
{
    /// <summary>The folder name used next to the settings overrides file when <c>DataProtection:KeyPath</c> is not set.</summary>
    public const string DefaultFolderName = "DataProtection-Keys";

    /// <summary>
    /// Persist the key ring to a folder (<c>DataProtection:KeyPath</c>, default <see cref="DefaultFolderName"/> next to the overrides
    /// file), so a recreated container or an IIS pool without a user profile keeps the keys that protect stored secrets, sessions and
    /// tokens. Does nothing in cluster mode, where <c>AddBaGetHa</c> keeps the ring in the database. The application name is left at its
    /// default on purpose: changing it would change the purpose of everything already protected.
    /// </summary>
    public static IServiceCollection AddBaGetDataProtection(this IServiceCollection services, IConfiguration configuration, string overridesPath)
    {
        var ha = configuration.GetSection(nameof(BaGetOptions.Ha)).Get<HaOptions>();
        if (ha is { Enabled: true }) return services;

        var keyPath = configuration.GetSection(nameof(BaGetOptions.DataProtection)).Get<DataProtectionKeyOptions>()?.KeyPath;
        var folder = ResolveKeyFolder(keyPath, overridesPath);

        services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(folder));

        return services;
    }

    /// <summary>The absolute key folder: <paramref name="keyPath"/> (relative to the overrides file's folder), else the default beside the file.</summary>
    internal static string ResolveKeyFolder(string? keyPath, string overridesPath)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(overridesPath)) ?? Directory.GetCurrentDirectory();

        return string.IsNullOrWhiteSpace(keyPath)
            ? Path.Combine(directory, DefaultFolderName)
            : Path.GetFullPath(keyPath.Trim(), directory);
    }
}
