using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BaGet.Identity.Application;

/// <summary>Registration of what the Application project provides whether or not accounts are enabled.</summary>
public static class IdentityApplicationServiceCollectionExtensions
{
    /// <summary>
    /// Registers the screen gate. Called by <c>AddBaGetAccounts</c> (which stays the entry point of a host); the account services themselves are
    /// registered there only while accounts are enabled.
    /// </summary>
    public static IServiceCollection AddBaGetIdentityApplication(this IServiceCollection services)
    {
        services.TryAddScoped<ScreenGate>();

        return services;
    }
}
