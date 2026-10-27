# Extension.BaGet.Identity.Api

User accounts, package owners, organizations and per-user API keys for
[BaGet Extension](https://pieroviano.github.io/BaGet/), with their screens (sign-in, profile, API keys, package management, admin):
MVC controllers, views and their JSON twins (`/api/ui`).

```powershell
dotnet add package Extension.BaGet.Identity.Api
```

## Usage

```csharp
services.AddBaGetWebApplication(ConfigureBaGetApplication);
services.AddBaGetAccounts(configuration);
```

`AddBaGetAccounts` reads `Accounts:Enabled` **while registering**, so the configuration must be complete before it
runs. Disabled, it adds an empty authentication setup and only the global `ApiKey` authenticates.

| Package | Holds |
|---|---|
| `Extension.BaGet.Identity.Api` (this) | Screens: controllers, views, form models, `AddBaGetAccounts` |
| `Extension.BaGet.Identity.Base` | MVC infrastructure the screens build on: controller and twin bases, `?handler=` selection, `[RepositoryPage]`, `UiApiInvoker`, `UiApiSecurityFilter`, `RequestPrincipalResolver` |
| `Extension.BaGet.Identity.Application` | Use cases, DTOs, business services (`BaGet.Identity.Application.*`; no MVC reference) |

`MaintenanceAwareSignInManager` (Application) lets a sign-in complete while the read-only `MaintenanceGate` is closed (it skips the
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

## Screens: MVC controllers and views

Every screen is a controller action and a view; the package ships **no `@page` file**. URLs and `?handler=` form posts are unchanged from the Razor Pages era; a screen is identified by its controller and default GET action (`asp-controller`/`asp-action`, `Url.Action`, `RedirectToAction`), not by a page name (`asp-page`, `Url.Page` and `RedirectToPage` with BaGet page names no longer resolve). The old page-model types (`LoginModel`, `UsersModel`, `AccountPageModel`, ...) were removed (breaking for code that referenced them).

| Folder | Role |
|---|---|
| `Controllers/FrontEnd` | The view (screen) controllers: `Account*`, `ProfileController`, `PackageManage`, `NpmManage`, `MavenManage`, `DockerManage`, `ContactOwners`, `ScanArtifact`, `ValidationStatusPage`, `AdminUsers`, `AdminOrganizations`, `AdminSettings`, `AdminPackages`, `AdminOperations`, `AdminGroups`, `AdminPermissions`, `AdminProjects`, `AdminAudit`, `AdminRepositories`, `AdminRetention`, `AdminScanning`, `AdminReplication`. Each binds the form, calls its twin in process and renders |
| `Controllers/UI` | The JSON twins (`/api/ui`, see [UI API](https://pieroviano.github.io/BaGet/ui-api/)). Account self-service and package-ownership twins are screen only (403 over HTTP), except the public profile |
| `Views/<Controller>/<Action>.cshtml` | The screens; `Views/Shared` holds the partials. A host overrides a view with its own `Views/<Controller>/<Action>.cshtml` |
| `Models/Packages` | Form models of the package manage screens (npm, Maven, Docker, scan) |
| `Extensions` | `AddBaGetAccounts`, `RepositoryLinks` (`Url.RepositoryContent`) |

In a view, link with `asp-controller` / `asp-action` (and `asp-route-handler="X"` for a named handler); an MVC view has no ambient page, so each
handler form names its controller and action.

## Layout

One type per file, no nested types, namespace = `BaGet.Identity` + folder (`CodeLayoutTests`).

## Links

[Configuration](https://pieroviano.github.io/BaGet/configuration/) · [Source](https://github.com/pieroviano/BaGet)
