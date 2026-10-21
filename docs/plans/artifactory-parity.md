# Artifactory parity: multi-repository, multi-format (NuGet, npm, Maven, Docker), permissions, retention, scanning, HA, API, replication

Turns BaGet Extension from one NuGet feed per instance into a repository manager: many repositories of four
formats, each local, remote or virtual, with Artifactory's permission model, plus retention, vulnerability scanning,
an admin REST API, replication and high availability.

**Size.** This is several products' worth of work. Each phase below ships on its own and gets **its own
implementation run** (worktrees, review). Do not implement the whole plan in one run.

## Status (2026-10-21)

| Phase | State | Commits / where |
|---|---|---|
| P0 Repository foundation | **Done** | `8836612`..`6fc2c2a` (2026-10-20); deviations under P0 |
| P1 Remote and virtual NuGet | **Done**: remote + caching (track `p1-remote`, [remote-repositories.md](../remote-repositories.md)); virtual + D3 (track `p1-virtual`, [virtual-repositories.md](../virtual-repositories.md)) | `dc2ef5e`..`53a399a` (remote), `ef4c403` (virtual), review fixes `3e6c8e2`, `b64b039`, `06696f3`, `103489c`, `9636888`, `312b363`; `IVirtualRepositoryAdministration` creates `Virtual`, `RepositoryService.CreateAsync(..., Virtual)` stays refused (no members) |
| P2 Access control | **Done** (implemented with its UI by the UI-coverage plan, since removed); documented in [access-control.md](../access-control.md) | tracks `access`, `access-ui`, `keys` |
| P3-P10 | Not started | No format layer, npm/Maven/OCI, retention, scanning, `/api/v1`, replication, `AddDataProtection` or health endpoints exist |

## Blocking — resolve before coding

| # | Check | Why it blocks | How |
|---|---|---|---|
| **B1** | Data model for four formats | NuGet's tables (`Package`, dependencies, frameworks; `AbstractContext.cs:102-104` unique `(Id, NormalizedVersionString)`) don't fit npm (scoped names, dist-tags), Maven (GAV, snapshots, `maven-metadata.xml`) or OCI (blobs by digest, manifests, tags). | Spike the schema in D1 on all four relational providers: generate the migration, then confirm `dotnet ef migrations list` and no pending model changes per provider (CLAUDE.md, *Migrations*). |
| **B2** | Large blobs | Docker layers reach many GB. OCI needs chunked uploads (`PATCH` with `Content-Range`), resumable sessions, digest verification while streaming, and `HEAD`/range `GET`. `IStorageService` does whole-stream `PutAsync`; Kestrel/IIS body limits are ~2 GB (`ConfigureBaGetOptions.cs:82,133`). | Prototype a 5 GB layer through each storage provider (FileSystem, S3 multipart, Azure block blobs, GCS resumable, Aliyun multipart) without buffering in memory. Measure memory. |
| **B3** | Docker client auth | `docker login` needs the token flow (`WWW-Authenticate: Bearer realm=…/v2/token`), not API keys. npm uses `_authToken`; Maven uses HTTP Basic in `settings.xml`. | Confirm each client's flow against real CLIs (B6) before designing C2's credential types. |
| **B4** | HA prerequisites | SQLite and the FileSystem store on local disk can't be shared. In-memory state: `MaintenanceGate`, `DatabaseCopyService` (`DependencyInjectionExtensions.cs:90-91`), `ContactRateLimiter`/`PasswordResetRateLimiter` (`AccountsServiceCollectionExtensions.cs:43-44`), `TelemetryStatus`. No `AddDataProtection` anywhere, so each node mints its own cookie keys. | List every singleton holding state (`grep TryAddSingleton`) and classify each as per node or shared. Decide the shared store (D6). |
| **B5** | Vulnerability data source | Licensing and offline use. | OSV.dev (API free, data CC-BY 4.0 / per-source licences) covers NuGet, npm and Maven. Docker images need image scanning (OS packages), which OSV doesn't do. Decide in D7. |
| **B6** | Client test rigs | Protocol tests need the real `dotnet nuget`, `npm`, `mvn` and `docker` clients. | Testcontainers images with each CLI (`node`, `maven`, `docker:dind`), skipped without Docker like `BaGet.Database.Tests`. Confirm `docker:dind` runs on the CI and dev machines (privileged). |
| **B7** | Upgrade path | Existing installs have one feed. URLs, storage paths (`packages/…`, `PackageStorageService.cs:12`), owners and API keys must keep working. | **Done (P0):** `UpgradeFromPreRepositoryTests` (SQLite app level incl. the `packages/…` store) and `EngineUpgradeTests` (all four engines, `82bfe77`). |
| **B8** | Azure Table database | Non-relational, packages only (CLAUDE.md). | **Decided (P0), applies to P1:** Azure Table and `Search:Type=AzureSearch` serve `default` only (`SupportsMultipleRepositories=false`; creating another repository, remote ones included, is refused rather than refusing startup). There `Mirror:*` is read directly (no `mirror` row can exist) and works as before. |
| **B9** | Where a remote's credentials live (P1) | A password must not sit in clear in `Repository.Settings`, logs or a page. | **Done (P1):** protected with ASP.NET Core data protection (`IRemoteCredentialProtector`; `Microsoft.AspNetCore.DataProtection.Abstractions` in Core restores cleanly beside the 9.0.21 pins), stored as `protectedPassword` in the `Settings` JSON, never read back to a page, never logged; a host with no data protection refuses to store one. No schema change: `Settings` is an unbounded string on all four providers, so no migration. The keys live with the host's key ring (P10 moves them into the DB). |
| **B10** | Virtual repositories: storage, scope, the services to wrap (P1) | A virtual repository must not need a migration, must be refused where only `default` exists, and its reads must reuse the member's real services rather than reimplement them | **Done (P1):** members are names in `Repository.Settings` (an unbounded string on all four providers, snapshots checked: `longtext`/`text`/`nvarchar(max)`/`TEXT`), so no migration and no pending model change. `SupportsMultipleRepositories` (Azure Table, `AzureSearch`) already refuses non-default repositories, virtual included. Every read runs in the member's own `CreateRepositoryScope` (its filter, storage prefix, upstream, access check), so nothing member-specific is reimplemented; the wrappers delegate untouched when the repository is not virtual |

## Decisions

| # | Decision (taken) | Consequence |
|---|---|---|
| D0 | **Formats: NuGet, npm, Maven, Docker (OCI)** | A format layer in Core. Each format is a module with its own protocol, metadata tables and routes. Multi-month scope. |
| D1 | **Common `Repository`, format-specific metadata.** Tables `Repositories`, `Projects`; NuGet tables gain `RepositoryKey`; new `Npm*`, `Maven*`, `Oci*` tables per format | No lossy "generic artifact" table. Each format keeps native queries. Four migrations per provider per phase. |
| D2 | **URLs: `/` stays the `default` NuGet repository; others at `/r/{repo}/…`** (`/r/{repo}/v3/index.json`, `/r/{repo}/api/v2`, `/r/{repo}/npm/`, `/r/{repo}/maven/`; Docker at `/v2/{repo}/…` as OCI requires) | Upgrades need no client changes. Existing data moves into `default` with storage paths unchanged (`packages/…` = `default`); new repositories use `repos/{repo}/…`. |
| D3 | **Repository kinds: local, remote (upstream cache), virtual (ordered members + default deploy target)**, for every format | `MirrorOptions` (`MirrorOptions.cs:8-38`) becomes a remote NuGet repository created by the upgrade when `Mirror:Enabled`. |
| D4 | **Full Artifactory permission model:** users, **groups**, **permission targets** (repositories by name or pattern + include/exclude path patterns, actions Read / Annotate / Deploy-Cache / Delete-Overwrite / Manage), **projects** (repositories grouped, project admins, project roles), anonymous access per repository, **access tokens** (scoped, expiring) | Package ownership (`PackageOwner`, `Accounts.cs:62`) stays as a NuGet-specific rule *inside* Deploy permission. Private reads become possible: every read route goes through an authorization check. |
| D5 | **Retention, scanning, REST API, replication, HA all in scope** | P6-P10. |
| D6 | **HA = shared relational DB + shared object storage + Redis** (distributed locks, rate limits, gate state, data-protection keys in the DB) | SQLite and FileSystem storage remain single node only; HA is refused at startup with them. |
| D7 | **Scanning: OSV for NuGet/npm/Maven; Docker via optional Trivy** (external binary or server) | Without Trivy, Docker images are not scanned and the UI says so. |

## Phases

Order = dependency. Each phase: blocking checks first, tests first, full suite green, 0 warnings, docs in the same
commits, Opus review.

### P0 — Repository foundation (NuGet only, no behaviour change) — DONE

Implemented 2026-10-20 (`8836612` foundation, `dcc140d` routes, `e033c20` storage layout, `520d8c1` admin UI,
`8af8352`/`e5ed514`/`1482068`/`23e70df` suites against `/` and `/r/second/`, `82bfe77` B7, `b0f83e1`/`6fc2c2a` hardening).
Deviations from the table below, as built:

| Item | As built |
|---|---|
| `Repository.Settings` (JSON), `ProjectKey`, `Project` entity | Columns/table exist; nothing reads or writes them yet (P1/P2) |
| `AnonymousRead` | Enforced since P2 U2: gates the Everyone group's Read ([access-control.md](../access-control.md)) |
| Migration | Also seeds the `default` row with key 1 (PostgreSQL `setval`); `EnsureDefaultAsync` restores it at startup |
| Delete repository | Refused for `default`, for a repository with packages or a pending validation; purges catalog, owners, validations, abuse reports and `repos/{name}/` in storage |
| Razor Pages | Stay at `/` and show `default` only; repository-aware pages (`/r/{repo}/…`, header switcher, upload picker) were added later by the UI-coverage plan |
| Mirror | Serves `default` only (`UpstreamClientFactory`); conversion into a remote repository is P1 |

| Item | Where |
|---|---|
| `Repository` (`Key`, `Name` unique, `Format`, `Kind`, `ProjectKey?`, settings JSON, `AnonymousRead`), `Project` entities | `src/BaGet.Core/Entities/` new. Model in `AbstractContext.OnModelCreating` (`AbstractContext.cs:~90`) |
| `RepositoryKey` on `Package`, `PackageOwner`, `CatalogEntry`, `PackageValidation`, `AbuseReport`; unique index → `(RepositoryKey, Id, NormalizedVersionString)` | `AbstractContext.cs:102-104, 236, 269-270`. 4 migrations; data migration creates `default` and assigns every row |
| `IRepositoryContext` (scoped): the current repository, resolved from the route (`/r/{repo}`) or `default` | new in Core. Every `IPackageDatabase` / `ISearchService` / storage call takes it, no global feed |
| Routes: register every existing endpoint twice, at `/` (default) and `/r/{repo}/` | `BaGetEndpointBuilder.cs` (262 lines), `Routes.cs`. `IUrlGenerator` (`IUrlGenerator.cs:8`) builds repo-aware URLs so service indexes advertise the right base |
| Storage: `default` → `packages/…` (unchanged), others → `repos/{repo}/packages/…`; symbols likewise | `PackageStorageService.cs:12, 218-230`, `SymbolStorageService` |
| Admin UI: repository list, create/edit/delete (local NuGet only in P0) | `src/BaGet.Identity/Pages/Admin/Repositories*` |

Verification: the existing suite is unchanged against `/`; the same suite parameterised against `/r/second/`; B7 upgrade test.

### P1 — Remote and virtual NuGet repositories — DONE

| Item | Note |
|---|---|
| Remote: upstream URL (V3 or V2), credentials (sensitive), cache period for metadata, "store artifacts locally" | **Done.** Generalises `PackageService` mirroring per repository. Upgrade converts `Mirror:*` into a remote repo; `default`'s virtual (D3) is wave 2, see *As built* |
| Virtual: ordered members, default deploy repository, resolution order for search/registration/download (first hit), merged search with paging | **Done**, see *As built: virtual repositories* below |
| Caching | **Done.** Metadata TTL, negative cache for 404s, offline mode |

#### As built: remote repositories and caching (track `p1-remote`)

| Item | As built |
|---|---|
| Model | `Repository.Kind = Remote`, upstream in `Repository.Settings` (`RemoteRepositorySettings`: URL, protocol V3/V2, user, protected password, metadata TTL, negative TTL, offline, store locally, timeout). No migration |
| Fetching | `UpstreamClientFactory` returns `RemoteUpstreamClient` for every repository: `IRemoteUpstreamSource` resolves the upstream of the current repository, the client applies offline, TTLs, stale-on-error and health (`RemoteRepositoryState`, in memory per node). `V2/V3UpstreamClient` gained `propagateErrors` so only a real 404 is cached as "not found" |
| **Seam for D3 (wave 2)** | `IRemoteUpstreamSource.ResolveAsync(repository)`: remote -> its own settings; `default` with `Mirror:Enabled` -> the `mirror` remote repository (created/synced at startup by `EnsureMirrorRepositoryAsync`; URL/protocol/timeout stay owned by `Mirror:*`); else none. A virtual repository resolves its members here. Until then the default repository stores mirrored packages in its own rows, exactly as before |
| Pass-through | `StoreLocally=false`: `PackageService` answers from the upstream and `IPackageService.OpenUncachedAsync` streams downloads; nothing is stored |
| Access | `Deploy` is never granted on a remote (`AccessEvaluator`, administrators too); `[RemoteRepositoryReadOnly]` on push and symbol push (`403`, also without accounts); upload picker and page exclude it. Read, Delete, Annotate follow the model |
| UI | `/admin/repositories` creates remote repositories; `/admin/repositories/{name}`: upstream form, *Test connection* (unsaved values), cache status, *Purge cache*, *Purge package*, browse link; mirror note on `default`. Create, save, test and purge ask for the password again |
| Tests | `RemoteRepositoryTests` (service + HTTP, in-memory upstream, fake clock), `AdminRemoteRepositoriesTests`, `RemoteUpstreamSecurityTests`, `MirrorRepositoryTests`, `UpgradeFromPreRepositoryMirrorTests`, `RemoteRepositorySettingsTests` (Core) |
| Not done | V2 remotes are not exercised end to end (NuGet.Protocol uses its own HTTP stack, so there is no in-memory upstream); remembered answers and counters are per node (P10 shares them); purge runs inline (a very large cache would deserve an `AdminJobService` job) |

#### As built: virtual repositories and D3 (track `p1-virtual`)

| Item | As built |
|---|---|
| Model | `Repository.Kind = Virtual`, `VirtualRepositorySettings` JSON in `Repository.Settings` (member names in order, deploy repository name). No migration (B10). Nested virtuals expanded in place, each repository once, cycles refused on save and cut on read |
| Reads | `VirtualRepositoryReader` runs each question in the member's own scope, only for members the caller may read (Read on the member and the package). Download/nuspec/readme/icon/symbols: first member that has the version. Version list and registration index: union of versions, each from the first member that has it. Search/autocomplete/dependents: merge ([docs](../virtual-repositories.md#what-a-client-sees)). V2 feed and vulnerability index read the members' stored data. Wrappers: `VirtualPackageService`, `VirtualPackageContentService`, `VirtualSymbolStorageService`, `RepositorySearchService` (`AddRepositoryAwareSearch`) |
| Writes | Moved to the deploy repository by `RepositoryResolutionMiddleware` (write routes and the validation status) and `RepositoryPageFilter` (Upload page); authorized there. Deploy is never granted on the virtual repository itself. No deploy repository: `403` |
| Catalog | Not served by a virtual repository (404, and the service index omits it) |
| Pages | `Statistics`, `ReportAbuse`, `Manage`, `ContactOwners` are 404 on a virtual repository; `Index`, `Package`, `Upload` work |
| UI | `/admin/repositories`: create virtual; `/admin/repositories/{name}`: members (add, remove, up/down), deploy picker, *Where would a package come from?*, effective access; a member shows where it is used; delete refused for a member; upload link only when a deploy target exists; Upload page names the deploy repository |
| D3 | `default` + `Mirror:Enabled` is a virtual repository of [its rows, `mirror`] that keeps its URL and stores in its own rows (as before); no stored virtual row stands for it. A virtual repository may list `default` (mirror fall-through included) or `mirror` (its own cache). See [docs](../virtual-repositories.md#default-and-the-mirror) |
| Tests | `VirtualRepositoryTests`, `VirtualRepositoryAccessTests`, `VirtualRemoteMemberTests`, `VirtualDefaultMirrorTests`, `VirtualSearchBoundTests`, `AdminVirtualRepositoriesTests` (BaGet.Tests); `VirtualRepositorySettingsTests`, `RepositorySearchServiceTests` (Core) |
| Deviations from the sketch | (1) Registration index and version list **merge versions across members** instead of taking the first member that has the package id: with a local and a nuget.org member, first-hit by id would hide every upstream version of an id the local member also holds (Artifactory merges too); a single version is still first-hit. (2) Search is a **windowed merge** rather than a lazy k-way merge: each member is read for its first `skip + take + 1` results (500 per call, at most 10,000; a member of at most 2,000 is read completely), then one ordered union. The members' orders (database collation, per-member downloads) are not comparable, so a lazy merge could not give an exact total or exact summed downloads: the total is exact while every member was read completely, else an upper-bound estimate, and a summed-downloads rank can be slightly low at a window edge. (3) *Last updated* is not a virtual sort (results carry no date). (4) `ISearchService` is wrapped by `AddRepositoryAwareSearch()` (last registration), not by a new interface, so pages and tests keep injecting `ISearchService`. (5) `RepositoryService.CreateAsync(Virtual)` stays refused; `IVirtualRepositoryAdministration` creates. (6) Member edits need no password confirmation (they expose nothing: reads are filtered per caller) |
| Review fixes (Opus review of P1) | **SSRF:** *Test connection* asks for the password, follows redirects only within the typed origin (5 hops), reads at most 1 MB; `Remote:BlockPrivateAddresses` (default off, documented exposure) refuses loopback/private/link-local upstreams on create, save, test and on every V3 connection (`b64b039`). **Same credential rule:** test and save both use `SameCredentialScope` (origin + user name) (`b64b039`). **Preview:** shares the request rules: each member's `PackageService.ProbeAsync` (stored copy, then `IUpstreamClient.ProbeAsync`, same offline / remembered-404 gate as a download), so the default repository's mirror counts and an offline upstream is `Offline` (`06696f3`). **Gate:** `MirrorAsync` no longer swallows `MaintenanceException` (503 + `Retry-After` for a remote and through a virtual) (`3e6c8e2`). **Anonymous read:** create forms choose it; a remote with credentials starts closed; the auto-created `mirror` stays anonymous like `default` (`9636888`). **Search:** bounded as above (`103489c`). **B7:** `UpgradeFromPreRepositoryMirrorTests` (`312b363`). Rejected: none. Nuance on finding 2: a remembered version list could not reach `WouldFetch` through the page (saving a remote clears its answers), but the preview did ignore offline mode and the mirror fall-through |
| Not done | No per-member include/exclude path rules; no merged catalog; remote members' health and answers stay per node |

### P2 — Access control (D4)

> Done: implemented with its UI by the UI-coverage plan (since removed); see [access-control.md](../access-control.md).

| Item | Note |
|---|---|
| Groups, memberships; permission targets (repos + patterns, include/exclude path patterns, actions × users/groups); projects (roles: project admin, developer, viewer; repos owned by a project) | New tables, 4 migrations. `BaGetPrincipal` (`BaGetPrincipal.cs:11`) gains the resolved permission set |
| Authorization on **every** route: read included. Anonymous read per repository | Policy-based (`IAuthorizationService` + requirement per action). NuGet reads accept Basic auth (user + access token or API key as password), as NuGet clients send |
| Access tokens: scoped to repos/actions, expiring, revocable; existing scoped API keys map onto them | Existing keys keep working (B7) |
| UI and audit log for every permission change | Admin pages; audit through the existing log + a table |

### P3 — Format layer + npm

| Item | Note |
|---|---|
| `IFormatModule` (routes, storage layout, metadata indexing, search provider, retention hooks, scanner adapter); NuGet refactored onto it | Core abstraction; NuGet behaviour unchanged (full suite) |
| npm registry API: packument `GET /{name}`, scoped `@scope/name`, tarball download, `npm publish` (`PUT`), dist-tags, deprecate, unpublish rules, `npm login` / `_authToken`, `/-/v1/search` | Tables `NpmPackages`, `NpmVersions`, `NpmDistTags`. Remote = registry.npmjs.org proxy; virtual merges packuments |
| Tests | Real `npm` CLI in a container: publish, install, dist-tag, scoped packages, remote and virtual |

### P4 — Maven

| Item | Note |
|---|---|
| Maven 2 layout `GET/PUT/HEAD /{group/path}/{artifact}/{version}/{file}`, checksums (`.sha1`, `.sha256`, `.md5`), `maven-metadata.xml` generation, SNAPSHOT resolution (timestamped), release vs snapshot policy per repo, POM parsing for search | Tables `MavenArtifacts`, `MavenVersions`, `MavenFiles` |
| Remote = Maven Central proxy; virtual = merged metadata | |
| Tests | Real `mvn deploy` / `mvn dependency:get`, and Gradle if B6 allows |

### P5 — Docker / OCI distribution

| Item | Note |
|---|---|
| OCI Distribution Spec v1.1: `/v2/`, blobs (`HEAD/GET`, monolithic and chunked upload sessions, cross-repo mount), manifests (`PUT/GET/DELETE` by tag and digest, image index), tags list, referrers API, `docker login` token endpoint (B3) | Tables `OciRepositories`, `OciManifests`, `OciTags`, `OciBlobs` (digest, size, refcount). Storage by digest, deduplicated |
| Streaming storage (B2): `IStorageService` gains a multipart/append API | All storage providers |
| Garbage collection of unreferenced blobs | Runs under P6's retention engine |
| Remote = Docker Hub / any registry proxy (with its token auth); virtual = ordered pull-through | |
| Tests | `docker:dind`: push, pull, multi-arch index, chunked upload, conformance subset (`opencontainers/distribution-spec` conformance tests) |

### P6 — Retention and cleanup

| Item | Note |
|---|---|
| Policies per repository or project: keep last N versions, delete older than X, prerelease/snapshot-only, unused for X days (last download), remote cache size cap, untagged Docker manifests | `RetentionPolicies` table; per-format hooks from `IFormatModule` |
| Dry run report, schedule, audit, respects legal hold | Background worker; write gate aware |

### P7 — Vulnerability scanning (D7)

| Item | Note |
|---|---|
| OSV client (online + optional offline mirror), scan on deploy and on schedule, results table, policies per repo/project: warn, block download, block deploy by severity | Reuses the existing `PackageVulnerability` model for NuGet's VulnerabilityInfo resource |
| Docker: optional Trivy integration (configured path or server URL) | Unscanned images marked |
| UI: per package/version findings, repository dashboard | |

### P8 — Admin REST API

| Item | Note |
|---|---|
| `/api/v1/…` for repositories, projects, users, groups, permission targets, tokens, retention, scans, replication; OpenAPI document | Same authorization as P2 (Manage). Versioned, paged. |

### P9 — Replication

| Item | Note |
|---|---|
| Push (on deploy and scheduled) and pull replication between BaGet instances per repository; delete propagation optional; resumable; conflict = target wins unless configured | Uses P8's API + format protocols. Replication log table |

### P10 — High availability (D6)

| Item | Note |
|---|---|
| Data protection keys persisted to the DB; Redis (`IDistributedCache`, Redis locks) for rate limiters, the maintenance gate, copy status, telemetry status, retention/replication job leases | Every B4 singleton becomes shared or explicitly per node |
| Background workers run once cluster-wide (leases) | |
| Startup refuses HA mode with SQLite or FileSystem storage | |
| Health endpoints (`/health/live`, `/health/ready`) for load balancers | |

## Verification

| # | Check | Expected |
|---|---|---|
| 1 | Upgrade (B7) | A pre-upgrade database and store serve unchanged at `/`. Existing API keys, owners and URLs work. The mirror becomes remote + virtual |
| 2 | Each phase | Full suite green, 0 warnings, each format's real CLI tests (B6) green |
| 3 | Multi-repo isolation | Same id/version in two repos are independent: push, delete, search, catalog |
| 4 | Permissions | Matrix test: every route × action × (anonymous, reader, deployer, project admin, admin) |
| 5 | Private read | `dotnet restore`, `npm install`, `mvn`, `docker pull` succeed with credentials and get 401 without them |
| 6 | Remote / virtual | Upstream down: cached artifacts still served; first-hit order respected |
| 7 | Docker large layer | 5 GB push and pull with bounded memory (B2) |
| 8 | Retention | Dry run equals actual deletion; nothing outside the policy touched |
| 9 | Scanning | Known-vulnerable package flagged; blocking policy returns 403 with reason |
| 10 | Replication | Push/pull converges after interruption |
| 11 | HA | Two nodes behind a balancer: sign-in, rate limits, gate, jobs run once; kill a node mid-push |

## Risks

| Risk | Handling |
|---|---|
| Scope (multi-month) | Phase gating: each phase ships and is reviewed alone; stop after any phase with a working product |
| Breaking existing installs | D2 + B7 upgrade test is a merge gate for P0 |
| Permission bypass on a forgotten route | Deny-by-default endpoint filter: a route with no declared action requirement fails a startup check |
| OCI conformance gaps | Run the official conformance suite in CI (P5) |
| Storage providers without streaming append | B2. A provider that can't is refused for Docker repositories |
| Four DB providers × many migrations | Each phase's migrations generated and verified on all four (CLAUDE.md *Migrations*); engine tests (`BaGet.Database.Tests`) extended |
| Licensing of vulnerability data | B5; attribution in docs/UI |
| EF 9 pin (Pomelo) | No phase may raise EF; Redis/OCI libraries checked against the `Microsoft.Extensions` 9.0.21 pin like OpenTelemetry was |

## Out of scope

- Other Artifactory formats (PyPI, Go, Helm, Conan, generic, Terraform, …). The format layer (P3) makes them additive later.
- Xray-grade binary/license analysis beyond OSV and Trivy; JFrog Distribution / release bundles; build info and AQL.
- Federated (multi-site active-active) repositories: replication (P9) covers push/pull only.
