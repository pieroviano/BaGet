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

Baseline: **1558 tests** across `BaGet.Core.Tests` (493), `BaGet.Tests` (784),
`BaGet.Protocol.Tests` (118), `BaGet.Azure.Tests` (102), `BaGet.Web.Tests` (41),
`BaGet.Protocol.Samples.Tests` (8), `BaGet.Database.Tests` (12). No external services needed; integration tests
use SQLite and a temporary directory.

`BaGet.Database.Tests` copies between every ordered pair of SQLite / SQL Server / PostgreSQL / MySQL with
`DatabaseCopyService` on Testcontainers servers (`TestcontainersPackageVersion` in `tests/Directory.Build.props`;
images `mssql/server:2022-latest`, `postgres:17`, `mysql:8.4`, pulled on first run). Without a Docker daemon the 12
tests **skip** (`Assert.Skip`), so the suite stays green; with one they take ~2 minutes.

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
| `BaGet.Core` | Domain: entities, `IPackageDatabase`, `IStorageService`, `ISearchService`, indexing, mirroring, options |
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
`Main` wraps it in a McMaster `CommandLineApplication` (`import downloads`, `catalog rebuild`, `account reset-password|promote|demote <user>`), which
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

**MVC application parts are registered by BaGet, not the host.** `AddBaGetApplicationParts`
(`src/BaGet.Web/Extensions/IServiceCollectionExtensions.cs`) adds every assembly with controllers or pages; a host
without the Web SDK has no generated `[ApplicationPart]` attributes. A new such assembly must be added there.

Routes are named constants in `src/BaGet.Web/Routes.cs`, mapped in `BaGetEndpointBuilder.MapEndpoints`,
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
records the exception type only (`Fail`). `TelemetryOptions` (`Telemetry:*`, not `[RestartRequired]`) validates itself
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
instrumentation redacts `url.query` values and records no headers, EF records no SQL text; never add an `Enrich` copying
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

Unit tests reach internals through `InternalsVisibleTo`, declared in each **project file** (not
`AssemblyInfo.cs`):

| Project | Visible to |
|---|---|
| `BaGet.Core` | `BaGet.Core.Tests`, `BaGet.Tests`, `BaGet.Database.Tests`, `BaGet.Catalog`, `BaGet.Identity` |
| `BaGet`, `BaGet.Catalog`, `BaGet.Identity`, `BaGet.Validation`, `BaGet.Database.*` | `BaGet.Tests` |
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
