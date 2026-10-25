using System.Threading.Tasks;
using BaGet.Telemetry;
using BaGet.Web;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;

namespace BaGet;

/// <summary>
/// Declared partial and public so that WebApplicationFactory can name it as the entry
/// point of the integration tests. The building blocks are extension methods in <c>Hosting/</c>.
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
        commands.AddBaGetCommands(app);
        commands.Option("--urls", "The URLs that BaGet Extension should bind to.", CommandOptionType.SingleValue);

        commands.OnExecuteAsync(async cancellationToken =>
        {
            await app.RunMigrationsAsync(cancellationToken);
            await app.BootstrapAdministratorsAsync(cancellationToken);
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

        var overridesPath = builder.Configuration.AddBaGetSettingsOverrides(builder.Environment);
        builder.AddBaGetLogging();
        builder.Services.AddBaGetTelemetry(builder.Configuration);
        builder.WebHost.ConfigureBaGetWebHost();
        builder.Services.AddBaGetHost(builder.Configuration, overridesPath);

        return builder.Build().UseBaGetPipeline();
    }
}
