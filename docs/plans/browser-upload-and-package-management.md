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
| Token | 128 bits from `RandomNumberGenerator`, hex encoded (`StoragePackageStagingService.CreateToken`) — a token reaches storage, so `OpenAsync`/`RemoveAsync` refuse anything that is not 32 lowercase hex characters before it becomes a path |
| Owner | Recorded in `staging/{token}.json`; `OpenAsync` compares it ordinally and answers null otherwise, so one user cannot confirm another's upload. With accounts off every upload shares the empty owner, as the API-key path has no user either |
| Paths | `staging/{token}.json` (metadata, written first so the sweep can always find the upload) and `staging/{token}.nupkg` |
| Null store | `Storage:Type=Null` reads back nothing, so a staged upload reports itself as an unknown token instead of yielding a null stream |
| Sweep | `SweepAsync` removes every upload staged before a cutoff; `StagingSweepWorker` (`src/BaGet.Web/`) runs it hourly with a 24-hour cutoff |
| Listing | The sweep needs to enumerate a prefix, which `IStorageService` cannot. New optional capability `IListableStorageService`, implemented by `FileStorageService` and `NullStorageService`. A store that does not implement it logs a warning and is **not** swept — the cloud providers (Azure, S3, GCS, Aliyun) still need it |

### C3 — Upload page (`src/BaGet.Web/Pages/Upload.cshtml`)

Keep the existing CLI tabs (`Upload.cshtml:64-104`) below the form. Add `Upload.cshtml.cs`:

| Handler | Does |
|---|---|
| `OnPostStageAsync(IFormFile package)` | Stage, read metadata with `PackageArchiveReader`, render the preview |
| `OnPostSubmitAsync(string token)` | Re-open, authorize `PackageAction.Push` on the ID read from the package, `SubmitAsync`, `OnPushedAsync`, then `RemoveAsync` |
| `OnPostCancelAsync(string token)` | `RemoveAsync`, return to step 1 |

Preview shows id, version, description, authors, license, dependencies by framework, and the readme.
Submit stays disabled until the terms checkbox is ticked, matching nuget.org's shape.

Mode selection, per D3:

```csharp
// Accounts off: there is no user to own the package, so the pusher proves itself with an API
// key, exactly as the PUT endpoint requires. Accounts on: the signed-in session is the identity.
var principal = _accounts.Value.Enabled
    ? _sessionPrincipals.Create(User)
    : await _authentication.AuthenticatePrincipalAsync(ApiKey, cancellationToken);
```

`IsReadOnlyMode` must short-circuit both modes, as `PackagePublishController.cs:46` does.

### C4 — Symbols

The same two-step page accepting `.snupkg`, branching on the extension to `ISymbolIndexingService`.
Symbols carry no standalone metadata, so step 2 shows the target package and the PDBs found.

### C5 — My packages (`src/BaGet.Identity/Pages/Account/Packages.cshtml`)

An `AccountPageModel` with `RequiresSignIn => true`, listing `GetOwnedPackageIdsAsync` with each ID's
latest version, version count and downloads, linking to `Manage`. Add `~/account/packages` to the
signed-in nav at `_Layout.cshtml:40-42`.

### C6 — Delete and upload-a-version on `Manage`

- `OnPostDeleteAsync(string version)` calling `IPackageDeletionService.TryDeletePackageAsync`, the
  button labelled from `PackageDeletionBehavior` (B3), behind a confirmation.
- An "Upload new version" link to the upload page carrying the ID.

## Verification

| # | Step | Expected |
|---|---|---|
| 1 | `dotnet test BaGet.sln -p:GeneratePackageOnBuild=false` | The 836 existing tests still green |
| 2 | Stage, preview, submit while signed in | 201, package listed, staged file gone |
| 3 | Stage as user A, submit with user B's cookie | Rejected; A's file untouched |
| 4 | Submit an ID owned by another user | 403, nothing indexed |
| 5 | Push a brand-new ID through the browser | Succeeds, pusher becomes owner (`OnPushedAsync`) |
| 6 | `Accounts:Enabled=false` with a correct and an incorrect API key | Accepted / 401, and the page never 404s |
| 7 | `IsReadOnlyMode=true` | Both modes refuse |
| 8 | Request a staged file's storage path over HTTP | 404 (B1) |
| 9 | Cancel, then reuse the token | Token unknown |
| 10 | Upload the same version twice | 409 as a readable message, not a stack trace |
| 11 | `.snupkg` for a package that does not exist | The message B2 establishes |
| 12 | Delete a version under each `PackageDeletionBehavior` | Unlisted or hard-deleted accordingly |
| 13 | A real `dotnet nuget push` after a browser upload | Unaffected; the API path must not regress |

## Risks

| Risk | Likelihood | Handling |
|---|---|---|
| Staged files accumulate | **High if unmanaged** | The sweep in C2 is in scope; verification 2 and 9 assert removal |
| Staging reachable as a download | Medium | B1 is blocking; the temp-file fallback is stated above |
| Antiforgery versus large multipart | Medium | B4 before coding |
| Two-step doubles wall time for large packages | Low | Staging writes once; `SubmitAsync` reads from storage, not the network |
| D2 misread as granting superuser | Low | `isSuperuser: false` is explicit and verification 4 pins it |
| API key posted in a form body when accounts are off | Low | Same exposure as the `X-NuGet-ApiKey` header over the same channel; must not be logged |

## Out of scope

- Email confirmation, password reset, external login providers.
- Editing package metadata after upload — nuget.org does not allow it either.
- Organization-level upload UI beyond the ownership `GetOwnedPackageIdsAsync` already resolves.
