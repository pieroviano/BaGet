using System;
using System.Linq;
using System.Text.Json.Serialization;
using BaGet.Catalog;
using BaGet.Core;
using BaGet.Identity;
using BaGet.Validation;
using BaGet.Web;
using BaGet.Web.V2;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.DependencyInjection;

namespace BaGet;

public static class IServiceCollectionExtensions
{
    public static IServiceCollection AddBaGetWebApplication(
        this IServiceCollection services,
        Action<BaGetApplication> configureAction)
    {
        services
            .AddRouting(options => options.LowercaseUrls = true)
            .AddControllers(options =>
            {
                // The one read check of every repository page (404 when the caller may not see the repository).
                options.Filters.Add<RepositoryPageFilter>();

                // The read check of every protocol route (401 for anonymous callers, 404 without Read access).
                options.Filters.Add<RepositoryReadFilter>();

                // A refusal by the vulnerability scanning policy is answered 403 with its reason, in the shape of the client's protocol.
                options.Filters.Add<ScanBlockedFilter>();

                // With nullable reference types on, MVC treats every non-nullable reference-type
                // property or parameter it binds as [Required]. Optional query values such as the
                // package list's "packageType" and "framework" would then fail validation whenever a
                // request leaves them out, and the page answer 400. Validation stays what the
                // attributes say it is; an explicit [Required] still applies.
                options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
            })
            .AddBaGetApplicationParts()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            });

        services.AddRazorPages(options =>
        {
            // Add /packages route to the Index page
            options.Conventions.AddPageRoute("/Index", "/packages");

            // Every repository page also at /r/{repo}/� (must come after the alias so it gets a twin as well).
            options.Conventions.Add(new RepositoryPageRouteConvention());
        });

        // Page links stay inside the repository being browsed (see RepositoryUrlHelper).
        services.AddSingleton<Microsoft.AspNetCore.Mvc.Routing.IUrlHelperFactory, RepositoryUrlHelperFactory>();

        services.AddHttpContextAccessor();
        services.AddScoped<RequestPrincipalResolver>();
        services.AddScoped<OciCallerResolver>();
        services.AddScoped<OciAuthorizationFilter>();
        services.AddTransient<IUrlGenerator, BaGetUrlGenerator>();

        services.AddBaGetApplication(configureAction);
        services.AddBaGetCatalog();
        services.AddBaGetValidation();

        // Browser uploads are staged between their two requests; whatever nobody comes
        // back to confirm or cancel is collected here.
        services.AddHostedService<StagingSweepWorker>();

        return services;
    }

    /// <summary>
    /// Register every BaGet assembly that holds controllers or Razor Pages. MVC otherwise finds
    /// them only through the [ApplicationPart] attributes the Web SDK generates into the entry
    /// assembly, which a host built with another SDK (a worker service, a test runner) lacks:
    /// accounts, the catalog, validation and the V2 feed would then answer 404.
    /// </summary>
    internal static IMvcBuilder AddBaGetApplicationParts(this IMvcBuilder builder)
    {
        var assemblies = new[]
        {
            typeof(PackageContentController).Assembly,
            typeof(V2FeedController).Assembly,
            typeof(CatalogController).Assembly,
            typeof(RepositorySignaturesController).Assembly,
            typeof(AccountsServiceCollectionExtensions).Assembly,
        };

        return builder.ConfigureApplicationPartManager(manager =>
        {
            foreach (var assembly in assemblies)
            {
                // The entry assembly may already have contributed it.
                var name = assembly.GetName().Name;
                if (manager.ApplicationParts.Any(part => part.Name == name)) continue;

                var factory = ApplicationPartFactory.GetApplicationPartFactory(assembly);
                foreach (var part in factory.GetApplicationParts(assembly))
                {
                    manager.ApplicationParts.Add(part);
                }
            }
        });
    }
}