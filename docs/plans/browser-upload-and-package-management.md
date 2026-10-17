# Browser upload and package management UI

Give signed-in users a nuget.org-like flow: upload a package through the browser with a metadata
preview, and manage the packages they own.

## Already built — do not rebuild

| Capability | Where |
|---|---|
| Register, Login, Logout, account Profile, API keys, Organizations | `src/BaGet.Identity/Pages/Account/` |
| Public profile | `src/BaGet.Identity/Pages/Profiles/Profile.cshtml` |
| Per-package owner page: owners, unlist, relist, deprecate, abuse reports | `src/BaGet.Identity/Pages/Packages/Manage.cshtml.cs:62-110` |
| List the IDs a user owns (incl. via organization) | `PackageOwnershipService.GetOwnedPackageIdsAsync` — `src/BaGet.Identity/PackageOwnershipService.cs:83` |
| Submission pipeline (validation-aware) | `IPackageSubmissionService` — `src/BaGet.Core/Indexing/IPackageSubmissionService.cs` |
| Push authorization + first-push ownership | `IPackageAuthorizationService` — `src/BaGet.Core/Authentication/IPackageAuthorizationService.cs:22` |
| Multipart limit already unbounded | `ConfigureBaGetOptions.Configure(FormOptions)` — `src/BaGet/ConfigureBaGetOptions.cs:80-83` |
| Sign in / API keys nav | `src/BaGet.Web/Pages/Shared/_Layout.cshtml:35-49` |

**So the real gaps are:** a browser upload, a "my packages" list, version delete, and symbols in the UI.
`Unlist`/`Relist` already exist (`Manage.cshtml.cs:78-81`, rendered at `Manage.cshtml:50-52`) and only
need to be reachable from the new list.

## Decisions

| # | Decision | Consequence |
|---|---|---|
| D1 | Two-step upload with metadata preview | Needs the `.nupkg` staged between two requests. No session or TempData exists in the repo today, so staging is new infrastructure — see C2. |
| D2 | A signed-in session gets `ApiKeyScopes.All`, `isSuperuser: false`, glob `*` | Ownership still decides: `OwnershipAuthorizationService.cs:44-48` requires `IsOwnerAsync` for an existing ID, so a user can push a new ID or one they own, nothing else. Not an escalation over the API-key path. |
| D3 | With `Accounts:Enabled=false`, show the form with a pasted API key | The upload page **cannot** derive from `AccountPageModel`: that base answers 404 whenever accounts are disabled (`AccountPageModel.cs:42-46`). It needs its own base handling both modes. |
| D4 | Management covers: my-packages list, version delete, upload-a-version from the package page, `.snupkg` | Delete must honour `BaGetOptions.PackageDeletionBehavior`; `Unlist`/`Relist` are reuse, not new work. |

## Blocking verification — before writing code

| # | Check | Why it blocks | How |
|---|---|---|---|
| B1 | A staged `.nupkg` must not be downloadable by anyone who guesses its name | Staging in `IStorageService` shares a store with served packages. | Inspect `BaGetEndpointBuilder.MapPackageContentRoutes` and `FileStorageService` path handling; add a test requesting the staging path over HTTP. |
| B2 | `.snupkg` push requires the package to already exist | Decides the UI's error text and whether symbols can be a first upload. | Read `SymbolIndexingService.IndexAsync` for its "package not found" result. |
| B3 | `PackageDeletionBehavior` semantics | `Unlist` vs `HardDelete` makes "Delete" mean two different things; the button must say which. | `src/BaGet.Core/Configuration/BaGetOptions.cs:43` and `PackageDeletionService`. |
| B4 | Antiforgery with a large multipart body | Razor Pages validate the token before model binding; with a several-hundred-MB file the token must still be read first. | Prototype a 200 MB multipart POST against a stub page; confirm nothing beyond `FormOptions` is needed. |
| B5 | `IStorageService` resolves when accounts are disabled | The API-key mode of D3 stages files too. | Provider-resolved in `Program.ConfigureServices`; assert with an integration test at `Accounts:Enabled=false`. |

B1 and B5 passed, so the temp-file fallback is dropped: no route in BaGet turns a request into a
storage path, and `IStorageService` is provider-resolved independently of `Accounts:Enabled`.

## Changes

### C1 — Session principal (`src/BaGet.Identity/`)

New `SessionPrincipalFactory`, so the browser path reuses the same authorization as API keys.

```csharp
/// <summary>
/// The principal for a signed-in browser session. A session carries every scope - ownership
/// still decides which IDs it may touch - so the UI can do what the user's own API key could.
/// </summary>
public BaGetPrincipal Create(ClaimsPrincipal user)
    => new BaGetPrincipal(
        userId: _users.GetUserId(user),
        userName: user.Identity.Name,
        isSuperuser: false,
        scopes: ApiKeyScopes.All,
        packageGlobPattern: "*");
```

### C2 — Staging (`src/BaGet.Core/`)

New `IPackageStagingService` plus a storage-backed implementation, so the preview step survives a
multi-instance deployment and works on all four storage providers.

```csharp
public interface IPackageStagingService
{
    /// <returns>An opaque token identifying the staged upload.</returns>
    Task<string> StageAsync(Stream package, string ownerId, CancellationToken cancellationToken);

    /// <returns>Null if the token is unknown, expired, or was staged by a different user.</returns>
    Task<Stream> OpenAsync(string token, string ownerId, CancellationToken cancellationToken);

    Task RemoveAsync(string token, CancellationToken cancellationToken);
}
```

Built (`src/BaGet.Core/Staging/`), with `Task<int> SweepAsync(DateTime stagedBefore, ...)` added to the
interface above:

| Point | As built |
|---|---|
| Token | 128 bits from `RandomNumberGenerator`, hex encoded (`StoragePackageStagingService.CreateToken`) — a token reaches storage, so `OpenAsync`/`RemoveAsync` refuse anything that is not 32 lowercase hex characters before it becomes a path. It is logged only at `Debug`; above that a short SHA-256 correlation id stands in, so a log reader cannot act on what they read |
| Owner | Recorded in `staging/{token}.json`; `OpenAsync` **and** `RemoveAsync` compare it ordinally and refuse otherwise, so one user can neither confirm nor discard another's upload. With accounts off every upload shares the empty owner, as the API-key path has no user either |
| Paths | `staging/{token}.json` (metadata, written first so the sweep can always find the upload) and `staging/{token}.nupkg` |
| Null store | `Storage:Type=Null` reads back nothing, so a staged upload reports itself as an unknown token instead of yielding a null stream |
| Sweep | `SweepAsync` removes every upload staged before a cutoff; `StagingSweepWorker` (`src/BaGet.Web/`) runs it hourly with a 24-hour cutoff. An upload whose metadata is **absent** or will never parse is collected whatever its age — nobody can open it. One whose metadata is merely unreadable *this time* (a store briefly unreachable, a file a backup holds open) is kept: "unreadable now" must not discard an upload staged a minute ago. The two are told apart from the sweep's own listing |
| Listing | The sweep needs to enumerate a prefix, which `IStorageService` cannot. New optional capability `IListableStorageService`, implemented by **every** store BaGet ships: `FileStorageService`, `NullStorageService`, `BlobStorageService`, `S3StorageService`, `GoogleCloudStorageService`, `AliyunStorageService`. A store that does not implement it logs a warning and is not swept; `StorageCanBeSweptTests` pins that none of the shipped ones is in that state |

### C3 — Upload page (`src/BaGet.Web/Pages/Upload.cshtml`)

Built. The CLI tabs stay, below the form. `UploadModel` is a plain `PageModel`, **not** an
`AccountPageModel` (D3): that base answers 404 while accounts are disabled.

| Handler | As built |
|---|---|
| `OnPostStageAsync(IFormFile package, string apiKey)` | Refuse read-only (401) → refuse a server nothing can authenticate against (401) → authenticate (401) → reject anything but `.nupkg`/`.snupkg` (400) → read the archive (400 if unreadable) → reject a `.snupkg` whose nuspec does not declare `SymbolsPackage` (400) → `AuthorizeAsync(Push, id)` **before** storing (403) → `StageAsync`, a storage failure reported as a sentence (500) → render the preview |
| `OnPostSubmitAsync(string token, string apiKey, bool acceptTerms)` | Refuse read-only (401) → refuse a server nothing can authenticate against (401) → authenticate (401) → `OpenAsync(token, userId)`, null ⇒ 404 "no longer staged" → re-read the archive → require the checkbox (400, upload kept) → `AuthorizeAsync` again (403) → `SubmitAsync`, `OnPushedAsync`, `RemoveAsync` |
| `OnPostCancelAsync(string token, string apiKey)` | Refuse read-only (401) → refuse a server nothing can authenticate against (401) → authenticate (401) → `RemoveAsync(token, userId)`, false ⇒ 404 "no longer staged". Owner-checked like the other two: the token is the only handle on an upload, so anyone who learned one could otherwise make its owner upload the whole file again, and the deletion is a store write read-only mode is meant to freeze. The button posts from the confirm form, so the one API key field serves both |

**A server nothing can authenticate against** is accounts off with no `ApiKey`, which is how
BaGet ships. Nothing there tells one visitor from another, so the page explains that instead
of rendering a file picker and an API key box that would accept anything, including nothing.
`PUT /api/v2/package` is untouched — the page is a convenience, not the authorization model —
and `Program.WarnIfNothingAuthenticates` says so once at startup.

| Point | As built |
|---|---|
| Preview | ID, version, description, authors, license (expression, else URL), dependencies by framework, readme. `PackageDependencies.GroupByFramework` and `ReadmeMarkdown` are shared with the package page, so a preview cannot describe the package differently from how it will be served |
| Terms | Alpine disables the button until the box is ticked, **and** the handler refuses without it - with the upload kept staged, so ticking the box is all that is needed |
| Mode | `AccountsEnabled` ⇒ `SessionPrincipalFactory.Create(User)`, resolved from `HttpContext.RequestServices` because the factory is only registered when accounts are on. Otherwise `AuthenticatePrincipalAsync(apiKey)` |
| API key | Asked again on the confirm form rather than carried in a hidden field, so the key is never written into HTML (the risk table's last row) |
| Authorization | Checked at both steps: the first spares the store a file nobody may publish, the second is the one that gates publishing (ownership can change while a preview is open) |
| Antiforgery | Nothing added, per B4: the form tag helper's single hidden token plus the existing `FormOptions` and Kestrel settings are sufficient, and field order is irrelevant. No `[RequestSizeLimit]`/`[RequestFormLimits]` |
| Failure | Every refusal is a sentence on the page with a matching status code (401/403/404/400/409/500), never a stack trace |

### C4 — Symbols

Built into the same page. The **content** decides the pipeline, not the file name: a symbol package
declares `packageType="SymbolsPackage"` in its own nuspec, so renaming a file cannot route it to a
pipeline its content does not belong to.

| Point | As built |
|---|---|
| Preview | The target ID and version, and the `.pdb` entries found. `IPackageDatabase.FindOrNullAsync(id, version, includeUnlisted: true)` runs at **step one**, so a missing target is reported before the upload is confirmed (B2) |
| Result | Branched on `SymbolIndexingResult`, not on a status code: `PackageNotFound` ⇒ "Symbols can only be uploaded for a package version already on this server. Push {id} {version} first."; `InvalidSymbolPackage` ⇒ a message about entries, non-portable PDBs or PDBs that do not match. The push endpoint reports both as 400, so a status-driven UI would say the wrong thing |
| Ownership | No `OnPushedAsync` on this path, as on the symbol endpoint: a symbol upload claims no ID and grants no ownership. Pinned by a test |

### C5 — My packages (`src/BaGet.Identity/Pages/Account/Packages.cshtml`)

Built. `PackagesModel : AccountPageModel`, `RequiresSignIn => true`, linked from the signed-in nav
(`_Layout.cshtml:41`, between the profile and the API keys).

| Point | As built |
|---|---|
| Source | `Ownership.GetOwnedPackageIdsAsync(userId, userName)`, so organization-owned IDs are listed too. Ownership decides, not the package table: an ID whose every version was hard-deleted keeps its owners and stays listed, with "None" as its latest version |
| Per ID | One `IContext.Packages` projection over every owned ID, giving latest version (highest, listed or not, marked "(unlisted)"), version count and summed downloads. `IContext`, not `IPackageDatabase`: the ownership query the page starts from already needs `IContext`, so a per-ID `FindAsync` loop bought nothing and cost six round trips per ID (it eagerly loads dependencies, package types, target frameworks, deprecation and vulnerabilities to compute three numbers). Highest version is still chosen in memory — SemVer ordering is not SQL's |
| Links | `Manage` for the ID, and "Upload new version" (`/Upload?id=…`) per row |

### C6 — Delete and upload-a-version on `Manage`

Built. `Unlist`/`Relist` are untouched and reachable from the new list.

| Point | As built |
|---|---|
| `OnPostDeleteAsync(string version)` | Read-only ⇒ a message and nothing touched, mirroring the DELETE endpoint → parse the version (404) → `IPackageDeletionService.TryDeletePackageAsync`. No notification here: `PackageDeletionService` notifies the catalog itself |
| Label (B3) | `Unlist` ⇒ "Unlist this version", "Hidden from search and from the version list; anyone who knows the exact version can still restore it." `HardDelete` ⇒ "Delete this version permanently", plus the caveats B3 establishes: the rows and the `.nupkg`/`.nuspec`/readme/icon go, indexed PDBs keep being served, and an external search index keeps the version until reindexed. The button never claims to delete on a server that unlists |
| Confirmation | `onclick="return confirm(…)"` carrying that same label and caption |
| `false` from the service | Reported as "was not in the database", not as "nothing happened": a hard delete removes the files either way |
| Upload a version | `asp-page="/Upload" asp-route-id="@Model.Id"`; `UploadModel.Id` is a bound hint the page shows, never a source of the published ID — that is read from the file |

`src/BaGet.Identity/Pages/Packages/` was matched by `.gitignore`'s `**/packages/*`, so `Manage` had
never been committed. A negation was added and the page is now tracked.

## Verification

| # | Step | Expected |
|---|---|---|
| 1 | `dotnet test BaGet.sln -p:GeneratePackageOnBuild=false` | 928 green (880 before C5/C6, plus 12, plus 36 for the review fixes below) |
| 2 | Stage, preview, submit while signed in | 200 and a published message (a page, not the API's 201), the package served, the token unknown afterwards |
| 3 | Stage as user A, submit with user B's cookie | Rejected; A's file untouched |
| 4 | Submit an ID owned by another user | 403, nothing indexed |
| 5 | Push a brand-new ID through the browser | Succeeds, pusher becomes owner (`OnPushedAsync`) |
| 6 | `Accounts:Enabled=false` with a correct and an incorrect API key | Accepted / 401, and the page never 404s |
| 7 | `IsReadOnlyMode=true` | Both modes refuse |
| 8 | Request a staged file's storage path over HTTP | 404 (B1) |
| 9 | Cancel, then reuse the token | Token unknown |
| 10 | Upload the same version twice | 409 as a readable message, not a stack trace |
| 11 | `.snupkg` for a package that does not exist | The message B2 establishes |
| 12 | Delete a version under each `PackageDeletionBehavior` | Unlisted or hard-deleted accordingly, and the button says which |
| 13 | A real `dotnet nuget push` after a browser upload | Unaffected; the API path must not regress |
| 14 | A signed-in user's "my packages" list | Only the IDs they own; sign-in required |

Steps 2-11 are pinned by `tests/BaGet.Tests/BrowserUploadTests.cs`; step 8 by
`StagingIntegrationTests`, step 13 by the existing push tests, which stayed green, and steps 12 and
14 by `tests/BaGet.Tests/PackageManagementPageTests.cs` (one application per `PackageDeletionBehavior`,
since a test host configuration is fixed when it is built).

## Risks

| Risk | Likelihood | Handling |
|---|---|---|
| Staged files accumulate | **High if unmanaged** | The sweep in C2 is in scope; verification 2 and 9 assert removal |
| Staging reachable as a download | Medium | B1 is blocking; the temp-file fallback is stated above |
| Antiforgery versus large multipart | Medium | B4 before coding |
| Two-step doubles wall time for large packages | Low | Staging writes once; `SubmitAsync` reads from storage, not the network |
| D2 misread as granting superuser | Low | `isSuperuser: false` is explicit and verification 4 pins it |
| API key posted in a form body when accounts are off | Low | Same exposure as the `X-NuGet-ApiKey` header over the same channel; must not be logged |

## Deviations and follow-ups

Found by review after C1-C6 and resolved in the same branch.

| # | Change | Why it matters |
|---|---|---|
| R1 | `AccountAuthenticationService` no longer treats an empty `ApiKey` as "every caller is the superuser" while accounts are on | **Behavioural break.** An accounts-enabled server with no global key now refuses a keyless push (401) instead of accepting it as the superuser. Without it the ownership gate the new pages rely on was bypassable by sending no key at all: `dotnet nuget push` with no key could overwrite an ID someone else owned. Accounts off is unchanged — `ApiKeyAuthenticationService` still leaves an unset key open |
| R2 | Browser upload closed when accounts are off and no `ApiKey` is set | The shipped default. The page was a one-click publish button for any visitor, with a password box that was ignored. `PUT /api/v2/package` is deliberately left as it was, and startup now warns that the server accepts packages from anyone |
| R3 | `IPackageStagingService.RemoveAsync` takes an `ownerId` and returns whether it removed anything | **Interface break** (`BaGet.Core` is packed). `OpenAsync` checked the owner and `RemoveAsync` did not, so a leaked token discarded someone else's pending upload |
| R4 | The staging token is logged at `Debug`; a correlation id is logged above it | The token was the only secret over a staged upload and was written at `Information` on every upload |
| R5 | Every write handler on `Manage` is refused in read-only mode, in one page filter | Only `Delete` checked. `Unlist` — which the page's own caption calls the same operation — still wrote, as did relist, deprecate, undeprecate, add/remove owner and resolve report. The API refuses all of them with 401 |
| R6 | The upload page's push instructions are suppressed in read-only mode | It said nothing can be pushed and then offered copy-paste commands that answer 401 |
| R7 | `IListableStorageService` on all four cloud stores | `SweepAsync` was a no-op on Azure, S3, GCS and Aliyun, so staged uploads accumulated there forever |
| R8 | A `.snupkg` whose nuspec does not declare `SymbolsPackage` is refused (400) | It was published as a package, storing a symbols archive as that ID and version's `.nupkg`, while the symbol endpoint read the identical bytes as symbols |
| R9 | A storage failure while staging is a sentence and a 500 | It was an unhandled exception: a bare 500 in production, a stack trace under the developer exception page |
| R10 | `CryptographicOperations.FixedTimeEquals` for the global API key | Hardening, not a usable attack in this shape; the browser form is a new caller of that comparison |

Known and not changed:

| Point | Why |
|---|---|
| Public surface | `PackageModel.DependencyGroupModel` and `PackageModel.DependencyModel` were nested public types and are now top-level `BaGet.Web.DependencyGroupModel` / `DependencyModel`, shared with the upload preview (C3). `BaGet.Web` is packed, so this breaks a consumer that compiled against the previous package. Accepted: the preview and the package page must describe a package the same way, and a nested display model cannot be shared. Recorded here because it was not called out when it was made |
| No staging quota | An authorized caller can park unbounded bytes under `staging/` until the 24-hour sweep reclaims them, and nothing counts them against the package store. `IStorageService` has no quota, and a per-owner cap would need either a per-owner prefix in the staging layout or one metadata read per staged upload on every stage. Comparable to pushing many distinct IDs, which is equally unbounded. R7 at least makes retention effective on every store; R2 removes the anonymous half of it |

## Out of scope

- Email confirmation, password reset, external login providers.
- Editing package metadata after upload — nuget.org does not allow it either.
- Organization-level upload UI beyond the ownership `GetOwnedPackageIdsAsync` already resolves.
