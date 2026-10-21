# Full UI coverage: repository-aware pages, access control (groups, permission targets, projects), admin tools

Goal: every capability of the server is usable from the web UI, not only from REST or the CLI. This plan
**implements `artifactory-parity.md` P2 (access control)**; that plan's P2 row is to be marked "moved here".

Inventory that motivates it (2026-10-21, HEAD `1433dba`):

| Area | Backend | UI |
|---|---|---|
| Groups, permissions, private reads | **absent**. `Repository.AnonymousRead` is stored and edited but never enforced | none |
| Non-default repositories | every protocol route also under `r/{repo}/` (`BaGetEndpointBuilder.cs:20-33`) | only admin create/edit/delete; **every other page shows `default` only**, Upload has no repository |
| Admin operations | `catalog rebuild`, `import downloads` CLI only (`Program.cs:61-95`); no quarantine or abuse overview | none |
| Account tools | no admin user creation, no admin key listing, no organization rename/delete | — |
| Metadata | vulnerabilities via REST only (`PackageMetadataAdminController.cs:91-121`); `SetVerifiedAsync` has no caller (`PackageOwnershipService.cs:172`); symbols cannot be listed or deleted | deprecation only |

## Blocking — resolve before coding

| # | Check | Why it blocks | How |
|---|---|---|---|
| **B1** (resolved, see below) | Page routes under `r/{repo}/` without breaking links | CLAUDE.md: a second page route (`AddPageRoute`, `IServiceCollectionExtensions.cs:43`) makes `asp-page`/`Url.Page` emit empty `href`s. U1 needs every page reachable at `/x` and `/r/{repo}/x` | Prototype an `IPageRouteModelConvention` that adds an `r/{repo}/`-prefixed selector to every page, the existing one keeping `Order` 0. Test `Url.Page` with and without an ambient `repo` value, `asp-page` in a layout, `RedirectToPage`, and the existing `/packages` alias. If generation is ambiguous, fall back to an explicit `asp-route-repo` everywhere via one tag helper. Decide before U1 |
| **B2** (resolved, see below) | Private reads with real NuGet clients | Enforced reads (U2) answer 401; `dotnet`, `nuget.exe`, Visual Studio and PowerShellGet must send credentials | Against a test host: `dotnet nuget add source -u <user> -p <api key>` and `--store-password-in-clear-text`, `nuget.exe sources add`, PowerShellGet `-Credential`. Confirm `401` + `WWW-Authenticate: Basic realm=...` triggers them, and that symbol downloads (Visual Studio) and the V2 feed authenticate the same way |
| **B3** (resolved, see below) | Upgrade equivalence | Existing installs must behave exactly as today after U2 (anonymous read everywhere, push and manage by ownership) | U2's migration seeds built-in targets (D3). A test runs the whole current authorization suite (`OwnershipAuthorizationService` cases, push/unlist/delete/deprecate, Manage page) on the seeded model with no edits: identical outcomes |
| **B4** (resolved, see below) | Cost of a check per request | Reads are hot (registration, flat container, search) | Measure the evaluator with 50 targets / 20 groups; cache the resolved permission set per principal per request plus a short per-user cache invalidated by an "access model version" bumped on every change |
| **B5** (resolved, see below) | Single-repository backends | Azure Table (`NullContext`) and `Search:Type=AzureSearch` serve `default` only (`SupportsMultipleRepositories=false`) | Switcher, repository pickers and per-repository targets hidden there; permission tables need a relational database. Under Azure Table, keep today's model and say so on the admin pages |
| B6 (resolved, see below) | Symbol deletion | No `ISymbolStorageService` delete; symbols are keyed by PDB, not by package (`SymbolStorageService.cs:50-76`) | Find where a `.snupkg` → PDB keys mapping can be recorded at index time. Without it, list/delete per package is impossible for existing symbols; decide (record from now on + document) |

### B1 outcome

| Finding | Decision |
|---|---|
| The `IPageRouteModelConvention` twin selector (`r/{repo}/` + each selector, `Order` -1, `/packages` alias included) generates correctly: no `repo` value gives the plain URL, a `repo` value the prefixed one, `repo = null` leaves; `Url.Page`, `asp-page`, forms and `RedirectToPage` agree | Implemented: `RepositoryPageRouteConvention` |
| **Ambient `repo` is not carried to a different page** (only to the same page): `asp-page="/Upload"` in the layout of `/r/second/…` dropped the prefix | Not a per-link `asp-route-repo` tag helper but one `IUrlHelper` decorator (`RepositoryUrlHelperFactory`): adds `repo` to every link to a repository page; an explicit value wins; other pages untouched |
| Hard-coded `~/packages/…` hrefs ignore routing | Replaced by `Url.Page` in the repository pages |

### B2-B5 outcomes (track `access`)

| # | Finding | Decision / where |
|---|---|---|
| B2 | Run against a real host (`Anonymous read` off on `second`): `dotnet package search` / `dotnet restore` get `401` + `WWW-Authenticate: Basic realm="BaGet"` anonymously and with a wrong password or the right key under another user name; with `-u <owner> -p <key>` they work (`NU1301` otherwise). `nuget.exe` 6.2.1 `install` likewise (its credential providers run on the challenge). V2 and symbol routes challenge and accept Basic identically. **Not runnable here:** Visual Studio and PowerShellGet (needs the NuGet provider bootstrap from the internet); documented per client in `docs/access-control.md` | Credentials: password = API key, user name checked against the key's owner (`RequestPrincipalResolver`); `http` sources need `allowInsecureConnections` in the client config, not a server matter |
| B3 | `AccessUpgradeEquivalenceTests` (written first, green on the pre-U2 code except the seed check): a pre-repository database migrated by the current code serves anonymous reads in both repositories, refuses anonymous writes, and keeps ownership/scope/administrator outcomes. The existing authorization suite runs on the same seeded model (the harness migrates a fresh database). **The plan's seed was too narrow:** "Authenticated users deploy" needs Annotate, Delete and Manage as well, or unlisting/deprecating by owners (decided by ownership alone before) would break | Seed: Anyone reads (Read, Everyone); Authenticated users deploy (Annotate, Deploy, Delete, Manage; Authenticated) |
| B4 | `AccessEvaluatorTests.ACheckAgainstFiftyTargetsAndTwentyGroupsCostsMicroseconds`: 50 targets, 20 groups, 20 000 package-level checks, 6.5 microseconds each in memory (development machine) (the assertion allows 500). Whole model cached as `AccessSnapshot`; invalidated by `AccessModelVersion` (bumped by a SaveChanges interceptor, so no editor forgets) and a 30 s TTL for other instances; memberships per user loaded once per snapshot | `BaGet.Core/Access/AccessSnapshot.cs` |
| B5 | Azure Table (`NullContext`): the evaluator allows Read to all and writes to any authenticated principal (today's model); the switcher/pickers are already hidden (`SupportsMultipleRepositories`). `Search:Type=AzureSearch` with a relational database keeps the model for the `default` repository. The admin pages of U3 must say so | `AccessEvaluator`, `AccessEvaluatorTests.WithoutARelationalDatabase...` |

### B6 outcome

| Finding | Decision |
|---|---|
| `SymbolIndexingService` already holds package ID/version and each PDB's file + key when it stores a PDB | Record there: new `PackageSymbol` table (`IRepositoryScoped`, index on package and on file+key; 4 migrations `AddPackageSymbols`), written through `ISymbolRegistry` after `MaintenanceGate.ThrowIfClosed` |
| `ISymbolStorageService` had no delete | Added `DeletePortablePdbContentAsync`; registry deletes rows first, then content not referenced by another version (a PDB can be shared) |
| Symbols indexed before cannot be mapped (the `.snupkg` is not kept; storage is keyed by file and key only) | **No backfill**: still served, not listed per package; documented on the Manage page and in `docs/configuration.md` |
| Azure Table has no relational tables | `IsSupported` false: nothing recorded, Manage shows a note |

## Decisions

| # | Decision (taken) | Consequence |
|---|---|---|
| D1 | **Implement P2 access control now** (groups, memberships, permission targets, projects with roles, enforced anonymous read, private reads, audit) | Largest part. Four migrations per phase, B3 is a merge gate |
| D2 | **Pages under `/r/{repo}/`** mirroring the feeds, plus a repository switcher in the header; `/` stays `default` | Shareable per-repository URLs; B1 decides the mechanism |
| D3 | **Upgrade = today's behaviour**: seeded targets "Anyone reads every repository" (`*`, Read, the built-in *Everyone* principal) and "Authenticated users deploy" (`*`, Deploy, built-in *Authenticated*), with ownership still applied inside Deploy/Delete/Manage | No install changes until an admin edits them |
| D4 | **Ownership stays** as the NuGet rule inside Deploy/Delete/Manage (`OwnershipAuthorizationService.cs:42-113`). A permission is necessary; ownership remains necessary for existing IDs | Admin pages explain both layers |
| D5 | **Server administrators keep full access** (`IsAdministrator`) | Matches today's bypass; fix the stale doc comment at `Accounts.cs:14-18` |
| D6 | **API keys: optional repository scope** (none = all, as today) | Effective rights = key scopes ∩ key repositories ∩ the user's permissions |
| D7 | **Add the missing backend operations** (admin operations, account tools, metadata) and fix the four UI inconsistencies (U8) | — |

## Access model (U2)

| Entity (new, `BaGet.Core/Access`) | Fields |
|---|---|
| `Group` | `Key`, `Name` (unique, `RepositoryNames`-style rule), `Description`, `Created`, `IsBuiltIn` (*Everyone*, *Authenticated*: not deletable, membership implicit) |
| `GroupMember` | `GroupKey`, `UserId` |
| `PermissionTarget` | `Key`, `Name`, `RepositoryPatterns` (names or `*` globs), `IncludePatterns`, `ExcludePatterns` (package-ID globs, default `*`), `ProjectKey?` |
| `PermissionGrant` | `TargetKey`, `PrincipalType` (User / Group / Organization), `PrincipalId`, `Actions` flags: `Read`, `Annotate` (deprecation, vulnerabilities), `Deploy` (push, new versions), `Delete` (unlist/delete/overwrite), `Manage` (owners, target-scoped admin) |
| `Project` (exists, `Repository.cs:67-79`) + `ProjectMember` | `ProjectKey`, `UserId`/`GroupKey`, `Role` (Admin / Developer / Viewer → Manage+Delete+Deploy+Annotate+Read / Deploy+Annotate+Read / Read) on the project's repositories |
| `AccessAuditEntry` | `Created`, `Actor`, `Action`, `Subject`, `Details` (JSON); written on every change in this table set |
| `ApiKeyRepository` | `ApiKeyKey`, `RepositoryKey` (D6) |

```csharp
public interface IAccessEvaluator                        // BaGet.Core/Access
{
    Task<bool> IsAllowedAsync(BaGetPrincipal? principal, RepositoryInfo repository,
        string? packageId, AccessAction action, CancellationToken cancellationToken);
    Task<IReadOnlyList<RepositoryInfo>> ReadableRepositoriesAsync(BaGetPrincipal? principal, CancellationToken ct);
}
```

| Enforcement point | Change |
|---|---|
| Every protocol read (service index, search, autocomplete, registration, content, V2, catalog, symbols, vulnerabilities, signatures) | Endpoint filter on the `MapRoute` registrations (`BaGetEndpointBuilder.cs:20-33`): `Read` on the resolved repository; 401 + `WWW-Authenticate: Basic` when anonymous, 404 when authenticated without access (no existence leak) |
| Credentials on reads | `HttpRequestExtensions` (`:13,66-69`) also accepts `Authorization: Basic` (password = API key; user name checked against the key's owner), B2 |
| Writes | `OwnershipAuthorizationService` first requires the matching action, then today's ownership rules (D4) |
| Pages (U1) | `RepositoryPageModel` base: `Read` on the page's repository; actions require their action |
| `Repository.AnonymousRead` | Becomes a shortcut that adds/removes the *Everyone* Read grant for that repository; the edit page shows the effective grants |

## Phases

Each phase: blocking checks first, tests first, full suite green, 0 warnings, docs in the same commits, Opus review.

### U1 — Repository-aware pages (D2)

Status: implemented (B1 above). Access hook: `IRepositoryAccess` (`BaGet.Core`, `OpenRepositoryAccess` = every NuGet repository) is the single place U2's
`IAccessEvaluator` replaces: switcher, upload picker, My packages and `RepositoryPageFilter` (404) all ask it. No `RepositoryPageModel` base was needed:
the filter is global and a page model reads `IRepositoryContext`.

| Change | Where |
|---|---|
| Route convention (B1) adding `r/{repo}/` to every page in `BaGet.Web` and the package pages of `BaGet.Identity`; `RepositoryResolutionMiddleware` already resolves `{repo}` | `IServiceCollectionExtensions.cs:40-44` |
| Header switcher listing `ReadableRepositoriesAsync`; current repository in the page title; hidden when only one (B5) | `_Layout.cshtml` |
| Every page uses the request's repository: Index/search, Package (download/icon via `IUrlGenerator`), Statistics, ReportAbuse, Manage, ContactOwners, My packages (grouped by repository), Profiles, Admin Packages, Admin UserPackages | each page model |
| **Upload to a chosen repository**: repository picker (writable ones), staging, submit, CLI snippets (`GetServiceIndexUrl` / `GetV2FeedUrl` for that repository) | `Upload.cshtml(.cs):146-305` |
| Admin Repositories: "Browse" and "Upload" links per repository | `Admin/Repositories.cshtml` |
| `PackageDetailsUriTemplate` / `ReportAbuseUriTemplate` advertised for every repository once pages exist there | `BaGetUrlGenerator.cs:173,215` |

### U2 — Access control backend (D1, D3-D5)

Status: **implemented** (track `access`; B2-B5 above).

The access model above; 4 migrations; seed (D3); `IAccessEvaluator` with the B4 cache; enforcement points; Basic auth;
audit entries; `AnonymousRead` mapping; Azure Table falls back to today's model (B5).

| Deviation from the sketch above | Why |
|---|---|
| Reads are enforced by `RepositoryReadFilter`, a global MVC **resource filter** reading a `RepositoryRouteMetadata` that `MapRoute` adds, not an endpoint filter | Endpoint filters exist for minimal APIs, not controllers; a global MVC filter also reaches hosts that map their own pipeline |
| `ApiKeyRepository` (D6) not created here | It belongs to U4 |
| `AnonymousRead` is a **gate** on the Everyone group's Read for that repository (turning it on re-creates an `Anonymous read {name}` target if none grants Everyone Read), not a stored grant that is added and removed | A seeded target with pattern `*` cannot be split per repository; the flag keeps its column and its meaning |
| Group/target names allow 128 characters, spaces and `._-` (`AccessNames`) | A generated `Anonymous read {name}` must fit a 64-character repository name |
| `PermissionTarget.IsBuiltIn`, `PackageAction.Annotate`, `ProjectMember` has `PrincipalType` (User or Group) instead of two nullable columns | Restorable built-ins; deprecation edits map to Annotate, unlist/delete to Delete; one column pair |
| Read include/exclude patterns protect per-package routes (those with `{id}`); search, autocomplete and V2 listings are filtered per repository only | Filtering result sets by pattern needs the search/query layer; documented, open |
| Deploy/Delete/Annotate/Manage do not imply Read | As Artifactory; a private repository needs explicit Read grants |

### U3 — Access control UI (`BaGet.Identity/Pages/Admin`)

Status: **implemented** (track `access-ui`; `AccessAdminPagesTests`, `AccessInspectionTests`; user docs `docs/access-control.md#admin-pages`).

| Deviation from the sketch below | Why |
|---|---|
| `/admin/users/{name}` already existed (U6): extended with groups, project roles and effective permissions instead of created | no second page |
| Password rule: every change except creating an empty group/target/project and renaming a group | "grants and deletions" also covers memberships, project roles and pattern edits, which widen access the same way |
| Project administrators may not assign repositories or delete the project; a project target's preview shows only the project's repositories | assigning would let them take any repository; the preview would disclose private repository names |
| Grant editing = a "Set grant" form (prefilled by `?principalType=&principalName=`) plus Remove, not inline checkboxes per row | one password per form instead of per row |
| Azure Table: pages redirect to `/admin/access-unavailable`; `/account/access` says there is no model | B5: "say so on the admin pages" |
| `ConfirmPasswordView.ReturnPage` gets the real URL on the new pages | the older pages pass a page name, which the sign-in-again flow cannot return to |

| Page | Actions |
|---|---|
| `/admin/groups`, `/admin/groups/{name}` | create, rename, delete (not built-ins), add/remove members (user search), list the group's grants |
| `/admin/permissions`, `/admin/permissions/{name}` | create/edit targets: repository patterns (with matching-repository preview), include/exclude patterns (with a test box: "does `Contoso.Lib` match?"), grants per user/group/organization with action checkboxes |
| `/admin/projects`, `/admin/projects/{name}` | create, assign repositories, members with roles |
| `/admin/users/{name}` (new detail page) | groups (add/remove), organizations, projects, **effective permissions per repository** (evaluator explanation: which target grants what) |
| `/admin/repositories/{name}` (extend) | who can read/deploy/delete/manage (effective), anonymous read toggle wired to D3 |
| `/admin/audit` | access audit log, filter by actor/subject/date |
| Account: `/account/access` | the signed-in user's own effective permissions per repository |

All POSTs: admin (or project admin for their project), antiforgery, password/re-auth confirmation for grants and
deletions, `MaintenanceGate.ThrowIfClosed`.

### U4 — Repository-scoped API keys (D6)

Status: **implemented** (track `keys`). `ApiKeyRepository` (`ApiKeyKey` FK with cascade, `RepositoryKey` without one, unique pair; migrations `AddApiKeyRepositories` x4);
`BaGetPrincipal.RepositoryKeys` (null = all); `AccessEvaluator` applies it first: other repository = no action (administrators included), Read judged as anonymous.

| Deviation from the sketch below | Why |
|---|---|
| Out-of-limit Read is evaluated as anonymous, not denied | A key must never see less than no key at all (open repositories stay readable), nor more |
| Deleting a repository deletes keys limited to it alone | No rows would mean "all repositories": silently widening the key |
| Picker lists repositories the user can **read** and the service rejects unknown keys | A key cannot be limited to a repository the user cannot see; Deploy is not required (the user's permissions still decide at use) |
| Admin user page shows the repositories column | Requested by the accounts track |

Original sketch:

`ApiKeyRepository` table; API keys page (`Account/ApiKeys.cshtml.cs:45`) gets a repository multi-select (readable
repositories, "all" default); `AccountAuthenticationService.cs:35-64` loads the key's repositories into `BaGetPrincipal`;
evaluator intersects. Existing keys = all repositories.

### U5 — Admin operations (implemented in track `ops`)

| Feature | Backend | UI |
|---|---|---|
| Catalog rebuild per repository | background job wrapping `CatalogRebuilder.RebuildAsync` (`CatalogRebuilder.cs:42`), single slot, progress | `/admin/operations` button + status (added/deleted counts) |
| Import downloads per repository | job wrapping `DownloadsImporter.ImportAsync` (`DownloadsImporter.cs:29`) | same page |
| Validation queue / quarantine | list pending and quarantined submissions (`PackageValidationRunner.cs:60` + query), **retry**, **release** (index anyway, audited), **reject** (delete staged file) | `/admin/validation` |
| Abuse reports, all repositories | list, filter unresolved, resolve, open Manage | `/admin/abuse` |

### U6 — Admin account tools

Create a user (with optional generated password / invite email); list and revoke any user's API keys
(`ApiKeyService.cs:70,84`); list all organizations, rename, delete (refuse while it owns packages; reuse the
last-owner rules of `PackageOwnershipService`), manage members. Pages under `/admin/users/{name}` and
`/admin/organizations`.

### U7 — Package metadata

Status: implemented (track `metadata`). Shared rules: `PackageVulnerabilityRules` (`BaGet.Core`), used by `PackageMetadataAdminController` and the page.
The Manage handlers are `AddVulnerability`, `RemoveVulnerability`, `SetVerified` (administrator only, `PackageOwnershipService.SetVerifiedAsync`), `DeleteSymbols`;
all work in the page's repository and are refused in read-only mode by the page's existing gate. Tests: `PackageMetadataPageTests`.

| Feature | Where |
|---|---|
| Vulnerabilities editor (add/remove advisory URL, severity, range) on Manage, calling the same validation as `PackageMetadataAdminController.cs:156-189` (shared service) | Manage page |
| Verified (reserved) flag set/clear, admin only, via `SetVerifiedAsync` | Manage page |
| Symbols per version: list PDBs, delete (B6) | Manage page |

### U8 — UI inconsistencies

**Done** (`UiConsistencyTests`). Links go through `RepositoryPageLinks` (`BaGet.Identity`), the one seam for where a repository's package pages live: default at `/packages/…`, others at `/r/{name}/packages/…` (404 until the repository-aware pages exist). "Manage" is shown to owners holding the Manage action (the seeded model gives every signed-in user that) and to administrators. The Upload nav rule is `UploadAvailability` (read-only, closed maintenance gate, nothing to authenticate with).

| Fix | Where |
|---|---|
| "Manage package" only for owners, admins, or holders of Manage | `Package.cshtml:361-366` |
| Upload nav item hidden when no upload is possible (read-only, nothing can authenticate, no writable repository) | `_Layout.cshtml:67` |
| Account-deletion blockers show the repository and link to that repository's Manage page | `Account/Settings.cshtml:21-30`, `AccountDeletionService.cs:108-131` |
| Admin delete-user: offer deleting sole-owned IDs in **every** repository, not only `default`; Admin Packages lists ownerless IDs per repository | `AccountDeletionService.cs:198-200`, `Admin/Packages.cshtml.cs:34` |

### U9 — Docs

`docs/configuration.md` (access model, private feeds, Basic auth client setup, upgrade defaults), a new
`docs/access-control.md`, `docs/advanced/api.md` (401/404 semantics), `CLAUDE.md` (access model, page routing
convention), `artifactory-parity.md` (P2 moved here), mkdocs nav and `BaGet.sln` docs folder.

## Order

U8 (independent) and B1 → U1 → U2 → U3 ∥ U4 → U5 ∥ U6 ∥ U7 → U9. U2 needs U1 so pages can enforce per repository.

## Verification

| # | Check | Expected |
|---|---|---|
| 1 | Upgrade (B3): pre-U2 database, no edits | Every current authorization test passes unchanged; anonymous reads still 200 |
| 2 | Pages at `/r/second/…` | Search, package, upload, manage, stats, report abuse act on `second`; links stay in `second`; `/` unchanged |
| 3 | Upload to `second` from the browser | Package appears only in `second`'s feed |
| 4 | Remove *Everyone* Read on `second` | Anonymous feed/page → 401 + Basic challenge; `dotnet restore` with `-u/-p` works (B2); a user without Read gets 404 |
| 5 | Group grant | Member gains Deploy on `second`; removal revokes on the next request (cache invalidated) |
| 6 | Include/exclude patterns | Deploy for `Contoso.*` only; push of `Other.Lib` → 403 |
| 7 | Project roles | Developer pushes, Viewer reads only, project admin manages members of that project only |
| 8 | Key repository scope | Key limited to `second` → 403 on `default` |
| 9 | Admin operations | Catalog rebuild/import run with status; quarantine release indexes the package; abuse list spans repositories |
| 10 | Account tools, metadata, U8 fixes | Each page test; audit entries for every access change |
| 11 | Azure Table (B5) | No switcher, no access pages; behaviour as today |
| 12 | Full suite, 0 warnings, Opus review per phase | — |

## Risks

| Risk | Handling |
|---|---|
| Locking users out on upgrade | D3 seed + B3 gate; built-in targets restorable from the admin page |
| Existence leaks of private repositories/packages | 404 (not 403) when authenticated without Read; switcher lists readable repositories only; search filtered |
| Per-request cost | B4 cache + access-model version |
| Link generation under two routes | B1 decided before U1 |
| Credential prompts in clients | B2 matrix documented per client |
| Scope (multi-week) | Phases merge independently; U8 ships first |

## Out of scope

npm/Maven/Docker formats, remote/virtual repositories, retention, scanning, admin REST API (`/api/v1`), replication, HA —
remaining `artifactory-parity.md` phases.
