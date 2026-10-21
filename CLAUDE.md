# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

A fork of [loic-sharma/BaGet](https://github.com/loic-sharma/BaGet), a lightweight NuGet and symbol
server. The fork targets `net10.0` and has diverged substantially (accounts, V2 feed, validation,
catalog). Some work here is upstreamed as PRs against `loic-sharma/BaGet`.

## Build and test

**Always pass `-p:GeneratePackageOnBuild=false`.** `src/Directory.Build.props` sets
`GeneratePackageOnBuild=true` with `PackageOutputPath=$(SolutionDir)Packages\`, and `Packages` is a
**symlink to `D:\Starb\Packages`**, a shared NuGet feed. A plain build publishes packages into it, and
an `AfterTargets="Pack"` target deletes `%userprofile%\.nuget\Packages\Extension.<project>` (when `...\Packages\<project>` exists).

| Task | Command |
|---|---|
| Build | `dotnet build BaGet.sln -p:GeneratePackageOnBuild=false` |
| Test (all) | `dotnet test BaGet.sln -p:GeneratePackageOnBuild=false` |
| Test (one project) | `dotnet test tests/BaGet.Core.Tests/BaGet.Core.Tests.csproj -p:GeneratePackageOnBuild=false` |
| Test (one class or method) | add `--filter-class "*UploadPageTests"` or `--filter-method "*Name"` (xUnit v3 filters; VSTest `--filter` is gone) |
| Run | `cd src/BaGet && dotnet run -p:GeneratePackageOnBuild=false` |

Baseline: **2057 tests** across `BaGet.Core.Tests` (556), `BaGet.Tests` (1195),
`BaGet.Protocol.Tests` (118), `BaGet.Azure.Tests` (105), `BaGet.Web.Tests` (53),
`BaGet.Protocol.Samples.Tests` (8), `BaGet.Database.Tests` (16), `BaGet.Telemetry.Tests` (6). On a machine with a local Jaeger
(see the "Enabled local Jaeger" commit) 13 telemetry tests (`TelemetryExportTests`, `TelemetrySettingsPageTests`,
`TelemetryE2ETests`) fail on a clean checkout too; not investigated. Checked at `e213fe0` without Jaeger: 11 of them (`TelemetryExportTests` x3,
`TelemetrySettingsPageTests` x8) fail on a clean checkout, and `DatabaseCopyTests.ACopyEmitsADatabaseCopySpan...` fails in the full run (passes alone). No external services needed; integration tests
use SQLite and a temporary directory.

`BaGet.Database.Tests` copies between every ordered pair of SQLite / SQL Server / PostgreSQL / MySQL with
`DatabaseCopyService` on Testcontainers servers (`TestcontainersPackageVersion` in `tests/Directory.Build.props`;
images `mssql/server:2022-latest`, `postgres:17`, `mysql:8.4`, pulled on first run). Without a Docker daemon the 16
tests **skip** (`Assert.Skip`), so the suite stays green; with one they take ~2 minutes. 4 of them
(`EngineUpgradeTests`) are the B7 upgrade on each engine: `PreRepositoryDatabase` (also linked into `BaGet.Tests`) migrates
to the last migration before `AddRepositories` (looked up per provider, timestamps differ), fills the old schema with raw
parameterised SQL, then the current migrations must put every row in repository key 1 and allow the same ID/version in key 2.

`BaGet.Telemetry.Tests` (shares `BaGetApplication` etc. from `BaGet.Tests` as linked files; `Containers.cs`) runs a real host against
`jaegertracing/all-in-one:1.76.0` (traces; query API read back) and `otel/opentelemetry-collector:0.140.0` (debug exporter, read back
from the container log) and skips without Docker (~1 minute with). It turns telemetry on through `SettingsService` (no restart) and
covers: trace visible in Jaeger within 10 s (Grpc and HttpProtobuf; ~1-2 s measured), a dead endpoint (requests unaffected, status
failing, restore resumes), Jaeger rejecting metrics/logs (`Unimplemented`; status fails, never silent), metrics/logs only while their
own endpoint is set, and a stalled collector (bounded queue drops spans, memory flat, median push latency within 5 %, off/on blocks
interleaved). `Otlp:Protocol` applies to **every** signal, so a logs endpoint must match it (gRPC port with `Grpc`).

Tests are **xUnit v3** (`xunit.v3`, version lever `XUnitPackageVersion` in `tests/Directory.Build.props`), so test
projects are executables (`OutputType=Exe`) running on **Microsoft.Testing.Platform**: `global.json`
`"test": { "runner": "Microsoft.Testing.Platform" }` opts `dotnet test` in, and there is no VSTest adapter,
`Microsoft.NET.Test.Sdk` or coverlet collector. `ITestOutputHelper` lives in `Xunit`, not `Xunit.Abstractions`.

`build.ps1` wraps clean/restore/build, with `-Run` and `-Publish`.

Two gotchas when running locally:
- It listens on **6060**: `Kestrel:Endpoints:Http:Url` in `appsettings.json` outranks `--urls` and
  `ASPNETCORE_URLS`, so they are silently ineffective (override with `Kestrel__Endpoints__Http__Url`).
- `nuget.config` lists an untracked `Packages` folder as a source; create it (`mkdir Packages`) on a clean checkout or restore fails with NU1301.
- `dotnet run` makes the SDK rewrite `src/BaGet/Properties/launchSettings.json` (it drops the IIS
  Express profile). Revert it rather than committing it.

## Architecture

| Project | Role |
|---|---|
| `BaGet.Core` | Domain: entities, repositories, `IPackageDatabase`, `IStorageService`, `ISearchService`, indexing, mirroring, options |
| `BaGet.Web` | V3 endpoints, Razor Pages, `BaGetEndpointBuilder`, `IUrlGenerator` |
| `BaGet.Web.V2` | Legacy V2 OData/Atom feed, gated by `Feeds:EnableV2` |
| `BaGet.Protocol` | Standalone **client** for NuGet V3 (also shipped as a package) |
| `BaGet.Identity` | Accounts, API keys, ownership; gated by `Accounts:Enabled` |
| `BaGet.Catalog`, `BaGet.Validation` | Catalog resources/rebuild, validation pipeline |
| `BaGet.Database.{Sqlite,SqlServer,PostgreSql,MySql}` | One `IContext` + migrations per provider |
| `BaGet.Azure`, `BaGet.Aws`, `BaGet.Gcp`, `BaGet.Aliyun` | Cloud storage/search/table implementations |
| `BaGet` | Host and CLI entry point |

### Subsystems are chosen at runtime, not at registration

Storage, database, search and indexing are each resolved through `IProvider<TService>` chains:
`DependencyInjectionExtensions.GetServiceFromProviders<T>` walks every registered provider and takes
the first non-null, each provider deciding from configuration (`Database:Type`, `Storage:Type`,
`Search:Type`). To add an implementation, register a provider in
`src/BaGet.Core/Extensions/DependencyInjectionExtensions.Providers.cs` and an
`app.AddXyz()` in `Program.ConfigureBaGetApplication` — do not resolve concrete types directly.

### Entry point: minimal hosting

`src/BaGet/Program.cs` holds everything (there is no `Startup`): `BuildApplication` runs
`ConfigureConfiguration` / `ConfigureWebHost` / `ConfigureServices`, then `ConfigurePipeline`.
`Main` wraps it in a McMaster `CommandLineApplication` (`import downloads [-r repo]`, `catalog rebuild [-r repo]`, `account reset-password|promote|demote <user>`), which
is configured to **collect unrecognised arguments** so host arguments like `--environment` pass
through.

**Settings overrides file.** `ConfigureConfiguration` inserts `appsettings.Overrides.json` (path from
`SettingsOverridesPath`, default in the content root / `BAGET_CONFIG_ROOT`) after the last `appsettings*.json`
source and before environment variables and the command line. `SettingsService` /
`SettingsOverridesStore` (`BaGet.Core`) write it; `AddBaGetOptions` records every options type as a
`SettingsSchemaRegistration`, and `[RestartRequired]` / `[SensitiveSetting]` on options classify them.
`[AllowedSettingValues(...)]` (string settings; `Database/Storage/Search:Type`) makes the catalog emit an `Enum`
choice with those values and is also what `ConfigureBaGetOptions` validates against (one list);
`[SettingExample(dependsOn, whenValue, example)]` gives a setting per-value examples (`SettingDescriptor.Examples`),
rendered by `_SettingField` as a placeholder/help line (never the value) via `SettingPresentation`. Examples travel in one `data-examples` JSON attribute (no per-value attribute names); the catalog throws `InvalidOperationException` for duplicate `whenValue`s (case-insensitive) or mixed `dependsOn` on one property. Tests
set `SettingsOverridesPath` to the temp directory; `UseSetting` values arrive through the *last* (chained host)
provider, so they shadow saved overrides — integration tests that save a setting must use keys the harness does
not set.

**Registration-time configuration reads are load-bearing.** `AddBaGetAccounts` decides from
`Accounts:Enabled` *while registering services*, so configuration must be in place before
`BuildApplication` runs. This is why `tests/BaGet.Tests/Support/BaGetApplication.cs` supplies settings
with `builder.UseSetting(...)` and **not** `ConfigureAppConfiguration`, which is merged too late.
Anything read through `IOptions`/`IOptionsSnapshot` is unaffected.

**Read-only gate.** `MaintenanceGate` (`BaGet.Core`, singleton, in memory) closes every database write:
`MaintenanceSaveChangesInterceptor`, added in `AddBaGetDbContextProvider`, throws `MaintenanceException` from
`SaveChanges` while it is closed; `MaintenanceMiddleware` (`BaGet.Web`) maps that to 503 + `Retry-After`; the layout
shows a banner. `CloseUntilRestart` cannot be reopened. Code that stores a file and then writes the database must call `MaintenanceGate.ThrowIfClosed` first (`PackageIndexingService`, `ValidatingPackageSubmissionService`, `CatalogWriter`) so a refusal leaves no orphan file. Sign-in must still work (an admin needs the post-copy status): `MaintenanceAwareSignInManager` skips only the lockout-counter reset while closed; a failed attempt is still a refused write. A context built with `MaintenanceGate.AllowWrites(builder)` is
exempt (the copy's target). Code that catches broad `Exception` around a write must not swallow `MaintenanceException`
(controllers, `PackageValidationRunner`); `ValidationWorker` retries after it, `PackageDatabase.AddDownloadAsync` ignores it.
Raw SQL would bypass the gate: there is none outside migrations, keep it so (the one exception, the database copy's
rollback `ExecuteDelete`, runs on the copy's own target context, never on the application's).

**Repositories.** `Repository`/`Project` (`BaGet.Core/Repositories`); `default` (key `Repository.DefaultKey` = 1) is the
repository at `/`. `Package`, `PackageOwner`, `CatalogEntry`, `PackageValidation`, `AbuseReport`, `PackageSymbol` are `IRepositoryScoped`
(`RepositoryKey`, column default 1 = how the `AddRepositories` migration assigns pre-existing rows; no foreign key).
The migration (all four providers) also inserts the `default` row with key 1 (PostgreSQL `setval`s the sequence), so
applying migrations outside the app (`dotnet ef database update`, a script) leaves no gap for a new repository to take key 1. `AbstractContext` filters those six on `IRepositoryContext.Current.Key` with global
query filters evaluated per query (the scope is found through EF's `ApplicationServiceProvider`, so a hand-built
context works in `default`) and stamps new rows on save; anything that must see every repository calls
`IgnoreQueryFilters()` (`DatabaseTable` copy/count/delete, `AccountDeletionService`, `PackageValidationRunner` pending list
and submission lookup). A new per-package table must implement `IRepositoryScoped` and get the filter in
`OnModelCreating`. Whoever picks the repository (routing, a worker, a command) calls `IRepositoryContext.Use(info)` before
querying, or uses `IServiceScopeFactory.CreateRepositoryScope(info)`; the validation runner switches to the queued
submission's repository itself. `IRepositoryService` finds/lists/creates (`RepositoryNames` holds the one name rule);
`EnsureDefaultAsync` restores a missing `default` row (explicit key 1) at startup and before `CreateAsync`
(which refuses when rows exist but none has key 1). The copy target is "empty" with only that seeded row, which
`DatabaseCopyService` deletes before copying the source's rows. Azure Table (`NullContext`) and `Search:Type` =
`AzureSearch` (one global index, document keys without a repository) have the default repository only:
`SupportsMultipleRepositories` is false, `CreateAsync` and `TablePackageDatabase` refuse others and
`RepositoryService.FindAsync` serves `default` only. The mirror serves `default` only (`UpstreamClientFactory`).
`IRepositoryAdministration` (list with package counts, `SetAnonymousReadAsync`, `DeleteAsync`: refuses `default` and any repository with packages or a pending validation; its
catalog entries, owners, validations and abuse reports are purged with it, and so is `repos/{name}/` in storage when the store is `IListableStorageService`) backs the admin pages `Repositories`, `RepositoryEdit`, `RepositoryDelete` in `BaGet.Identity/Pages/Admin`
(names are immutable; only `AnonymousRead` is editable; writes call `MaintenanceGate.ThrowIfClosed`; feed URLs derive from `IUrlGenerator`).

**Admin operations** (`BaGet.Core/Operations`). `AdminJobService` (singleton) runs `IAdminOperation`s (`DownloadsImportOperation` in Core,
`CatalogRebuildOperation` in `BaGet.Catalog`; register more with `TryAddEnumerable`) in the background in a repository scope, one run per
(operation, repository), status in memory; `TryStart` calls `MaintenanceGate.ThrowIfClosed`. `IValidationAdministration` (impl
`BaGet.Validation/ValidationAdministration`, uses `PackageValidationRunner.ReleaseAsync`/`UseSubmissionRepositoryAsync`) lists pending/quarantined
submissions of all repositories (`IgnoreQueryFilters`) and retries/releases/rejects; `IAbuseReportAdministration` does the same for abuse reports.
`IAdminAudit` (default `LoggingAdminAudit`: a log line) is the seam for the access-audit table; release, reject, retry, resolve and job
start/end go through it. Pages (`BaGet.Identity/Pages/Admin`): `Operations`, `Validation`, `Abuse`; the Identity project reaches these through the
Core interfaces (it cannot reference Validation/Catalog). Tests: `AdminOperationsTests`.

**Admin account tools** (`BaGet.Identity/Pages/Admin`; each action re-asks the admin's password, calls `MaintenanceGate.ThrowIfClosed`, logs at Warning).
`Users` has a separate *Create a user* form (`AdminUserCreationService`: typed, generated-and-shown-once, or invite = unknown password + `PasswordResetLinkSender`).
`UserDetail` (`/admin/users/{name}`) is a list of `<section id="user-section-*">` blocks (account, organizations, API keys with revoke) that other features extend.
`Organizations` / `OrganizationDetail` use `PackageOwnershipService`: `RenameOrganizationAsync` rewrites `PackageOwner.OwnerName` in **every repository** (`IgnoreQueryFilters`) in the same save and refuses a name used by a user or organization;
`DeleteOrganizationAsync` refuses while it owns any package ID in any repository; `SetMemberAdminAsync`/`RemoveMemberAsync` keep one administrator. The `{name}` route segment bars `/ \ ? # %` in names created here.

**Symbols per package.** Symbols are stored by PDB (`symbols/{file}/{key}`), so `ISymbolRegistry` / `SymbolRegistry` (`BaGet.Core/Storage`)
records a `PackageSymbol` row (lower-cased ID, normalized version, file, key; repository-scoped, no foreign key) when
`SymbolIndexingService` stores a PDB (after `MaintenanceGate.ThrowIfClosed`; a `MaintenanceException` is rethrown, not turned into
`InvalidSymbolPackage`). It backs the Manage page's Symbols list/delete: `DeleteAsync` removes the rows first, then each PDB via
`ISymbolStorageService.DeletePortablePdbContentAsync` unless another version of the repository still records it. PDBs indexed before the
table existed (migration `AddPackageSymbols`) have no row: still served, not listable (the `.snupkg` is not kept, so no backfill).
Azure Table (`NullContext`) records nothing (`IsSupported` false). Hard-deleting a package leaves its symbols and rows (they keep being served, as documented).
Vulnerability input rules live in `PackageVulnerabilityRules` (`BaGet.Core/Metadata`), shared by `PackageMetadataAdminController` and the Manage page.

**Repository routes.** `BaGetEndpointBuilder.MapRoute` registers every protocol route (V3, V2, symbols, catalog,
deprecation/vulnerability, repository signatures) twice: plain (the `default` repository) and as `r:<name>` under
`r/{repo}/` (`Routes.RepositoryPathPrefix`, `RepositoryRouteName`); a new route goes through it, never straight to
`MapControllerRoute`. `RepositoryResolutionMiddleware` (after `UseRouting`, before authentication; a host must call
`UseRepositoryResolution`) maps the `{repo}` route value to `IRepositoryContext.Use`; an unknown name (or a non-NuGet
repository) is `404`, never the default. `BaGetUrlGenerator` reads the repository from `HttpContext.RequestServices` on
each call (the registration/search builders are singletons, so an injected scoped context would pin them to one
repository), picks the `r:` route variant and prefixes `AbsoluteUrl`, so every advertised URL stays in the repository and
after the `PathBase`. The V2 feed's base is `IUrlGenerator.GetV2FeedUrl`. **Repository pages.** The pages that act on a repository (`RepositoryPageRouteConvention.RepositoryPages`: Index, Package,
Statistics, ReportAbuse, Upload, Manage, ContactOwners, Profile, Admin Packages/UserPackages) are served at their own route and at
`/r/{repo}/…`: the convention adds an `r/{repo}/` **twin selector** (`Order` -1) to every selector of the page, `/packages` alias
included; it must be registered after `AddPageRoute`. Never give a page a second page route (`AddPageRoute`) for this: link
generation then produces empty `href`s. ASP.NET Core carries an ambient route value only to links to the *same* page, so
`RepositoryUrlHelper` (an `IUrlHelper` decorator from `RepositoryUrlHelperFactory`) adds the request's `repo` to every
`Url.Page` / `asp-page` / `RedirectToPage` link to a repository page; an explicit `repo = x` wins and `repo = null` leaves for the
default repository (a link to any other page is untouched). New repository page: add it to `RepositoryPages`. The request's
repository is `IRepositoryContext`; `IRepositoryAccess` (`BaGet.Core`, default `OpenRepositoryAccess`: every NuGet repository, default
only when `SupportsMultipleRepositories` is false) is the one place that decides which repositories the UI offers: the header
switcher (`_Layout`, only on repository pages, hidden for fewer than two), the Upload picker, My packages (grouped by repository)
and `RepositoryPageFilter` (404 on a repository the caller cannot read). Every repository advertises its own
`PackageDetailsUriTemplate` / `ReportAbuseUriTemplate`. Links from account/admin pages to a package page of a given repository go through `RepositoryPageLinks` (`BaGet.Identity`; default at `/packages/…`, others `/r/{name}/packages/…`, the pages' twin routes); `AccountDeletionService` works per repository (`SoleOwnedPackage`, deletes in each via `CreateRepositoryScope`).

**Storage layout** (`RepositoryStorage`, `BaGet.Core/Repositories`): `default` keeps `packages/…`, `symbols/…`, `catalog/…`,
`validation/…`, `staging/…` byte for byte (existing stores need no migration); every other repository nests the same
layout under `repos/{name}/` (`repos` is no default prefix, so no collision). `PackageStorageService`,
`SymbolStorageService`, `StoragePackageStagingService`, `CatalogWriter`/`CatalogReader` and the validation staging
(`ValidatingPackageSubmissionService.StagedPath(repo, id)`, used by the runner after it switches repository) take an
optional `IRepositoryContext` (null = default). Never build a path from a name without `RepositoryStorage.Prefix`, which
re-validates it. `StagingSweepWorker` sweeps each repository in its own scope.

**Access model** (`BaGet.Core/Access`; user docs `docs/access-control.md`). Tables `Groups`, `GroupMembers`, `PermissionTargets`, `PermissionGrants`,
`ProjectMembers`, `AccessAuditEntries` (migration `AddAccessControl` x4; grants/members cascade from their group/target/project). The migration seeds
groups *Everyone*/*Authenticated* (keys 1, 2), targets "Anyone reads every repository" (Read) and "Authenticated users deploy" (Annotate|Deploy|Delete|Manage,
not only Deploy: ownership-based unlist/deprecate/Manage page worked before the model, so B3 equivalence needs them) and one grant each (PostgreSQL `setval`s
the sequences). `IAccessEvaluator` (scoped; `AccessEvaluator`) answers `IsAllowedAsync(principal, repository, packageId?, action)`:
superuser and `IsAdministrator` always, else the union of grants of targets matching repository (globs) and package (include/exclude; a null id is a
repository-level question), for the principal's user id, groups (Everyone/Authenticated implicit), organizations, plus project roles on
`Repository.ProjectKey`. `Repository.AnonymousRead` = false drops *Everyone*'s Read there (`AccessSnapshot.Granted`); `RepositoryAdministration.SetAnonymousReadAsync`
re-creates an "Anonymous read {name}" target when turning it on finds none. `NullContext` (Azure Table) has no model: Read open, writes for any authenticated
principal. State is one `AccessSnapshot` in the singleton `AccessModelCache` (targets, grants, repositories, project members; per-user memberships loaded lazily
and kept with it), reloaded when `AccessModelVersion` changes or after 30 s; `AccessModelSaveChangesInterceptor` (added next to the maintenance one) bumps the
version after any committed save touching those tables, `Repository`, `Project`, `Organization(Member)`, so no editor can forget; `ExecuteUpdate/Delete` would
bypass it (none do). Edits go through `IAccessAdministration` (audits in the same save, refused by the maintenance gate). `OwnershipAuthorizationService` asks the
evaluator first (Push -> Deploy, Manage -> Delete, new `PackageAction.Annotate` -> Annotate; repository = `IRepositoryContext.Current`), then applies ownership.
**Reads**: `BaGetEndpointBuilder.MapRoute` tags every protocol endpoint with `RepositoryRouteMetadata`; `RepositoryReadFilter` (a global MVC resource filter, so
every host gets it; `GET`/`HEAD` only) requires Read on the repository and, for a route with `{id}`, on the package: anonymous -> 401 +
`WWW-Authenticate: Basic realm="BaGet"`, signed in without Read -> 404. Credentials: `RequestPrincipalResolver` (Basic password = API key with the user name
checked against the key's owner, else `X-NuGet-ApiKey`, else the cookie session through `ISessionPrincipalFactory`, implemented by `SessionPrincipalFactory`);
bad credentials count as none. Pages: `RepositoryPageFilter` -> `IRepositoryAccess` (`AccessRepositoryAccess`, replaces `OpenRepositoryAccess`): anonymous visitor
is challenged (sign-in redirect) when the host has a default challenge scheme, else 404. Database copy treats the built-in groups/targets/grants as seeded rows
(`SeededCountAsync`, removed from the target before copying). Account/organization deletion removes the identity's memberships, grants and project roles.
The Manage page and its link need ownership **and** the Manage action (`ManagePermission.HeldAsync`); administrators bypass both. Search/autocomplete/V2 listings are filtered per repository only, not per package pattern.

**Access admin pages (U3, `BaGet.Identity/Pages/Admin`; user docs `docs/access-control.md#admin-pages`).** `AccessPageModel : AdminPageModel` is the base of
`Groups`, `GroupDetail`, `Permissions`, `PermissionDetail`, `Projects`, `ProjectDetail`, `Audit`: it overrides the admin check with a virtual `AuthorizeAsync` (default
server administrator; `Projects`/`ProjectDetail`/`PermissionDetail` also admit the project's `Admin` role holders, found by route value), redirects to
`AccessUnavailable` when `IContext` is `NullContext`, calls `MaintenanceGate.ThrowIfClosed` on every POST, and `ActAsync(password, confirm, change, redirect)` asks for
the password (`IsConfirmedAsync`, the `_ConfirmPassword` partial) and turns `AccessModelException` into the page's `Error`. Password rule: every change except
creating an empty group/target/project and renaming a group. Project administrators may not assign repositories or delete the project, and a project target's preview
is cut to the project's repositories. Reads for the pages are on `IAccessAdministration` (`ListGroups/GetGroup/ListTargets/GetTarget/ListProjects/GetProject/
ProjectRolesOfUser/IsProjectAdmin/ResolvePrincipal/SearchUsers/Audit(AuditFilter)`, plus `RenameGroup`, `DeleteProject`, `SetRepositoryProject`); the
evaluator explains (`IAccessEvaluator.ExplainAsync` -> `AccessExplanation`/`AccessReason`, from the same `AccessSnapshot.Granted` walk, so explanation and decision cannot
diverge) and summarises a repository (`DescribeAsync` -> `RepositoryAccessSummary.Who(action)`). `AccessPatterns` (public wrapper over the internal `GlobSet`) is what
the pattern preview and test box use. `/admin/users/{name}` got the groups, projects and effective-permission sections, `/admin/repositories/{name}` the effective
access, `/account/access` (`Account/Access`) lists only repositories where the user holds an action (no existence leak). The partials `_Explanation`, `_PatternFields`.

**MVC application parts are registered by BaGet, not the host.** `AddBaGetApplicationParts`
(`src/BaGet.Web/Extensions/IServiceCollectionExtensions.cs`) adds every assembly with controllers or pages; a host
without the Web SDK has no generated `[ApplicationPart]` attributes. A new such assembly must be added there.

Routes are named constants in `src/BaGet.Web/Routes.cs`, mapped in `BaGetEndpointBuilder.MapEndpoints` (via `MapRoute`, see *Repository routes*),
and absolute URLs are built only via `IUrlGenerator` — add a member there rather than assembling URLs
in a page or controller.

### Second databases: `IDatabaseContextFactory`

Each relational provider registers an `IDatabaseContextFactory` (`BaGet.Core`; `Add{Sqlite,SqlServer,PostgreSql,MySql}Database`,
none for AzureTable) that opens an `IContext` on an *arbitrary* connection string, configured through the same
`Configure` method as the application's own context (`Database:MySqlServerVersion` honoured, else `AutoDetect`
against the target). Resolve `IEnumerable<IDatabaseContextFactory>` and match `DatabaseType`; `Create(..., configure)`
takes an `Action<DbContextOptionsBuilder>` hook. The caller owns and disposes the context.

`IContext.BeginKeyedInsertAsync` / `EndKeyedInsertAsync(IEntityType)` bracket inserts with explicit generated keys:
SQL Server runs `SET IDENTITY_INSERT` and keeps the connection open between the calls (the setting is per session);
PostgreSQL `setval`s the serial/identity sequence past the max key in `End`; SQLite and MySQL need nothing (explicit
keys advance their counters).

### Database copy (`DatabaseCopyService`, `BaGet.Core/Entities/Copy`)

The Settings page's "Copy all data" option queues a `DatabaseCopyRequest` (`TryQueue` closes the gate and refuses a
second copy); `DatabaseCopyWorker` (single-slot channel) runs it; progress is in memory (`Status`), polled via
`?handler=CopyStatus` (admin only). `DatabaseTable.InDependencyOrder` topologically sorts `Model.GetEntityTypes()` by
foreign keys (Identity's tables included), so a new table is covered with no list to keep. Rows are read in batches of
1000 ordered by primary key; they are loaded **tracked** only to capture **shadow properties** (the FK of a package's
types/frameworks/dependencies is shadow) into `CopyRow.Shadow`, then the tracker is cleared. Pre-flight
(`DatabaseCopyPreflight`, given the target's **design-time** model for column types/collations) runs before the target is created or
migrated and reports, only where the target engine enforces it: over-length strings (SQL Server/PostgreSQL/MySQL), case-only
duplicates in unique indexes and keys (columns `NOCASE`/`citext`/`*_ci`, else the engine default: SQL Server, MySQL) and local dates
(PostgreSQL); a non-empty target needing migration is refused with "schema was updated"; `Unspecified` dates are stored as UTC. Verification uses an order-independent key checksum (`DatabaseTable.Checksum`; engines collate differently). Once settings saving starts, a failure keeps the target and closes the gate until restart (`FailAfterSaveAsync`). A cancellation inside `SettingsService.SaveAsync` happens before the file rename, so it rolls back. Test seams
`DatabaseCopyService.AfterBatch` / `BeforeSettingsSaved` / `AfterSettingsSaved` inject failures. Errors are scrubbed of the connection string
(`DatabaseCopyService.Scrub`); never log it.

### Logging

The host (and only the host) uses Serilog: `Program.ConfigureLogging` clears the Microsoft providers and calls
`AddSerilog(... ReadFrom.Configuration ... , writeToProviders: true)`, so the `Serilog` section of `appsettings.json`
is the one source of truth for levels and sinks (Console, File `logs/baget-.log`, resolved against the content
root / `BAGET_CONFIG_ROOT` for every File sink by `ResolveLogFilePaths`). `ApplyDefaultMinimumLevels` sets a code floor
before `ReadFrom` (Microsoft and `Hosting.Diagnostics` Warning: that logger prints `?apikey=`), so a host with its own
appsettings is safe. A sink with `Enabled=false` is blanked (`DisableSinks`; an empty env var is deleted on Windows).
On Windows a Serilog EventLog sink (Warning+, source `.NET Runtime`, `Serilog:EventLog:Enabled`) replaces the Microsoft
provider that `ClearProviders` removes. SelfLog goes to stderr. MinimumLevel reloads live, sinks need a restart. Libraries keep `Microsoft.Extensions.Logging.ILogger`. There is
**no static/bootstrap logger** (it would throw "already frozen" when `WebApplicationFactory` builds a second host).
`app.UseSerilogRequestLogging()` sits after forwarded headers/path base; its template has the path only, never the
query or headers (API keys). `writeToProviders` delivers each event twice to a registered `ILoggerProvider`;
tests that count lines use `Distinct()`. The harness switches the sinks off with `Enabled=false`.
`WebApplicationFactory` `UseSetting` gives every parent key of a setting an empty value, which Serilog reads as a sink
name, so a test that needs a working File sink runs `Program.BuildApplication` with command-line args instead.

### Telemetry

Libraries emit through `BaGetDiagnostics` (`BaGet.Core/Telemetry`): `Source` (`ActivitySource`) and `Meter`, both named
`BaGet`, plain `System.Diagnostics` (no OpenTelemetry package outside the host). `StartActivity` returns null without a
listener, so spans cost nothing. Spans: `package.index` (`PackageIndexingService`), `package.validate`
(`PackageValidationRunner`), `mirror.fetch` (`PackageService`), `database.copy` + `database.copy.table`
(`DatabaseCopyService`); counters `baget.packages.pushed`, `baget.downloads`, `baget.validation.results`. Tags are
`BaGetDiagnostics.Tags` (id, version, result, table, counts); never keys, connection strings or user data, and a failure
records the exception type only (`Fail`). `TelemetryOptions` (`Telemetry:*`, live except `ServiceName` and `Metrics:ExportInterval`, which are `[RestartRequired]`) validates itself
(`Validate()`, called from `ConfigureBaGetOptions`). Tests listen with `DiagnosticsCapture` (linked into `BaGet.Tests`);
the sources are process-wide, so tests that count measurements join its `Diagnostics` collection (runs alone).

**Export (host only, `src/BaGet/Telemetry`).** `AddBaGetTelemetry` (called from `BuildApplication` next to `ConfigureLogging`)
always builds the providers; whether anything is exported is decided live from `IOptionsMonitor<TelemetryOptions>`
(`AddBaGetOptions` has no change token, so it registers a `ConfigurationChangeTokenSource` for `Telemetry`):
`DynamicSampler` drops everything unless `Enabled` and a traces endpoint are set, then `ParentBased(TraceIdRatio)`;
`ReloadableTraceExporter` / `ReloadableMetricExporter` (`ReloadableExporter<T>`) and `ReloadableOpenTelemetrySink` hold the
real OTLP exporter in a `SwappableHolder<T>` (lease counted: a swap never waits for or cuts an export, the old exporter is
disposed once after its last lease, `ForceFlush` / `Shutdown` reach the current one) and rebuild only when
`TelemetryEndpoints.Key` changes (endpoint, protocol, headers, timeout, level), since every configuration reload fires
`OnChange`. Off = no inner exporter, no connection. Logs are a Serilog sink (added in `ConfigureLogging`), **not** an
`ILoggerProvider` (`writeToProviders` would export each event twice); it carries `TraceId`/`SpanId`, applies
`Logs:MinimumLevel`, ignores `Grpc.Net.Client` / `System.Net.Http` events (feedback loop) and sees only what the Serilog
levels let through. Traces use `Otlp:Endpoint`; metrics and logs only their own endpoint (Jaeger takes traces only); with
`HttpProtobuf` `/v1/<signal>` is appended. Queue bounded (2048, `TelemetryServiceCollectionExtensions.MaxTraceQueueSize`).
`TelemetryStatus` (per signal: last attempt/success, failing since, scrubbed error) is fed by the exporters
(`ExportStatusHandler` for logs); every message goes through `TelemetryStatus.Scrub` (URLs cut to host, header values
replaced). `TelemetryConnectionTester` exports one synthetic span with explicit settings. The Settings page (BaGet.Identity cannot reference the host) reaches both through `ITelemetryDiagnostics` (`BaGet.Core`, implemented by the host's `TelemetryDiagnostics`; null args = saved values): the Telemetry section's status table, and `SettingsModel.OnPostTestTelemetryAsync` (admin, antiforgery, password-confirmed, posted unsaved `v:Telemetry:Otlp:*` fields, blank sensitive Headers = saved, nothing saved; `TelemetrySettingsPageTests`). **Privacy:** the ASP.NET Core
instrumentation redacts `url.query` values and records no headers, `SqlTextRedactingProcessor` strips the SQL text the EF instrumentation always records (`db.query.text`/`db.statement`/`db.query.summary`, renaming a span named from them; `SqlTextRedactingProcessorTests`, `TelemetryExportTests.DatabaseSpansNeverCarrySqlText`); the log sink uses the startup `ServiceName` (not part of the rebuild key), `ReloadableOpenTelemetrySink.Dispose` waits for sinks still leased by an `Emit`; a host without `ITelemetryDiagnostics` (embedded) gets a "no effect here" note on the Settings Telemetry section; logs are exported as written, audit lines with user names included (documented); never add an `Enrich` copying
either (`TelemetryExportTests.NeitherQueryValuesNorHeadersReach...`). **SDK quirks:** `BaseExporter.ParentProvider` (the
resource) has an internal setter, so `ReloadableExporter.Adopt` sets it by reflection on the inner exporter, and the batch
processor's `DroppedCount` is read by reflection; both degrade (empty resource, 0) if the SDK changes. Versions are levers in
`src/Directory.Build.props` (`OpenTelemetryPackageVersion` 1.20.0 needs Options/Configuration 10.0.0, which restores cleanly
beside the 9.0.21 pins because the Web SDK shared framework supplies them; 1.13.x would avoid that but has known
vulnerabilities; `OpenTelemetry.Instrumentation.EntityFrameworkCore` exists only as prerelease, `1.20.0-beta.1`, and is
behind the live `Traces:Database`). Tests: `InMemoryTelemetry` (replaces `ITelemetryExporterFactory` through the harness's
`configureServices`; telemetry is off in the harness unless a test enables it, saving through `SettingsService` so the
reload path is the Settings page's), `FakeOtlpCollector` (real exporters over HTTP, no Docker), `ReloadableExporterTests`.

### Tests

`BaGetApplication : WebApplicationFactory<Program>` is the integration harness: real HTTP against the
real pipeline, SQLite in a temp directory, `SystemTime` mocked to 2020-01-01Z. Prefer it over mocking
when the behaviour is observable over HTTP; several real defects here were only caught by a real
client (e.g. the V2 feed's XML declaration).

**Protocol suites run twice.** `ApiIntegrationTests`, `NuGetClientIntegrationTests`, `NuGetV2ClientIntegrationTests`,
`BaGetClientIntegrationTests`, `V2FeedIntegrationTests` and `CatalogIntegrationTests` are abstract `…Base` classes taking a
repository name; the plain class runs them at `/`, `…SecondRepository` at `/r/second/` (relative URIs resolve against
the client's `BaseAddress`; expected absolute URLs go through `Expect`; every repository's service index has 17 entries).
A new protocol test class should follow the pattern. Helpers:
`CreateRepositoryAsync`, `CreateRepositoryScopeAsync`, `AddPackageAsync(..., repository)`.
`RepositoryLifecycleIsolationTests` pushes/unlists/relists/deprecates in one repository and asserts the other's whole read
surface is unchanged; `UpgradeFromPreRepositoryTests` (B7) starts the real app on a pre-repository SQLite database and
`packages/…` store and asserts everything is served unchanged at `/`, keys/owners still work and no file moves.
`RepositoryPagesTests` / `RepositoryAccountPagesTests` cover the pages at `/r/second/` (links stay inside, switcher, upload target, My packages grouped,
admin Browse/Upload links, `IRepositoryAccess` refusal = 404); `RepositoryPageRoutingFacts` (`BaGet.Web.Tests`) the convention and `RepositoryUrlHelper`.

**Access tests** (`BaGet.Tests`): `AccessUpgradeEquivalenceTests` (B3 merge gate: a pre-repository database migrated by the current code behaves as before),
`AccessEvaluatorTests` (decisions, administration, cache, audit, 50-target cost) with `GlobSetTests`, `PrivateRepositoryTests` (401/404/Basic on every protocol route,
page redirect, push permissions), `ManagePermissionTests`, `AccessInspectionTests` (explanations, repository summary, admin reads, rename, project delete, filtered audit, patterns),
`AccessAdminPagesTests` (the U3 pages end to end: roles, passwords, project administrators, audit entries, 503 while closed). A test that needs a private repository removes the seeded targets through `IAccessAdministration`.

Unit tests reach internals through `InternalsVisibleTo`, declared in each **project file** (not
`AssemblyInfo.cs`):

| Project | Visible to |
|---|---|
| `BaGet.Core` | `BaGet.Core.Tests`, `BaGet.Tests`, `BaGet.Database.Tests`, `BaGet.Catalog`, `BaGet.Identity` |
| `BaGet`, `BaGet.Catalog`, `BaGet.Identity`, `BaGet.Validation`, `BaGet.Database.*` | `BaGet.Tests` (`BaGet` also `BaGet.Telemetry.Tests`) |
| `BaGet.Web` | `BaGet.Tests`, `BaGet.Web.Tests` |
| `BaGet.Azure`, `BaGet.Protocol` | `BaGet.Azure.Tests`, `BaGet.Protocol.Tests` |
| `BaGet.Web.V2` | `BaGet.Web.V2.Tests` (no such project; `BaGet.Tests` has no access) |
| `BaGet.Aws`, `BaGet.Gcp`, `BaGet.Aliyun` | none |

## Packaging

Every packable project's `README.md` is its package readme (`PackageReadmeFile`, set in `src/Directory.Build.props`
when the file exists). It renders on nuget.org, so links must be absolute.

`Extension.BaGet` (the `BaGet` host) also packs `BaGet.Web/wwwroot` at `wwwroot/` (a `None` item in
`src/BaGet/BaGet.csproj`), for hosts that embed BaGet without the Web SDK's static web assets and serve those
files from `wwwroot/_content/BaGet.Web` (e.g. BaGet.Service).

## Package versions

All shared versions are single levers in `src/Directory.Build.props`; provider projects must read them
rather than pinning literals (divergence there once resolved an EF 8 request to EF 10).

**Entity Framework is pinned to 9.0.21 and must not be raised to 10.** `Pomelo.EntityFrameworkCore.MySql`
has no EF 10 release; its 9.0.0 constrains `Microsoft.EntityFrameworkCore.Relational` to
`[9.0.0, 9.0.999]`. Raising EF breaks MySQL at runtime (`MethodNotFound` on
`AbstractionsStrings.ArgumentIsEmpty`) and turns an `NU1608` warning into a hard `NU1107`. All four
providers move together. `Microsoft.Extensions.*` tracks EF (9.0.21) because holding it lower is an
`NU1605` downgrade error.

## Migrations

Each `BaGet.Database.*` project has an `IDesignTimeDbContextFactory` using a hardcoded connection
string and `ServerVersion.Parse` (never `AutoDetect`, which opens a live connection). A schema change
needs a migration **per provider**, and all four must report no pending model changes.

- Verify a provider without a server: `dotnet ef migrations list --context <X>Context` in its folder.
- **`dotnet ef migrations remove --no-build` has deleted the wrong (previous) migration** on PostgreSQL
  and MySQL because of a stale assembly; without `--no-build` it needs a live database. Delete the
  generated files by hand and restore the snapshot from `HEAD` instead.
- Model-level cascade-delete configuration caused `PendingModelChangesWarning` across every integration
  test; orphan cleanup lives in `PackageDatabase.HardDeletePackageAsync` instead.

Documentation is mkdocs (`mkdocs.yml`, `docs/`).
