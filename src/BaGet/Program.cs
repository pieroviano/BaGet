using System;
using System.Threading;
using System.IO;
using System.Linq;
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
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.FileProviders.Physical;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BaGet;

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
            Description = "BaGet Extension: a light-weight NuGet service",

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

        commands.Command("account", account =>
        {
            account.Command("reset-password", reset =>
            {
                reset.Description = "Set a generated password for a user and switch off their two-factor authentication.";
                var userName = reset.Argument("user", "The user name.").IsRequired();
                var email = reset.Option("--email <address>", "Set this email address for the user and mark it confirmed. Needed for a user with no address when SMTP is configured.", CommandOptionType.SingleValue);
                reset.OnExecuteAsync(async cancellationToken =>
                {
                    using var scope = app.Services.CreateScope();
                    var recovery = scope.ServiceProvider.GetService<AccountRecoveryService>();
                    if (recovery == null)
                    {
                        Console.Error.WriteLine("Accounts are not enabled (\"Accounts:Enabled\").");
                        return 1;
                    }

                    var password = await recovery.ResetPasswordAsync(userName.Value, cancellationToken, email.Value());
                    if (password == null)
                    {
                        Console.Error.WriteLine($"There is no user named \"{userName.Value}\".");
                        return 1;
                    }

                    Console.WriteLine($"New password for {userName.Value}: {password}");
                    Console.WriteLine("Two-factor authentication is off for this user. Sign in and change the password now.");

                    var users = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<BaGetUser>>();
                    #pragma warning disable CS8604
                    var user = await users.FindByNameAsync(userName.Value);
                    #pragma warning restore CS8604
                    if (string.IsNullOrEmpty(user?.Email) && scope.ServiceProvider.GetRequiredService<IOptions<EmailOptions>>().Value.IsConfigured)
                    {
                        Console.WriteLine("This user has no email address, and sign-in needs a confirmed one while SMTP is configured. Run again with --email <address>.");
                    }

                    return 0;
                });
            });

            foreach (var (name, promote) in new[] { ("promote", true), ("demote", false) })
            {
                account.Command(name, change =>
                {
                    change.Description = promote
                        ? "Make a user an administrator, who may change the server's settings and promote other administrators."
                        : "Remove a user's administrator flag. The last administrator cannot be demoted.";
                    var userName = change.Argument("user", "The user name.").IsRequired();
                    change.OnExecuteAsync(async cancellationToken =>
                    {
                        using var scope = app.Services.CreateScope();
                        var administrators = scope.ServiceProvider.GetService<AdministratorService>();
                        if (administrators == null)
                        {
                            Console.Error.WriteLine("Accounts are not enabled (\"Accounts:Enabled\").");
                            return 1;
                        }

                        var result = promote
                            ? await administrators.PromoteAsync(userName.Value, "command line", cancellationToken)
                            : await administrators.DemoteAsync(userName.Value, "command line", cancellationToken);

                        switch (result)
                        {
                            case AdministratorChange.UserNotFound:
                                Console.Error.WriteLine($"There is no user named \"{userName.Value}\".");
                                return 1;
                            case AdministratorChange.LastAdministrator:
                                Console.Error.WriteLine("The last administrator cannot be demoted. Promote another user first.");
                                return 1;
                            case AdministratorChange.Unchanged:
                                Console.WriteLine(promote
                                    ? $"{userName.Value} is already an administrator."
                                    : $"{userName.Value} is not an administrator.");
                                return 0;
                            default:
                                Console.WriteLine(promote
                                    ? $"{userName.Value} is now an administrator."
                                    : $"{userName.Value} is no longer an administrator.");
                                return 0;
                        }
                    });
                });
            }
        });

        commands.Option("--urls", "The URLs that BaGet Extension should bind to.", CommandOptionType.SingleValue);

        commands.OnExecuteAsync(async cancellationToken =>
        {
            await app.RunMigrationsAsync(cancellationToken);
            await BootstrapAdministratorsAsync(app, cancellationToken);
            await app.RunAsync(cancellationToken);
        });

        await commands.ExecuteAsync(args);
    }

    /// <summary>
    /// Promote the users named by <c>Accounts:InitialAdministrators</c>; on a server with no users,
    /// create the default administrator unless <c>Accounts:CreateDefaultAdministrator</c> is off;
    /// and warn while it still has the default password. Does
    /// nothing while accounts are disabled, since then there is no <see cref="AdministratorService"/>.
    /// </summary>
    internal static async Task BootstrapAdministratorsAsync(WebApplication app, CancellationToken cancellationToken)
    {
        using var scope = app.Services.CreateScope();
        var administrators = scope.ServiceProvider.GetService<AdministratorService>();
        if (administrators == null) return;

        var options = scope.ServiceProvider.GetRequiredService<IOptions<AccountsOptions>>().Value;
        await administrators.PromoteInitialAdministratorsAsync(options.InitialAdministrators, cancellationToken);

        // Just created, it already said so; otherwise repeat the warning at every start until it is changed.
        var created = options.CreateDefaultAdministrator
            && await administrators.CreateDefaultAdministratorAsync(cancellationToken);
        if (!created && await administrators.HasDefaultPasswordAsync())
        {
            app.Logger.LogWarning(
                "The administrator {User} still has the default password. Sign in and change it.",
                AdministratorService.DefaultAdministratorUserName);
        }
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

        var overridesPath = ConfigureConfiguration(builder.Configuration, builder.Environment);
        ConfigureWebHost(builder.WebHost);
        ConfigureServices(builder.Services, builder.Configuration, overridesPath);

        var app = builder.Build();

        ConfigurePipeline(app);

        return app;
    }

    /// <summary>
    /// Apply BAGET_CONFIG_ROOT and add the Settings page's overrides file as a configuration
    /// source. It sits right after the last appsettings*.json source, so it beats those
    /// files but not user secrets, environment variables or the command line: an operator's
    /// explicit environment still wins.
    /// </summary>
    /// <returns>The overrides file's absolute path.</returns>
    private static string ConfigureConfiguration(ConfigurationManager configuration, IWebHostEnvironment environment)
    {
        var root = Environment.GetEnvironmentVariable("BAGET_CONFIG_ROOT");

        if (!string.IsNullOrEmpty(root))
        {
            configuration.SetBasePath(root);
        }

        var path = SettingsOverridesStore.ResolvePath(
            configuration,
            string.IsNullOrEmpty(root) ? environment.ContentRootPath : root);
        var directory = Path.GetDirectoryName(path);

        try
        {
            #pragma warning disable CS8604
            Directory.CreateDirectory(directory);
            #pragma warning restore CS8604
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // A read-only configuration root: the page then reports the error when saving.
        }

        // A file provider needs an existing directory; without one there is nothing to load,
        // and the host must still start.
        if (!Directory.Exists(directory)) return path;

        var source = new JsonConfigurationSource
        {
            Path = Path.GetFileName(path),
            Optional = true,
            ReloadOnChange = true,
            FileProvider = new PhysicalFileProvider(directory, ExclusionFilters.None),
        };

        var index = configuration.Sources.ToList().FindLastIndex(s => s is JsonConfigurationSource) + 1;
        configuration.Sources.Insert(index, source);

        return path;
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

    private static void ConfigureServices(IServiceCollection services, IConfiguration configuration, string overridesPath)
    {
        services.AddSingleton(new SettingsOverridesStore(overridesPath));

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
        #pragma warning disable CS8634
        services.AddScoped(DependencyInjectionExtensions.GetServiceFromProviders<IContext>);
        services.AddTransient(DependencyInjectionExtensions.GetServiceFromProviders<IStorageService>);
        services.AddTransient(DependencyInjectionExtensions.GetServiceFromProviders<IPackageDatabase>);
        services.AddTransient(DependencyInjectionExtensions.GetServiceFromProviders<ISearchService>);
        services.AddTransient(DependencyInjectionExtensions.GetServiceFromProviders<ISearchIndexer>);
        #pragma warning restore CS8634

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

        // Take the Settings page's startup snapshot now, so that "pending restart" is judged
        // against what this process started with.
        app.Services.GetRequiredService<SettingsService>();

        // Reject a request whose "Host" header is not one BaGet serves, before anything
        // reads it. Every absolute URL BaGet advertises is built from that header, so a
        // forged host would otherwise decide where clients are sent.
        //
        // The default builder happens to add this middleware as well, but BaGet cannot
        // rely on that: a host built with a slim or empty builder does not, and the check
        // is BaGet's to own. Running it twice is a repeated string comparison, which is
        // cheaper than depending on the builder's choices.
        app.UseHostFiltering();

        WarnIfNothingAuthenticates(app, options);
        WarnIfEmailLinksCannotBeTrusted(app);

        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
            app.UseStatusCodePages();
        }

        // The configured path base may be absent, "." meaning the root, or a path that
        // is missing its leading slash. Normalise it once so that startup cannot fail on
        // an unusable value and so both uses below agree on it.
        #pragma warning disable CS8602
        var pathBase = NormalizePathBase(options.PathBase);
        #pragma warning restore CS8602

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
        app.UseMaintenanceMiddleware();

        // WebApplication is itself an endpoint route builder, so the endpoints are mapped
        // onto it directly rather than inside a UseEndpoints callback.
        new BaGetEndpointBuilder().MapEndpoints(app);
    }

    /// <summary>
    /// Say once, at startup, that email was switched off because links in it could not be
    /// trusted: see <see cref="EmailLinkPolicy"/>.
    /// </summary>
    private static void WarnIfEmailLinksCannotBeTrusted(WebApplication app)
    {
        var accounts = app.Configuration.GetSection(nameof(BaGetOptions.Accounts)).Get<AccountsOptions>();
        var email = app.Configuration.GetSection(nameof(BaGetOptions.Email)).Get<EmailOptions>();
        if (accounts?.Enabled != true || email?.IsConfigured != true) return;

        var site = app.Configuration.GetSection(nameof(BaGetOptions.Site)).Get<SiteOptions>();
        if (!EmailLinkPolicy.CanBuildLinks(site?.PublicUrl))
        {
            app.Logger.LogWarning(EmailLinkPolicy.UnavailableWarning);
        }
    }

    /// <summary>
    /// Say once, at startup, that this server accepts packages from anyone. With no
    /// global API key and no accounts there is nothing to tell one caller from another,
    /// so every push, overwrite and delete is open to whoever can reach the server. That
    /// is a legitimate configuration on a private network and the shipped default, which
    /// is exactly why it must not be silent.
    /// </summary>
    private static void WarnIfNothingAuthenticates(WebApplication app, BaGetOptions? options)
    {
        #pragma warning disable CS8602
        if (options.IsReadOnlyMode) return;
        #pragma warning restore CS8602
        if (!string.IsNullOrEmpty(options.ApiKey)) return;

        var accounts = app.Configuration.GetSection(nameof(BaGetOptions.Accounts)).Get<AccountsOptions>();
        if (accounts?.Enabled == true)
        {
            // Accounts on and no global key: nothing authenticates a caller who sends no
            // key, by design - otherwise ownership could be bypassed - so a keyless push
            // (for example from CI written before accounts were on) gets a 401.
            app.Logger.LogWarning(
                "Accounts are enabled and no global \"ApiKey\" is configured, so keyless pushes "
                + "are refused with 401. Create a user API key on the \"API keys\" page and push "
                + "with it, or set \"ApiKey\".");
            return;
        }

        app.Logger.LogWarning(
            "No API key is configured and accounts are disabled, so anyone who can reach this "
            + "server may publish packages, claim package IDs and delete versions. Set \"ApiKey\", "
            + "or turn on \"Accounts:Enabled\", to require authentication.");
    }

    /// <summary>
    /// Turn a configured path base into one the request pipeline can use. An absent
    /// value, "." and "/" all mean "no path base"; anything else is given the leading
    /// slash that <see cref="PathString"/> requires.
    /// </summary>
    internal static PathString NormalizePathBase(string? value)
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
