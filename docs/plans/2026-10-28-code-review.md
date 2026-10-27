# Code review: whole codebase (2026-10-28)

A pull-request-style review of `main` at `da058997`. Eight independent Opus reviewers each covered one area. They read code only (nothing built or run) and reported a finding only after following its call path in the source. Speculative findings were dropped.

## Summary

| Area | High | Medium | Low |
|---|---|---|---|
| [Security, authentication, authorization](#security-authentication-and-authorization) | 0 | 3 | 3 |
| [NuGet path](#nuget-path-core-services-protocols-v2-catalog-validation-client) | 2 | 7 | 4 |
| [npm and Maven](#npm-and-maven-formats) | 2 | 2 | 2 |
| [Docker/OCI and streaming storage](#dockerOCI-registry-and-streaming-storage) | 1 | 3 | 1 |
| [Data layer and background jobs](#data-layer-and-background-jobs) | 1 | 3 | 1 |
| [Cloud storage and scanning](#cloud-storage-providers-and-vulnerability-scanning) | 1 | 7 | 6 |
| [Web UI and its APIs](#web-ui-and-its-apis) | 0 | 1 | 3 |
| [Host, configuration, logging, telemetry](#host-configuration-logging-and-telemetry) | 1 | 4 | 3 |
| **Total (61)** | **8** | **30** | **23** |

No critical finding. The one low-medium finding (Manage page permissions) is counted under Low.

## High findings at a glance

| # | Finding | Where |
|---|---|---|
| H1 | Concurrent downloads, or a download racing an unlist, answer 500 on SQL Server/MySQL (download counter with a concurrency token) | `PackageDatabase.AddDownloadAsync` |
| H2 | A failed write leaves a truncated file on the filesystem store, and the version can never be pushed again | `FileStorageService.PutAsync` |
| H3 | Two concurrent npm publishes of one version leave a tarball that doesn't match its integrity | `NpmRegistryService` publish |
| H4 | A remote npm cache can be poisoned by build metadata in the tarball file name | `RemoteNpmRegistry` |
| H5 | OCI layer-usage counts are lost under concurrent pushes, so GC can delete layers still in use | `OciRegistryService` / `OciGarbageCollector` |
| H6 | In a cluster, after a database copy the other nodes reopen and write to the old database | `DatabaseCopyService` / `MaintenanceGate` |
| H7 | A Docker image escapes scanning when its manifest sets `artifactType` or `subject` | `DockerFormatScanner.IsImage` |
| H8 | Without HA, data-protection keys aren't persisted, so stored secrets are lost when the container is recreated | host `AddDataProtection` |

## Needing your decision (not plain defects)

- **Shipped telemetry settings:** `appsettings.json` ships with telemetry on (`localhost:4317`), and `appsettings.Overrides.json` is tracked and published. Both are your deliberate settings; the test-harness part is a plain fix.
- **Default administrator:** decide whether `Admin`/`Admin1234!` is forced to change at first sign-in, or replaced by a generated password printed once.
- **Docker image user:** decide whether the image runs as non-root on port 8080, which changes the documented port.

## How to read a finding

Each finding gives its severity, **Where** (file and line), **Problem**, **Failure scenario** and **Suggested fix**. At the end of each area, **Checked and fine** lists what the reviewer verified as correct.

## Security, authentication and authorization

### MEDIUM: Package-level Read patterns are bypassed on the NuGet V2 by-id endpoints
- **Where:**
  - `src/BaGet.Web.V3/Filters/RepositoryReadFilter.cs:43`
  - `src/BaGet.Web.V3/Routing/BaGetEndpointBuilder.cs:372-395`
  - `src/BaGet.Web.V2/Controllers/V2FeedController.cs:92-170, 292` (`TrimLiteral`)
- **Problem:**
  - The read filter checks the package against the raw `{id}` route value.
  - On `Packages(Id='X',Version='Y')` that value is the OData literal with its quotes; the quotes are stripped only later, inside the action.
  - On `FindPackagesById()?id='X'` and `Packages()?id='X'` the id is in the query string, so the filter checks the repository only.
- **Failure scenario:**
  - Everyone has Read on `default`, with Exclude `Secret.*`.
  - The V3 registration for `secret.pkg` correctly answers 404.
  - `GET /api/v2/Packages(Id='Secret.Pkg',Version='1.0.0')` and `FindPackagesById()?id='Secret.Pkg'` return the package anyway.
  - Include-only grants break in reverse: an allowed package answers 404.
  - `v3/autocomplete?id=` has the same gap.
- **Suggested fix:** either of these:
  - unquote the OData literal, and read the query-string `id`, in the filter for those routes;
  - in the controller, check `IAccessEvaluator.IsAllowedAsync(..., id, Read)` after `TrimLiteral` and answer 404 on refusal.

### MEDIUM: Change password allows unlimited guessing of the current password
- **Where:**
  - `src/BaGet.Identity.Application/UseCases/Account/AccountSettingsUseCase.cs:68-105`
  - compare `src/BaGet.Identity.Application/Security/AccountConfirmation.cs:31-44`
- **Problem:**
  - `ChangePasswordAsync` neither checks the lockout nor counts a wrong current password.
  - Every other password confirmation goes through `IsConfirmedAsync`, which does both.
- **Failure scenario:**
  - An attacker holding a stolen session brute-forces the current password through this form, and nothing locks.
  - The recovered password then unlocks every password-gated action: email change, 2FA reset, account deletion, admin confirmations.
- **Suggested fix:** call `AccountConfirmation.IsConfirmedAsync` before changing the password.

### MEDIUM: Forwarded headers are trusted from any client
- **Where:**
  - `src/BaGet/ConfigureBaGetOptions.cs:70-77` (`KnownNetworks.Clear(); KnownProxies.Clear();`)
  - `src/BaGet/Hosting/HostPipelineExtensions.cs:54`
- **Problem:** every request can set `X-Forwarded-For` and `X-Forwarded-Proto`, even when BaGet is reached directly with no proxy in front.
- **Failure scenario:**
  - Rotating `X-Forwarded-For` defeats the per-client password-reset rate limit (`AccountUiApiController.cs:100` → `PasswordResetRateLimiter`).
  - The logged `ClientIp` is spoofed.
  - `X-Forwarded-Proto: https` sent over plain HTTP is believed.
- **Suggested fix:** make the trusted proxies and networks a setting, defaulting to the framework's loopback-only trust; clear the lists only on an explicit opt-in.

### LOW-MEDIUM: The NuGet Manage page checks one permission for every change, while the protocol needs a different one per action
- **Where:**
  - `src/BaGet.Identity.Application/UseCases/Packages/PackageManageUseCase.cs:91-123` (`ManagePermission.HeldAsync`)
  - compare `OwnershipAuthorizationService` as used by `PackagePublishController.cs:138` and `PackageMetadataAdminController.cs:181`
- **Problem:**
  - Unlist, relist, delete, deprecate, vulnerabilities and symbols are all gated only by the Manage action.
  - The protocol requires Delete or Annotate for the same changes.
- **Failure scenario:**
  - A holder of Manage only can delete and unlist from the page, although `DELETE api/v2/package` refuses them.
  - A holder of Annotate without Manage can deprecate through the protocol but is refused the page.
- **Suggested fix:**
  - In each handler, call `IPackageAuthorizationService.AuthorizeAsync` with the protocol's action, as the npm and Maven pages already do.
  - Keep the Manage check only for adding and removing owners.

### LOW: `Remote:BlockPrivateAddresses` misses IPv6 forms that embed IPv4, and is bypassed through a proxy
- **Where:**
  - `src/BaGet.Core/Repositories/Remote/RemoteAddressPolicy.cs:29-61`
  - `src/BaGet.Core/Repositories/Remote/RemoteHttpClientFactory.cs:88-96`
- **Problem:**
  - Only IPv4-mapped addresses (`::ffff:a.b.c.d`) are unwrapped.
  - Three IPv6 forms that embed an IPv4 address are not checked: NAT64 `64:ff9b::/96`, 6to4 `2002::/16` and IPv4-compatible `::a.b.c.d`.
  - With a system proxy, the connect callback sees the proxy's address instead of the target's.
- **Failure scenario:** on a NAT64 network, the URL `http://[64:ff9b::a9fe:a9fe]/` reaches the cloud metadata service (169.254.169.254).
- **Suggested fix:**
  - Unwrap or block those prefixes.
  - When the policy is on, disable the proxy or check the target host itself.

### LOW: The unconfirmed-email answer does not depend on the password
- **Where:**
  - `src/BaGet.Identity.Application/Security/MaintenanceAwareSignInManager.cs:58-60`
  - `SignInUseCase.cs:89-96`
  - `NpmTokenService.cs:63-70`
- **Problem:**
  - For an unconfirmed account, `NotAllowed` is answered before the password is checked.
  - The pending-approval case was already reordered to check the password first; this case wasn't.
- **Failure scenario:** anyone can find out that an account exists and is unconfirmed by signing in with any password.
- **Suggested fix:**
  - Check in this order for every `NotAllowed` cause: lockout, then the password (counting failures).
  - Answer `NotAllowed` only to the right password.

**Checked and fine:**
- **Credentials and API keys:** `RequestPrincipalResolver` resolves credentials in order, and writes never use the cookie. API keys are 256-bit, looked up by SHA-256, with expiry. The global key is compared in fixed time, and an open server's superuser is refused where the docs say.
- **`/api/ui` and `/api/v1`:** API keys only; the global key is refused on admin twins; account screens answer 403 over HTTP.
- **Access model:** the cache is invalidated when the model changes, and Deploy is never granted on remote or virtual repositories.
- **Hiding existence:** npm, Maven and Docker read checks don't reveal whether a package or private repository exists.
- **OCI:** tokens and their scopes are checked correctly.
- **npm login:** locks out on failure and creates a repository-limited key.
- **Sign-in flows:** lockout, 2FA, external sign-in and re-authentication work as documented.
- **Redirects and outbound credentials:** redirects are local only; credentials go only to the upstream's own origin.
- **XSS:** readme Markdown is sanitised, and the remaining `Html.Raw` uses are safe.
- **Antiforgery:** present on every screen.

## NuGet path (core services, protocols, V2, catalog, validation, client)

### HIGH: Concurrent downloads (or a download racing unlist/relist) answer 500 on SQL Server and MySQL
- **Where:**
  - `src/BaGet.Core/PackageDatabase.cs:340-350` (`AddDownloadAsync`) and `:494-516` (`TryUpdatePackageAsync`)
  - `src/BaGet.Core/Entities/AbstractContext.cs:801` (`RowVersion`)
  - `src/BaGet.Core/Content/DefaultPackageContentService.cs:60`
- **Problem:**
  - The download counter is a read-modify-write on a tracked `Package` that carries a concurrency token (`rowversion` on SQL Server, `timestamp(6)` on MySQL).
  - Only `MaintenanceException` is caught; no handler for `DbUpdateConcurrencyException` exists anywhere in `src`.
- **Failure scenario:**
  - Two CI agents restore the same `.nupkg` at once. The second save throws, and the download answers 500.
  - Unlist, relist and deprecation fail the same way when a download hits the same row.
  - On every engine, concurrent downloads also lose counts.
- **Suggested fix:**
  - Make the increment atomic (`ExecuteUpdateAsync(p => p.Downloads + 1)`), or catch the concurrency exception as best effort.
  - Retry unlist and relist once.

### HIGH: Filesystem storage leaves a truncated file at the final path, and the version can never be pushed again
- **Where:**
  - `src/BaGet.Core/Storage/FileStorageService.cs:71-88`
  - `src/BaGet.Core/Indexing/PackageIndexingService.cs:185-237`
  - `src/BaGet.Core/Storage/PackageStorageService.cs:66-76`
- **Problem:**
  - `PutAsync` writes straight into the final path (`FileMode.CreateNew`), with no temp file, no rename and no cleanup on failure.
  - A later put of the same path compares against the partial file and returns `Conflict`.
- **Failure scenario:**
  - A push is cancelled mid-copy (client disconnect, full disk, mirror timeout). A truncated `.nupkg` stays on disk and no database row is written.
  - Every retry answers 409 `PackageAlreadyExists`. `AllowPackageOverwrites` does not help: it only deletes when a row exists.
  - A mirrored version becomes a permanent 404, and readers can be served the partial file.
  - The same applies to PDBs, catalog leaves and staged validation files.
- **Suggested fix:**
  - Write to a temp file in the same directory, then `File.Move(temp, path, overwrite: false)`.
  - Delete the partial file on failure.

### MEDIUM: Unbounded `take` and negative `skip` on V3 search and autocomplete
- **Where:** `src/BaGet.Web.V3/Controllers/Protocols/SearchController.cs:19-82` → `src/BaGet.Core/Search/DatabaseSearchService.cs`
- **Problem:** nothing clamps `take` or `skip` on the non-virtual path. The V2 feed clamps to 1000; V3 does not.
- **Failure scenario:**
  - An anonymous `GET /v3/search?take=2147483647` loads every version with its related data into memory, which can take the server down.
  - `skip=-1` produces an invalid OFFSET and answers 500.
- **Suggested fix:** clamp in the controller: `take` to 0..1000 and `skip` to ≥ 0.

### MEDIUM: An upstream error response is taken for the package, and 404 responses are never disposed
- **Where:**
  - `src/BaGet.Protocol/PackageContent/RawPackageContentClient.cs:44-62` and `:65-82`
  - called through `V3UpstreamClient.DownloadPackageOrNullAsync:42-67` and `RemoteUpstreamClient.DownloadPackageOrNullAsync:226-245`
- **Problem:**
  - Only `NotFound` is checked, so the body of a 401, 403, 429 or 5xx answer is returned as package content.
  - On a 404 the `HttpResponseMessage` (read with `ResponseHeadersRead`) is never disposed.
- **Failure scenario:**
  - Upstream answers 503: the health page shows the upstream as OK, the HTML is indexed as `InvalidPackage`, the client gets a 404, and nothing is remembered.
  - A burst of 404 misses exhausts the pooled connections.
- **Suggested fix:**
  - Dispose the response on a 404.
  - Call `EnsureSuccessStatusCode()` for any other status, disposing on failure, so the existing `propagateErrors` handling applies.

### MEDIUM: Two first fetches of the same mirrored version: one of them gets a false 404
- **Where:**
  - `src/BaGet.Core/PackageService.cs:164-226` (`MirrorAsync`)
  - `src/BaGet.Core/Indexing/PackageIndexingService.cs:227-260`
  - `src/BaGet.Core/Storage/FileStorageService.cs:79-87`
- **Problem:** nothing serialises mirroring and indexing of the same version. The second request gets `PackageAlreadyExists`, `Conflict` or `IOException`, and all three are reported as "not found".
- **Failure scenario:** two restores ask for the same uncached version at once. One gets a 404 and fails, although the version now exists.
- **Suggested fix:** either of these:
  - a `KeyedLocks` per (repository, id, version), as npm and Maven already use;
  - treat `PackageAlreadyExists`, or a package that exists after the failure, as success.

### MEDIUM: `dotnet nuget push` of symbols fails (404) whenever validation is enabled
- **Where:**
  - `src/BaGet.Core/Indexing/SymbolIndexingService.cs:84-92`
  - `ValidatingPackageSubmissionService.SubmitAsync`
- **Problem:** a symbol push needs the `Package` row, which only exists once the validation worker releases the package.
- **Failure scenario:**
  - `dotnet nuget push x.nupkg` answers 202.
  - `x.snupkg`, pushed right after, answers 404 `PackageNotFound`, so the symbols are lost.
  - None of this is documented.
- **Suggested fix:**
  - Stage the symbols with the pending submission of the same id/version and index them on release.
  - At minimum, answer with an explicit message and document the behaviour.

### MEDIUM: V2 `Search()`/`Packages()` run one query per id and ignore the prerelease and SemVer2 filters
- **Where:**
  - `src/BaGet.Web.V2/Controllers/V2FeedController.cs:200-237` and `:92-107`
  - `V2FeedPackage.FromPackages`
- **Problem:**
  - Each id on the page runs its own `FindAsync` with 5 includes, for up to 1000 ids per page.
  - Every listed version is returned, so `includePrerelease=false` still lists prereleases.
  - SemVer2 versions are served when `semVerLevel` is absent; old V2 clients can't parse them.
  - Paging counts ids while entries are per version, and the feed never emits a `next` link.
- **Suggested fix:**
  - Load the page's ids in one query.
  - Filter the returned versions by `IsPrerelease` and `SemVerLevel` (also in `FindPackagesById`).

### MEDIUM: Catalog commit order is not coordinated across cluster nodes
- **Where:** `src/BaGet.Catalog/Storage/CatalogWriter.cs:31` (static `CommitLock`), `:178-211`
- **Problem:** timestamps stay in order only because of a lock inside one process plus "read the last entry, then insert".
- **Failure scenario:** two nodes commit at once, and key K+1 gets a timestamp no later than K's. A catalog consumer that keeps a timestamp cursor then skips that entry permanently.
- **Suggested fix:**
  - Take an `IJobLease` `catalog:{repo}`, or have the database assign the timestamp inside a serializable transaction.
  - At least document the limitation in `docs/high-availability.md`.

### MEDIUM: `PackageIndexingService` never disposes the temp streams for the nuspec, readme and icon
- **Where:** `src/BaGet.Core/Indexing/PackageIndexingService.cs:134-172`
- **Problem:**
  - The temp `FileStream`s (opened `DeleteOnClose`) and the raw nuspec stream are never disposed.
  - This happens on every path, including the early returns at `:189` and `:236`.
- **Failure scenario:** a busy push or mirror server piles up open handles and temp files until the garbage collector runs.
- **Suggested fix:** `await using`, or `try/finally`.

### LOW: A failed validation run leaves the submission pending forever
- **Where:**
  - `src/BaGet.Validation/Queue/ValidationWorker.cs:70-74`
  - `src/BaGet.Validation/PackageValidationRunner.cs:277-281`
  - `src/BaGet.Validation/ValidationAdministration.cs:157`
- **Problem:**
  - Any exception other than the maintenance one leaves the submission `Pending`; on a single node it is retried only at the next restart.
  - Meanwhile:
    - `RejectAsync` refuses pending submissions.
    - Pushing the same version again answers 409.
    - Deleting the repository is refused.
- **Suggested fix:**
  - Requeue with backoff.
  - Let administrators reject a stuck pending submission.

### LOW: Symbol download with a short key answers 500
- **Where:** `src/BaGet.Core/Storage/SymbolStorageService.cs:84` (`key.Substring(0, 32)`), called outside the `try` at `:45`
- **Failure scenario:** `GET /api/download/symbols/a.pdb/abc/a.pdb` throws `ArgumentOutOfRangeException` and answers 500 instead of 404.
- **Suggested fix:** validate the key's length and characters, and return null (a 404).

### LOW: The catalog index reads every commit timestamp on each request
- **Where:** `src/BaGet.Catalog/Database/CatalogDatabase.cs:282-292`, called from `CatalogReader.GetIndexAsync:41`
- **Problem:** each `index.json` request costs O(total entries), although it only needs the count and the timestamp at each page boundary.
- **Suggested fix:** count the entries, then read only the timestamps at the page-boundary keys.

### LOW: `v3/dependents` returns the same package several times
- **Where:** `src/BaGet.Core/Search/DatabaseSearchService.cs` `FindDependentsAsync`
- **Problem:**
  - The query runs per version, and `Take(20)` comes before `Distinct()` on (Id, Description, Downloads).
  - Versions with different download counts stay separate rows, so one id appears once per version and fewer than 20 distinct dependents come back.
- **Suggested fix:** group by Id (summing downloads), then take 20.

**Checked and fine:**
- **Maintenance gate:** `ThrowIfClosed` runs before every file write, and `MaintenanceException` is never swallowed.
- **Repository isolation:** storage and queries are isolated per repository, and writes to virtual repositories are routed to the deploy repository.
- **Push authorization:** it uses the same archive parse as indexing.
- **Remote client:** credentials are never logged, and only true 404s are cached as "not found".
- **Correctness:**
  - virtual-repository merges;
  - version and case handling;
  - the V2 `$top`/`$skip` clamps;
  - failures in package-change notifications never fail the push.

## npm and Maven formats

### HIGH: Two concurrent npm publishes of the same version can leave a published tarball whose bytes don't match its integrity
- **Where:**
  - `src/BaGet.Core/Formats/Npm/NpmRegistryService.cs:643-664`, `:707-728`
  - compare `src/BaGet.Core/Formats/Maven/MavenRepositoryService.cs:691-714`, which locks for exactly this case
- **Problem:**
  - The check for an existing version reads the rows outside any lock.
  - A storage `Conflict` is then treated as a leftover file: the file is deleted and rewritten.
  - Unlike Maven, npm has no per-path lock and no re-check of the row under the lock.
- **Failure scenario:** two `npm publish` runs of `pkg@1.0.0` with different bytes:
  1. Publish A stores its file.
  2. Publish B gets `Conflict`, deletes A's file and writes its own bytes.
  3. A commits its row with A's integrity.
  4. B hits the unique index and answers 409, but keeps its file.
  - Every later `npm install` of that version fails with EINTEGRITY.
  - Variant: `1.0.0+a` and `1.0.0+b` in parallel both pass the check, so two rows are committed for one npm version.
- **Suggested fix:**
  - Lock the storage path (`KeyedLocks`) and re-check the rows by `Comparable` under the lock.
  - Delete a conflicting file only when no row names it; otherwise answer 409 without touching storage.

### HIGH: A remote npm repository's cache can be poisoned by build metadata in the tarball file name
- **Where:**
  - `src/BaGet.Core/Formats/Npm/RemoteNpmRegistry.cs:104-123, 187-190, 202` and `FindVersion` at `:338-355`
  - `NpmRegistryService.cs:252-254` (exact match) against `:428-431` (`Comparable` match)
- **Problem:**
  - The requested file name `pkg-1.0.0+x.tgz` is matched to the upstream's `1.0.0` while ignoring the build metadata.
  - But the cached row and file are stored under the requested spelling `1.0.0+x`, not under the upstream's version.
- **Failure scenario:**
  1. Any reader requests `pkg-1.0.0+x.tgz` first, so `1.0.0+x` is cached.
  2. The real URL `pkg-1.0.0.tgz` then misses the exact-match lookup and re-downloads the whole tarball.
  3. Storing is skipped as "already stored", so the client gets a 404.
  - The version stays uninstallable from the remote (and from any virtual repository over it) until a purge. Every attempt also costs a full upstream download.
- **Suggested fix:**
  - Store under the upstream's version, and answer 404 to a file name whose version is not exactly the upstream's.
  - Alternatively, resolve stored rows by `Comparable` and serve the stored spelling.

### MEDIUM: npm versions that differ only in prerelease case collide in storage
- **Where:**
  - `src/BaGet.Core/Formats/Npm/NpmNames.cs:148, 183-184`
  - `NpmRegistryService.cs:647-664` (publish) and `:1044-1064` (`DeleteTarballAsync`)
  - `src/BaGet.Database.SqlServer/SqlServerContext.cs:38-45`: `NpmVersion.Version` has no binary collation
- **Problem:**
  - `Comparable` keeps the case of the prerelease label, so `1.0.0-Beta` and `1.0.0-beta` count as different versions.
  - On a case-insensitive store (FileSystem on Windows or macOS) their tarball paths are the same file.
- **Failure scenario:**
  - **Publish:** publishing `1.0.0-beta` while `1.0.0-Beta` exists overwrites the published file, so the old version fails integrity. Even a `pushversion`-scoped key can do this.
  - **Delete:** `DeleteTarball` of `1.0.0-beta` deletes the published, out-of-window `1.0.0-Beta` file with only Push permission, bypassing the Manage requirement for old versions.
- **Suggested fix:**
  - Compare versions case-insensitively for clashes and identity, or refuse case-only variants as Maven does.
  - In `DeleteTarballAsync`, check rows case-insensitively before deleting a file.

### MEDIUM: npm "manifest confusion": the packument serves the manifest the client sent, not the tarball's `package.json`
- **Where:** `NpmRegistryService.cs:621-631`, `:692`, `BuildStoredManifest` `:763-790`, `BuildPackument` `:183`
- **Problem:**
  - The fields npm uses when installing (`dependencies`, `bin`, `os`, `cpu`, `hasInstallScript`) come from the request's manifest.
  - The tarball's own `package.json` is compared only for name and version.
- **Failure scenario:**
  - A publisher declares harmless dependencies and no install script, while the tarball's `package.json` has a `postinstall` script.
  - The package pages and lockfiles show the declared manifest, but `npm install` runs what the tarball holds.
  - npmjs has the same weakness.
- **Suggested fix:**
  - Build the stored install fields from the tarball's `package.json`, keeping only the registry-owned `dist`.
  - At minimum, refuse a publish whose install fields differ from the tarball's.

### LOW: Malformed npm request bodies answer 500 instead of 400
- **Where:**
  - `src/BaGet.Core/Formats/Npm/NpmPublishBody.cs:143`
  - `src/BaGet.Web.V3/Controllers/Protocols/NpmController.cs:348-349` (login), `:533-535` (`PutRevision` dist-tags)
- **Problem:** `GetValue<string>()` throws on a number or a boolean, and the exception isn't caught.
- **Failure scenario:** `{"name":"x","password":1}` sent to login answers 500.
- **Suggested fix:** use `TryGetValue<string>`, as `PublishAsync` already does.

### LOW: Group-level Maven metadata in remote and virtual repositories is authorized against an unrelated artifact name
- **Where:**
  - `src/BaGet.Core/Formats/Maven/RemoteMavenRepository.cs:63-64`
  - `VirtualMavenRepository.cs:54-55`
  - `MavenNames.cs:196-201`
  - `RepositoryReadFilter.cs:74-89`
- **Problem:**
  - For `org/acme/maven-metadata.xml` there is a single candidate, `org:acme`, so Read is checked on that artifact.
  - The document served is often the group-level plugin list of `org.acme`.
- **Failure scenario:**
  - A user denied `org.acme:*` but allowed on `org:acme` can read the plugin artifact ids of `org.acme` through a remote or virtual repository.
  - Only names leak.
- **Suggested fix:**
  - Identify the document even when there is a single candidate.
  - For a plugin list, require repository-level Read only, or check Read per listed artifact.

**Checked and fine:**
- **Validation:** npm and Maven name and path validation; publish-body streaming and caps; integrity and `package.json` name/version checks; bounded tarball reader.
- **Authorization:** authorization before lookup, with refusals hidden from callers who cannot read; unpublish window; login and logout rules.
- **Gate:** `ThrowIfClosed` on every write.
- **Remote npm:** integrity verified, packument size capped, cache bounded, one download per version.
- **Remote Maven:** hashes verified, HTML answers treated as absent, `-SNAPSHOT` names never stored.
- **Maven deploy:** refuses case-only variants and rules redeploys; it locks with a row re-check.
- **Maven columns:** SQL Server identifier columns use a binary collation.
- **Virtual repositories:** member filtering and write routing are correct.
- **Replicators:** they verify what they copy.

## Docker/OCI registry and streaming storage

### HIGH: Concurrent manifest pushes miscount layer usage, and the garbage collector can then delete layers still in use
- **Where:**
  - `src/BaGet.Core/Formats/Docker/OciRegistryService.cs:580-625` (`PutManifestCoreAsync`: `link.Manifests++`)
  - `OciRegistryService.cs:729-740` (`DeleteManifestRowAsync`)
  - `src/BaGet.Core/Entities/AbstractContext.cs:596-606` (no concurrency token on `OciBlobLink`)
  - Readers of the count:
    - `OciGarbageCollector.cs:196-202, 331`
    - `OciRegistryService.PruneAsync:819-822`
    - `DeleteBlobAsync:773`
- **Problem:**
  - `OciBlobLink.Manifests` (how many manifests use a layer) is updated with a read-modify-write on tracked rows: EF writes the new value as an absolute number.
  - Nothing prevents two concurrent pushes from overwriting each other's update:
    - the transaction uses the default isolation level;
    - no row is locked;
    - the row has no concurrency token.
  - SQLite is safe only because its transactions start with `BEGIN IMMEDIATE`. HA refuses SQLite, so every clustered deployment is exposed.
- **Failure scenario:**
  1. Image `app` has manifest M1 using layer L, so the count is 1.
  2. CI pushes `:a` (M2) and `:b` (M3) in parallel, both using L. Both transactions read 1 and write 2; the correct count is 3.
  3. M1 and M2 are deleted later, bringing the count to 0 while M3 still uses L.
  4. After `Oci:PruneGrace`, the GC (or a manual Prune) removes L's link, plus the blob row and file when no other image holds L.
  5. `GET blobs/L` for M3 then answers 404: data loss.
  - The reverse race (two concurrent deletes) leaves the count above 0, so the blob is never collected.
- **Suggested fix:** either of these:
  - update the count atomically (`SET Manifests = Manifests + 1`), or add a concurrency token and retry;
  - more robustly, have GC, Prune and DeleteBlob compute "in use" from the stored manifests (as `CountReferencingManifestsAsync` already does) instead of trusting the stored count.

### MEDIUM: A blob deletion racing an upload of the same blob leaves a blob row with no file
- **Where:**
  - `OciRegistryService.cs:665-698` (`ReleaseBlobsAsync`)
  - `OciGarbageCollector.cs:328-377` (`SweepAsync`)
  - `src/BaGet.Core/Storage/FileStorageService.cs:209-232` (an upload committed onto an existing file reports `AlreadyExists`)
  - `OciRegistryService.cs:401, 427, 493-499` (`RecordBlobAsync`)
- **Problem:**
  - Deletion commits the row removal first, then deletes the file.
  - Upload commits the file first, treating an existing file as success, then inserts the row.
  - Nothing orders the two against each other.
- **Failure scenario:**
  1. Image A, the only holder of layer L, is deleted, and its row removal is committed.
  2. Meanwhile a push to image C completes an upload of L. Storage still sees the file and answers `AlreadyExists`, and a fresh row is recorded.
  3. The deletion now deletes the file.
  4. C's manifest push succeeds, but `GET blobs/L` answers 404.
- **Suggested fix:** any of these:
  - after an `AlreadyExists` commit, check the file again once the row is committed, and fail the upload if it is gone (the client retries);
  - make the deleters re-check for a row after deleting the file;
  - serialise both paths per digest.

### MEDIUM: Prune and blob deletion skip the GC's safeguards
- **Where:**
  - `OciRegistryService.cs:813-831` (`PruneAsync`, used by `DockerManageUseCase.PruneAsync:182`)
  - `OciRegistryService.cs:763-786` (`DeleteBlobAsync`)
  - compare `OciGarbageCollector.cs:189-202` and `:319-347`
- **Problem:**
  - Prune deletes links that no manifest uses and that are older than `PruneGrace`. Unlike the GC, it does not skip images with an open upload session and does not re-check inside a transaction.
  - `DeleteBlobAsync` checks the count outside any transaction.
  - The delete itself matches by key only, with no condition on the count.
- **Failure scenario:**
  - A multi-layer push over a slow link takes more than 1 h. An administrator presses Prune; the early layers are dropped, and the final manifest push fails with `MANIFEST_BLOB_UNKNOWN`. The GC would have skipped this image.
  - A manifest push that commits between Prune's read and its delete is left without its layer link.
- **Suggested fix:** reuse the GC's logic: skip images with a live upload session, and delete only after re-checking the count in a serializable transaction (or with `DELETE ... WHERE Manifests = 0`).

### MEDIUM: Upstream credentials can be sent to a token realm named by a redirect target on another origin
- **Where:** `src/BaGet.Core/Formats/Docker/OciUpstreamClient.cs`
  - `:727-747` (challenge handling)
  - `:762-793` (`SendFollowingRedirectsAsync`)
  - `:849-886` (`FetchTokenAsync`)
- **Problem:**
  - Redirects are followed by hand, and the credential is withheld from other origins.
  - But a 401 from the redirect target (a CDN or object store) carrying `WWW-Authenticate: Bearer realm="https://other/..."` is still answered.
  - `FetchTokenAsync` then sends the repository's Basic credentials to that realm. This contradicts the class's own promise (`:110-111`).
- **Failure scenario:**
  1. A private registry redirects blob downloads to third-party storage.
  2. That host is compromised or misconfigured and answers 401 with its own realm.
  3. BaGet sends it the upstream user name and password.
- **Suggested fix:** act on a challenge only when the response came from the registry's own origin. Otherwise treat the 401 as a failure or an absence.

### LOW: Manifests with foreign (non-distributable) layers are always refused
- **Where:**
  - `OciRegistryService.cs:584-603`
  - `OciManifestParser.cs:155-159`
- **Problem:**
  - Clients never upload foreign or non-distributable layers, by design.
  - BaGet still requires every layer to be present and answers `MANIFEST_BLOB_UNKNOWN`. `registry:2` accepts them.
- **Failure scenario:** pushing a legacy Windows image whose base layers are foreign fails.
- **Suggested fix:** exempt descriptors that have a foreign or non-distributable media type and non-empty `urls` from the presence check and from usage counting.

**Checked and fine:**
- **Authorization:** handled by `OciAuthorizationFilter`, with the same challenge for missing and private repositories.
- **Blob access:** blobs are served only through image links. Cross-repository mounts check the caller's pull right, and upload sessions are bound to their image.
- **Tokens:** audience, expiry and per-member virtual grants are checked, and no credential is stored inside.
- **Digests:** verified on uploads, on pushed manifests (exact bytes) and on upstream manifests and blobs. When a cached blob is fetched, its last bytes are held back until the digest is checked.
- **Ranges and offsets:** range parsing and Content-Range math, `WindowedReadStream` resume, and upload offsets with 416 are correct.
- **Races handled:** concurrent pushes of the same tag or image retry, and `OciPushUndo` restores a moved tag.
- **Maintenance gate:** `ThrowIfClosed` is called on every write.
- **GC:** skips images with live upload sessions, re-checks serializably (apart from the miscount above), and fences orphan files.
- **Memory:** bounded throughout.

## Data layer and background jobs

### HIGH: After a database copy succeeds in a cluster, the other nodes reopen and write to the old database
- **Where:** `src/BaGet.Core/Entities/Copy/DatabaseCopyService.cs:313` (`CloseUntilRestart`; also `FailAfterSaveAsync` :375); `src/BaGet.Core/Entities/MaintenanceGate.cs:52` (`LocalReason`); `src/BaGet.Core/Ha/ClusterSyncService.cs:201-233` (`SyncGateAsync`)
- **Problem:** the copy closes the cluster gate with a published `Close`; on success it switches to `CloseUntilRestart`, which is not published, so `LocalReason` becomes null and the gate owner deletes the shared key.
- **Failure scenario:** nodes A and B, copy on A succeeds → A's next sync deletes `gate` → B's `ApplyRemote(null)` reopens → B writes to the old database until the restart; those writes are lost at cut-over (split brain if the overrides file is per node).
- **Suggested fix:** keep publishing a permanent closure (`LocalReason` returns `_reason` after `CloseUntilRestart` over a published `Close`), or refuse the copy while `ClusterRuntime.IsEnabled`.

### MEDIUM: In a cluster the copy starts reading without confirming the other nodes are read-only
- **Where:** `DatabaseCopyService.TryQueue` (:123-150), `RunAsync` (:181+); `HaOptions.cs:56` (`SyncInterval` 2 s)
- **Problem:** the gate is published best-effort and the worker reads at once; other nodes write until their next successful sync, and a node without Redis never closes.
- **Failure scenario:** node B pushes after A read `Packages`; counts and checksum compare against the copy-time source, so the copy succeeds without B's row.
- **Suggested fix:** refuse the copy in HA, or wait `SyncInterval` + margin and require every heartbeating node to report `GateClosed`.

### MEDIUM: The replication orphan sweep can overwrite a finished run with "Interrupted"
- **Where:** `src/BaGet.Core/Replication/ReplicationJobs.cs:388-406` (`SweepOrphansAsync`)
- **Problem:** `Running` rows are loaded tracked, then the lease is taken and `Status = Interrupted` is set without re-reading; the lease is released before the single save.
- **Failure scenario:** a run finishes (`Succeeded`) between the sweep's query and its lease → the sweep marks it `Interrupted`; an administrator resumes a run that had completed.
- **Suggested fix:** after taking the lease re-read the row (`ReloadAsync`), skip it unless still `Running`, save while the lease is held.

### MEDIUM: Delete propagation removes a target item that changed after it was replicated
- **Where:** `src/BaGet.Core/Replication/ReplicationPlanner.cs:69-76`; executed in `ReplicationEngine.cs:287-296`
- **Problem:** for a ledger entry missing at the source the planner deletes what the target holds under the key without comparing the ledger digest; bypasses `Conflict = TargetWins`.
- **Failure scenario:** Docker push replication with `PropagateDeletes`: `app:latest` replicated at digest A; the peer moves `latest` to B; the source deletes `latest` → the next run deletes the peer's B, content the source never had (same on pull for moved local tags/snapshots).
- **Suggested fix:** propagate only when `!entry.DiffersFrom(held)`; otherwise conflict, or forget under `TargetWins`.

### LOW: A data-protection key written during a copy is not carried to the new database
- **Where:** `src/BaGet/Ha/DatabaseXmlRepository.cs:42-62`
- **Problem:** keys go through a gate-exempt context to the source database; a key created after the copy read `DataProtectionKeys` is missing in the target.
- **Failure scenario:** after the restart on the copy, cookies/antiforgery tokens and protected remote/replication secrets made with that key fail.
- **Suggested fix:** document it, or re-copy `DataProtectionKeys` just before verification.

**Checked and fine:** `OwnershipOrphanCleanup` order and per-engine SQL; migration sets match across providers (engine-specific: `NpmCaseSensitiveNames`, `FixVersionCaseSensitivity`, `AddSearchDimensions`); all 13 repository-scoped sets filtered; maintenance and access-model interceptors; database copy batching, shadow keys, rollback, seeded rows; `ArtifactUsageRecorder` bounds; retention holds and leases; replication run row, deploy notice, peer credentials and redirects; `ClusterJobLease`/`RedisClusterStore` (NX PX + Lua); `AdminJobService`, `ScanWorker`/`ScanQueue`, `StagingSweepWorker`; `RepositoryAdministration.DeleteAsync` cascades.

## Cloud storage providers and vulnerability scanning

### HIGH: A Docker image escapes scanning if its manifest sets `artifactType` or `subject`
- **Where:**
  - `src/BaGet.Core/Formats/Docker/DockerFormatScanner.cs` (`IsImage`, `GetImageReferenceAsync`)
  - `src/BaGet.Core/Formats/Docker/OciManifestParser.cs:122,167`
  - `src/BaGet.Web.V3/Controllers/Protocols/OciController.cs` (push ~612, pull ~337)
  - `ScanGate.CheckDownloadCoreAsync`
- **Problem:**
  - A manifest counts as an image only when it has no `subject` and its declared `artifactType` names an image.
  - Both fields come from the pushed document, and Docker and containerd ignore them when they run an image.
- **Failure scenario:** an ordinary image manifest with `"artifactType":"application/x-foo"`, or with a `subject` pointing at any existing manifest, arrives by push or from a remote repository's upstream.
  - **On push:** nothing is scanned and no deploy block applies. With `subject` set, the stored-image check is skipped entirely.
  - **On pull:** neither "block unscanned" nor the severity block applies, so the image is served and never scanned.
- **Suggested fix:**
  - Decide "is an image" from the manifest's media type and its config descriptor's media type, ignoring the self-declared fields.
  - Alternatively, treat such manifests as unscannable, still subject to "block unscanned" when they have layers.

### MEDIUM: In a virtual repository the scan check can judge one member while another member serves the content
- **Where:**
  - `ScanGate.CheckDownloadAsync`
  - content readers: `VirtualReadServices.cs:172`, `VirtualNpmRegistry.cs:196`, `VirtualMavenRepository.cs:83`, `VirtualOciRegistry`
  - `VirtualRepositoryReader.ReadableMembersAsync` / `TryAsync`
- **Problem:**
  - The check walks every member and stops at the first that holds the version.
  - The content comes from the first member the caller can read, skipping any member whose read throws.
- **Failure scenario:**
  - A virtual repository has members [A, B], both holding `x 1.0`. A is clean or has no policy; B blocks high severity and has critical findings.
  - A caller who can read B but not A is judged on A (allowed) and served B's vulnerable copy.
  - The same happens when A's storage read throws.
- **Suggested fix:**
  - Judge the member that actually serves the content: run the check inside the same member walk.
  - Alternatively, filter the check's members exactly as the content path does.

### MEDIUM: Scan results survive deletion or replacement, so a re-published version inherits the old verdict
- **Where:**
  - `ScanResults` are deleted only by `RepositoryAdministration.cs:186`, by `ScanService.ScanAsync` and by the sweep.
  - Package deletion, npm unpublish and Maven purge or redeploy don't delete them.
  - `ScanGate.CheckDownloadCoreAsync` trusts any row that has a scan date.
- **Failure scenario:**
  - **NuGet:** a clean `foo 1.0` is hard-deleted and a vulnerable `foo 1.0` pushed. It is served on the old clean verdict until a rescan, which can take up to `RescanInterval` (24 h) with `ScanOnDeploy=false`. "Block unscanned" is bypassed too, because the old row counts as a scan.
  - **Maven:** the same happens on `AllowRedeploy` and on every new `-SNAPSHOT` build, since the result is keyed by `1.0-SNAPSHOT`.
- **Suggested fix:**
  - Delete the result on every delete or replace path, or key results by content digest.
  - Reset the result when a version is stored.

### MEDIUM: An OSV connection dropped mid-response becomes a 500 instead of "source unavailable"
- **Where:** `src/BaGet.Core/Scanning/OsvVulnerabilitySource.cs` `SendAsync`
- **Problem:**
  - A connection drop while the body is read (with `ResponseHeadersRead`) throws `HttpIOException`, an `IOException`, and it isn't caught.
  - It never becomes the "source unavailable" error (`ScanUnavailableException`).
- **Failure scenario:**
  - The push answers 500, instead of being let through as documented when the source is down.
  - The scan records no failure, and the repository sweep aborts (see the next finding).
- **Suggested fix:** map `IOException` (including `HttpIOException`) to `ScanUnavailableException`.

### MEDIUM: One bad artifact aborts the whole repository sweep, every time
- **Where:**
  - `ScanService.ScanRepositoryAsync` (no per-artifact catch)
  - `ScanWorker.SweepAsync`
  - `MavenFormatScanner.ListComponentsAsync` (catches only `FileNotFoundException`/`DirectoryNotFoundException`)
- **Problem:**
  - Any unexpected exception for one artifact ends the sweep of that repository.
  - Artifacts are listed in a stable order, so every artifact after the bad one is never rescanned.
- **Failure scenario:** any of these triggers it:
  - **Cloud stores:** a missing POM on S3, Azure, GCS or Aliyun throws the provider's own exception, not `FileNotFoundException`. That same exception also makes a Maven download in a "block unscanned" repository answer 500.
  - **OSV drops:** the `IOException` from the previous finding.
  - **Offline mode:** a dependency version with non-ASCII digits throws `FormatException` in `OsvVersions.Number`.
- **Suggested fix:**
  - Catch per artifact, record a failure and continue.
  - Map the providers' "not found" exceptions to one common exception.

### MEDIUM: A pass-through remote repository is never scanned, so its download policy has no effect
- **Where:**
  - `ScanGate.CheckDownloadCoreAsync`
  - `RemoteNpmRegistry.OpenTarballAsync` with `!StoreLocally`
  - NuGet `OpenUncachedAsync`
- **Problem:**
  - With `StoreLocally=false` nothing is stored, so every download counts as a first download that nobody has scanned.
  - Neither "block unscanned" nor the severity block ever applies, yet the policy page shows the policy as in force.
- **Suggested fix:**
  - Check the components of the fetched artifact (like the deploy check) before streaming it.
  - Alternatively, refuse blocking policies on pass-through repositories and document it.

### MEDIUM: S3, GCS and Aliyun load whole objects into memory
- **Where:**
  - `S3StorageService.GetAsync` (also ignores the cancellation token)
  - `GoogleCloudStorageService.GetAsync`
  - `PutAsync` in S3, GCS and Aliyun
- **Problem:** downloads and uploads go through a `MemoryStream` that holds the whole object.
- **Failure scenario:**
  - Each concurrent download of a 500 MB package takes 0.5–1 GB of managed memory.
  - Pushes and remote caching of large files have the same cost.
- **Suggested fix:**
  - Return the provider's response stream, as Azure and Aliyun already do.
  - Upload from a seekable temp file instead of a `MemoryStream`.

### MEDIUM: Azure Search is updated only on push, so unlist, relist and delete leave it stale
- **Where:** `ISearchIndexer` is called only by `PackageIndexingService`; unlist, relist and delete never call `AzureSearchIndexer.IndexAsync`.
- **Failure scenario:**
  - With `Search:Type=AzureSearch`, unlisted or deleted versions stay in search and autocomplete.
  - Relisted versions stay missing.
  - Both last until the next push of the same id.
- **Suggested fix:** re-index the id through `ISearchIndexer` after unlist, relist and delete.

### LOW: The AWS `AssumeRoleArn` check tests the base credentials, not the assumed ones
- **Where:** `src/BaGet.Aws/AwsApplicationExtensions.cs` `AssumeRoleAsync`
- **Problem:**
  - It throws "Unable to assume role" when the base credentials have no session token.
  - Long-term IAM user keys, the usual base for assuming a role, never have one.
- **Failure scenario:** `AssumeRoleArn` combined with static or environment keys always fails on first storage use.
- **Suggested fix:** check the assumed credentials instead, or drop the check.

### LOW: Trivy output over 64 MB hangs the scan for the full timeout
- **Where:** `src/BaGet.Core/Scanning/Trivy.cs` `ProcessTrivyRunner.ScanImageAsync` / `ReadCappedAsync`
- **Problem:**
  - When the output cap is hit, the reader stops draining stdout, and the child process blocks on a full pipe.
  - The scan then waits the full 10 min timeout and reports "did not finish in time".
- **Suggested fix:** wait on the reader tasks with `Task.WhenAny`, and kill the process when a reader fails.

### LOW: Azure Table insert fails for large package metadata
- **Where:** `src/BaGet.Azure/Table/TableOperationBuilder.cs` `AddPackage`
- **Problem:**
  - `Dependencies`, `ReleaseNotes` and `Description` are stored as single string properties with no size check.
  - Azure Table caps a property at 64 KB.
- **Failure scenario:** the push fails with an uncaught 400 `StorageException` and answers 500 after the files were already stored.
- **Suggested fix:** validate, truncate or split those properties.

### LOW: Offline OSV matching orders unknown Maven qualifiers before the release
- **Where:** `src/BaGet.Core/Scanning/OsvAdvisory.cs` `OsvVersions.Rank`
- **Problem:**
  - Maven orders an unknown qualifier after the release; here `2.0-jre < 2.0`.
  - Every unknown qualifier also compares equal to every other.
- **Failure scenario:**
  - An advisory with `fixed: 2.0` marks `2.0-jre` as affected (a false positive).
  - Advisories whose bounds carry such a qualifier can be misjudged either way.
- **Suggested fix:** rank unknown qualifiers above `sp` and compare them lexically.

### LOW: The online OSV caches can be stale or incomplete without saying so
- **Where:** `OsvVulnerabilitySource` (`OsvState.Advisories`, `QueryIdsAsync` `MaxPages`)
- **Problem:**
  - Cached advisories never expire.
  - Ids beyond 10 result pages are dropped silently.
  - The answer cache is never pruned.
- **Suggested fix:**
  - Give advisories the same lifetime as answers.
  - Log or fail when result pages are cut off.

### LOW: GCS creates a new `StorageClient` on every operation
- **Where:** `GoogleCloudStorageService` `CreateClientAsync`
- **Problem:** every call builds a new HTTP client and resolves the credentials again.
- **Suggested fix:** create one client per service lifetime.

Not carried over: "Docker blobs are not gated" (config and layers of a blocked image are downloadable by digest). This is intended and documented in `docs/vulnerability-scanning.md`.

**Checked and fine:**
- **Provider chain:** the provider chooser and the host's registration order are correct, and every cloud credential option is a sensitive setting.
- **Listing:** S3, Aliyun and Azure page through their listings fully and handle the configured prefix.
- **Azure Table and Search:** keys and escaping are correct, and search batches split when too large.
- **Scan checks:** the download check is present on every artifact route, and the deploy check on every store and replication path. Blocked answers have the protocol's shape.
- **Policies:** the policy cache works as documented.
- **OSV ranges:** range evaluation is correct.
- **Trivy runner:** arguments can't be injected, the password stays out of logs, and a timeout kills the whole process tree.
- **Scanner-pull exemption:** limited correctly; it never lifts a known vulnerable image's block.
- **Scan storage and queue:** both are correct.

## Web UI and its APIs

### MEDIUM: The four manage screens reveal that a private repository exists (403 instead of 404)
- **Where:**
  - Controllers, in `src/BaGet.Identity.FrontEnd/Controllers/FrontEnd/`:
    - `PackageManageController.cs:40-55`
    - `NpmManageController.cs:35`
    - `MavenManageController.cs:41`
    - `DockerManageController.cs:36`
  - Refusals:
    - `src/BaGet.Identity.Application/UseCases/Packages/PackageManageUseCase.cs:91-106`
    - `NpmManageUseCase.cs:99`
    - `MavenManageUseCase.cs:100`
    - `DockerManageUseCase.cs:81-85`
- **Problem:**
  - Each controller runs the ownership check in its own `OnActionExecutionAsync`.
  - That runs before the global `RepositoryActionFilter`, so a non-owner gets 403 before the repository Read check can answer 404.
- **Failure scenario:**
  - A signed-in user without Read on the private repository `secret` gets 403 on `GET /r/secret/packages/x/manage`.
  - The same request for a repository that doesn't exist gets 404, so repository names can be enumerated.
  - On a virtual repository a non-administrator also gets 403, while the documented answer is 404.
- **Suggested fix:**
  - Run the repository page check (`RepositoryPageGate.CheckAsync`) before `EnterAsync` in the four controllers.
  - Alternatively, have `EnterAsync` answer NotFound when the caller cannot read the repository or package.

### LOW: Two admin endpoints answer 500 when `userName` is missing
- **Where:**
  - `src/BaGet.Identity.Application/UseCases/Admin/AdminUsersUseCase.cs:127-150` (reset-password), `:155-190` (send-reset-link)
  - `AccountRecoveryService.cs:35`
- **Problem:** a null `userName` reaches `UserManager.FindByNameAsync`, which throws `ArgumentNullException`.
- **Failure scenario:** `POST /api/ui/admin/users/reset-password` with only `{"password":"..."}` answers 500 instead of "There is no user with that name."
- **Suggested fix:** answer the "no user" error when `string.IsNullOrWhiteSpace(userName)`, as approve and promote already do.

### LOW: The admin screens never close writes, and several user actions skip `ThrowIfClosed`
- **Where:**
  - `src/BaGet.Identity.Application/Screens/ScreenGate.cs:93-97` (the `Administrator` branch returns before the write step)
  - `src/BaGet.Frontend.Base/Controllers/Base/AdminControllerBase.cs:30-41`
  - In `AdminUsersUseCase`: `ResetPasswordAsync`, `SendResetLinkAsync`, `ChangeAsync`
  - `AdminUserDeletionUseCase.DeleteAsync`
- **Problem:**
  - Only the access-administrator role checks for writes.
  - For the actions listed, the only protection left is the database interceptor, which stops database writes but not side effects such as email.
- **Failure scenario:** while the gate is closed, *Email a reset link* sends the email and reports success on a read-only server.
- **Suggested fix:** do either of these:
  - pass `IsWrite` for non-GET requests in both admin bases and honour it in the `Administrator` branch;
  - call `ThrowIfClosed()` in the methods listed.

### LOW: Re-authentication redirects outside the app when it runs under a path base
- **Where:**
  - Return address built from `Request.Path + Request.QueryString` in:
    - `AccessControllerBase.Confirmation`
    - `RepositoryControllerBase.Confirmation`
    - `PasswordConfirmation.cs:58-63`
  - Redirect: `ExternalSignInUseCase.cs:339`
- **Problem:** `Request.Path` excludes the `PathBase`.
- **Failure scenario:** BaGet hosted at `/baget`: an administrator who signs in only through OIDC is sent to `/admin/groups/x` (404) after signing in again.
- **Suggested fix:** build the return address as `"~" + Request.Path + Request.QueryString`, or include `Request.PathBase`.

**Checked and fine:**
- **`/api/ui` access:** `UiApiSecurityFilter` order; keys only; the global key and limited keys refused on admin twins; account screens answer 403 over HTTP.
- **Repository limits on keys:** enforced by `RepositoryActionFilter`.
- **Project administrators:** authorization matches between the view and the twin.
- **`/api/v1` against the screens:** password confirmation, last-administrator protection, the read-only gate and 404 ordering match.
- **Other access:** API documentation access; redirects are local only.
- **Views:** no XSS found; antiforgery on every form; no over-posting.
- **API/MVC split:** application parts and global filters are registered correctly.
- **Use cases and handlers:** password re-asks are present; `PageHandlerAttribute` scoping is correct.

## Host, configuration, logging and telemetry

### HIGH: On a single node the data-protection keys are never persisted, so recreating the container silently loses every stored secret
- **Where:**
  - `src/BaGet/Ha/HaServiceCollectionExtensions.cs:37`: the only key-ring configuration, and it applies only in HA
  - Related: `Dockerfile`, `docs/installation-manual.md:40-60`, `src/BaGet.Core/Repositories/Remote/RemoteRepositorySettings.cs:276-300`
- **Problem:**
  - Without HA the default key ring is used. In the container that is `/root/.aspnet/DataProtection-Keys`, outside the documented `/var/baget` volume.
  - Under IIS without a loaded user profile, the keys exist only in memory.
- **Failure scenario:** after a `docker run --rm` restart or an upgrade, the key ring is new.
  - Remote repository passwords and replication peer keys can no longer be decrypted. They are then treated as absent, so upstreams are called anonymously and replication fails, with no error saying why.
  - Sessions, antiforgery tokens, OCI registry tokens and Identity reset/confirmation tokens all become invalid.
- **Suggested fix:**
  - Always persist the key ring with `PersistKeysToFileSystem` under a `DataProtection:KeyPath` setting. Default it next to the overrides file or `BAGET_CONFIG_ROOT`, and call `SetApplicationName("BaGet")`.
  - Document it in the Docker and IIS install steps.
  - Log a warning when a secret cannot be unprotected.

### MEDIUM: A host that skips `UseRepositoryResolution` serves every `/r/{repo}/` route from the default repository, and the shipped sample does
- **Where:**
  - `src/BaGet.Web.V3/Routing/BaGetEndpointBuilder.cs:77-100`
  - `samples/BaGetWebApplication/Program.cs:32-36`: no `UseRepositoryResolution`, no authentication, no maintenance middleware
- **Problem:**
  - Only `RepositoryResolutionMiddleware` turns `{repo}` into the request's repository and answers 404 to an unknown name.
  - Without the middleware, the request's repository stays `default`, and nothing downstream notices.
- **Failure scenario:** on such a host:
  - `GET /r/private/v3/...` serves `default`'s packages.
  - A push to `/r/whatever/api/v2/package` is stored in `default`.
- **Suggested fix:**
  - Fail closed: the middleware sets a marker, and the read filter or a `MapRoute` endpoint filter refuses requests that have a `repo` value but no marker.
  - Fix the sample's pipeline and its static files.

### MEDIUM: CLI exit codes are discarded, so failures and invalid configuration exit with 0
- **Where:** `src/BaGet/Program.cs:16-48`
- **Problem:**
  - `Main` returns `Task`, so the result of `commands.ExecuteAsync` is lost.
  - A failed options check also just `return`s.
- **Failure scenario:**
  - `baget account promote typo || exit 1` succeeds, although `docs/configuration.md` promises exit code 1.
  - A service with an invalid configuration exits successfully, so `restart: on-failure` and systemd don't see the failure.
- **Suggested fix:**
  - Make `Main` return `Task<int>` and `return await commands.ExecuteAsync(args)`.
  - Return a non-zero code when the options check fails.

### MEDIUM: Telemetry export is on in the shipped `appsettings.json`, and the test harness never turns it off
- **Where:**
  - `src/BaGet/appsettings.json:130-139`: the comment says off by default, but `Enabled` is `true` with endpoint `http://localhost:4317`
  - `src/BaGet/appsettings.Overrides.json` is tracked (commit `53c6a3b9`) and is published with the app
  - `tests/BaGet.Tests/Support/BaGetApplication.cs`
- **Problem:**
  - Every install exports traces (paths, repository and package names) to whatever listens on `localhost:4317`.
  - The overrides file ships with the publish output, so an upgrade by copying that folder replaces the operator's saved overrides.
  - Test hosts start with a live exporter, which is almost certainly why the 11 "known failing" telemetry tests fail: they assert that telemetry is off by default.
- **Suggested fix:**
  - Set `Telemetry:Enabled=false` in the test harness.
  - **Your decision** (both files hold your deliberate settings):
    - ship telemetry off, or move the Jaeger default to `appsettings.Development.json`;
    - untrack the overrides file and exclude it from publish.

### MEDIUM: The default administrator `Admin`/`Admin1234!` is fully usable on a network-exposed default install
- **Where:**
  - `src/BaGet/Hosting/HostAdministratorExtensions.cs:28-38`
  - `AdministratorService.cs:26,49-75`
  - `AccountsOptions.cs:44-49`: the XML doc says the password is changed "at the first sign-in"
  - `Dockerfile:5`
- **Problem:**
  - Only a log warning guards the default password; nothing forces the change.
  - The account can mint `admin:api` keys, which keep working after the password is changed.
- **Failure scenario:**
  - Anyone who reaches a fresh container signs in as `Admin` before the operator does, mints a key, and owns the server.
  - Self-registration is also open by default.
- **Suggested fix:** one of:
  - while the default password is in use, allow only changing it: no API keys, no `/api/v1` or `/api/ui` calls;
  - generate a random password and print it once at first start.

  Either way, correct the XML doc. **Your decision.**

### LOW: Every form accepts multipart bodies of about 2 GiB from anonymous callers
- **Where:**
  - `src/BaGet/ConfigureBaGetOptions.cs:65-68` (`MultipartBodyLengthLimit = int.MaxValue`)
  - `HostServiceCollectionExtensions.cs:23-28` (no Kestrel body limit)
- **Problem:**
  - Antiforgery validation reads the whole form before the action runs.
  - Multipart files above 64 KB are buffered to temp files.
- **Failure scenario:** a few concurrent anonymous 2 GiB POSTs to `/account/login` fill the temp disk.
- **Suggested fix:**
  - Set a small default limit.
  - Raise it per action, with `[RequestFormLimits]` or `[RequestSizeLimit]`, only on the upload and push endpoints.

### LOW: In Development the overrides file outranks user secrets, contrary to the comment
- **Where:** `src/BaGet/Hosting/HostConfigurationExtensions.cs:17-21,60`
- **Problem:** user secrets are also a `JsonConfigurationSource`, so the overrides file is inserted after them and takes precedence.
- **Suggested fix:** insert after the last `appsettings*` source, or fix the comment.

### LOW: The Docker image runs as root
- **Where:** `Dockerfile`: no `USER` instruction; listens on port 80
- **Suggested fix:** add `USER $APP_UID`, listen on 8080, and document the volume ownership. **Your decision** (changes the documented port).

Duplicate of a security finding, counted there: forwarded headers trusted from any client.

**Checked and fine:**
- **Request logging:** paths only, with redaction; the enricher order and level floors are correct.
- **Traces:** query values redacted, no headers recorded, SQL text removed.
- **Swappable exporters:** lease, disposal and reload logic are correct, and the sampler behaves as documented.
- **Error messages:** `TelemetryStatus.Scrub` strips URLs and header values.
- **Settings:** the overrides file is written atomically under a lock, and sensitive values are never returned.
- **Log-level overrides:** source names are validated.
- **Middleware order:** correct, and CORS is configured without credentials.
- **CLI rules:** the commands apply the same rules as the UI.
- **HA:** startup checks and key storage are correct.
- **Options validation:** uses the same lists the Settings page offers.
