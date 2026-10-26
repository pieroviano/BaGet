# Extension.BaGet.Web

The NuGet V3 server endpoints and controller/view UI of [BaGet Extension](https://pieroviano.github.io/BaGet/). Use it
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
| `AddBaGetWebApplication` | MVC (controllers and views; Razor Pages stay registered for a host's own pages), `IUrlGenerator`, core services, catalog, validation; registers every BaGet controller assembly as an application part, so hosts without the Web SDK work |
| `BaGetEndpointBuilder.MapEndpoints` | Maps the V3, V2, catalog and validation routes, each at `/` (default repository) and `/r/{repo}/` |
| `RepositoryActionRouteConvention` / `RepositoryActionFilter` | The controller actions marked `[RepositoryPage]` (list, package, statistics, upload, ...): the `/r/{repo}/` twin of every route, and the one repository check (`RepositoryPageGate`, which asks `IRepositoryAccess`) |
| `RepositoryUrlHelperFactory` | Keeps links inside the repository being browsed (adds the request's `repo` to links to repository pages, and resolves a legacy page name such as `/Admin/Users` to its controller action) |
| `Controllers/`, `Views/`, `Workflows/`, `ViewModels/` | The UI: `HomeController`, `PackageController`, `StatisticsController`, `ReportAbuseController`, `ApiCatalogController`, `LegalController`, `UploadController`, `NpmBrowseController`, `MavenBrowseController`, `DockerBrowseController`; each view is `Views/<Controller>/<Action>.cshtml`. `Views/Shared` holds the layout and partials (the layout and `_Avatar`/`_ScanFindings` are referenced by name, so a host copy in its own `Views/Shared` or `Pages/Shared` overrides them); a host overrides a view with its own `Views/<Controller>/<Action>.cshtml`. There is no `Pages/` folder (the old forwarding views for `Pages/...` paths were removed; reference `Views/Shared` or name the partial). The old page-model types (`IndexModel`, `PackageModel`, ...) and `RepositoryPageFilter` / `RepositoryPageRouteConvention` were removed (breaking for code that referenced them) |
| `Controllers/Api/` | The JSON twins of the screens, `GET /api/ui/...` (and `/api/ui/r/{repo}/...`): `[UiApi]` actions on `UiApiControllerBase` that call a use case of `Extension.BaGet.Web.Application` and answer a `ScreenResult<T>`. The view controller calls its twin in process and only renders the answer. So far `LegalUiApiController` and `StatisticsUiApiController` (the pilot), the browse twins (`home`/`packages`, package, report abuse, API catalog), `UploadUiApiController` (multipart stage, JSON submit/cancel), `NpmBrowseUiApiController`, `MavenBrowseUiApiController` and `DockerBrowseUiApiController`. A shown screen that carries its own status (an upload refused with 403, a package page that explains a missing package with 404) is answered with it. Authentication: cookie with the antiforgery header (`GET /api/ui/antiforgery`) or an API key |
| `RepositoryResolutionMiddleware` | Turns the `{repo}` route value into `IRepositoryContext`; unknown repository: `404`, never the default |
| `MaintenanceMiddleware` | Maps `MaintenanceException` (a write refused while `MaintenanceGate` in `Extension.BaGet.Core` is closed, e.g. during a database copy) to `503` + `Retry-After` |
| `IUrlGenerator` | Builds every absolute URL the server returns |
| `wwwroot` | Static files, served at `_content/BaGet.Web` |

## Links

[Documentation](https://pieroviano.github.io/BaGet/) · [Configuration](https://pieroviano.github.io/BaGet/configuration/) · [Server API](https://pieroviano.github.io/BaGet/advanced/api/) · [Source](https://github.com/pieroviano/BaGet)
