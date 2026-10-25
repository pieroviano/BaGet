# Plan: JSON UI API, view rendering only in controllers, Application projects, OpenAPI + Scalar

**Goal.** Every controller action that returns `View(...)` gets a JSON twin under `/api/ui/...` that does the work and returns the model; the
view action calls that API action **in process** and only renders the model. An Angular/React (or any) frontend can then be written against
`/api/ui` alone. Business logic moves out of the UI projects into one Application project per area. OpenAPI documents (built-in
`Microsoft.AspNetCore.OpenApi`) plus a Scalar UI describe `/api/ui`, `/api/v1` and the package protocols, for administrators only.

**Status:** not started.

## Decisions (user)

| # | Decision | Consequence |
|---|---|---|
| D1 | View action **invokes the API controller action in process** | `UiApiInvoker` creates the API controller with the view action's `ControllerContext` (same `HttpContext`, `RouteData`, `ModelState`) and calls the action method; no HTTP hop. The API action's own filters do **not** run in process, so the view controller base must run the same gates first (shared `ScreenGate`, see Target shape). |
| D2 | `/api/ui` auth = **cookie + API keys** | Cookie callers: antiforgery header (`RequestVerificationToken`) on unsafe methods, token from `GET /api/ui/antiforgery`. Key callers (Basic password / Bearer / `X-NuGet-ApiKey`, via `RequestPrincipalResolver`): no antiforgery; act as the key's owner within its scopes and repository limits; admin screens need `admin:api` (as `/api/v1`); account self-service (password, 2FA, external logins, API keys, sign-in/out, deletion) is **cookie only** (a key never changes credentials). |
| D3 | Projects **per area** | New `BaGet.Identity.Application` (under `BaGet.Identity`) and `BaGet.Web.Application` (under `BaGet.Web.V3`). Application projects hold use cases, DTOs (today's view models) and business services; they reference no `Microsoft.AspNetCore.Mvc*`/Razor assembly (test-enforced). UI projects keep controllers (view + API), views, MVC infrastructure. |
| D4 | **Parallel workflow** (waves of Sonnet worktree tracks, Opus review per wave, final review/fix loop, max 5 agents, one full test run at a time) | See Waves. |
| D5 | OpenAPI = **`Microsoft.AspNetCore.OpenApi`** | Documents `ui`, `v1`, `protocols` at `/openapi/{doc}.json`; `/api/v1/openapi.json` stays as an alias of `v1`. `ApiCatalog` keeps feeding the `/admin/api` page; its JSON generator (`ApiCatalog.cs:125-171`) is retired. |
| D6 | Coverage = **`/api/ui`, `/api/v1`, package protocols** | Protocol controllers are conventionally routed (`BaGetEndpointBuilder.MapRoute`), which ApiExplorer ignores: the `protocols` document is built by a document transformer from `EndpointDataSource` (`RepositoryRouteMetadata`, template, methods, route parameters; plain and `r/{repo}` variants). |
| D7 | Scalar + OpenAPI JSON for **administrators only** | Signed-in server administrator (cookie) or an administrator's `admin:api` key; anyone else 404. Accounts off: only the global `ApiKey` (when configured), else 404. `/api/v1/openapi.json` already required an admin key: unchanged for keys, now also open to an admin cookie. |

Non-breaking (unchanged from the MVC migration): URLs, forms, `?handler=`, `asp-page` names, redirects and status codes of every screen.
`tests/BaGet.Tests/Contracts/ui-routes.txt` and `ui-forms.txt` stay **byte-identical**; `public-api.txt` stays a superset (types moved to an
Application assembly keep name and namespace and get `[TypeForwardedTo]` in the old assembly).

## Blocking verification (wave 0, before coding)

1. **Packages.** Pick `Microsoft.AspNetCore.OpenApi` 10.0.x (patch matching the shared framework; restores beside EF 10.0.13 / Extensions 10.0.13 without `NU1605`) and `Scalar.AspNetCore` latest stable 2.x released ≥ 2 weeks ago; add levers `MicrosoftAspNetCoreOpenApiPackageVersion`, `ScalarAspNetCorePackageVersion` in `src/Directory.Build.props` (next to `OpenTelemetryPackageVersion`, line 72). Record versions here.
2. **Forwarded types in the snapshot.** `PublicApiSnapshotTests` (`tests/BaGet.Tests/Contracts/PublicApiSnapshotTests.cs:24-27`) reads exported types per assembly; confirm a type moved with `[TypeForwardedTo]` still counts (add `Assembly.GetForwardedTypes()` / include the Application assemblies, keyed by namespace + name, not assembly). Must pass before any type moves.
3. **Route snapshot isolation.** `UiRoutes` (contracts) selects endpoints by `CompiledPageActionDescriptor` / `LegacyPageAttribute`: API actions must carry **no** `[LegacyPage]` (they use `[UiApi(screen)]`), so `ui-routes.txt` is untouched. Verify with the pilot.
4. **`RequestPrincipalResolver` placement.** It lives in `BaGet.Web.V3` (ctor deps `IAuthenticationService` (Core), `ISessionPrincipalFactory` (Core)); Identity's API controllers need it: move it to `BaGet.Identity` (`Mvc/`, namespace kept + `[TypeForwardedTo]`) or expose through Core. Decide in wave 0.
5. **In-process invocation.** Prove on the pilot that `UiApiInvoker` returns the API action's value, maps non-object results (404, Forbid, Challenge, status codes) unchanged, and shares `ModelState`; and that `[ApiController]` is **not** used on `/api/ui` controllers (its automatic 400 would differ between HTTP and in-process calls; validation is explicit in the action).

## Current state (what changes)

| Where | Today | Change |
|---|---|---|
| `src/BaGet.Identity/Workflows/*` (34), `src/BaGet.Web.V3/Workflows/*` (9) | Screen logic, `internal`, built per request with `HttpContext` + `IUrlHelper` (`AdminWorkflow.cs:25`), return `IActionResult?` (null = render), write `ModelState`/`TempData` | Become **use cases** in the Application project: DI-constructed, inputs explicit (`ClaimsPrincipal caller`, form DTO, `CancellationToken`), return `ScreenResult<TModel>`; no `IActionResult`, `HttpContext`, `IUrlHelper`, `ModelState`, `TempData` |
| `src/BaGet.Identity/ViewModels/*` (40), `src/BaGet.Web.V3/ViewModels/*` (10) | Public view models | Move to the Application project = the JSON DTOs (`[TypeForwardedTo]` left behind). Members that only views need (display helpers) stay as view-side extension methods |
| `src/BaGet.Identity/*.cs` root services (`AccountApprovalService`, `AccountDeletionService`, `AdminUserCreationService`, `AdministratorService`, `ApiKeyService`, `PackageOwnershipService`, `NpmTokenService`, `OwnershipAuthorizationService`, `AccountAuthenticationService`, `SessionPrincipalFactory`, `MaintenanceAwareSignInManager`, `PasswordResetLinkSender`, rate limiters, email senders, `AvatarService`, `ManagePermission`, `ScanAccess`, `RepositoryAccessRow`) | Business services in the UI assembly | Move to `BaGet.Identity.Application` (namespaces kept, forwarded). `AddBaGetAccounts` (`AccountsServiceCollectionExtensions.cs:20`) stays the public entry point in `BaGet.Identity`, calling `AddBaGetIdentityApplication` |
| `src/BaGet.Web.V3/` `InstallSnippets`, `NpmInstallSnippets`, `MavenSnippets`, `DockerSnippets`, `ReadmeMarkdown`, `PackageDependencies`, `PackageFrameworks`, `LegalContent`, `ScanFindingsView`, `UploadAvailability` | Presentation data computed for views | Move to `BaGet.Web.Application` (the DTOs carry install snippets, rendered readme HTML, legal text, so a SPA needs no server-side templating) |
| `src/BaGet.Identity/Mvc/*` bases (`AccountControllerBase`, `AdminControllerBase`, `AccessControllerBase`, ...) | Gates in `OnActionExecuting` | Gate logic extracted to `ScreenGate` (Application: returns `GateOutcome` = Allowed / NotFound / SignIn / Forbidden / AccessUnavailable / AccountsOff) used by view bases (redirect/404 as today) and new API bases (JSON 401/403/404) in the same order |
| `src/BaGet.Web.V3/RepositoryActionRouteConvention.cs:12-21` | `r/{repo}/` twin for `[RepositoryPage]` actions | For templates starting `api/ui/`, the twin is `api/ui/r/{repo}/<rest>`; `RepositoryActionFilter` answers API actions with JSON 401/404 instead of the sign-in redirect |
| `src/BaGet.Web.V3/Api/ApiCatalog.cs:125-171`, `Api/OpenApiController.cs:12-27` | In-house OpenAPI for `/api/v1` | Replaced by `MapOpenApi`; `/api/v1/openapi.json` alias; `ApiCatalog` endpoint list stays for `/admin/api` |
| `tests/BaGet.Tests/UiCoverageInventoryTests.cs:76-80` | Every controller needs a `docs/ui-coverage.md` row | `[UiApi]` actions are covered through their screen (no new rows); the test also requires every `[LegacyPage]` view action to have its `[UiApi]` twin |

## Target shape

**Projects and references.** `BaGet.Identity.Application` -> Core. `BaGet.Identity` -> Identity.Application. `BaGet.Web.Application` -> Core,
Catalog, Validation, Identity.Application. `BaGet.Web.V3` -> Web.Application, Identity (+ existing). Both new projects:
`Microsoft.NET.Sdk` + `FrameworkReference Microsoft.AspNetCore.App` (Identity, data protection), `README.md` (package readme, absolute links),
signed, packable as `Extension.BaGet.Identity.Application` / `Extension.BaGet.Web.Application`, `InternalsVisibleTo` `BaGet.Tests` (+ the UI
project) with `Key="$(BaGetPublicKey)"`, in `BaGet.sln` (`src` folder), referenced by `src/BaGet/BaGet.csproj` transitively. No controllers
there, so `AddBaGetApplicationParts` is unchanged.

**Result type** (Application, public): `ScreenResult<T>` = `Status` (Ok, Invalid, NotFound, Forbidden, SignIn, Next), `Model` (T), `Errors`
(field -> messages, `""` = form-level), `Next` (`Screen` = legacy page name, `RouteValues`, `Message` flash), `StatusCode` (when a screen
renders with a non-200 code, e.g. Upload). JSON: `ApiJson.Options` (camelCase, enums by name).

**API controller** (UI project, `Controllers/Api/<Feature>UiApiController.cs`): `[Route("api/ui/<screen path>")]`, `[UiApi("/Admin/Users")]`
on each action (the screen), base `UiApiControllerBase` (+ `AccountUiApiControllerBase`, `AdminUiApiControllerBase`, ... mirroring the view
bases through `ScreenGate`), returns `ActionResult<ScreenResult<T>>`. HTTP mapping: Ok/Next 200, Invalid 400 (body still has model + errors),
NotFound 404, Forbidden 403, SignIn 401 (`WWW-Authenticate`), maintenance 503 (middleware). Routes: GET screen = `GET /api/ui/<path>`;
`OnPost` = `POST /api/ui/<path>`; named handler `X` = `POST|GET /api/ui/<path>/<x-in-kebab-case>`; repository screens also at
`/api/ui/r/{repo}/<path>`. Bodies: JSON (`[FromBody]`); file uploads multipart (`[FromForm]`). Browser-only steps (external login challenge
and callback, file downloads that are already plain URLs) stay view-only and are listed in `docs/ui-api.md`.

**View controller**: calls `await Api<UsersUiApiController, UsersViewModel>(c => c.Get(...))` (`UiApiInvoker` in `BaGet.Identity/Mvc`),
then `Render(result, "Index")`: Ok/Invalid -> `View(model)` (errors copied into `ModelState`, status code set), NotFound -> `NotFound()`,
SignIn -> challenge (today's redirect), Forbidden -> today's answer, Next -> `TempData` message + `RedirectToPage(next.Screen, next.RouteValues)`
(legacy mapping, repository kept). No business logic in view controllers (test: view controllers resolve no Application service directly).

**OpenAPI + Scalar** (host side, `BaGet.Web.V3/OpenApi/`): `AddOpenApi("ui")`, `AddOpenApi("v1")`, `AddOpenApi("protocols")` with
`ShouldInclude` by route prefix; security schemes cookie, Basic, Bearer, `X-NuGet-ApiKey`; transformer for `protocols` (D6); `MapOpenApi("/openapi/{documentName}.json")`
+ alias `/api/v1/openapi.json`; `MapScalarApiReference("/scalar")` listing the three documents; `OpenApiAccessMiddleware`/endpoint filter = D7.
Linked from `/admin/api` and the admin navigation. Mapped by a new `BaGetEndpointBuilder.MapApiDocumentation` (called from `MapEndpoints`).

## Waves and tracks

| Wave | Track | Scope |
|---|---|---|
| 0 | `uiapi-infra` | Blocking checks 2-5; both Application projects (empty-ish, wired, packable, in sln/host); move Identity root services (table above) with forwarding; `ScreenResult<T>`, `ScreenGate`, `UiApiControllerBase` family, `UiApiInvoker`, `[UiApi]`, `/api/ui` authentication + antiforgery + `GET /api/ui/antiforgery`, route convention for `api/ui`; tests `ApplicationLayeringTests`, `UiApiParityTests`, `UiApiAccessTests` (skeleton), `UiApiContractTests` (new snapshot `Contracts/ui-api.txt`); **pilot**: Legal (`/terms`, `/privacy`) and Statistics end to end; a "How to split a screen" section in this plan |
| 0 | `openapi` | Blocking check 1; D5-D7: packages, three documents, protocols transformer, Scalar at `/scalar`, admin-only gate, `/api/v1/openapi.json` alias, retire the in-house generator (keep `ApiCatalog` for `/admin/api`), link from `/admin/api`; tests `OpenApiDocumentTests` (valid JSON, every `/api/v1` and `/api/ui` action and every protocol route present, admin cookie/key 200, anonymous/user/limited key 404) |
| 1 | `split-browse` | Index (+`/packages`), Package, Statistics (if not pilot), ReportAbuse, Admin/Api page |
| 1 | `split-formats` | Upload (multipart), NpmPackages, NpmPackage, NpmUpload, MavenArtifacts, MavenArtifact, MavenUpload, DockerImages, DockerImage |
| 1 | `split-account` | Account/* screens, Profile; cookie-only rule (D2); login sets the cookie through the API too; external login stays view-only |
| 1 | `split-manage` | Packages/Manage, NpmManage, MavenManage, DockerManage, ContactOwners, Scan, ValidationStatus |
| 1 | `split-admin-core` | Users, UserDetail, DeleteUser, UserPackages, Packages, Organizations, OrganizationDetail, Settings (+ CopyStatus, TestTelemetry), Cluster, Formats, Operations, Validation, Abuse, AccessUnavailable |
| 2 | `split-admin-access` | Groups, GroupDetail, Permissions, PermissionDetail, Projects, ProjectDetail, Audit (project-admin by route value) |
| 2 | `split-admin-repos` | Repositories, RepositoryEdit (all handlers), RepositoryDelete |
| 2 | `split-admin-policies` | Retention, RetentionPolicy, RetentionHolds, Scanning, ScanRepository, ScanProject, Replication, ReplicationConfig |
| 3 | `split-finish` | No workflow left in UI projects; view controllers contain no logic; `docs/ui-api.md` (new page: auth, antiforgery, routes, `ScreenResult`, error mapping, browser-only steps, OpenAPI/Scalar; in `BaGet.sln` docs folder, `docs/index.md`, `mkdocs.yml` nav); CLAUDE.md architecture + "UI API" paragraph, READMEs (four projects), `docs/admin-api.md` (OpenAPI/Scalar), `docs/ui-coverage.md` note, devops upgrade note (assemblies split, forwarded types); plan Status "implemented" |

Every track: move its workflows to use cases + DTOs in the Application project, add the API controller, reduce the view controller to
invoke + render, keep every gate, extend `UiApiAccessTests` (anonymous / user / admin / key / limited key / no antiforgery) and the parity
test for its screens, update docs of what it changes.

## Verification

- Build 0 warnings; `ui-routes.txt`, `ui-forms.txt` byte-identical; `public-api.txt` superset; new `ui-api.txt` reviewed.
- `UiApiParityTests`: every `[LegacyPage]` view action (per handler) has a `[UiApi]` twin; for each screen address of the ui-forms harness and
  each caller, the JSON of `GET /api/ui/...` equals the model the view rendered (captured by a test-only result filter) and the statuses
  correspond (view redirect to sign-in <-> 401, 404 <-> 404).
- `ApplicationLayeringTests`: the Application assemblies reference no `Microsoft.AspNetCore.Mvc*` / `Razor*` assembly; no type in UI projects
  named `*Workflow`.
- `UiApiAccessTests`: cookie write without antiforgery header = 400; key write without header = allowed; key on account self-service = 401/403;
  admin screens with a non-`admin:api` key = 403; repository-limited key outside its repositories = 404; gate closed = 503.
- `OpenApiDocumentTests` (above). Full suite once per integration (one `BaGet.Tests` process at a time, ~40 GB).

## Risks

| Risk | Mitigation |
|---|---|
| In-process call skips the API action's filters | View bases run the same `ScreenGate` first; `UiApiAccessTests` cover the HTTP path; parity test covers the in-process path |
| JSON exposes data a view never showed (DTO members used only server-side, e.g. secrets in remote settings) | DTO review per track: `[JsonIgnore]` on anything not rendered; remote passwords are never in a model today (`RemoteSettingsCodec`); `UiApiAccessTests` asserts no `password`/`protectedPassword`/`apiKey` value in any response |
| CSRF through `/api/ui` with the cookie | Antiforgery header required for cookie writes; CORS not enabled (same origin only) |
| Moving public types breaks hosts | `[TypeForwardedTo]` + same namespace; snapshot check 2 |
| `ModelState` semantics differ (view binding validates, API body binding validates) | Use cases validate explicitly (DataAnnotations through a shared validator); view copies `Errors` back |
| Large DTO graphs (EF entities) serialised | DTOs must not expose EF entities with navigation cycles; tracks map to flat records where a view model holds entities |
| Parallel tracks editing shared files (CLAUDE.md, sln, `docs/ui-coverage.md`) | Integrator resolves; tracks append one sentence each |
