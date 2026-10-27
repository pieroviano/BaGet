using System.Threading.Tasks;
using BaGet;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using BaGet.Web.V3.Extensions;

namespace BaGetWebApplication;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddBaGetWebApplication(app =>
        {
            // Use SQLite as BaGet's database and store packages on the local file system.
            app.AddSqliteDatabase();
            app.AddFileStorage();
        });

        // The web UI (screens and views); leave it out for a host that serves only the APIs.
        builder.Services.AddBaGetFrontEnd();

        var app = builder.Build();

        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseStaticFiles();
        app.UseRouting();

        // Add BaGet's endpoints.
        new BaGetEndpointBuilder().MapEndpoints(app);

        await app.RunMigrationsAsync();
        await app.RunAsync();
    }
}
