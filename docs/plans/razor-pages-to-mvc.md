# Plan: Razor Pages → MVC (controllers + views), no breaking changes

Status: **planned** (not started). Execution: parallel tracks in git worktrees (`wt/<slug>`), one wave at a time, rebase +
`merge --ff-only` per track, max 5 agents at once (Sonnet implements, Opus reviews).

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
| 2 | `mvc-admin-repos` | Repositories, RepositoryEdit (487 lines, 11 handlers), RepositoryDelete → `AdminRepositoriesController` | those files; `RepositoryPageModel` shim |
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
