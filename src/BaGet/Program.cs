using System;
using System.Threading.Tasks;
using BaGet.Catalog;
using BaGet.Core;
using BaGet.Identity;
using BaGet.Web;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HostFiltering;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.Razor.RuntimeCompilation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace BaGet
{
    /// <summary>
    /// Declared partial and public so that WebApplicationFactory can name it as the entry
    /// point of the integration tests.
    /// </summary>
    public partial class Program
    {
        public static async Task Main(string[] args)
        {
            var app = BuildApplication(args);

            if (!app.ValidateStartupOptions())
            {
                return;
            }

            var commands = new CommandLineApplication
            {
                Name = "baget",
                Description = "A light-weight NuGet service",

                // The host consumes arguments of its own - "--environment",
                // "--contentRoot", "--applicationName" and the rest - and they reach this
                // parser too. Rejecting them would make a legitimate invocation such as
                // "baget --environment Staging" fail before the service starts.
                UnrecognizedArgumentHandling = UnrecognizedArgumentHandling.CollectAndContinue,
            };

            commands.HelpOption(inherited: true);

            commands.Command("import", import =>
            {
                import.Command("downloads", downloads =>
                {
                    downloads.OnExecuteAsync(async cancellationToken =>
                    {
                        using (var scope = app.Services.CreateScope())
                        {
                            var importer = scope.ServiceProvider.GetRequiredService<DownloadsImporter>();

                            await importer.ImportAsync(cancellationToken);
                        }
                    });
                });
            });

            commands.Command("catalog", catalog =>
            {
                catalog.Command("rebuild", rebuild =>
                {
                    rebuild.Description = "Append the catalog entries missing for the packages in the database.";
                    rebuild.OnExecuteAsync(async cancellationToken =>
                    {
                        using (var scope = app.Services.CreateScope())
                        {
                            var rebuilder = scope.ServiceProvider.GetRequiredService<CatalogRebuilder>();

                            await rebuilder.RebuildAsync(cancellationToken);
                        }
                    });
                });
            });

            commands.Option("--urls", "The URLs that BaGet should bind to.", CommandOptionType.SingleValue);

            commands.OnExecuteAsync(async cancellationToken =>
            {
                await app.RunMigrationsAsync(cancellationToken);
                await app.RunAsync(cancellationToken);
            });

            await commands.ExecuteAsync(args);
        }

        /// <summary>
        /// Build the application: configuration, services and the request pipeline.
        /// </summary>
        /// <remarks>
        /// The integration tests reach the host through this, by way of
        /// WebApplicationFactory, which runs <see cref="Main"/> and takes the application as
        /// it is built. The command line wiring in <see cref="Main"/> therefore runs only
        /// when BaGet is started for real.
        /// </remarks>
        public static WebApplication BuildApplication(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            ConfigureConfiguration(builder.Configuration);
            ConfigureWebHost(builder.WebHost);
            ConfigureServices(builder.Services, builder.Configuration);

            var app = builder.Build();

            ConfigurePipeline(app);

            return app;
        }

        private static void ConfigureConfiguration(IConfigurationBuilder configuration)
        {
            var root = Environment.GetEnvironmentVariable("BAGET_CONFIG_ROOT");

            if (!string.IsNullOrEmpty(root))
            {
                configuration.SetBasePath(root);
            }
        }

        private static void ConfigureWebHost(IWebHostBuilder web)
        {
            web.ConfigureKestrel(options =>
            {
                // Remove the upload limit from Kestrel. If needed, an upload limit can
                // be enforced by a reverse proxy server, like IIS.
                options.Limits.MaxRequestBodySize = null;
            });
        }

        private static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
            // Ideally we'd use:
            //
            //       services.ConfigureOptions<ConfigureBaGetOptions>();
            //
            //       However, "ConfigureOptions" doesn't register validations as expected.
            //       We'll instead register all these configurations manually.
            // See: https://github.com/dotnet/runtime/issues/38491
            services.AddTransient<IConfigureOptions<CorsOptions>, ConfigureBaGetOptions>();
            services.AddTransient<IConfigureOptions<FormOptions>, ConfigureBaGetOptions>();
            services.AddTransient<IConfigureOptions<ForwardedHeadersOptions>, ConfigureBaGetOptions>();
            services.AddTransient<IConfigureOptions<HostFilteringOptions>, ConfigureBaGetOptions>();
            services.AddTransient<IConfigureOptions<IISServerOptions>, ConfigureBaGetOptions>();
            services.AddTransient<IValidateOptions<BaGetOptions>, ConfigureBaGetOptions>();

            services.AddBaGetOptions<IISServerOptions>(nameof(IISServerOptions));
            services.AddBaGetWebApplication(ConfigureBaGetApplication);
            services.AddBaGetAccounts(configuration);

            // You can swap between implementations of subsystems like storage and search using BaGet's configuration.
            // Each subsystem's implementation has a provider that reads the configuration to determine if it should be
            // activated. BaGet will run through all its providers until it finds one that is active.
            services.AddScoped(DependencyInjectionExtensions.GetServiceFromProviders<IContext>);
            services.AddTransient(DependencyInjectionExtensions.GetServiceFromProviders<IStorageService>);
            services.AddTransient(DependencyInjectionExtensions.GetServiceFromProviders<IPackageDatabase>);
            services.AddTransient(DependencyInjectionExtensions.GetServiceFromProviders<ISearchService>);
            services.AddTransient(DependencyInjectionExtensions.GetServiceFromProviders<ISearchIndexer>);

            services.AddSingleton<IConfigureOptions<MvcRazorRuntimeCompilationOptions>, ConfigureRazorRuntimeCompilation>();

            services.AddCors();
        }

        private static void ConfigureBaGetApplication(BaGetApplication app)
        {
            // Add database providers.
            app.AddAzureTableDatabase();
            app.AddMySqlDatabase();
            app.AddPostgreSqlDatabase();
            app.AddSqliteDatabase();
            app.AddSqlServerDatabase();

            // Add storage providers.
            app.AddFileStorage();
            app.AddAliyunOssStorage();
            app.AddAwsS3Storage();
            app.AddAzureBlobStorage();
            app.AddGoogleCloudStorage();

            // Add search providers.
            app.AddAzureSearch();
        }

        private static void ConfigurePipeline(WebApplication app)
        {
            var options = app.Configuration.Get<BaGetOptions>();

            // Reject a request whose "Host" header is not one BaGet serves, before anything
            // reads it. Every absolute URL BaGet advertises is built from that header, so a
            // forged host would otherwise decide where clients are sent.
            //
            // The default builder happens to add this middleware as well, but BaGet cannot
            // rely on that: a host built with a slim or empty builder does not, and the check
            // is BaGet's to own. Running it twice is a repeated string comparison, which is
            // cheaper than depending on the builder's choices.
            app.UseHostFiltering();

            if (app.Environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseStatusCodePages();
            }

            // The configured path base may be absent, "." meaning the root, or a path that
            // is missing its leading slash. Normalise it once so that startup cannot fail on
            // an unusable value and so both uses below agree on it.
            var pathBase = NormalizePathBase(options.PathBase);

            app.UseForwardedHeaders();
            app.UsePathBase(pathBase);

            // UsePathBase only sets the request's path base if the incoming path starts
            // with it. A reverse proxy may instead strip the path base before forwarding,
            // leaving nothing for UsePathBase to match, and BaGet would then advertise
            // URLs that bypass the proxy. Apply the configured path base in that case.
            if (pathBase.HasValue)
            {
                app.Use((context, next) =>
                {
                    if (!context.Request.PathBase.HasValue)
                    {
                        context.Request.PathBase = pathBase;
                    }

                    return next();
                });
            }

            app.UseStaticFiles();
            app.UseRouting();

            app.UseCors(ConfigureBaGetOptions.CorsPolicy);
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseOperationCancelledMiddleware();

            // WebApplication is itself an endpoint route builder, so the endpoints are mapped
            // onto it directly rather than inside a UseEndpoints callback.
            new BaGetEndpointBuilder().MapEndpoints(app);
        }

        /// <summary>
        /// Turn a configured path base into one the request pipeline can use. An absent
        /// value, "." and "/" all mean "no path base"; anything else is given the leading
        /// slash that <see cref="PathString"/> requires.
        /// </summary>
        internal static PathString NormalizePathBase(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return PathString.Empty;
            }

            var trimmed = value.Trim().TrimEnd('/');
            if (trimmed.Length == 0 || trimmed == ".")
            {
                return PathString.Empty;
            }

            return new PathString(trimmed.StartsWith("/") ? trimmed : "/" + trimmed);
        }
    }
}
