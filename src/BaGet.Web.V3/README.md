# Extension.BaGet.Web

The NuGet V3 server endpoints and Razor Pages UI of [BaGet Extension](https://pieroviano.github.io/BaGet/). Use it
to embed BaGet in your own ASP.NET Core host.

```powershell
dotnet add package Extension.BaGet.Web
```

## Usage

```csharp
builder.Services.AddBaGetWebApplication(app =>
{
    app.AddSqliteDatabase();   // Extension.BaGet.Database.Sqlite
    app.AddFileStorage();
});
builder.Services.AddBaGetAccounts(builder.Configuration); // Extension.BaGet.Identity

// Pick each subsystem from configuration (Database:Type, Storage:Type, Search:Type).
builder.Services.AddScoped(DependencyInjectionExtensions.GetServiceFromProviders<IContext>);
builder.Services.AddTransient(DependencyInjectionExtensions.GetServiceFromProviders<IStorageService>);
builder.Services.AddTransient(DependencyInjectionExtensions.GetServiceFromProviders<IPackageDatabase>);
builder.Services.AddTransient(DependencyInjectionExtensions.GetServiceFromProviders<ISearchService>);
builder.Services.AddTransient(DependencyInjectionExtensions.GetServiceFromProviders<ISearchIndexer>);

var app = builder.Build();
app.UseStaticFiles();
app.UseRouting();
app.UseRepositoryResolution();     // /r/{repo}/ selects the repository (404 if unknown); required, else /r/x would serve the default one
app.UseAuthentication();
app.UseAuthorization();
app.UseOperationCancelledMiddleware();
app.UseMaintenanceMiddleware();   // 503 + Retry-After for writes refused by a closed MaintenanceGate
new BaGetEndpointBuilder().MapEndpoints(app);
```

Run migrations with `host.RunMigrationsAsync()` (honours `RunMigrationsAtStartup`). Hosts on the generic host can call
`hostBuilder.UseBaGet(configure)` instead of `AddBaGetWebApplication`. The complete reference host is
`Program.BuildApplication` in `Extension.BaGet` (`AddBaGetHost` / `UseBaGetPipeline`, under `src/BaGet/Hosting`).

| Item | Role |
|---|---|
| `AddBaGetWebApplication` | MVC, Razor Pages, `IUrlGenerator`, core services, catalog, validation; registers every BaGet controller assembly as an application part, so hosts without the Web SDK work |
| `BaGetEndpointBuilder.MapEndpoints` | Maps the V3, V2, catalog and validation routes, each at `/` (default repository) and `/r/{repo}/` |
| `RepositoryPageRouteConvention` / `RepositoryUrlHelperFactory` | Serve the repository pages (list, package, statistics, upload, ...) also at `/r/{repo}/…` and keep their links inside the repository; `RepositoryPageFilter` asks `IRepositoryAccess` |
| `RepositoryActionRouteConvention` / `RepositoryActionFilter` | The same for the controller actions that replace Razor Pages (`[RepositoryPage]`): the `/r/{repo}/` twin of every route, and the one repository check (`RepositoryPageGate`, shared with `RepositoryPageFilter`) |
| `LegalController`, `Views/` | The first pages served by a controller (`/terms`, `/privacy`); `Views/Shared` holds the layout and partials every page and view uses (the old `Pages/Shared` paths forward to it; the layout and `_Avatar`/`_ScanFindings` are referenced by name so a host copy overrides them); a host overrides a view with its own `Views/<Controller>/<Action>.cshtml` |
| `RepositoryResolutionMiddleware` | Turns the `{repo}` route value into `IRepositoryContext`; unknown repository: `404`, never the default |
| `MaintenanceMiddleware` | Maps `MaintenanceException` (a write refused while `MaintenanceGate` in `Extension.BaGet.Core` is closed, e.g. during a database copy) to `503` + `Retry-After` |
| `IUrlGenerator` | Builds every absolute URL the server returns |
| `wwwroot` | Static files, served at `_content/BaGet.Web` |

## Links

[Documentation](https://pieroviano.github.io/BaGet/) · [Configuration](https://pieroviano.github.io/BaGet/configuration/) · [Server API](https://pieroviano.github.io/BaGet/advanced/api/) · [Source](https://github.com/pieroviano/BaGet)
