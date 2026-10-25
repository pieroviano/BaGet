# Plan: Razor Pages → MVC (controllers + views), no breaking changes

Status: **wave 0 done: mvc-infra (21c457c..HEAD of main, merged ff)**; **wave 1 done: mvc-browse, mvc-formats-ui, mvc-account, mvc-manage, mvc-admin-core (6cc7b61..HEAD of main, merged ff; full run 3655 tests, only the 11 known Telemetry failures)**; waves 2-3 planned. Execution: parallel tracks in git worktrees (`wt/<slug>`), one wave at a time, rebase +
`merge --ff-only` per track, max 5 agents at once (Sonnet implements, Opus reviews).

### Wave 0 results

| Check | Result |
|---|---|
| 1. `@addTagHelper *, BaGet.Web` | **Was broken**: `NavLinkTagHelper` did not apply (no `active` class on any header link; build and pages gave no error). Fixed to `BaGet.Web.V3` in its own commit, `NavLinkRenderingTests` |
| 2. Contract snapshots | Committed on the unmigrated pages (`437fb8b`): `tests/BaGet.Tests/Contracts/{ui-routes,ui-forms,public-api}.txt` (LF, `.gitattributes`), tests `UiRouteContractTests`, `UiFormContractTests`, `PublicApiSnapshotTests`. Regenerate only with `BAGET_UPDATE_CONTRACTS=1` and only for a reviewed contract change |
| 3. Razor Pages behaviours | Verified (tests): unknown `?handler=` renders the page with its default handler (`/stats?handler=NoSuchHandler` = 200; a named handler of another HTTP method does not hide the default); HEAD is answered by the GET handler; a POST without antiforgery token is 400 for **every** form of every page (`EveryPostWithoutAnAntiforgeryTokenIsRefused`, 3 callers). Page-model `OnPageHandlerExecuting` overrides run before the global `RepositoryPageFilter` (the model filter has order `int.MinValue`); controllers behave the same, so the bases override `OnActionExecuting`/`OnActionExecutionAsync` |

What the snapshots record: **ui-routes** = per page name: lower-cased route template (twins, `/packages` alias, catch-alls, constraints), route order, `METHOD:handler` list (HEAD folded into GET; a controller action reports the same lines through `[LegacyPage]` + `[PageHandler]`).
**ui-forms** = for `anonymous`/`user`/`admin` and ~190 addresses (every page, plus `/r/{repo}/` in a repository of each format, a remote, a virtual, a private and an unknown one): status, redirect target, `<title>`, every `<form>` (method, action with `?handler=`, field names/types, antiforgery token), every link to a BaGet page. Texts, hidden values, tokens, dates, ids are not recorded.
**public-api** = one line per public type, base type, interface and public/protected member of `BaGet.Web.V3` and `BaGet.Identity`; a migration may add lines, never remove or change one.

### Wave 1 results: mvc-browse

| Item | Note |
|---|---|
| Migrated | Index (+`/packages`), Package, Statistics, ReportAbuse, Admin/Api -> `HomeController`, `PackageController`, `StatisticsController`, `ReportAbuseController`, `ApiCatalogController`; views in `Views/<Controller>/` |
| Workflows are **public** | Plan said `internal`: a controller's public constructor cannot take an internal type (CS0051) and MVC needs it public |
| Multi-route action | Link generation breaks ties by template text, so `Url.Page("/Index")` gave `/index`, not the old `/packages`: `[RouteNotForLinks("", "index")]` (+ `RouteNotForLinksConvention`, before the twin convention) |
| Namespace trap | `Views/Package`, `Views/ApiCatalog` generate `BaGet.Web.Package`/`.ApiCatalog` (hides the types): per-view `@namespace` |
| Relative page names | `asp-page="Package"`, `Url.Page("Index")` became absolute (`/Package`, `/Index`): relative names need a Razor Page context |
| ReportAbuse | GET takes route values only (a bound model would show "required" errors on a plain GET); POST binds `ReportAbuseViewModel` |
| Tests | `IndexModelFacts`/`PackageModelFacts` -> `IndexWorkflowFacts`/`PackageWorkflowFacts` + one shim smoke test each |

### Wave 1, track mvc-manage (done): `Packages/*`

| Point | Result |
|---|---|
| Workflows | `internal sealed *Workflow` per screen, built with `new` from the `HttpContext` (not DI-scoped): `BaGet.Identity/Workflows`, base `PackageWorkflowBase` (`Show` marker = "render the screen") |
| Naming | the validation screen's controller is `ValidationStatusPageController` (`ValidationStatusController` is the validation protocol controller in `BaGet.Validation`; one controller name for both confuses the coverage audit and view lookup) |
| View models | public, `internal set`, `ViewModels/`; the views keep their `Model.X` names |
| Check order | the manage screens' ownership/format/permission check runs in the controller's `OnActionExecutionAsync` (before `RepositoryActionFilter`); ids come from route values |
| Forms | an MVC view has no ambient page, so `asp-page-handler` forms also carry `asp-page="/Packages/X"` |
| Stray POST | a manage controller has a `NoHandler` fallback (`[HttpPost, PageHandler]`, 404) **without** `[LegacyPage]` (so `ui-routes.txt` is unchanged): else a `<form method=post>` with no `?handler=` is 404 at routing, before the antiforgery 400 |

### Wave 2, track mvc-admin-access (done): Groups, GroupDetail, Permissions, PermissionDetail, Projects, ProjectDetail, Audit

| Point | Result |
|---|---|
| Controllers | `AdminGroups/Permissions/Projects/AuditController : AccessControllerBase`; workflows `GroupsWorkflow`, `GroupDetailWorkflow`, `PermissionsWorkflow`, `PermissionDetailWorkflow`, `ProjectsWorkflow`, `ProjectDetailWorkflow`, `AuditWorkflow` over `AccessWorkflow` (caller flags `IsServerAdministrator`/`CurrentUserId` set by the base class that ran first) |
| Project-admin rule | `AccessAuthorization` (shared by controllers and shims); the controller's `AuthorizeAsync` chooses by the action's `[LegacyPage]` (detail screens read the `name` route value) |
| Messages | `Flash(model)` (`AccessViewModel : IFlashModel`); the `[TempData] Message/Error` of `AccessControllerBase` stay unused (their key prefix differs) |
| Stray POST | detail screens get an `[ImplicitPageHandler]` POST that renders the screen (404 for an unknown name; the page rendered an empty one) |
| `AccessPageModel` | unchanged (Retention/Replication/Scanning pages still derive from it) |

### How to migrate a page (for tracks of waves 1-3)

1. **Logic first.** Move the page model's logic into `internal sealed class <Feature>Workflow` (scoped) so the controller and the `[Obsolete]` shim of the page model both call it. Keep the page model class (name, namespace, public constructor, properties, handler methods) as the shim; mark it and its `*Model` helpers `[Obsolete("... removed in the next major version")]`. Delete the `.cshtml` (`@page`).
2. **Controller.** `public sealed class AdminUsersController : AdminControllerBase` (pick the base from `Mvc/`: `UiControllerBase` for public screens, `AccountControllerBase`, `AdminControllerBase`, `AccessControllerBase`, `RepositoryControllerBase`, `RetentionControllerBase`, `ReplicationControllerBase`), in `BaGet.Identity/Controllers` or `BaGet.Web.V3/Controllers`. Put `[Route("same/template/as/@page")]` on the action (or controller), `[LegacyPage("/Admin/Users")]` (the exact old view engine path) on every action of that page, `[RepositoryPage(...)]` for pages of `RepositoryPageRouteConvention.RepositoryPages` (Format, NotOnVirtual, NotOnRemote, Upload, FrontPage from the old lists in `RepositoryPageFilter`/`PageFormats`).
3. **Actions.** `OnGet` = `[HttpGet, HttpHead, PageHandler]`; `OnGetX` = `[HttpGet, PageHandler("X")]`; `OnPost` = `[HttpPost, PageHandler]`; `OnPostX` = `[HttpPost, PageHandler("X")]`. The default handler of a page that has named handlers **must** carry `[PageHandler]`. `[BindProperty]` becomes `[FromForm]`/`[FromQuery]` parameters (a `[BindProperty(SupportsGet = true)]` is a `[FromRoute]`/`[FromQuery]` parameter). Public helper methods of a controller base or controller must be `[NonAction]` or static/protected, else they become routable.
4. **Views.** `git mv Pages/X.cshtml Views/<Controller>/<Action>.cshtml`; `@page`/`@model XModel` becomes `@model <ViewModel>` (view models in `ViewModels/`, public if a partial uses them); `asp-page` and `asp-page-handler` may stay (the legacy page mapping resolves them to the action, same URL and `?handler=`) or become `asp-controller`/`asp-action` with `asp-route-handler`. `return Page()` becomes `return View("<Name>", model)`; `[TempData]` properties stay properties of the controller. Partials are in `Views/Shared` (reference them as `~/Views/Shared/_X.cshtml`).
5. **Never change** a route, a form field name, a handler name, an `asp-page` name, an antiforgery check, or a gate (`ThrowIfClosed`, password confirmation, admin/project-admin check, 404 rules). The three snapshots must pass unchanged; if one must change, stop and report why.
6. **Tests.** Existing HTTP tests drive URLs and stay as they are. Retarget `tests/BaGet.Web.Tests/Pages/*ModelFacts.cs` to the workflow and keep one smoke test of the shim. `UiCoverageInventoryTests` and `AdminApiAccessTests` already resolve pages through `[LegacyPage]`.
7. **Docs** of the changed behaviour in the same commit (XML docs, `docs/ui-coverage.md` only if a capability moves, READMEs).

## Decisions (user)

| # | Decision | Consequence |
|---|---|---|
| D1 | "No breaking changes" = HTTP surface + configuration + extension methods **+ every public C# type** | Every public `PageModel` and helper type keeps its name, namespace, assembly and public members as an `[Obsolete]` shim (removed in the next major). A public-API snapshot test guards it. |
| D2 | All 72 pages (`BaGet.Web.V3` 16, `BaGet.Identity` 56) | One programming model for BaGet's own UI. `AddRazorPages`/`MapRazorPages` **stay registered** so a host's own pages and views keep working (D1); BaGet ships no `@page` file at the end. |
| D3 | Form posts keep `?handler=X` exactly | A `[PageHandler("X")]` action selector picks the action from the `handler` route/query value, like Razor Pages. No form, bookmark, script or test URL changes. |
| D4 | One controller per feature (~25) | Views in `Views/<Controller>/`, view models in `ViewModels/`. Tracks own whole features, so the parallel tracks don't touch the same files. |

## Blocking verification (before any coding, wave 0)

1. **Tag helper registration after the `BaGet.Web` → `BaGet.Web.V3` rename.** `src/BaGet.Web.V3/Pages/_ViewImports.cshtml:8` says
   `@addTagHelper *, BaGet.Web`, but the assembly is now `BaGet.Web.V3`, so `NavLinkTagHelper` (`src/BaGet.Web.V3/NavLinkTagHelper.cs`)
   may silently no longer apply (active nav link). Check the rendered header; fix to `BaGet.Web.V3` in its own commit if broken.
2. **Contract snapshots on today's Razor Pages** (the definition of "not breaking"; committed before any migration):
   - `UiRouteContractTests`: every page endpoint → `METHOD template handler(s) [r/{repo} twin]`, sorted, to
     `tests/BaGet.Tests/Contracts/ui-routes.txt`. Includes `/packages` alias, catch-alls (`{**image}`, `{*id}`), constraints (`{key:int}`), HEAD.
   - `UiFormContractTests`: GET each page as anonymous, signed-in user and administrator on a seeded harness (one repository per format,
     a remote, a virtual, a policy, a replication config, a group/target/project) and record per page: status, redirect target,
     every `<form>` (method, action, input/select/textarea names, antiforgery present), every `<a href>` to a BaGet page,
     `<title>`. Normalize tokens/dates/ids. Snapshot to `Contracts/ui-forms.txt`.
   - `PublicApiSnapshotTests`: every public type/member of `BaGet.Web.V3` and `BaGet.Identity` (namespace, signature) to
     `Contracts/public-api.txt`; after migration the set must be a **superset** (new members allowed, none removed/changed).
   These three must pass byte-identical (public API: superset) after every track.
3. Confirm Razor Pages behaviours the snapshots must capture (write a short note in this plan's Status section):
   unknown `?handler=` (Razor Pages renders the page with no handler), HEAD served by `OnGet`, a POST without antiforgery → 400.

## Current coupling to Razor Pages (what must be replaced)

| Where | What | Replacement (wave 0) |
|---|---|---|
| `src/BaGet.Web.V3/Extensions/IServiceCollectionExtensions.cs:49-55` | `AddRazorPages` + `AddPageRoute("/Index","/packages")` + `RepositoryPageRouteConvention` | Keep `AddRazorPages` (D2); add `RepositoryActionRouteConvention : IApplicationModelConvention` (r/{repo} twin selector, `Order` -1, for actions marked `[RepositoryPage]`) |
| `…IServiceCollectionExtensions.cs:28` | `options.Filters.Add<RepositoryPageFilter>()` (`IAsyncPageFilter`) | `RepositoryActionFilter : IAsyncActionFilter`, driven by `[RepositoryPage(Format, NotOnVirtual, NotOnRemote)]` instead of the path lists `RepositoryPageFilter.cs:30-49` |
| `src/BaGet.Web.V3/RepositoryPageRouteConvention.cs:26` | `RepositoryPages`, `PageFormats`, `AnyFormatPages` (by page path) | Data moves to the attribute; the static members stay, `[Obsolete]`, computed from the attributes (D1) |
| `src/BaGet.Web.V3/RepositoryUrlHelper.cs:21` | adds `repo` to `Url.Page`/`asp-page`/`RedirectToPage` links | Same for `Url.Action`/`asp-action`/`RedirectToAction` to `[RepositoryPage]` actions, **plus legacy page-name mapping**: a page link whose page has no page endpoint resolves to the action carrying `[LegacyPage("/Admin/Users")]`. So unmigrated views, `RedirectToPage` calls and host views linking to BaGet pages keep producing the same `href` while tracks run in parallel |
| `src/BaGet.Web.V3/NavLinkTagHelper.cs:62` | active link from `PageActionDescriptor.ViewEnginePath` | Also read `LegacyPageAttribute` from the endpoint metadata |
| `src/BaGet.Web.V3/RepositoryResolutionMiddleware.cs:48` | `isPage` = page descriptor | `isPage` = page descriptor **or** `[RepositoryPage]`/`[LegacyPage]` metadata |
| `src/BaGet.Web.V3/ScanBlockedFilter.cs:26` | HTML answer for pages | Also for actions returning views (`[LegacyPage]` metadata) |
| `src/BaGet.Web.V3/Api/ApiOperationAttribute.cs:17`, `tests/BaGet.Tests/AdminApiAccessTests.cs:204` | `uiPage` = page path that must exist | Unchanged strings; the test resolves them through `[LegacyPage]` too |
| `tests/BaGet.Tests/UiCoverageInventoryTests.cs:100-127` | enumerates `CompiledPageActionDescriptor` | Enumerates page descriptors **and** `[LegacyPage]` actions (same names, so `docs/ui-coverage.md` needs no change) |
| `src/BaGet.Identity/Pages/Account/AccountPageModel.cs:21`, `Admin/AdminPageModel.cs:12`, `AccessPageModel.cs:29`, `RepositoryPageModel.cs:10`, `ReplicationPageModel.cs:82`, `RetentionPageModel.cs:94`, `src/BaGet.Web.V3/Pages/LegalPageModel.cs` | shared page behaviour (admin check, `NullContext` redirect, gate on POST, `ActAsync`, password confirmation) | `AccountControllerBase`, `AdminControllerBase`, `AccessControllerBase`, … with the same behaviour in `OnActionExecutionAsync`; the old bases stay as `[Obsolete]` shims |
| partials referenced by path, e.g. `~/Pages/Admin/_ConfirmPassword.cshtml` (35 uses), `_Reauthenticate` (13+5), 7 others; `_Layout`, `_Avatar`, `_ScanFindings` in `Pages/Shared` | view lookup | Move to `Views/Shared/` in wave 0; the old paths keep a one-line forwarding partial (D1 for host views); both Razor Pages and MVC find `Views/Shared` |
| `src/BaGet.Identity/AccountsServiceCollectionExtensions.cs:100-102` | `LoginPath`/`AccessDeniedPath` `/account/login` | Unchanged (URL contract) |

Razor Pages behaviours the controller base must reproduce (each covered by the contract tests):
- antiforgery validated automatically on every POST → `[AutoValidateAntiforgeryToken]` on the base controller;
- `handler` from route value then query string; `OnGetX` handlers (`Settings?handler=CopyStatus`, `Login?handler=Callback`, `Reauthenticate?handler=Callback`); unknown handler = default handler;
- HEAD answered by the GET action → `[HttpGet, HttpHead]`;
- `[BindProperty]` binds on POST only (`SupportsGet` exceptions) → explicit `[FromForm]`/`[FromQuery]` view-model parameters;
- `return Page()` re-renders at the same URL with `ModelState` → `return View(model)`; `[TempData]` properties → `TempData`;
- `PageModel` filters (`OnPageHandlerExecutionAsync` overrides in the bases) → controller `OnActionExecutionAsync`; order relative to `RepositoryActionFilter` as today (several manage pages rely on the model check running first, e.g. `Packages/MavenManage.cshtml.cs:92`).

## Target shape (per feature)

```csharp
[LegacyPage("/Admin/Users")]                         // page name: links, nav, ui-coverage, ApiOperation(uiPage)
[Route("admin/users")]                               // same template as today's @page
public sealed class AdminUsersController : AdminControllerBase
{
    [HttpGet, HttpHead]                              public Task<IActionResult> Index(...)        => …View(vm);
    [HttpPost, PageHandler("Approve")]               public Task<IActionResult> Approve([FromForm] ApproveForm form) => …;
    [HttpPost, PageHandler("Promote")]               public Task<IActionResult> Promote(...) => …;
}
```
Logic moves out of the page model into `internal sealed class <Feature>Workflow` (one per controller, DI-scoped); the controller and the
`[Obsolete]` `UsersModel` shim both call it, so the shim keeps its public constructor/properties/handler methods without duplicate logic.
Views: `Views/AdminUsers/Index.cshtml` (the old `.cshtml` body, `@model` = view model; `asp-page`/`asp-page-handler` → `asp-controller`/`asp-action`
or kept as `asp-page` where the legacy mapping makes them equivalent). Repository pages: `[RepositoryPage(Format = …, NotOnVirtual = true)]`.

## Waves and tracks

| Wave | Track (`wt/<slug>`) | Pages → controllers | Owns |
|---|---|---|---|
| 0 | `mvc-infra` | none migrated | blocking checks 1-3; `PageHandlerAttribute`, `LegacyPageAttribute`, `RepositoryPageAttribute`, `RepositoryActionRouteConvention`, `RepositoryActionFilter`, `RepositoryUrlHelper` action + legacy mapping, NavLink/middleware/ScanBlocked updates, controller bases, partials + layout to `Views/Shared` with forwarding stubs, `_ViewImports` for `Views/` in both projects, test updates (`UiCoverageInventoryTests`, `AdminApiAccessTests`), one pilot: `Privacy`/`Terms` → `LegalController` (proves the whole path) |
| 1 | `mvc-browse` | `Index` (+`/packages`), `Package`, `Statistics`, `ReportAbuse`, `Admin/Api` → `HomeController`, `PackageController`, `StatisticsController`, `ReportAbuseController`, `ApiCatalogController` | `src/BaGet.Web.V3/Pages/{those}`, `tests/BaGet.Web.Tests/Pages/IndexModelFacts.cs`, `PackageModelFacts.cs` (retarget to the workflows; keep one shim smoke test) |
| 1 | `mvc-formats-ui` | `Upload`, `NpmPackages`, `NpmPackage`, `NpmUpload`, `MavenArtifacts`, `MavenArtifact`, `MavenUpload`, `DockerImages`, `DockerImage` → `UploadController`, `NpmBrowseController`, `MavenBrowseController`, `DockerBrowseController` | those pages (Upload is the largest: 647 lines, `PostStage/Submit/Cancel`, the virtual-to-deploy redirect via `RequestedVirtualItemKey`) |
| 1 | `mvc-account` | `Account/*` (16) + `Profiles/Profile` → `AccountController` (login, external callback, 2FA, recovery, logout, register, confirm, forgot/reset, reauthenticate), `AccountSettingsController` (Settings, TwoFactor, ApiKeys), `AccountOrganizationsController`, `AccountPackagesController` (Packages, Access, Profile), `ProfileController` | `src/BaGet.Identity/Pages/Account/**`, `Profiles/**`, `AccountReauthenticationTests` if it references models |
| 1 | `mvc-manage` | `Packages/*` (Manage, NpmManage, MavenManage, DockerManage, ContactOwners, Scan, ValidationStatus) → `PackageManageController`, `NpmManageController`, `MavenManageController`, `DockerManageController`, `ContactOwnersController`, `ScanArtifactController`, `ValidationStatusController` | `src/BaGet.Identity/Pages/Packages/**` |
| 1 | `mvc-admin-core` | `Admin/` Users, UserDetail, DeleteUser, UserPackages, Packages, Organizations, OrganizationDetail, Settings, Cluster, Formats, Operations, Validation, Abuse, AccessUnavailable → `AdminUsersController`, `AdminOrganizationsController`, `AdminSettingsController`, `AdminPackagesController`, `AdminOperationsController` (Operations, Validation, Abuse, Cluster, Formats, AccessUnavailable) | those `Admin/*` files |
| 2 | `mvc-admin-access` | Groups, GroupDetail, Permissions, PermissionDetail, Projects, ProjectDetail, Audit → `AdminGroupsController`, `AdminPermissionsController`, `AdminProjectsController`, `AdminAuditController` | those files; `AccessPageModel` shim |
| 2 | `mvc-admin-repos` (done) | Repositories, RepositoryEdit (487 lines, 11 handlers), RepositoryDelete → `AdminRepositoriesController` | those files; `RepositoryPageModel` shim |
| 2 | `mvc-admin-policies` | Retention, RetentionPolicy, RetentionHolds, Scanning, ScanRepository, ScanProject, Replication, ReplicationConfig → `AdminRetentionController`, `AdminScanningController`, `AdminReplicationController` | those files; `RetentionPageModel`, `ReplicationPageModel` shims |
| 3 | `mvc-finish` | none | remove `RepositoryPageRouteConvention` registration and `RepositoryPageFilter` (types stay `[Obsolete]`), delete empty `Pages` folders (shims move to `Compat/`), convert remaining `asp-page` uses in BaGet's own views, docs (below), CLAUDE.md, final contract run |

Why this split: wave 1 = 5 tracks (the agent limit), all in disjoint folders; the admin access/repository/policy tracks share
`AccessPageModel`-derived bases and partials with each other, so they run in wave 2 after `mvc-admin-core` has fixed the admin base controller.
Shared files a track may touch only by **appending**: `_Layout` nav links (unchanged hrefs thanks to the legacy mapping, so normally untouched),
`docs/ui-coverage.md` (page names unchanged, so normally untouched). Conflicts are resolved at the per-track rebase.

Per track definition of done: its pages have no `.cshtml`/`.cshtml.cs` left (except shims), the three contract snapshots pass
unchanged, build 0 warnings, full suite = baseline (`CLAUDE.md`, 3574 tests, known failures only) + new tests, a public-API superset.

## Docs (wave 3, same commit as the code they describe)

`CLAUDE.md` (Razor Pages paragraphs: *Repository pages*, `RepositoryPageRouteConvention`/`RepositoryPageFilter`/`RepositoryUrlHelper`, every "page" →
controller/view, the IVT/test sections), `docs/advanced/architecture.md` (UI layer), `src/BaGet.Web.V3/README.md` and `src/BaGet.Identity/README.md`
(folder tables, extension points: custom views override `Views/<Controller>/<Action>.cshtml`), `docs/devops-manual.md` if it mentions pages.
`docs/ui-coverage.md` keeps page names (`/Admin/Users`) as the stable identifiers.

## Verification

1. Wave 0: the three snapshots are committed **on the unmigrated code** and pass; the `Legal` pilot passes them.
2. Every track: `dotnet build BaGet.sln -p:GeneratePackageOnBuild=false` (0 warnings), the contract tests, the feature's existing HTTP tests
   (unchanged: they drive URLs), then the full suite in the worktree; after rebase on `main`, again before `merge --ff-only`.
3. Opus review per wave of the merged diff: antiforgery on every POST action, handler selection, gates (`ThrowIfClosed`, password
   confirmation, admin/project-admin authorization), `r/{repo}` twins and 404 rules, open redirects in `returnUrl` handling (Login, Reauthenticate).
4. Manual smoke at the end: run the host, click through each nav entry at `/` and `/r/{repo}/`, sign in/out, 2FA, one admin change per area.

## Risks

| Risk | Mitigation |
|---|---|
| A silently lost antiforgery check on a POST | base controller `[AutoValidateAntiforgeryToken]`; contract test posts each form without a token and expects 400 |
| Link generation produces empty `href`s (the known Razor Pages trap, see `RepositoryPageRouteConvention.cs:18`) | legacy mapping in one place (`RepositoryUrlHelper`), the `<a href>` snapshot catches any change |
| Filter order changes (model check vs repository filter) | explicit `Order` on `RepositoryActionFilter`; the 404-vs-redirect cases are in the form snapshot (anonymous/user/admin) |
| Public API shims drift or duplicate logic | shims call the same `Workflow`; `PublicApiSnapshotTests` superset; removal scheduled for the next major |
| Large pages (Upload 647, Manage 552, RepositoryEdit 487, Users 406 lines) stall a track | they are each a track's first item; split the workflow, not the URL |
| Host views or embedders referencing `~/Pages/...` partials or `asp-page` to BaGet pages | forwarding partials + legacy page-name mapping (kept after wave 3) |
| Concurrent user commits on `main` (recent restructure) | each integrator rebases and re-runs the contracts; never squash user commits |
