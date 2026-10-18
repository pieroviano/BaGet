using System;
using System.Text.Json.Serialization;
using BaGet.Catalog;
using BaGet.Core;
using BaGet.Validation;
using BaGet.Web;
using Microsoft.AspNetCore.Mvc;
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
                // With nullable reference types on, MVC treats every non-nullable reference-type
                // property or parameter it binds as [Required]. Optional query values such as the
                // package list's "packageType" and "framework" would then fail validation whenever a
                // request leaves them out, and the page answer 400. Validation stays what the
                // attributes say it is; an explicit [Required] still applies.
                options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
            })
            .AddApplicationPart(typeof(PackageContentController).Assembly)
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            });

        services.AddRazorPages(options =>
        {
            // Add /packages route to the Index page
            options.Conventions.AddPageRoute("/Index", "/packages");
        });

        services.AddHttpContextAccessor();
        services.AddTransient<IUrlGenerator, BaGetUrlGenerator>();

        services.AddBaGetApplication(configureAction);
        services.AddBaGetCatalog();
        services.AddBaGetValidation();

        // Browser uploads are staged between their two requests; whatever nobody comes
        // back to confirm or cancel is collected here.
        services.AddHostedService<StagingSweepWorker>();

        return services;
    }
}