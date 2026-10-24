using BaGet.Core;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace BaGet.Ha;

/// <summary>Registers cluster mode (<c>Ha:Enabled</c>) in the host. Without it nothing here is registered and a node behaves as a single one.</summary>
internal static class HaServiceCollectionExtensions
{
    /// <summary>
    /// When <c>Ha:Enabled</c>: the Redis <see cref="IClusterStore"/> (created on first use), the cluster-wide <see cref="IJobLease"/>
    /// in place of the in-process one, and the data protection key ring in the database under one application name (the default is
    /// the content root path, which differs between nodes). Call after the BaGet services are registered, so the lease replaces theirs.
    /// </summary>
    public static IServiceCollection AddBaGetHa(this IServiceCollection services, IConfiguration configuration)
    {
        // Read now, like Accounts:Enabled: it decides what is registered.
        var ha = configuration.GetSection(nameof(BaGetOptions.Ha)).Get<HaOptions>() ?? new HaOptions();
        if (!ha.Enabled) return services;

        services.TryAddSingleton<IClusterStore>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<HaOptions>>().Value;

            return new RedisClusterStore(options.ConnectionString ?? string.Empty, options.KeyPrefix);
        });
        services.Replace(ServiceDescriptor.Singleton<IJobLease, ClusterJobLease>());

        services.AddDataProtection().SetApplicationName("BaGet");
        services.AddOptions<KeyManagementOptions>()
            .Configure<IServiceScopeFactory>((options, scopes) => options.XmlRepository = new DatabaseXmlRepository(scopes));

        return services;
    }
}
