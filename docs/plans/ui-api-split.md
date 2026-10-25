# Plan: JSON UI API, view rendering only in controllers, Application projects, OpenAPI + Scalar

**Goal.** Every controller action that returns `View(...)` gets a JSON twin under `/api/ui/...` that does the work and returns the model; the
view action calls that API action **in process** and only renders the model. An Angular/React (or any) frontend can then be written against
`/api/ui` alone. Business logic moves out of the UI projects into one Application project per area. OpenAPI documents (built-in
`Microsoft.AspNetCore.OpenApi`) plus a Scalar UI describe `/api/ui`, `/api/v1` and the package protocols, for administrators only.

**Status:** wave 0 `uiapi-infra` done (infrastructure + pilot Legal, Statistics); `openapi` track and waves 1-3 pending.

| Blocking check | Outcome (wave 0) |
|---|---|
| 1 Packages | `openapi` track |
| 2 Forwarded types | Done. `PublicApiSnapshotTests` scans the Application assemblies too and compares lines **without the assembly column** (`public-api.txt` untouched); new `EveryTypeOfTheSnapshotStillResolvesThroughItsOriginalAssembly` checks `Assembly.GetType` through the assembly that listed a type (follows `[TypeForwardedTo]`, nested types included). 34 Identity root types forwarded (`BaGet.Identity/TypeForwards.cs`), `ReadmeMarkdown`, `RequestPrincipalResolver`, `BasicCredentials` (`BaGet.Web.V3/TypeForwards.cs`). Web.V3 workflows and view models are **not** in the pruned snapshot: moving them needs no forwarder (forward anyway what a host may use) |
| 3 Route isolation | Done. API actions carry `[UiApi]` only; `ui-routes.txt` and `ui-forms.txt` unchanged (UiRouteContractTests, UiFormContractTests pass byte-identical) |
| 4 `RequestPrincipalResolver` | Moved with `BasicCredentials` to `BaGet.Identity/Mvc` (namespace `BaGet.Web`, forwarded from Web.V3); Identity cannot reference Web.V3, and Core would need `HttpContext`. `GetApiKey()` stays in Web.V3; the resolver reads `X-NuGet-ApiKey` itself |
| 5 In-process invocation | Done. `UiApiInvoker` creates the twin with `ActivatorUtilities`, shares the view action's `ControllerContext` (same `HttpContext`, `RouteData`, `ModelState`), returns the `ScreenResult<T>` and maps plain results (`NotFound()`, `Challenge()`, `Forbid()`, status codes) to a decision while keeping the original result (`UiApiResponse.Raw`, returned unchanged by `Render`). `[ApiController]` is not used (`UiApiParityTests.AUiApiActionIsNeverALegacyPageAndLivesUnderApiUi`) |

Deviations from the sketch above: JSON enums keep `ApiJson`'s PascalCase names (`"Ok"`, `"NotFound"`; the sketch said camelCase for enums too); `ScreenGate` returns `GateOutcome` and the view and API bases both call it, but `AddBaGetIdentityApplication` registers only the gate (the account services stay in `AddBaGetAccounts`, after its `Enabled` check); `LegalViewModel` (`HtmlString`) stays in Web.V3 for hosts that override the view, the twin's DTO is `LegalScreen` (HTML as text); `SignIn` answers `401` with a `Bearer` challenge (a `Basic` one makes a browser prompt on `fetch`); the two hard assertions "no `*Workflow` in UI projects" and "every `[LegacyPage]` has a twin" are **pending-list** checks (see Verification) until `split-finish`.

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
   **Recorded (wave 0, openapi):** `Microsoft.AspNetCore.OpenApi` 10.0.13 (needs `Microsoft.OpenApi` [2.12.2, 3.0); restores without `NU1605`; 10.0.14 is newer than the 10.0.13 shared framework/EF pins), `Scalar.AspNetCore` 2.17.14 (published 2026-10-06; 2.18.0 was 14 days old, 2.18.2/2.18.3 newer).
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

## How to split a screen (waves 1-3)

Worked example: `LegalController` / `LegalUiApiController` / `LegalUseCase` and `StatisticsController` / `StatisticsUiApiController` / `StatisticsUseCase`; the infrastructure test twins are at the end of `tests/BaGet.Tests/UiApiAccessTests.cs`.

**1. Where things go**

| Piece | Project / folder | Name |
|---|---|---|
| Use case (scoped class, one per area of screens; DI-constructed) | `src/BaGet.Web.Application/UseCases/` or `src/BaGet.Identity.Application/UseCases/` (new) | `<Area>UseCase` (one method per GET and per handler: `GetAsync`, `CreateAsync`, ...) |
| DTO (today's view model; record or class, flat, no EF entity graph) | same project, root or `ViewModels/`, **same namespace as today** (`BaGet.Web`, `BaGet.Identity.ViewModels`) | unchanged name; if it is public and listed in `public-api.txt`, add `[assembly: TypeForwardedTo(typeof(...))]` to `BaGet.Web.V3/TypeForwards.cs` / `BaGet.Identity/TypeForwards.cs` |
| Moved business helpers (snippets, readme, `AccountSupport` rules) | the Application project | namespace kept |
| JSON twin | `src/BaGet.Web.V3/Controllers/Api/<Feature>UiApiController.cs` (browse, format, upload, manage screens) or `src/BaGet.Identity/Controllers/Api/` (account, admin) | `<Feature>UiApiController` |
| View controller | stays where it is, reduced to invoke + render | unchanged |
| Registration | `AddBaGetWebUseCases` (Web) / `AddBaGetAccounts`, **after** the `Enabled` check, for account services (Identity) | `TryAddScoped<<Area>UseCase>()` |

**2. The use case.** Inputs explicit: `ClaimsPrincipal caller`, the form DTO, `CancellationToken`. Returns `ScreenResult<TModel>`: `Ok(model)`; `Invalid(model, errors)` (field -> messages, `""` = the form) for refused input, **validated explicitly** (DataAnnotations through a shared validator; the view's model binding no longer validates); `NotFound()` also for "you may not know it exists"; `GoTo(new ScreenNext("/Page", routeValues, message))` after a successful write (the page name is the `[LegacyPage]` of the next screen; the flash message replaces `TempData`). It sees no `HttpContext`, `IUrlHelper`, `ModelState`, `TempData`, `IActionResult`. Absolute links (emails, snippets) come from `IUrlGenerator`/`EmailLinkPolicy`, never from the request's host. A write calls `MaintenanceGate.ThrowIfClosed` (never catch `MaintenanceException`); a password-confirmed action checks the password in the use case. No secret in a DTO: passwords, `protectedPassword`, connection strings, API key values (except the key just created, once) are `[JsonIgnore]`d or left out; a DTO holding an EF entity is mapped to a flat record.

**3. The twin.**

```csharp
[Route("api/ui")]                                   // or [Route("api/ui/admin")] ...
[RepositoryPage(NotOnVirtual = true)]               // same values as the view controller: adds api/ui/r/{repo}/...
public sealed class StatisticsUiApiController : UiApiControllerBase   // Account/Admin/AccessUiApiControllerBase per area
{
    [HttpGet("stats"), UiApi("/Statistics")]
    public async Task<ActionResult<ScreenResult<StatisticsViewModel>>> Get(CancellationToken ct)
        => Respond(await _useCase.GetAsync(ct));
}
```

- **Base**: `UiApiControllerBase` (public screens), `AccountUiApiControllerBase` (override `RequiresSignIn`/`RequiresEmail`; **cookie only**), `AdminUiApiControllerBase` (administrator; a key needs `admin:api`), `AccessUiApiControllerBase` (override `AuthorizeAsync` for project administrators; writes 503 while read-only). Retention, replication and repository bases: add `...UiApiControllerBase : AccessUiApiControllerBase` (or `AdminUiApiControllerBase`) with the same services as `RetentionControllerBase`/`ReplicationControllerBase`/`RepositoryControllerBase`, mirroring the view base.
- **Routes**: GET screen `GET api/ui/<view path>`; default POST `POST api/ui/<path>`; named handler `X` = `POST|GET api/ui/<path>/<x-kebab-case>` with `[UiApi("/Page", Handler = "X")]`; route values keep the view's names (`{id}`, `{name}`, `{version}`). Mark `[RepositoryPage(...)]` exactly like the view controller. Never `[LegacyPage]`, never `[ApiController]`, never `[AutoValidateAntiforgeryToken]` (the security filter does it).
- **Bodies**: `[FromBody] <Form>` (JSON); file uploads `[FromForm] IFormFile` + fields (multipart). Public helper methods are `[NonAction]` or non-public. Answer only through `Respond(...)` (Ok and Next 200, Invalid 400, NotFound 404, Forbidden 403, SignIn 401; maintenance 503 by the middleware).
- Browser-only steps (external login challenge and callback, a download that is already a plain URL) get no twin: list them for `docs/ui-api.md`.

**4. The view controller.** Keep every attribute (`[Route]`, `[PageHandler]`, `[LegacyPage]`, `[RepositoryPage]`, `[RequiresSignIn]`, ...) and the base. Replace the body by

```csharp
=> Render(await Api<StatisticsUiApiController, StatisticsViewModel>(api => api.Get(ct)), "Index" /*, model => viewModel */);
```

`Render`: a plain result of the twin is returned unchanged; Ok and Invalid -> `View(view, model)` (errors copied to `ModelState`, `StatusCode` applied); NotFound -> 404; SignIn -> challenge; Next -> flash + `RedirectToPageResult(screen, values)`. For a POST the controller still binds the form (`[FromForm]`) and passes the DTO to the twin: `Api<..>(api => api.Create(form, ct))`. A view controller resolves **no** Application service. The base class gate (`AccountControllerBase` ... `AccessControllerBase`) already ran `ScreenGate`, and the antiforgery filter of `UiControllerBase` the token check; do not repeat them. Views keep their `@model`; if the DTO differs from the old view model, map it in the `viewModel` argument (a host's view override keeps compiling). `Flash(...)` and `[TempData]` of the bases keep working for screens not split yet.

**5. Tests (per screen; before the code where behaviour is new).**

1. `UiApiParityTests`: a case with the same data through the page and `GET /api/ui/...` (the model fields the page prints), plus the status pairs: anonymous (page redirect <-> 401), non-admin (404 <-> 404), unknown repository (404 <-> JSON 404), read-only on a write (503 <-> 503). Writes: page POST and JSON POST give the same state.
2. `UiApiAccessTests`: a case per caller kind for the screen's base: anonymous / user / admin / API key / `admin:api` key / key limited to repositories / cookie write without the antiforgery header (400) / key write without it (allowed) / account screens with a key (403).
3. A response never contains `password`, `protectedPassword`, a connection string or another user's key value (`UiApiAccessTests.NoResponseEchoesTheCredentialsOfTheRequest` pattern).
4. Regenerate the snapshot: `BAGET_UPDATE_CONTRACTS=1 dotnet test ... --filter-class "*UiApiContractTests"` (`Contracts/ui-api.txt`; **never hand-merge it**, regenerate after a merge). Delete the screen's lines from `Contracts/ui-api-pending-screens.txt` and each removed `*Workflow` from `ui-api-pending-workflows.txt`.
5. Must stay green and byte-identical: `UiRouteContractTests`, `UiFormContractTests`, `PublicApiSnapshotTests`, `UiCoverageInventoryTests` (twins need no `ui-coverage.md` row), `ApplicationLayeringTests` (the Application assemblies reference no `Microsoft.AspNetCore.Mvc*`/`Razor*`).

**6. Gotchas.** The twin's filters do not run in process: anything an `OnActionExecuting` of the twin's base does must also be in the view base (the `ScreenGate` is shared for that reason). `HttpContext.User` of a key caller is the key owner (set by `UiApiSecurityFilter`); `UiApiCaller.Key(HttpContext)` is the key's `BaGetPrincipal` (scopes, repository limit) when a service must honour them. A new public Application type that a host could have used keeps its name and namespace. Docs of a track: its in-code XML docs, the area's README row, one sentence in this plan's Status; `docs/ui-api.md` and the CLAUDE.md paragraph belong to `split-finish`.

## Verification

- Build 0 warnings; `ui-routes.txt`, `ui-forms.txt` byte-identical; `public-api.txt` superset; new `ui-api.txt` reviewed.
- Pending lists (`Contracts/ui-api-pending-screens.txt`, `ui-api-pending-workflows.txt`): a check is **one-directional** (what is pending now must be listed), so a track only deletes the lines of what it moved and parallel tracks never conflict; `split-finish` empties both files and replaces the checks by "nothing pending".
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
