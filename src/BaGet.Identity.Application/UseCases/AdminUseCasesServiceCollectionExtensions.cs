using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BaGet.Identity.Application;

/// <summary>Registration of the use cases of the account and administration screens.</summary>
public static class AdminUseCasesServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="PasswordConfirmation"/> and the use cases of the administration screens. Called by <c>AddBaGetAccounts</c> after its
    /// <c>Accounts:Enabled</c> check: the use cases depend on the account services, which exist only while accounts are enabled, so a JSON twin
    /// resolves its use case inside the action (after the screen gate), never in its constructor.
    /// </summary>
    public static IServiceCollection AddBaGetAdminUseCases(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.TryAddScoped<PasswordConfirmation>();

        services.TryAddScoped<AdminUsersUseCase>();
        services.TryAddScoped<AdminUserDetailUseCase>();
        services.TryAddScoped<AdminUserDeletionUseCase>();
        services.TryAddScoped<AdminPackagesUseCase>();
        services.TryAddScoped<AdminOrganizationsUseCase>();
        services.TryAddScoped<AdminSettingsUseCase>();
        services.TryAddScoped<AdminOperationsUseCase>();
<<<<<<< HEAD
        services.TryAddScoped<AdminGroupsUseCase>();
        services.TryAddScoped<AdminPermissionsUseCase>();
        services.TryAddScoped<AdminProjectsUseCase>();
        services.TryAddScoped<AdminAuditUseCase>();
        services.TryAddScoped<RepositoryFeedUrl>();
        services.TryAddScoped<AdminRepositoriesUseCase>();
        services.TryAddScoped<AdminRepositoryEditUseCase>();
        services.TryAddScoped<AdminRepositoryDeleteUseCase>();
=======
        services.TryAddScoped<RetentionUseCase>();
        services.TryAddScoped<ScanningUseCase>();
        services.TryAddScoped<ReplicationUseCase>();
>>>>>>> 33bbb0c (UI API wave 2, split-admin-policies: use cases and JSON twins for Retention, RetentionPolicy, RetentionHolds, Scanning, ScanRepository, ScanProject, Replication, ReplicationConfig)

        return services;
    }
}
