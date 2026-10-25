# Extension.BaGet.Identity

User accounts, package owners, organizations and per-user API keys for
[BaGet Extension](https://pieroviano.github.io/BaGet/), with their Razor pages (sign-in, profile, API keys, admin).

```powershell
dotnet add package Extension.BaGet.Identity
```

## Usage

```csharp
services.AddBaGetWebApplication(ConfigureBaGetApplication);
services.AddBaGetAccounts(configuration);
```

`AddBaGetAccounts` reads `Accounts:Enabled` **while registering**, so the configuration must be complete before it
runs. Disabled, it adds an empty authentication setup and only the global `ApiKey` authenticates.

`MaintenanceAwareSignInManager` lets a sign-in complete while the read-only `MaintenanceGate` is closed (it skips the
failed-attempt counter reset; a wrong password is still refused), so administrators can reach the Settings page.

| Setting | Value |
|---|---|
| `Accounts:Enabled` | Off by default. On: callers authenticate with the global `ApiKey` if set, otherwise their own key |
| `Accounts:AllowRegistration` | Local sign-up from the sign-in page (default `true`) |
| `Accounts:RequireApproval` | A self-registered account (also first external sign-in) cannot sign in until an administrator approves it on `/admin/users` or `/admin/users/{name}` (or `baget account approve`); promoting a user approves it too (default `false`) |
| `Accounts:CreateDefaultAdministrator` | A server with no users creates `Admin` with a default password to change at first sign-in (default `true`) |
| `Accounts:InitialAdministrators` | `;`-separated existing users promoted while there is no administrator |
| `Accounts:OpenIdConnect:Authority`, `ClientId`, `ClientSecret`, `DisplayName` | External OpenID Connect provider (e.g. Entra ID) |
| `Email:Smtp:*` | Optional SMTP for confirmation and password-reset mail (MailKit) |
| `Site:PublicUrl` | Required for email links; without it email is suppressed |

## Screens: Razor Pages and MVC controllers

The web UI is moving from Razor Pages to controllers and views without changing a URL, form, link or public type (plan:
`docs/plans/razor-pages-to-mvc.md`). `BaGet.Identity.Mvc` holds what a UI controller builds on:

| Type | Role |
|---|---|
| `PageHandlerAttribute` | Picks the action by `?handler=` (route value, then query), like `OnPostApprove`; an unknown handler is the default handler |
| `LegacyPageAttribute` | The page name a controller action replaces (`/Admin/Users`): `Url.Page`, `asp-page` and `RedirectToPage` keep resolving, navigation and `ApiOperation.UiPage` use it |
| `ImplicitPageHandlerAttribute` | Marks the default `[PageHandler]` action that answers a verb the page had no handler for (a form posted to the page's own URL): it renders the screen; not a handler of the route contract |
| `RepositoryPageAttribute` | The action acts on the request's repository: `/r/{repo}/` twin routes and the repository check (format, upload, not on virtual / remote) |
| `UiControllerBase` | Antiforgery on every POST (`[AutoValidateAntiforgeryToken]`), `RequestedHandler` |
| `AccountControllerBase`, `AdminControllerBase`, `AccessControllerBase`, `RepositoryControllerBase`, `RetentionControllerBase`, `ReplicationControllerBase` | The controller counterparts of the page-model bases (`AccountPageModel`, `AdminPageModel`, `AccessPageModel`, `RepositoryPageModel`, `RetentionPageModel`, `ReplicationPageModel`) with the same checks in the same order; the page-model bases remain for pages not migrated yet and for compatibility |

Migrated: the account screens. `AccountController` (sign-in side), `AccountSettingsController` (settings, two-factor, API keys), `AccountOrganizationsController`, `AccountPackagesController` (my packages, my access, own profile) and `ProfileController` replace the `Pages/Account` and `Pages/Profiles` pages. Their rules live in `Workflows/` (`internal`, one per screen, built from an `AccountScreen`), shared with the `[Obsolete]` page-model shims that keep every public type; view models are in `ViewModels/`, views in `Views/Account*` and `Views/Profile`. `RequiresEmailAttribute` / `RequiresSignInAttribute` set the base checks per action.

Migrated screens keep their logic in an `internal` workflow (`Workflows/`) that the controller and the obsolete page-model shim both run; the package-management screens (`Packages/*`) are `PackageManageController`, `NpmManageController`, `MavenManageController`, `DockerManageController`, `ContactOwnersController`, `ScanArtifactController` and `ValidationStatusPageController`.

Migrated admin screens (wave 1): `AdminUsersController` (Users, UserDetail, DeleteUser, UserPackages), `AdminOrganizationsController`,
`AdminSettingsController`, `AdminPackagesController`, `AdminOperationsController` (Operations, Validation, Abuse, Cluster, Formats,
AccessUnavailable); controllers in `Controllers/`, views in `Views/<Controller>/`, view models in `ViewModels/`, the logic in internal
`Workflows/*Workflow` (shared with the obsolete page models in `Pages/Admin`, kept as shims). `AdminControllerBase.Flash(model)` carries
`Message`/`Error` across a redirect (the old `[TempData]` properties).

Partials and the layout live in `Views/Shared` (the old `Pages/...` paths forward to them); a host overrides a view with its own
`Views/<Controller>/<Action>.cshtml`.

## Links

[Configuration](https://pieroviano.github.io/BaGet/configuration/) · [Source](https://github.com/pieroviano/BaGet)
