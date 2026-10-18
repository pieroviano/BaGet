# Architecture

BaGet Extension is one ASP.NET Core (`net10.0`) host serving the NuGet V3 protocol, an optional V2 feed, a
symbol server and a Razor Pages UI. Storage, database and search are pluggable and chosen from configuration
at runtime.

## Projects

| Project | Role |
|---|---|
| `BaGet` | Host and CLI entry point: `Program.cs` (configuration, services, pipeline, commands) |
| `BaGet.Core` | Domain: entities, `IContext`, `IPackageDatabase`, storage, search, indexing, mirroring, options, settings |
| `BaGet.Web` | V3 controllers, Razor Pages, `BaGetEndpointBuilder`, `Routes`, `IUrlGenerator` |
| `BaGet.Web.V2` | Legacy V2 OData/Atom feed (`Feeds:EnableV2`) |
| `BaGet.Identity` | Accounts, API keys, ownership, administrators, account and admin pages (`Accounts:Enabled`) |
| `BaGet.Catalog` | V3 catalog resource, writer and rebuild |
| `BaGet.Validation` | Validation queue and worker, validators, repository signing (`Validation:Enabled`) |
| `BaGet.Database.{Sqlite,SqlServer,PostgreSql,MySql}` | One EF Core `IContext` and migrations per provider |
| `BaGet.Azure`, `BaGet.Aws`, `BaGet.Gcp`, `BaGet.Aliyun` | Cloud storage, Azure Table database and Azure Search |
| `BaGet.Protocol` | Standalone NuGet V3 **client** library (shipped as a package; used for mirroring) |

Dependencies point inwards: every project references `BaGet.Core`; only the host references everything.

## Startup — `src/BaGet/Program.cs`

| Step | What happens |
|---|---|
| `Main` | McMaster `CommandLineApplication`: `import downloads`, `catalog rebuild`, `account reset-password/promote/demote`; the default command runs migrations, promotes initial administrators and runs the host. Unknown arguments pass through to the host. |
| `ConfigureConfiguration` | `BAGET_CONFIG_ROOT` base path; inserts `appsettings.Overrides.json` (written by the Settings page) after `appsettings*.json`, before environment variables and the command line. |
| `ConfigureServices` | Options binding and validation (`AddBaGetOptions<T>`), web app, accounts, provider registration (`app.AddXyz()` per subsystem). |
| `AddBaGetWebApplication` | Registers every BaGet assembly with controllers or Razor Pages (`Web`, `Web.V2`, `Catalog`, `Validation`, `Identity`) as an MVC application part, so a host built without the Web SDK (e.g. a worker service calling `BaGet.Program.Main`) still serves them. |
| `ConfigurePipeline` | Middleware (below), then `BaGetEndpointBuilder.MapEndpoints`. |

Some configuration is read **while registering services** (e.g. `Accounts:Enabled`, `Email`, provider types),
so those settings need a restart; options read through `IOptionsSnapshot` apply live.

## Request pipeline

```
HostFiltering → (Dev: exception page, status code pages) → ForwardedHeaders → PathBase
→ StaticFiles → Routing → CORS → Authentication → Authorization → OperationCancelled → endpoints
```

- `AllowedHosts` drives host filtering: a request with any other `Host` header gets 400.
- Endpoints are named constants in `Routes.cs`; absolute URLs come only from `IUrlGenerator`.

## Runtime provider selection

Each subsystem is an `IProvider<TService>` chain. `GetServiceFromProviders<T>` asks every registered provider
and takes the first non-null answer; each provider decides from configuration.

| Subsystem | Key | Implementations |
|---|---|---|
| Database (`IContext`) | `Database:Type` | Sqlite, SqlServer, PostgreSql, MySql, AzureTable |
| Storage (`IStorageService`) | `Storage:Type` | FileSystem, AzureBlobStorage, AwsS3, GoogleCloud, AliyunOss |
| Search (`ISearchService`, `ISearchIndexer`) | `Search:Type` | Database, AzureSearch, Null |

New implementation: register a provider in `DependencyInjectionExtensions.Providers.cs` and an `app.AddXyz()`
in `Program.ConfigureBaGetApplication`.

## Endpoints

| Area | Paths |
|---|---|
| Service index | `v3/index.json` |
| Publish | `api/v2/package` (PUT push, DELETE delete/unlist, POST relist), `api/v2/symbol` |
| Content | `v3/package/{id}/index.json`, `…/{version}/{id}.{version}.nupkg`, `….nuspec`, `…/readme`, `…/icon` |
| Metadata | `v3/registration/{id}/index.json`, `v3/registration/{id}/{version}.json` |
| Search | `v3/search`, `v3/autocomplete`, `v3/dependents` |
| Deprecation, vulnerabilities | `v3/package/{id}/{version}/deprecation`, `…/vulnerabilities`, `v3/vulnerabilities/*` |
| Catalog | `v3/catalog/index.json`, `v3/catalog/page{n}.json`, `v3/catalog/data/{commitId}.json` |
| Validation, signing | `v3/package/{id}/{version}/validation`, `v3/repository-signatures/*` |
| Symbols | `api/download/symbols/…` |
| V2 feed | `api/v2`, `api/v2/$metadata`, `Packages()`, `FindPackagesById()`, `Search()` |
| UI | `/`, `/packages`, `/packages/{id}/{version}`, `/upload`, `/stats`, `/account/*`, `/admin/*`, `/profiles/*` |

## Push

```
PUT api/v2/package
 → IAuthenticationService.AuthenticatePrincipalAsync(api key) → BaGetPrincipal
 → IPackageAuthorizationService.AuthorizeAsync(principal, Push, id)
 → IPackageSubmissionService.SubmitAsync
     validation off → IPackageIndexingService.IndexAsync
     validation on  → stage + ValidationQueue → ValidationWorker → validators → IndexAsync (or reject)
```

`PackageIndexingService.IndexAsync`:

1. Read the `.nupkg`; reject unsafe entries; extract nuspec, readme and icon.
2. Existing version → `PackageAlreadyExists`, or replace it when `AllowPackageOverwrites` is on.
3. Save files: `packages/{id}/{version}/{id}.{version}.nupkg`, `{id}.nuspec`, `readme`, `icon` (lowercased).
4. Add the database row (`IPackageDatabase.AddAsync`).
5. `ISearchIndexer.IndexAsync`.
6. `PackageChangeNotifier` → every `IPackageChangeHandler` (e.g. `CatalogWriter`). Runs after the commit; a
   handler's failure is logged, never fails the push.

Browser uploads (`/upload`) use the same submission, with an extra staging step (`IPackageStagingService`)
between "stage" and "confirm"; `StagingSweepWorker` removes abandoned uploads.

## Download and mirroring

`PackageContentController` → `IPackageContentService` → `IPackageService` → `IPackageStorageService`.
With `Mirror:Enabled`, `PackageService` indexes a missing version from the upstream (`IUpstreamClient`, V3
through `BaGet.Protocol` or V2 legacy) before serving it; mirrored packages go straight to
`IPackageIndexingService`, never through validation.

Deletion follows `PackageDeletionBehavior`: `Unlist` (hide) or `HardDelete` (rows and files).
Administrators can always hard-delete from the UI.

## Authentication and authorization

| Piece | Behaviour |
|---|---|
| `BaGetPrincipal` | Who a key or session is: superuser flag, administrator flag, scopes, package glob |
| Accounts off | `ApiKeyAuthenticationService`: the global `ApiKey` (or none) is the superuser |
| Accounts on | `AccountAuthenticationService`: global key = superuser, else a hashed per-user key; `SessionPrincipalFactory` for browser sessions |
| `OwnershipAuthorizationService` | Ownership rules (new ID claimed by pusher, owned IDs need an owner, ownerless pre-account IDs only superuser/admin); administrators skip ownership but keep their key's scopes and glob |

Identity uses EF stores on whichever `IContext` the database provider resolved; the security stamp is checked
on every request.

## Data

`AbstractContext<T> : IdentityDbContext<BaGetUser>, IContext` holds the model: packages, dependencies,
target frameworks, package types, deprecations, vulnerabilities, validations, catalog entries, abuse reports,
users, organizations, owners, API keys. Each provider subclasses it and owns its migrations; a schema change
needs a migration in all four relational providers.

## Background work

| Hosted service | Project | Job |
|---|---|---|
| `ValidationWorker` | `BaGet.Validation` | Runs queued validations, then indexes or rejects |
| `StagingSweepWorker` | `BaGet.Web` | Deletes expired browser-upload staging |
| `BackgroundEmailService` | `BaGet.Identity` | Sends queued email (confirmation, reset, contact owners) |

## Settings

`AddBaGetOptions<T>` records each options type as a `SettingsSchemaRegistration`. `SettingsCatalog` reflects
over them (descriptions from XML docs, `[SensitiveSetting]`, `[RestartRequired]`); `SettingsService` validates
and writes `appsettings.Overrides.json` through `SettingsOverridesStore`; administrators edit it on
`/admin/settings`.

## Tests

| Project | Covers |
|---|---|
| `BaGet.Core.Tests` | Domain services, settings, storage, indexing (unit) |
| `BaGet.Tests` | Integration: `BaGetApplication : WebApplicationFactory<Program>` — real pipeline over HTTP, SQLite in a temp directory, `SystemTime` fixed to 2020-01-01Z |
| `BaGet.Protocol.Tests`, `BaGet.Protocol.Samples.Tests` | Client library |
| `BaGet.Web.Tests`, `BaGet.Azure.Tests` | Web helpers, Azure providers |

xUnit v3 on Microsoft.Testing.Platform; see [Debugging](debugging.md) for commands.
