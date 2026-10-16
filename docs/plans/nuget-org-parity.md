# Plan: extend BaGet to nuget.org functionality

Status: in progress. Phases 1 and 2 done. Blocking checks B1-B4 done (see **Blocking verification results**); B5 deferred to Phase 5.
Phase 1 onwards not started. Target branch policy: one reviewable PR per phase against
`loic-sharma/BaGet` `main`.

## Decisions taken (and their consequences)

| Decision | Consequence |
|---|---|
| Scope: V3 protocol completeness + accounts/owners/scoped keys + validation pipeline + V2 OData | ~9 phases, 4 new projects. No phase is optional. |
| Identity: ASP.NET Core Identity + external login (OIDC/Entra) | New `BaGet.Identity` project, Identity's own 7 tables, cookie auth added to a currently cookie-less app. Razor Pages UI grows sign-in/manage areas. |
| Layout: one project per area | `BaGet.Identity`, `BaGet.Catalog`, `BaGet.Validation`, `BaGet.Web.V2`. Each needs DI wiring in `DependencyInjectionExtensions.cs:90` and provider registration. |
| All 4 DB providers | Every schema change = 4 migrations (`src/BaGet.Database.*/Migrations`, currently 6–7 each). SQLite cannot alter an FK, so **all new relationships must be additive**. |
| All 4 storage providers | Catalog pages, signatures and validation artefacts go through `IStorageService` (`src/BaGet.Core/Storage/IStorageService.cs`), never the file system directly. |
| No breaking changes | Ownership backfills to a synthetic owner; API key stays valid; no reindex; no config key renames. Rules out making existing columns non-nullable. |
| Upstream-mergeable | No phase may depend on a later phase's schema. Phases 1–2 ship no schema change at all. |

## Blocking verification — do these before writing code

| # | Check | Why it blocks | How |
|---|---|---|---|
| B1 | Does `dotnet ef migrations add` on SQLite produce a clean migration for a **new table with a nullable FK to `Packages`**? | The whole additive-only strategy depends on it. One TODO this session (`AbstractContext.cs:107`) was already blocked by SQLite FK limits, and EF 10 fails the build on a model/snapshot mismatch (`PendingModelChangesWarning`). | Scaffold `PackageOwner` in a throwaway branch, run migrations for all 4 providers, then run `BaGet.Tests` (it migrates, unlike `Core.Tests` which uses `EnsureCreated`). |
| B2 | Confirm `Package.Key` (`src/BaGet.Core/Entities/Package.cs:10`) is a stable identity usable as an FK target | Ownership and validation rows reference packages. If any provider regenerates keys, the backfill corrupts. | Inspect the 4 migration snapshots for the `Packages` PK definition. |
| B3 | Does nuget.org's `VulnerabilityInfo` resource have a documented, implementable shape? | Phase 3 advertises it. If the format is undocumented, the resource must be dropped from scope rather than guessed. | Read the V3 spec + `GET https://api.nuget.org/v3/index.json`; compare against `src/BaGet.Protocol/Models/`. |
| B4 | Decide whether `BaGet.Protocol` (shipped as a NuGet package) may take new public members in a minor version | Phases 3 and 8 add models to it. A breaking change to a published SDK needs a major version. | Check the package's current version and its SemVer policy. |
| B5 | Does the existing single API key remain honoured once Identity is added? | "No breaking changes" requires it. | Write the test first: existing `ApiKeyAuthenticationServiceTests` must keep passing with Identity registered. |

## Blocking verification results (2026-10-16)

| # | Verdict | Finding |
|---|---|---|
| B1 | **Pass, after fixing the toolchain** | A new table with a nullable FK to `Packages` generates a plain `CreateTable` on SQLite - no table rebuild, no FK alteration, no data loss - and applies cleanly (`BaGet.Tests` 82/82). The additive-only strategy is sound. Getting there needed three fixes, below. |
| B2 | **Pass** | `Package.Key` is `int` / `ValueGeneratedOnAdd` in all four migration snapshots. Safe FK target. |
| B3 | **Pass, with corrections** | All the resources are documented and live on nuget.org. Corrections to this plan: `ReportAbuseUriTemplate` is published as `3.0.0-beta` **and** `3.0.0-rc`, not `3.0.0`. nuget.org also advertises `OwnerDetailsUriTemplate/6.11.0`, `ReadmeUriTemplate/6.13.0`, `PackageDisplayMetadataUriTemplate/3.0.0-rc`, `PackageVersionDisplayMetadataUriTemplate/3.0.0-rc`, `SearchGalleryQueryService/3.0.0-rc` and `LegacyGallery`, none of which this plan listed. Template placeholders are `{id}`, `{version}`, `{id-lower}`, `{version-lower}`, `{owner}`, `{lower_id}`. |
| B4 | **Pass** | `BaGet.Protocol` versions as `0.1.0.<yyddd>` (pre-1.0, date-stamped) via `src/Directory.Build.props:10`. Additive public members are acceptable. |
| B5 | Deferred | Nothing to check until Identity exists. Stays a test-first requirement of Phase 5. |

### Blockers found while running B1

| Blocker | Resolution |
|---|---|
| `dotnet ef` could not find any context: `ServerVersion.AutoDetect` in `MySqlApplicationExtensions` opens a live MySQL connection while options are built, and the design-time tools build options for every registered provider. | `DatabaseOptions.MySqlServerVersion` added; when set, the version is parsed instead of detected, so building options stays offline. |
| EF tools 9.0.0 could not load the EF 10.0.13 runtime. | Updated the global `dotnet-ef` tool to 10.0.13. |
| Design-time discovery built the whole application's DI, so one unusable provider broke migrations for all of them. | `IDesignTimeDbContextFactory` added to the Sqlite, SqlServer and PostgreSql projects. `dotnet ef` no longer needs `--startup-project`, and each provider is independent. Verified: 7, 6 and 6 migrations listed respectively. |

### MySQL was blocked; resolved by pinning back to EF Core 9

**Pomelo.EntityFrameworkCore.MySql has no release that supports EF Core 10.** Its latest, 9.0.0,
constrains `Microsoft.EntityFrameworkCore.Relational` to `[9.0.0, 9.0.999]`. While the solution
sat on EF 10 that constraint was violated through transitive resolution - the `NU1608` warning -
and the consequences were verified: a direct EF 10 reference in `src/BaGet.Database.MySql`
escalated to a hard `NU1107`, and resolving `MySqlContext` threw `MethodNotFound` for
`AbstractionsStrings.ArgumentIsEmpty`. MySQL hosting was broken.

**Resolved**: the solution is pinned to EF Core 9.0.21, keeping `net10.0`. Each provider now
reads its version from one property in `src/Directory.Build.props`, and `dotnet-ef` is pinned to
9.0.21 by `.config/dotnet-tools.json`. The legal version window is narrow - Pomelo caps
Relational at 9.0.999, Npgsql 9.0.4 floors it at 9.0.1, and the Design package floors it at
9.0.21 - so **9.0.21 sits at the bottom of the only range that satisfies all three**. There is no
room to go lower without also lowering the Design package.

Verified for all four providers, offline, in the host's unified dependency graph:

| Check | Sqlite | SqlServer | PostgreSql | MySql |
|---|---|---|---|---|
| `dbcontext info` - loads and builds the model | yes | yes | yes | **yes** |
| `migrations script` - provider SQL generator runs | yes | yes | yes | **yes** |
| `migrations list` | 7 | 6 | 6 | **6** |
| `has-pending-model-changes` | clean | clean | pending | pending |

MySQL building its model and generating SQL against EF 9 is the direct refutation of the
`MethodNotFound` above, and needs no MySQL server.

**"All four database providers" is restored as a constraint that holds.** Phases 3-6 may ship
migrations for every provider.

Two caveats carried forward:

- **PostgreSQL and MySQL report pending model changes.** For PostgreSQL this is **pre-existing**:
  the same check reports it at Npgsql 10.0.3 / EF 10, so the downgrade did not cause it. For MySQL
  it could never be checked before, because the context could not be constructed. Both snapshots
  are stamped `ProductVersion 3.1.18` and encode EF 3.1-era provider shapes. Phase 3 must
  regenerate a baseline migration for these two providers before adding tables to them, and must
  not assume a clean diff.
- **The test suite only exercises SQLite.** `PackageDatabaseTests`, `DatabaseSearchServiceTests`
  and `HostIntegrationTests` all use SQLite, so a green suite says nothing about the other three
  providers. The `dotnet ef` checks in the table above are the gate for those, not the tests.

## Current state

Advertised service-index resources — `src/BaGet.Core/ServiceIndex/BaGetServiceIndex.cs:35-41`:

| Resource | BaGet | nuget.org |
|---|---|---|
| `PackagePublish/2.0.0` | yes | yes |
| `SymbolPackagePublish/4.9.0` | yes | yes |
| `SearchQueryService` | yes | yes |
| `RegistrationsBaseUrl` | yes | yes |
| `PackageBaseAddress/3.0.0` | yes | yes |
| `SearchAutocompleteService` | yes | yes |
| `Catalog/3.0.0` | **no** | yes |
| `ReportAbuseUriTemplate/3.0.0-beta`, `/3.0.0-rc` | yes | yes |
| `PackageDetailsUriTemplate/5.1.0` | yes | yes |
| `VulnerabilityInfo/6.7.0` | **no** | yes |
| `RepositorySignatures/5.0.0` | **no** | yes |
| `OData V2 feed` | yes | yes |

Other gaps:

- `src/BaGet.Protocol/Models/SearchResult.cs` has no `owners`, `verified`, `deprecation` or `vulnerabilities`.
- `src/BaGet.Protocol/Models/PackageDeprecation.cs` exists for *reading* upstream; nothing server-side produces deprecation.
- `src/BaGet.Core/Authentication/ApiKeyAuthenticationService.cs:25` — one global key; an unset key leaves the server open. No concept of who pushed a package.
- `src/BaGet.Core/Indexing/PackageIndexingService.cs:94-95` — push is synchronous, with `TODO: Add more package validations` and no signature check.
- `src/BaGet.Core/Indexing/SymbolIndexingService.cs:130,148` — PDBs are not validated as portable, nor matched to a DLL.

## Phases

Each phase is one PR. Schema column counts are new tables unless stated.

### Phase 1 — Service index URI templates (no schema) — DONE

Add to `IUrlGenerator` (`src/BaGet.Core/IUrlGenerator.cs`, after line 50):

```csharp
string GetReportAbuseUriTemplate();
string GetPackageDetailsUriTemplate();
```

`BaGetUrlGenerator` (`src/BaGet.Web/BaGetUrlGenerator.cs`) returns templates with the literal
`{id}`/`{version}` placeholders the spec requires — these are templates, not generated links, so
they bypass `LinkGenerator` and use `AbsoluteUrl` (line 152).

`BaGetServiceIndex.GetAsync` gains:

```csharp
resources.AddRange(BuildResource("ReportAbuseUriTemplate", _url.GetReportAbuseUriTemplate(), "3.0.0-beta", "3.0.0-rc"));
resources.AddRange(BuildResource("PackageDetailsUriTemplate", _url.GetPackageDetailsUriTemplate(), "5.1.0"));
```

`PackageDetailsUriTemplate` points at the existing package page; `ReportAbuseUriTemplate` needs a
new Razor page under `src/BaGet.Web/Pages/`.

**Verify**: new tests in `tests/BaGet.Core.Tests/` asserting both resources and their `@type`;
existing `ApiIntegrationTests` service-index assertions updated (they compare whole documents).

### Phase 2 — V2 OData feed (no schema) — DONE

New project `src/BaGet.Web.V2`. Closes issues #778 and #455 (`nuget list`, `Install-Package` on
old clients).

| Endpoint | Maps to |
|---|---|
| `GET /api/v2/$metadata` | static EDM document |
| `GET /api/v2/Packages()` | `ISearchService.SearchAsync` |
| `GET /api/v2/FindPackagesById()` | `IPackageDatabase.FindAsync` |
| `GET /api/v2/package/{id}/{version}` | existing `PackageContentController` |
| `POST /api/v2/package` | existing `PackagePublishController.Upload` |

Registered from `BaGetEndpointBuilder.MapEndpoints` (`src/BaGet.Web/BaGetEndpointBuilder.cs:11`)
behind a `Feeds:EnableV2` option, default **on** (older clients are the reason the issues exist).

**Decision**: hand-written OData-shaped XML, not `Microsoft.AspNetCore.OData`. The dependency is
heavy and only ~5 endpoints are needed. **Consequence**: `$filter`/`$orderby` support is partial;
documented, not silently absent.

**Verify**: integration tests driving the real `nuget.exe`/`dotnet nuget` V2 code path against the
test host; assert `FindPackagesById` returns every version and that `$metadata` parses.

### Phase 3 — Deprecation and vulnerability metadata (schema: 2 tables) — BLOCKED PART WAY

New tables, both with a **nullable** FK to `Packages` (B1):

| Table | Columns |
|---|---|
| `PackageDeprecations` | `Key`, `PackageKey?`, `Reasons` (json), `Message`, `AlternatePackageId`, `AlternateVersionRange` |
| `PackageVulnerabilities` | `Key`, `PackageKey?`, `AdvisoryUrl`, `Severity`, `VersionRange` |

Entities in `src/BaGet.Core/Entities/`, configured in `AbstractContext.OnModelCreating`
(`src/BaGet.Core/Entities/AbstractContext.cs:42-47`) following the existing
`BuildPackageDependencyEntity` pattern. `IContext` (`src/BaGet.Core/Entities/IContext.cs:12`) gains
both sets; `NullContext` must be updated in step.

`RegistrationBuilder` (`src/BaGet.Core/Metadata/RegistrationBuilder.cs`) populates the inherited
`deprecation` property; `SearchResponseBuilder` populates new `SearchResult` members.

Admin endpoints: `PUT/DELETE /v3/package/{id}/{version}/deprecation`, authorised as owner
(Phase 5) or global key until then.

**Risk**: `SearchResult` and `PackageMetadata` are in the published `BaGet.Protocol` package — see
B4.

#### Status and the blocker found while generating the migrations

Done: the `PackageDeprecation` and `PackageVulnerability` entities, their model
configuration in `AbstractContext`, the sets on `IContext`/`NullContext`, the navigation
properties on `Package`, and **additive migrations for SQLite and SQL Server only** - each
is exactly two `CreateTable` plus two `CreateIndex`, with `Down` dropping them.

Not done: the migrations for PostgreSQL and MySQL, and the wiring into
`RegistrationBuilder` and `SearchResponseBuilder`. The feature is deliberately not read by
any code yet, so the two tables existing on only two providers breaks nothing.

**Why PostgreSQL and MySQL are blocked.** `dotnet ef migrations add` for those two scaffolds
"an operation that may result in the loss of data", because the pre-existing model drift
recorded above leaks into the feature migration:

| Provider | Unrelated operation scaffolded | Consequence |
|---|---|---|
| PostgreSQL | `AlterColumn Published`, `timestamp without time zone` -> `timestamp with time zone` | PostgreSQL reinterprets existing values as local time and converts them, shifting every publish date on any server not on UTC |
| MySQL | `AlterColumn` on `Tags`, `Summary`, `RepositoryUrl`, `ProjectUrl` and others, `longtext` -> `varchar(4000)`, plus `Key` changes | truncates any stored value longer than 4000 characters |

Neither may ship inside a feature migration. The options, which need a decision:

| Option | Cost |
|---|---|
| Resolve the drift first, as its own reviewed change | The honest fix. The PostgreSQL one is a data conversion, so it needs a migration plan of its own, not a silent `AlterColumn`. |
| Configure the model to match what the databases actually have | Provider-specific column type configuration. Keeps data untouched, but freezes the old shapes and conflicts with the UTC semantics `Package.Published` now has. |
| Trim the alterations out of the generated migration, keep the regenerated snapshot | Makes the migration additive and stops the drift re-appearing in later migrations, but leaves the model claiming types the database does not have - Npgsql would then read `Published` as if it were `timestamptz`. Rejected as a silent runtime hazard. |

Until this is settled, Phase 3 cannot be completed, and the plan's "all four database
providers" constraint does not hold for it.

### Phase 4 — Catalog resource (schema: 1 table; storage)

New project `src/BaGet.Catalog`. The catalog is an append-only log of package events, consumed by
mirrors.

- `CatalogLeafs` table: `Key`, `CommitId`, `CommitTimestamp`, `Type`, `PackageId`, `PackageVersion`.
- Leaf and page documents written through `IStorageService` so all four storage providers work.
- `PackageIndexingService` (`src/BaGet.Core/Indexing/PackageIndexingService.cs`) and
  `PackageDeletionService` emit leaves.
- `Catalog/3.0.0` added to the service index.

**Decision**: pages are regenerated on write, not on read. **Consequence**: a write costs an extra
storage round trip; reads stay cheap and cacheable.

**Risk**: this is the first feature that makes push do real work after the database commit. Failure
must not fail the push — log and reconcile, with a `catalog rebuild` CLI command
(`src/BaGet/Program.cs` already hosts commands such as `import`).

### Phase 5 — Identity, owners, scoped API keys (schema: Identity's 7 tables + 3)

New project `src/BaGet.Identity`.

- ASP.NET Core Identity with EF stores, over the **same** `DbContext` per provider so a deployment
  keeps one database. Identity tables are additive.
- External login via OIDC (`Authentication:OpenIdConnect:*` options).
- New tables: `Organizations`, `PackageOwners` (`PackageId`, `OwnerId`, nullable `PackageKey`),
  `ApiKeys` (`Key`, `OwnerId`, `HashedKey`, `Scopes`, `PackageGlobPattern`, `Expires`).
- `IAuthenticationService` (`src/BaGet.Core/Authentication/IAuthenticationService.cs:8`) gains an
  overload returning *who* authenticated and *what* they may do, rather than `bool`.

**No-breaking-change mechanics**:

- The existing global `ApiKey` keeps working as a superuser key. `ApiKeyAuthenticationService`
  becomes one `IAuthenticationService` implementation among several, selected by the existing
  provider pattern (`DependencyInjectionExtensions.Providers.cs:26`).
- Packages present before the upgrade have no owner row. Ownership checks treat "no owner" as
  "global key only", so nothing becomes inaccessible and **no reindex is needed**.
- Prefix reservation (`verified` in search results) is a flag on `PackageOwners`.

**Risk**: largest phase by far. Splitting it is tempting but ownership without identity is
meaningless. If it must be split, the seam is: 5a Identity + sign-in, 5b owners + scoped keys.

### Phase 6 — Validation pipeline (schema: 1 table; storage)

New project `src/BaGet.Validation`.

- `PackageValidations` table: `Key`, `PackageKey?`, `State`, `Type`, `Message`, `Started`, `Completed`.
- Push becomes: store → `Validating` → queue → validators → `Available` or `Quarantined`.
- Validators: NuGet signature/certificate check, portable-PDB check (implements the TODO at
  `SymbolIndexingService.cs:130`), PDB-to-DLL match (`:148`), plus an `IPackageValidator` hook for
  malware scanning.
- `RepositorySignatures/5.0.0` resource and signing keys.

**Decision**: in-process background queue by default, with an `IValidationQueue` abstraction.
**Consequence**: validation does not survive a restart in the default configuration; documented,
and a durable implementation can be added without touching callers.

**No-breaking-change mechanics**: validation is **off** by default. With it off, push behaves
exactly as today and no package enters `Validating`.

**Risk**: a package that is `Validating` must not be downloadable, which changes what
`PackageContentController` serves. With validation off this code path is unreachable — keep it that
way to protect existing deployments.

### Phase 7 — Search parity

- Prefix search on package id (`src/BaGet.Azure/Search/AzureSearchService.cs:111`).
- Prerelease/semver2/packageType filters (`:112`).
- Versions autocomplete (`:143`, and `src/BaGet.Azure/Table/TableSearchService.cs:63`).
- **Escape `packageId`** before interpolating it into a search query (`:152`) — a query-injection
  bug, worth doing on its own merits.
- Correct `TotalHits`: `SearchResponseBuilder.BuildSearch` currently reports the page size, while
  `AutocompleteResponse.TotalHits` is documented as the total disregarding skip/take. Needs a count
  query and changes a public response.

### Phase 8 — Search failover and client SDK

`src/BaGet.Protocol/Search/SearchClient.cs:27`, `AutocompleteClient.cs:27,40`, and
`NuGetClient.cs:165` — the service index may advertise several search endpoints; try them in order.
`NuGetClientFactory.cs:196` — refresh the cached service index periodically.

### Phase 9 — Statistics and web UI

Download statistics, owner-facing manage-package pages, report-abuse handling, profile pages.
Depends on Phase 5.

## Dependency order

```
1 ─┐
2 ─┼─> 3 ─> 4 ─┐
   │            ├─> 9
   └─> 5 ─> 6 ──┘
       7 (independent)
       8 (independent)
```

Phases 1, 2, 7 and 8 can land in any order and need no schema change beyond Phase 3's.

## Verification per phase

| Step | Command |
|---|---|
| Build | `dotnet build BaGet.sln -c Release` — 0 errors |
| Unit + integration | `dotnet test BaGet.sln` — currently 463/463; no phase may reduce it |
| Migrations | `dotnet ef migrations add <name>` in each of the 4 `BaGet.Database.*` projects, then `dotnet test tests/BaGet.Tests` (it applies migrations) |
| Upgrade-in-place | Restore a database built from the previous tag, run the new build, assert packages are still listed and downloadable |
| Protocol conformance | Point real `dotnet restore`, `nuget list` and `dotnet nuget push` at the test host |
| Determinism | Full suite three times (shared static state exists, e.g. `FrameworkCompatibilityService`'s static cache) |

## Risks

| Risk | Phase | Mitigation |
|---|---|---|
| SQLite cannot alter a foreign key; EF 10 errors on a model/snapshot mismatch | 3–6 | Additive tables with nullable FKs only. Verified by B1 before any code. |
| 4 providers × ~5 schema changes = ~20 migrations to keep consistent | 3–6 | One migration per phase per provider, never per entity. Snapshot diffs reviewed. |
| `BaGet.Protocol` is a published SDK; new members may be a breaking change | 3, 8 | Resolve B4 first; additive members only, no changed property types. |
| Identity reshapes auth for every endpoint | 5 | The current global key remains a valid superuser credential, covered by existing tests (B5). |
| Validation makes push asynchronous | 6 | Off by default; the `Validating` path is unreachable unless enabled. |
| Catalog write failures could fail a push that already succeeded | 4 | Write after commit, log on failure, provide a rebuild command. |
| Scope: 9 phases, 4 new projects, ~15 new tables | all | Each phase is independently mergeable and independently useful. Phases 1, 2 and 7 deliver value with no schema change. |
| Upstream is dormant (last push 2024-07, 51 open PRs) | all | Expect to carry these in the fork; keep each PR small enough to be reviewable if upstream revives. |

## Explicitly out of scope

- nuget.org's own infrastructure: Azure-only services, its CDN, its internal telemetry.
- Package content moderation decisions; only the report-abuse plumbing is in scope.
- Migrating the existing model TODO at `AbstractContext.cs:107` (making the package-to-dependency
  relationship required). It drops and recreates a foreign key, which SQLite cannot do, so it needs
  a database rebuild and a reindex — a breaking change this plan forbids.
