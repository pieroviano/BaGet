using BaGet.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BaGet.Validation;

public static class ValidationServiceCollectionExtensions
{
    /// <summary>
    /// Always registered; whether a push is validated is read from "Validation:Enabled"
    /// on every push, so with it off the only difference is an idle worker.
    /// </summary>
    public static IServiceCollection AddBaGetValidation(this IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Transient<IPackageSubmissionService, ValidatingPackageSubmissionService>());

        services.TryAddSingleton<IValidationQueue, InMemoryValidationQueue>();
        services.TryAddScoped<PackageValidationRunner>();
        services.TryAddScoped<RepositorySigner>();
        services.TryAddScoped<IValidationAdministration, ValidationAdministration>();

        services.TryAddEnumerable(ServiceDescriptor.Scoped<IPackageValidator, PackageEntriesValidator>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IPackageValidator, PackageSignatureValidator>());
        services.TryAddEnumerable(ServiceDescriptor.Transient<IServiceIndexResourceProvider, RepositorySignaturesServiceIndexProvider>());

        services.AddHostedService<ValidationWorker>();

        return services;
    }
}