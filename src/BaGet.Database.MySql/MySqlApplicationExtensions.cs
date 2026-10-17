using System;
using BaGet.Core;
using BaGet.Database.MySql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;

namespace BaGet;

public static class MySqlApplicationExtensions
{
    public static BaGetApplication AddMySqlDatabase(this BaGetApplication app)
    {
        app.Services.AddBaGetDbContextProvider<MySqlContext>("MySql", (provider, options) =>
        {
            var databaseOptions = provider.GetRequiredService<IOptionsSnapshot<DatabaseOptions>>();

            // Detecting the version connects to the server, and these options are built
            // whenever the contexts are enumerated - including by the design-time tools.
            // Prefer the configured version so that building options stays offline.
            var serverVersion = string.IsNullOrWhiteSpace(databaseOptions.Value.MySqlServerVersion)
                ? ServerVersion.AutoDetect(databaseOptions.Value.ConnectionString)
                : ServerVersion.Parse(databaseOptions.Value.MySqlServerVersion);

            options.UseMySql(databaseOptions.Value.ConnectionString, serverVersion);
        });

        return app;
    }

    public static BaGetApplication AddMySqlDatabase(
        this BaGetApplication app,
        Action<DatabaseOptions> configure)
    {
        app.AddMySqlDatabase();
        app.Services.Configure(configure);
        return app;
    }
}