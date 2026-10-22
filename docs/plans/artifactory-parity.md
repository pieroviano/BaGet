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
| P3 Format layer + npm | **Format layer done** (track `p3-format`, [formats.md](../formats.md)); **local npm registry done** (track `p3-npm`, [npm-registry.md](../npm-registry.md)); **remote and virtual npm done** (track `p3-npm-remote`, see *As built: remote and virtual npm* under P3). P3 is complete | `cf8cd41`..`da27c53` (format `cf8cd41`,`173c5a3`; npm `dd56089`..`62881d0`; remote/virtual `2f2a951`..`da27c53`); `IFormatModule`, NuGet on it, `Npm` format, npm tables (migration x4), Clients rig; `NpmRegistryService`, `NpmController`, npm pages, real-npm tests |
| P4 Maven | **Done**: local (track `p4-maven`), remote (Central proxy) and virtual (track `p4-maven-remote`); see [maven-repositories.md](../maven-repositories.md); Gradle not tested | `c97d37a` (remote/virtual), `769bd3e`, integration `7ff8c17`; branches `wt/p4-maven`, `wt/p4-maven-remote` |
| P5 Docker / OCI | **Done**: streaming storage (B2, track `p5-storage`, [formats.md](../formats.md#streaming-storage-b2), `6bf588a`, `b06565f`); local registry (track `p5-oci`, [docker-registry.md](../docker-registry.md), `f9bdd3e`..`49b2124`); remote, virtual and garbage collection (track `p5-oci-remote`, `9bd42b6`..`9f639e5`) | see *As built* under P5 |
| P6-P10 | Not started | No retention, scanning, `/api/v1`, replication, `AddDataProtection` or health endpoints exist |

## Blocking — resolve before coding

| # | Check | Why it blocks | How |
|---|---|---|---|
| **B1** | **Done (P3 format layer, npm; Maven/OCI sketched):** Data model for four formats | NuGet's tables (`Package`, dependencies, frameworks; `AbstractContext.cs:102-104` unique `(Id, NormalizedVersionString)`) don't fit npm (scoped names, dist-tags), Maven (GAV, snapshots, `maven-metadata.xml`) or OCI (blobs by digest, manifests, tags). | Spike the schema in D1 on all four relational providers: generate the migration, then confirm `dotnet ef migrations list` and no pending model changes per provider (CLAUDE.md, *Migrations*). **Result:** `NpmPackages` (repository-scoped, unique `(RepositoryKey, Name)`), `NpmVersions` (manifest JSON unbounded: `longtext`/`nvarchar(max)`/`text`/`TEXT`), `NpmDistTags`; migration `AddNpmTables` x4 generated, `dotnet ef migrations has-pending-model-changes` clean on all four; Maven and OCI table sketches in [formats.md](../formats.md#data-model-b1). Key lengths keep every unique index within SQL Server's and MySQL's limits. **OCI built (p5-oci):** six tables, migration `AddOciTables` x4, `has-pending-model-changes` clean on all four (see P5). |
| **B2** | **Done (p5-storage):** Large blobs | Docker layers reach many GB. OCI needs chunked uploads (`PATCH` with `Content-Range`), resumable sessions, digest verification while streaming, and `HEAD`/range `GET`. `IStorageService` does whole-stream `PutAsync`; the plan said Kestrel/IIS body limits are ~2 GB (`ConfigureBaGetOptions.cs:82,133`). | Prototype a 5 GB layer through each storage provider (FileSystem, S3 multipart, Azure block blobs, GCS resumable, Aliyun multipart) without buffering in memory. Measure memory. **Result:** `IStreamingStorageService : IStorageService` (begin / append at offset / commit to a final path with SHA-256 / abort / `GetInfo` / `GetRange`; [formats.md](../formats.md#streaming-storage-b2)) on FileSystem, Null, S3, Azure, GCS, Aliyun; shared contract `tests/BaGet.Storage.Tests` on Testcontainers. Measured with `BAGET_STORAGE_LARGE_BYTES`, 256 MiB chunks, peak growth of the test process's private memory (the blob never in memory): **FileSystem 5 GiB, 6 MiB** (139 s up, 34 s read); **Azure (Azurite) 5 GiB, 55 MiB** (195 s up, 122 s read); **S3 (S3Mock) 5 GiB, 68 MiB** (389 s up, 154 s commit, 64 s read); **GCS 1 GiB, 11 MiB** (the only GCS emulator that follows the resumable protocol, `oittaa/gcp-storage-emulator`, keeps blobs in RAM on one CPU: a 5 GiB run was abandoned after 36 min at 11 GB emulator memory; the 96 MiB contract run is 21 MiB); **Aliyun: not measured, no emulator exists** (code review + shared contract only). **Found by the 5 GiB runs (not the 96 MiB ones):** (1) Azure staged blocks were ordered by the *text* of their base64 ids, which is not index order past ~320 blocks, so a 5 GiB blob was assembled scrambled (now ordered by the index in the id, `BlockIdOrderTests`); (2) one 5 GiB `GET` on S3Mock was cut after ~1.8 GB, so S3, Aliyun and GCS now read a range as bounded 64 MiB requests, each re-requested from the exact byte on a dropped connection (`WindowedReadStream`). **Limits:** Kestrel's body limit is already off (`Program.cs:453`; the "~2 GB" was `FormOptions.MultipartBodyLengthLimit`, forms only); IIS caps one request at `IISServerOptions.MaxRequestBodySize` (BaGet default 250 MB) and `maxAllowedContentLength` (30 MB default, 4 GB ceiling), so a chunking client is fine and a single-`PATCH` client needs both raised (documented in formats.md and configuration.md). A store that cannot stream is refused for formats declaring `IFormatModule.RequiresStreamingStorage` (check in `RepositoryService.CreateAsync`; no such format until P5 OCI). Emulators: Azurite; **S3Mock instead of MinIO** (MinIO publishes no images any more); **`oittaa/gcp-storage-emulator` instead of fake-gcs-server** (which finalises a resumable session on the `bytes */*` status query, against the protocol); it ignores session `DELETE` and `ifGenerationMatch`, so those assertions are switched off for it. No UI page: the capability adds no operation an operator performs (the refusal surfaces as the repository-creation error). No OCI protocol yet (wave 2). |
| **B3** | **Done (npm verified; Docker probed; Maven documented):** Docker client auth | `docker login` needs the token flow (`WWW-Authenticate: Bearer realm=…/v2/token`), not API keys. npm uses `_authToken`; Maven uses HTTP Basic in `settings.xml`. | Confirm each client's flow against real CLIs (B6) before designing C2's credential types. **Result:** npm sends `Authorization: Bearer <_authToken>` only when configured (a `401` is final); `npm login` = `POST /-/v1/login` (web), on `404` the CouchDB-style `PUT /-/user/org.couchdb.user:NAME` returning `{ok,id,rev,token}`; so P3-npm needs access tokens as bearer tokens plus that endpoint (`NpmClientFlowTests`, real npm 10.9.9). Docker: `GET /v2/` 401 + `Bearer realm`, token endpoint called with Basic, then Bearer (probed on `docker:27-dind`). Maven: Basic after a 401 challenge (**verified in P4** with the real `mvn` 3.9, `MavenCliTests`; Maven 3.8.1+ also blocks plain-http repositories, so the test server's http needs the `maven-default-http-blocker` mirror overridden in `settings.xml`). Details in [formats.md](../formats.md#client-authentication-b3). **Docker now verified in the suite (p5-oci):** the real Docker 27 CLI in `docker:27-dind` (`DockerCliTests`) logs in through `GET /v2/` 401 + `Bearer realm` and `GET /v2/token` with Basic (user + API key), pushes and pulls with the token, and a wrong key fails the login. |
| **B4** | HA prerequisites | SQLite and the FileSystem store on local disk can't be shared. In-memory state: `MaintenanceGate`, `DatabaseCopyService` (`DependencyInjectionExtensions.cs:90-91`), `ContactRateLimiter`/`PasswordResetRateLimiter` (`AccountsServiceCollectionExtensions.cs:43-44`), `TelemetryStatus`. No `AddDataProtection` anywhere, so each node mints its own cookie keys. | List every singleton holding state (`grep TryAddSingleton`) and classify each as per node or shared. Decide the shared store (D6). |
| **B5** | Vulnerability data source | Licensing and offline use. | OSV.dev (API free, data CC-BY 4.0 / per-source licences) covers NuGet, npm and Maven. Docker images need image scanning (OS packages), which OSV doesn't do. Decide in D7. |
| **B6** | **Done for npm; dind feasible:** Client test rigs | Protocol tests need the real `dotnet nuget`, `npm`, `mvn` and `docker` clients. | Testcontainers images with each CLI (`node`, `maven`, `docker:dind`), skipped without Docker like `BaGet.Database.Tests`. Confirm `docker:dind` runs on the CI and dev machines (privileged). **Result:** `tests/BaGet.Clients.Tests` (`ClientContainers`, `HostRegistry`: a recording Kestrel server on `0.0.0.0`, reached from the container as `host.docker.internal`) runs the real npm in `node:22-alpine`, 6 tests. `docker:27-dind --privileged` starts and pulls on the dev machine (Docker Desktop/WSL2, ~25 s); CI must allow privileged containers (P5 tests skip otherwise). **P4:** `ClientContainers.MavenAsync()` (`maven:3.9-eclipse-temurin-21`) runs the real `mvn`, 4 tests in `MavenCliTests` (ran here with Docker; skip without). **P5 (p5-oci):** `DockerDaemon` starts `docker:27-dind --privileged` per run with `--insecure-registry host.docker.internal:{port}` and no TLS; the CLI runs in the same container on the unix socket; 8 tests in `DockerCliTests` (7 CLI scenarios and the conformance suite) ran here (Docker Desktop 29.8.2, WSL2), skipping without Docker or privileged containers. |
| **B7** | Upgrade path | Existing installs have one feed. URLs, storage paths (`packages/…`, `PackageStorageService.cs:12`), owners and API keys must keep working. | **Done (P0):** `UpgradeFromPreRepositoryTests` (SQLite app level incl. the `packages/…` store) and `EngineUpgradeTests` (all four engines, `82bfe77`). |
| **B8** | Azure Table database | Non-relational, packages only (CLAUDE.md). | **Decided (P0), applies to P1:** Azure Table and `Search:Type=AzureSearch` serve `default` only (`SupportsMultipleRepositories=false`; creating another repository, remote ones included, is refused rather than refusing startup). There `Mirror:*` is read directly (no `mirror` row can exist) and works as before. |
| **B9** | Where a remote's credentials live (P1) | A password must not sit in clear in `Repository.Settings`, logs or a page. | **Done (P1):** protected with ASP.NET Core data protection (`IRemoteCredentialProtector`; `Microsoft.AspNetCore.DataProtection.Abstractions` in Core restores cleanly beside the 9.0.21 pins), stored as `protectedPassword` in the `Settings` JSON, never read back to a page, never logged; a host with no data protection refuses to store one. No schema change: `Settings` is an unbounded string on all four providers, so no migration. The keys live with the host's key ring (P10 moves them into the DB). |
| **B10** | Virtual repositories: storage, scope, the services to wrap (P1) | A virtual repository must not need a migration, must be refused where only `default` exists, and its reads must reuse the member's real services rather than reimplement them | **Done (P1):** members are names in `Repository.Settings` (an unbounded string on all four providers, snapshots checked: `longtext`/`text`/`nvarchar(max)`/`TEXT`), so no migration and no pending model change. `SupportsMultipleRepositories` (Azure Table, `AzureSearch`) already refuses non-default repositories, virtual included. Every read runs in the member's own `CreateRepositoryScope` (its filter, storage prefix, upstream, access check), so nothing member-specific is reimplemented; the wrappers delegate untouched when the repository is not virtual |
| **B11** | **Done (p3-npm):** Scoped names through Kestrel | npm sends `@scope%2fname`; Kestrel keeps `%2F`, so routing could see one segment or two | Both shapes are routed (`{name}` and `@{scope}/{pkg}`) and `NpmNames.TryParse` decodes; the access patterns see the decoded lower-case name on routes (`RepositoryReadFilter`) and pages (`RepositoryPageFilter`). Real npm 10.9.9 publishes, installs and views `@acme/tool` through `%2f` (`NpmRegistryCliTests`); the test server decodes the path, so `NpmRegistryTests` covers the second shape |
| **B12** | **Done (p3-npm):** Publish body size and JSON escapes | `npm publish` puts the tarball in the JSON as base64 (a 40 MB tarball is a 53 MB string); other JSON encoders escape `/` and `+` (`\/`, `\u002B`) | `NpmPublishBody` copies the body to a temporary file, strips the `data` strings in one lexical pass and decodes each straight from the file (escapes undone, whitespace skipped); a 40 MB tarball published by real npm allocates under 24 MB on the server (`NpmLargeTarballCliTests`), a 24 MB one under 16 MB in the unit test. Found by the first test run: `System.Text.Json` itself escapes `+` |
| **B13** | **Done (p3-npm):** npm login against the real client | B3 recorded the flow; the registry needed a token that npm stores and sends back | `POST /-/v1/login` answers 404; `PUT /-/user/org.couchdb.user:NAME` checks the password with the sign-in page's rules (lockout, confirmed email; two-factor accounts refused) and answers `{ok,id,rev,token}`, the token being an **API key limited to the repository**; `npm logout` is `DELETE /-/user/token/TOKEN`. `npm login` needs a terminal (piped input crashes npm 10), so the test drives npm's own `npm-profile` login |
| **B14** | **Done (p3-npm):** Unpublish protocol | the policy had to match the requests real npm sends | `npm unpublish name@ver` = `GET ?write=true`, `PUT /name/-rev/REV` (packument without the version), `DELETE /name/-/file/-rev/REV`; `--force` = `DELETE /name/-rev/REV`; `npm deprecate` = `PUT /name` with the packument and no attachment. The revision is not checked |

| **B15** | **Done (p3-npm-remote):** What a remote npm repository trusts of its upstream | The upstream's packument names where the tarball is (`dist.tarball`) and what it hashes to; a hostile or odd registry could point this server at any address, send HTML, or send an endless body | Tarball URL is used **only when it is on the upstream's own origin** (scheme, host, port), else the conventional `{url}/{name}/-/{file}`; credentials are sent to that origin only (`HostScopedBasicAuthHandler`); a packument is read through a 128 MB cap and must be a JSON object; a downloaded tarball must match the announced `integrity` (sha512) / `shasum` or it is neither stored nor served (`502`); versions that are not semantic versions are dropped. `NpmUpstreamClient.TarballUri` (`RemoteAndVirtualNpmTests`), `ATarballUrlOnAnotherHostIsNotFollowed`, `ATarballThatDoesNotMatchTheIntegrityOfItsPackumentIsNeitherServedNorStored` |
| **B16** | **Done (p3-npm-remote):** Scoped names to the upstream | npm clients send `@scope%2fname`; a proxy that sent `@scope/name` is `404` on registries that route one segment | The upstream is always asked `@scope%2fname` (`NpmUpstreamClient.KeyName`/`EncodeName`); .NET keeps the `%2f` in `Uri` paths (verified: the in-memory upstream sees `%2f` in the raw path, and a second BaGet on Kestrel served it to the downstream); remembered answers are keyed with `%2f` too so `RemoteRepositoryState.ClearPackage` (which splits keys at the first `/`) still finds them. `AScopedPackageIsAskedOfTheUpstreamWithAnEncodedSlashAndComesBackScoped`, `APurgeOfAScopedPackageDropsItsAnswers`, real npm `AScopedPackageIsInstalledThroughARemoteRepositoryWithTheEncodedSlash` |
| **B17** | **Done (p3-npm-remote):** Where a remote npm repository keeps what it cached | A packument can be megabytes (all versions of a popular package); NuGet's cache rows (`Package`) do not fit | Tarballs and their `NpmPackages`/`NpmVersions`/`NpmDistTags` rows are stored exactly like a published version (no migration; four providers still report no pending model change), so counting, search, pages, access, the delete guard and the database copy need nothing new. The **packument is remembered in memory** (per node, at most 1000 answers per repository against NuGet's 20000) and is rebuilt from the stored rows when the upstream cannot answer (downloaded versions only) |
| **B18** | **Done (p3-npm-remote):** Writes through a virtual npm repository | `RepositoryResolutionMiddleware` moves a write route of a virtual repository to its deploy repository; the npm write routes had to qualify and the real client had to cope | npm write routes are method-constrained (`WritesOnly`), so they are moved before authentication; `INpmRegistry` itself also delegates a write to the deploy repository (`VirtualNpmRegistry`), for callers that are not routes. Verified with the real client: `npm publish` through the virtual repository lands in the deploy repository only (`AVirtualRepositoryMergesTheLocalAndTheRemoteMemberAndPublishesToItsDeployRepository`) |
| **B19** | **Done (p4-maven-remote):** What a remote Maven repository trusts of its upstream | A Maven upstream has no packument to announce hashes; some servers answer `200` with an HTML page for any path | A downloaded file is hashed while spooled and compared with the upstream's own `.sha1` (else `.sha256`, `.md5`); a mismatch is neither stored nor served (`502`); no published checksum = stored unverified; a `text/html` answer for a non-HTML path is a `404`; metadata must parse as Maven metadata; credentials go to the typed origin only (shared handler). `RemoteMavenRepositoryTests` |
| **B20** | **Done (p4-maven-remote):** Which artifact an ambiguous metadata path names | `a/b/c/maven-metadata.xml` is version `c` of `a:b` or artifact `b:c` of `a`, and the access patterns need the name before the upstream is asked | The upstream's document says (`groupId`/`artifactId`/`version`); else what is stored; else the artifact level. The read filter resolves the name only after repository-level Read passed, so a caller who cannot read the repository cannot make it ask the upstream (`APrivateRepositoryAsksNothingOfItsUpstream...`) |
| **B21** | **Done (p4-maven-remote):** Moving names, HEAD, plugin prefixes | `lib-1.0-SNAPSHOT.jar` changes under one name; Gradle/Maven send `HEAD`; `mvn dependency:...` resolves plugin prefixes from the group-level `org/apache/maven/plugins/maven-metadata.xml` | Moving snapshot names are streamed, never kept (a stored build answers only while the upstream is down); `IMavenRepository.HeadAsync` asks the upstream for headers only; group-level metadata is proxied (and merged by plugin prefix in a virtual repository). Real Central through a `*` mirror: `MavenCentralThroughARemoteRepository...` |

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

### P2 — Access control (D4) — DONE

> Done: implemented with its UI by the UI-coverage plan (since removed); see [access-control.md](../access-control.md).

| Item | Note |
|---|---|
| Groups, memberships; permission targets (repos + patterns, include/exclude path patterns, actions × users/groups); projects (roles: project admin, developer, viewer; repos owned by a project) | New tables, 4 migrations. `BaGetPrincipal` (`BaGetPrincipal.cs:11`) gains the resolved permission set |
| Authorization on **every** route: read included. Anonymous read per repository | Policy-based (`IAuthorizationService` + requirement per action). NuGet reads accept Basic auth (user + access token or API key as password), as NuGet clients send |
| Access tokens: scoped to repos/actions, expiring, revocable; existing scoped API keys map onto them | Existing keys keep working (B7) |
| UI and audit log for every permission change | Admin pages; audit through the existing log + a table |

### P3 — Format layer + npm — DONE

#### As built: format layer (track `p3-format`)

| Item | As built |
|---|---|
| `IFormatModule` + `FormatRegistry` (`BaGet.Core/Formats`) | Describes a format: route prefix, feed path, creatable kinds, storage folders, create-form fields, repository links, artifact counting / usage / purge. Per-request capabilities are scoped `IFormatService`s found by `GetFormatService<T>(format)`: `IFormatIndexer`, `IFormatSearchProvider` (NuGet implements both), `IFormatRetentionHooks` (P6), `IFormatScanner` (P7) (interfaces only). The registry refuses duplicate formats, route prefixes, storage folders and `repos` |
| NuGet on it | `NuGetFormatModule`: `RepositoryService.CreateCoreAsync` (kinds, messages unchanged), `RepositoryAdministration` (counts, delete guard and purge now call the module), admin feed URL / Browse / Upload links. No behaviour change: full suite identical |
| Routes | `BaGetEndpointBuilder.MapRoute` is public with a `format` parameter; `RepositoryRouteMetadata.Format`; `RepositoryResolutionMiddleware` 404s when the repository's format is not the route's, **including a plain (default-repository) route of a non-NuGet format** (default is NuGet) |
| `RepositoryFormat.Npm` | Appended (stored as name). No module registered, so none can be created yet (`NotSupportedException`); `RepositoryService.CreateAsync(..., fields, ...)` is the generic path |
| UI | `/admin/formats` (new, admin, linked from the menu and Repositories); Repositories page: links by module, per-format create form (`OnPostFormat`) for modules other than NuGet |
| Tables | B1 above |
| Tests | Core `FormatLayerTests` (registry, create, count/delete, NuGet adapters, npm tables), Web `RepositoryResolutionFormatFacts`, `AdminFormatsTests` (a fake npm module end to end), Clients `NpmClientFlowTests` |
| Deviations from the sketch | (1) `IFormatModule` is a **description** plus separate scoped services, not one object with methods: modules are singletons while indexing/search need the request's repository and scoped services. (2) A virtual repository is not in `CreatableKinds` (it is created with its members). (3) Not generalised yet because they are NuGet pages or NuGet-only listings: upload picker, header switcher, My packages, `Format == NuGet` filters in the access model and API-key limits (`AccessUpgradeCheck`, `IAccessEvaluator`, `ApiKeyService`, admin pickers); each must widen when npm repositories are creatable (first task of P3-npm) |

#### As built: local npm registry (track `p3-npm`)

User documentation: [npm-registry.md](../npm-registry.md).

| Item | As built |
|---|---|
| Service | `INpmRegistry` / `NpmRegistryService` (`BaGet.Core/Formats/Npm`, scoped): packument (full and install-only), tarball, publish, dist-tags, deprecate, unpublish, search. Writes take the caller and authorize through `IPackageAuthorizationService` (access model, then ownership), so routes and pages cannot differ. `NpmFormatModule` registered by `AddBaGetServices`; npm repositories are creatable (local only) |
| Tables | The format layer's `NpmPackages` / `NpmVersions` / `NpmDistTags`; **no new migration** (nothing was missing; the four providers still report no pending model changes) |
| Routes | `NpmController`, `BaGetEndpointBuilder.MapNpmRoutes` through `MapRoute(format: Npm)` in two shapes per package route; reads pass `RepositoryReadFilter` (the audit POSTs too: unconstrained), writes are method-constrained and authorize themselves with the **key-only** caller `RequestPrincipalResolver.ResolveForWriteAsync` (Basic password, Bearer token or `X-NuGet-ApiKey`; never the cookie session, which a browser sends by itself) |
| Authentication | `Authorization: Bearer` accepted for reads and writes. `INpmTokenService` (`NpmTokenService` in Identity) issues the `npm login` token = an API key of the user limited to the repository; `ApiKeyService` accepts a limit to any repository |
| Ownership | `PackageOwner` is reused (repository-scoped, key = lower-case npm name); `OwnershipAuthorizationService` asks the repository's format whether a name exists |
| Unpublish policy | **Decision (blocking check made by this track):** consistent with npmjs's 72-hour rule, expressed in this server's actions: a version (or a package whose every version is) younger than `Npm:UnpublishWindow` (72 h) can be unpublished by its owner with **Deploy**; older needs **Delete** + ownership + `package:unlist`. Zero window = always Delete. A deleted version number may be published again |
| UI | `NpmPackages` (browse/search), `NpmPackage` (readme, versions, dist-tags, dependencies, install and `.npmrc` snippets), `NpmUpload`, `Packages/NpmManage` (owners, tags, deprecate, unpublish with password); repository pages are format-aware (`RepositoryPageRouteConvention.PageFormats`, filter redirects the NuGet front page of an npm repository); header and switcher; My packages; API key repository picker; admin user deletion unpublishes sole-owned npm packages |
| Tests | `NpmRegistryTests` (40), `NpmAccessTests` (19), `NpmPagesTests` (33) in `BaGet.Tests`; `NpmPublishBodyTests`/`NpmTarballReaderTests`/`NpmNamesTests` and `NpmPublishBodyMemoryTests` in `BaGet.Core.Tests`; `NpmRegistryCliTests` (8) and `NpmLargeTarballCliTests` (1) in `BaGet.Clients.Tests` with the real npm |
| Deviations from the sketch | (0) Remote and virtual npm were done afterwards, see the next section. (1) No new migration (tables existed). (2) No `IFormatIndexer` for npm: publishing needs the caller, so it is `INpmRegistry.PublishAsync`; the format-layer search provider exists. (3) The `Format == NuGet` filters were widened (access listings, API-key limits, repository access, admin pickers); virtual repositories keep NuGet-only. (4) Pages are format-aware instead of NuGet-only: `RepositoryResolutionMiddleware` no longer format-checks Razor Pages (the page filter does). (5) Search matches package names only, and listings filter per package Read. (6) Remote (registry.npmjs.org proxy) and virtual npm repositories were not done in this track (local only); `INpmRegistry` was the seam. (7) `IUrlGenerator.GetNpmRegistryUrl` added. (8) `npm audit` answers "no advisories" until P7 |
| Not done | Download counts, `npm token`, `npm access`, `npm owner` (owners are on the Manage page), `npm star`, `dist.signatures`/provenance, ETag/304 on packuments, per-package size/quota limits, a startup check that every route declares an action requirement (the read filter and the per-route authorization are tested instead) |

#### As built: remote and virtual npm (track `p3-npm-remote`)

User documentation: [remote](../remote-repositories.md#npm-remote-repositories) and [virtual](../virtual-repositories.md#npm-virtual-repositories) repositories.

| Item | As built |
|---|---|
| Seam | `INpmRegistry` is now `RepositoryNpmRegistry`, which forwards per call to `NpmRegistryService` (local), `RemoteNpmRegistry` or `VirtualNpmRegistry` by `IRepositoryContext.Current.Kind` (resolved per call, because a write to a virtual repository continues in its deploy repository). Routes, pages, account deletion and My packages did not change. `INpmRegistry.ProbeAsync` added (the resolution preview) |
| Remote | `RemoteNpmRegistry` + `NpmUpstreamClient` over the P1 machinery, generalised: `RemoteRepositorySettings` (new `RemoteProtocol.Npm`, `ForFormat`; a password with no user name is a bearer token), `RemoteRepositoryState` (answers keyed `na:`/`nf:`/`nd:`, a per-call entry limit), `RemoteUpstreamSource`, `IRemoteHttpClientFactory` (BlockPrivateAddresses via its connect callback), `RemoteRepositoryAdministration` (test = ping then root, cache counts, purge through `NpmRegistryService.PurgeAsync`). Metadata TTL, negative cache (404 only), stale on error, offline, store locally (off = pass-through), `502` when nothing can answer, integrity check, one download per version at a time. Stored versions go through `NpmRegistryService.StoreCachedAsync` (file + rows + tags in one transaction, gate checked first) |
| Virtual | `VirtualNpmRegistry` over `VirtualRepositoryReader` (only readable members, each in its own scope, failing member skipped): packument merge (versions union with first member per version; dist-tags per tag from the first member that has it; time min created / max modified), tarball first-hit, merged search by name, writes delegated to the deploy repository. `IVirtualRepositories`/`IVirtualRepositoryAdministration` are format-aware (members and deploy target must have the virtual repository's format; `CreateAsync(..., format)`, `CandidatesAsync(..., format)`); `IFormatModule.SupportsVirtual` |
| UI | `/admin/repositories`: the remote and virtual forms ask for the format; `/admin/repositories/{name}`: npm remotes show the URL/credential fields without a protocol choice, *Test connection*, cache table, both purges (names validated as npm names), browse link to the module's page; npm virtuals show members of their format, deploy picker, preview with a package name; `/admin/formats` lists Virtual; the npm package page hides owners/Manage on a remote or virtual repository and `Packages/NpmManage` is `404` on a virtual one |
| Tests | `RemoteNpmRepositoryTests` (HTTP, in-memory `FakeNpmUpstream`, fake clock), `VirtualNpmRepositoryTests`, `AdminNpmRemoteVirtualTests` in `BaGet.Tests`; `RemoteAndVirtualNpmTests` (settings, bearer, tarball origin rule, merge) in `BaGet.Core.Tests`; `NpmRemoteVirtualCliTests` in `BaGet.Clients.Tests` (real npm against a second real BaGet as the upstream; one test against registry.npmjs.org, skipped offline) |
| Deviations from the sketch | (1) **Search on a remote repository covers the stored packages only** (Artifactory searches caches the same way); the upstream's `/-/v1/search` is not proxied. (2) **The packument is not persisted** (memory only, B17); after a restart with the upstream down only stored versions are listed. (3) **`502` for an unreachable upstream** instead of the NuGet side's "empty answer" (npm retries a 502, believes a 404). (4) The create forms gained a *Format* select instead of separate forms per format. (5) Existing NuGet wording of the admin messages is kept ("Remote repository x was created."); other formats name themselves. (6) Tests were written beside the code in one pass and the key ones were then proven by mutation (see *Verification*), not strictly first. (7) A repository named `mirror` that is not a NuGet remote one is never taken for the mirror (an npm remote can have that name) |
| Verification | Full solution run in the worktree with Docker running: 2587 tests (then +1 for the mirror guard = 2588), 13 failures, all of them the ones CLAUDE.md lists as failing on a clean checkout (`TelemetrySettingsPageTests` x8 and `TelemetryExportTests` x3 in `BaGet.Tests`, `TelemetryE2ETests` x2 in `BaGet.Telemetry.Tests`); `BaGet.Database.Tests` (16) and `BaGet.Clients.Tests` (21, including the registry.npmjs.org test) ran, none skipped. The key tests were proven by mutation (merge precedence, tarball origin rule, integrity check, the 502 mapping, write refusal, the per-version download lock, the mirror-name guard each turn at least one test red) |
| Not done | Proxying the upstream's search, `npm audit` against the upstream's advisories (P7), persisting packuments for restarts, per-member path rules, a remote npm repository as a member of a virtual NuGet repository (formats never mix), the Maven/OCI equivalents |
| Review fixes | (1) npm logout carries the token in its path: `RequestPathRedaction` masks it in the request log and (`PathRedactingProcessor`) in traces. (2) Login/logout (`RepositoryRouteMetadata.WritesContent` false) are not moved to a virtual repository's deploy repository; the login token covers the virtual repository, its members and its deploy repository. (3) The unpublish-window shortcut needs the `package:push` scope; a `package:pushversion` key is judged as for a deletion. (4) `NpmNames.TryParseLookup` accepts legacy upper case names for reads, proxying and purging; publishing stays strict |

| Item | Note |
|---|---|
| `IFormatModule` (routes, storage layout, metadata indexing, search provider, retention hooks, scanner adapter); NuGet refactored onto it | Core abstraction; NuGet behaviour unchanged (full suite) |
| npm registry API: packument `GET /{name}`, scoped `@scope/name`, tarball download, `npm publish` (`PUT`), dist-tags, deprecate, unpublish rules, `npm login` / `_authToken`, `/-/v1/search` | Tables `NpmPackages`, `NpmVersions`, `NpmDistTags`. Remote = registry.npmjs.org proxy; virtual merges packuments |
| Tests | Real `npm` CLI in a container: publish, install, dist-tag, scoped packages, remote and virtual |

### P4 — Maven — DONE

| Item | Note |
|---|---|
| Maven 2 layout `GET/PUT/HEAD /{group/path}/{artifact}/{version}/{file}`, checksums (`.sha1`, `.sha256`, `.md5`), `maven-metadata.xml` generation, SNAPSHOT resolution (timestamped), release vs snapshot policy per repo, POM parsing for search | Tables `MavenArtifacts`, `MavenVersions`, `MavenFiles` |
| Remote = Maven Central proxy; virtual = merged metadata | |
| Tests | Real `mvn deploy` / `mvn dependency:get`, and Gradle if B6 allows |

#### As built: remote and virtual Maven (track `p4-maven-remote`)

User documentation: [Maven repositories](../maven-repositories.md#remote-maven-repositories).

| Item | As built |
|---|---|
| Seam | `IMavenRepository` is `RepositoryMavenRepository` (per call by kind) over `MavenRepositoryService`, `RemoteMavenRepository`, `VirtualMavenRepository`. New: `HeadAsync`, `ProbeAsync`; `RemoteProtocol.Maven`; `MavenUpstreamClient` over the P1 machinery (`RemoteRepositoryState` path keys `mm:`/`mc:`/`mf:`, `ClearMatching`), `KeyedLocks` extracted from the npm remote |
| UI | Create/edit forms ask for the Maven format; test connection, cache table, purge by `group:artifact` and version text, virtual members and preview (`group:artifact`, any version); browse page notes and a Central `<mirror>` snippet; `docs/ui-coverage.md` |
| Tests | `RemoteMavenRepositoryTests`, `VirtualMavenRepositoryTests`, `AdminMavenRemoteVirtualTests` (about 68 together), `MavenRemoteVirtualCliTests` (6, real mvn; one uses real Maven Central) |
| Deviations from the sketch | (1) No schema change (reuses `Maven*` tables, no migration). (2) `ResolutionPreview.Version` is now a string and `PreviewAsync`/`PurgeArtifactAsync` take text versions: Maven versions (`5.3.20.RELEASE`) are not NuGet versions. (3) The release/snapshot policy does not apply to remote repositories. (4) Remote search/pages cover stored artifacts only; metadata/checksum answers are in memory only (as npm). (5) Test connection reports a `404` root as connected with a note. (6) Tests were written beside the code, key ones proven by mutation |
| Not done | Gradle against remote/virtual, per-member path rules, Maven search proxying, persisting metadata across restarts |

### P5 — Docker / OCI distribution — DONE

| Item | Note |
|---|---|
| OCI Distribution Spec v1.1: `/v2/`, blobs (`HEAD/GET`, monolithic and chunked upload sessions, cross-repo mount), manifests (`PUT/GET/DELETE` by tag and digest, image index), tags list, referrers API, `docker login` token endpoint (B3) | Tables `OciRepositories`, `OciManifests`, `OciTags`, `OciBlobs` (digest, size, refcount). Storage by digest, deduplicated |
| Streaming storage (B2): `IStorageService` gains a multipart/append API | **Done (track `p5-storage`)** as the separate `IStreamingStorageService` (no change to `IStorageService`), all providers, see B2 |
| Garbage collection of unreferenced blobs | **Done (track `p5-oci-remote`)** as admin operations `docker-gc` / dry run on the Operations page (password for the removing run); P6 schedules it. See [docker-registry.md](../docker-registry.md) |
| Remote = Docker Hub / any registry proxy (with its token auth); virtual = ordered pull-through | **Done (track `p5-oci-remote`)**: `RemoteOciRegistry`/`OciUpstreamClient` (token auth, `library/` names, streamed layer caching, TTLs, offline, `Remote:BlockPrivateAddresses`), `VirtualOciRegistry` (ordered pull-through, push to deploy repository), admin UI on Repositories/RepositoryEdit. Blocking checks: none assigned beyond B2/B3 (already done); no deviation. |
| Tests | `docker:dind`: push, pull, multi-arch index, chunked upload, conformance subset (`opencontainers/distribution-spec` conformance tests) |

#### As built: local Docker registry (track `p5-oci`)

User documentation: [docker-registry.md](../docker-registry.md).

| Item | As built |
|---|---|
| Format | `RepositoryFormat.Docker`; `DockerFormatModule` (local only, `RequiresStreamingStorage`, `FeedPath` `v2/{repo}/` relative to the server root); `OciOptions` (`Oci:*`) |
| Routes | `/v2/`, `/v2/token`, `/v2/{repo}/{**path}` (GET/HEAD, POST, PATCH, PUT, DELETE) at the **server root**, registered once through `MapRoute(..., serverRoot: true)` with `RepositoryRouteMetadata.ResolvesRepositoryItself`; `OciNames.Parse` splits the image name from the right. `OciAuthorizationFilter` is the single access point |
| Access | pull = Read, push = Deploy, delete = Delete on the image (`IOciAccess` over the access model; image name = package id of the patterns; key scopes and glob apply; no ownership). Unknown repository, private repository and an uncovered token all give the same `401` challenge |
| Token flow (B3) | `GET /v2/token`: Basic (API key as the password) or anonymous; signed, encrypted (data protection) short-lived (`Oci:TokenLifetime` 5 min) token carrying only the image scopes; no key inside; Basic/Bearer API keys also work directly on the routes |
| Tables | `OciRepositories`, `OciManifests`, `OciTags`, `OciBlobs` (digest, size, `RefCount`) as planned, plus `OciBlobLinks` and `OciUploads`; migration `AddOciTables` x4; tags binary-collated on SQL Server/MySQL; `Content` unbounded text |
| Storage | blobs once per repository at `docker/blobs/sha256/ab/<hex>` on the wave-1 `IStreamingStorageService`; manifests are rows |
| Protocol | blobs (HEAD/GET/ranges), monolithic/chunked/mounted uploads with `Content-Range` and 416, manifests and indexes by tag/digest, tag list with `n`/`last` and `Link`, referrers with `artifactType`, deletes of tag/manifest/blob/upload |
| UI | `DockerImages`, `DockerImage` (+ `?digest=` manifest detail with index platforms and layer sizes), `Packages/DockerManage` (delete tag/manifest/image, prune; Delete + password); login/pull/push/buildx/daemon snippets; Repositories create form and links; `docs/ui-coverage.md` |
| Tests | Core `OciNamesTests` (60); `OciRegistryTests` (HTTP, 79), `DockerPagesTests` (16) in `BaGet.Tests`; `DockerCliTests` (8) with the real Docker and the `opencontainers/distribution-spec` v1.1.0 conformance image: **75 of 80 specs run, 75 passed, 0 failed** (the suite skips the other 5 itself) |
| Deviations from the sketch | (1) **Six tables, not four**: `OciBlobLinks` (a blob is served only through the image that holds it, else image-level permissions could be bypassed by digest, and a manifest can only reference blobs of its own image) and `OciUploads` (upload sessions bound to their image, expiry). `OciBlobs.RefCount` counts holders (images), the file goes at zero. (2) Manifests are stored in the row (max 4 MiB), not as files. (3) The route is not in `MapRoute`'s twin shape but a server-root registration (`serverRoot`). (4) **No startup check** for routes without a declared action: the structure is deny by default (the filter is on the controller, `OciHttp.Required` has no default allow) and `EveryRegistryRouteIsServedByTheControllerThatAuthorizes…` fails if a Docker route is added without it. (5) The token endpoint takes an API key as the password, not the account password, and only `GET` (the OAuth `POST` flow is not offered; clients fall back to `GET`). (6) Only `sha256` digests; a cross-repository mount works within one BaGet repository (otherwise a normal upload starts, which the specification allows); `DELETE blob` is `405` while a manifest uses it. (7) Tests were written beside the code in one pass and the key ones were then proven by mutation (see *Verification*), not strictly first |
| Verification | Full solution run in the worktree with Docker running: 2988 tests, 14 failures, all of them the ones CLAUDE.md lists as failing on a clean checkout (`TelemetrySettingsPageTests` x8, `TelemetryExportTests` x3, `TelemetryE2ETests` x2, and `DatabaseCopyTests.ACopyEmitsADatabaseCopySpan…` in the full run); `BaGet.Database.Tests`, `BaGet.Storage.Tests` and `BaGet.Clients.Tests` (33, the Docker ones included) ran, none skipped; 0 build warnings; `dotnet ef migrations has-pending-model-changes` clean on all four providers. Eight mutations (blob served without its link, token action ignored, digest not verified, manifest references not checked, unknown repository answered 404 to anonymous callers, delete mapped to Push, tag not moved, blob in use deletable) each turned the matching test red. Found by the real clients, not by the HTTP tests: a read of `manifests/.INVALID_MANIFEST_NAME` must be 404 (conformance), not 400; a user with Deploy but no Read cannot `docker push` (Docker checks which layers exist first). Also fixed in passing: the NuGet upload page listed Docker (and npm/Maven) repositories whose upload page is 404 |
| Not done | Remote (Docker Hub proxy with its token auth) and virtual Docker repositories; blob garbage collection under the retention engine (P6; *Remove unused layers* exists per image); `_catalog`; token revocation; `sha512`; image scanning (P7) |

#### As built: remote and virtual Docker, garbage collection (track `p5-oci-remote`)

User documentation: [Docker registry](../docker-registry.md).

| Item | As built |
|---|---|
| Remote | `RemoteOciRegistry` + `OciUpstreamClient` (Bearer token auth, `library/` names for Docker Hub, streamed layer caching, TTLs, offline, `Remote:BlockPrivateAddresses`), `mirror.fetch` spans; admin UI on Repositories / RepositoryEdit |
| Virtual | `VirtualOciRegistry`: ordered pull-through, push to the deploy repository |
| Garbage collection | Admin operations `docker-gc` and its dry run on the Operations page; the removing run asks for the administrator's password (`IAdminOperation.RequiresConfirmation`, default false), the dry run does not; scheduling is P6 |
| Tests | `FakeOciUpstream`, `VirtualOciRepositoryTests`, `OciGarbageCollectorTests` (17), admin pages, `DockerRemoteVirtualCliTests` (5, real Docker; **not run at integration: no Docker daemon**) |
| Deviations | (1) GC password gate added per the destructive-operation rule. (2) No schema change |
| Verification (integration) | Full solution, no Docker: 3192 total, 3084 passed, 97 skipped (Docker suites), 11 failed = `TelemetryExportTests` x3 + `TelemetrySettingsPageTests` x8 (listed in CLAUDE.md); 0 build warnings. Rerun the Docker suites (`Clients`, `Storage`, `Database`, `Telemetry` E2E) with Docker |
| Blocking checks | B1, B2, B3, B6 resolved (table above); none open |

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
