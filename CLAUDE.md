# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

A fork of [loic-sharma/BaGet](https://github.com/loic-sharma/BaGet), a lightweight NuGet and symbol
server. The fork targets `net10.0` and has diverged substantially (accounts, V2 feed, validation,
catalog). Some work here is upstreamed as PRs against `loic-sharma/BaGet`.

## Build and test

**Always pass `-p:GeneratePackageOnBuild=false`.** `src/Directory.Build.props` sets
`GeneratePackageOnBuild=true` with `PackageOutputPath=$(SolutionDir)Packages\`, and `Packages` is a
**symlink to `D:\Starb\Packages`**, a shared NuGet feed. A plain build publishes packages into it, and
an `AfterTargets="Pack"` target deletes `%userprofile%\.nuget\Packages\<project>`.

| Task | Command |
|---|---|
| Build | `dotnet build BaGet.sln -p:GeneratePackageOnBuild=false` |
| Test (all) | `dotnet test BaGet.sln -p:GeneratePackageOnBuild=false` |
| Test (one project) | `dotnet test tests/BaGet.Core.Tests/BaGet.Core.Tests.csproj -p:GeneratePackageOnBuild=false` |
| Test (one class or method) | add `--filter "FullyQualifiedName~UploadPageTests"` |
| Run | `cd src/BaGet && dotnet run -p:GeneratePackageOnBuild=false` |

Baseline: **1373 tests, all green** across `BaGet.Core.Tests` (443), `BaGet.Tests` (669),
`BaGet.Protocol.Tests` (118), `BaGet.Azure.Tests` (102), `BaGet.Web.Tests` (33),
`BaGet.Protocol.Samples.Tests` (8). No external services needed; integration tests use SQLite and a
temporary directory.

`build.ps1` wraps clean/restore/build, with `-Run` and `-Publish`.

Two gotchas when running locally:
- It listens on **6060**, not the 5000 the readme claims — `Kestrel:Endpoints` in `appsettings.json`
  outranks `--urls`, so `--urls` is silently ineffective.
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
`Main` wraps it in a McMaster `CommandLineApplication` (`import downloads`, `catalog rebuild`), which
is configured to **collect unrecognised arguments** so host arguments like `--environment` pass
through.

**Settings overrides file.** `ConfigureConfiguration` inserts `appsettings.Overrides.json` (path from
`SettingsOverridesPath`, default in the content root / `BAGET_CONFIG_ROOT`) after the last `appsettings*.json`
source and before environment variables and the command line. `SettingsService` /
`SettingsOverridesStore` (`BaGet.Core`) write it; `AddBaGetOptions` records every options type as a
`SettingsSchemaRegistration`, and `[RestartRequired]` / `[SensitiveSetting]` on options classify them. Tests
set `SettingsOverridesPath` to the temp directory; `UseSetting` values arrive through the *last* (chained host)
provider, so they shadow saved overrides — integration tests that save a setting must use keys the harness does
not set.

**Registration-time configuration reads are load-bearing.** `AddBaGetAccounts` decides from
`Accounts:Enabled` *while registering services*, so configuration must be in place before
`BuildApplication` runs. This is why `tests/BaGet.Tests/Support/BaGetApplication.cs` supplies settings
with `builder.UseSetting(...)` and **not** `ConfigureAppConfiguration`, which is merged too late.
Anything read through `IOptions`/`IOptionsSnapshot` is unaffected.

Routes are named constants in `src/BaGet.Web/Routes.cs`, mapped in `BaGetEndpointBuilder.MapEndpoints`,
and absolute URLs are built only via `IUrlGenerator` — add a member there rather than assembling URLs
in a page or controller.

### Tests

`BaGetApplication : WebApplicationFactory<Program>` is the integration harness: real HTTP against the
real pipeline, SQLite in a temp directory, `SystemTime` mocked to 2020-01-01Z. Prefer it over mocking
when the behaviour is observable over HTTP; several real defects here were only caught by a real
client (e.g. the V2 feed's XML declaration).

Unit tests reach internals through `InternalsVisibleTo`, declared in each **project file** (not
`AssemblyInfo.cs`) — already present in `BaGet.Core`, `BaGet`, `BaGet.Azure`, `BaGet.Protocol`,
`BaGet.Identity`, `BaGet.Catalog`, `BaGet.Validation`, `BaGet.Web.V2`.

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
