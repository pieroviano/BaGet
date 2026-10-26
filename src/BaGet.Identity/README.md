# Extension.BaGet.Identity

User accounts, package owners, organizations and per-user API keys for
[BaGet Extension](https://pieroviano.github.io/BaGet/), with their screens (sign-in, profile, API keys, admin), served by MVC controllers and views.

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

The business services live in `Extension.BaGet.Identity.Application` (same namespace, types forwarded from this assembly); this package keeps the screens: controllers, views and the MVC infrastructure (`Mvc/`). `Mvc/Api` holds the base classes of the JSON UI API (`UiApiControllerBase`, `AccountUiApiControllerBase`, `AdminUiApiControllerBase`, `AccessUiApiControllerBase`), `UiApiInvoker` (a view controller calls its JSON twin in process), `UiApiSecurityFilter` (API keys only over HTTP: Bearer or `X-NuGet-ApiKey`; no cookie, no Basic, no antiforgery). The account screens (sign-in side, settings, two-factor, API keys, organizations, My packages/access, profile) are split: their controllers only bind the form and render, their JSON twins are under `Controllers/Api` (`/api/ui/account/...`, `/api/ui/profiles/{name}`; screen only, 403 over HTTP, except the public profile) and the rules are use cases in `Extension.BaGet.Identity.Application`.

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

## Screens: MVC controllers and views

Every screen is a controller action and a view; the package ships **no `@page` file**. URLs, `?handler=` form posts and page names (`asp-page`,
`Url.Page`, `RedirectToPage`) are unchanged from the Razor Pages era. The old page-model types (`LoginModel`, `UsersModel`, `AccountPageModel`, ...) were removed (breaking for code that referenced them).

| Folder / type | Role |
|---|---|
| `Controllers/` | One controller per feature: `Account*`, `ProfileController`, `PackageManage`, `NpmManage`, `MavenManage`, `DockerManage`, `ContactOwners`, `ScanArtifact`, `ValidationStatusPage`, `AdminUsers`, `AdminOrganizations`, `AdminSettings`, `AdminPackages`, `AdminOperations`, `AdminGroups`, `AdminPermissions`, `AdminProjects`, `AdminAudit`, `AdminRepositories`, `AdminRetention`, `AdminScanning`, `AdminReplication` |
| `Views/<Controller>/<Action>.cshtml` | The screens; `Views/Shared` holds the partials (the old `Pages/...` partial paths were removed). A host overrides a view with its own `Views/<Controller>/<Action>.cshtml` |
| `ViewModels/` | The models the views read (the screen DTOs are in `Extension.BaGet.Identity.Application`). The logic of every screen is in a use case there, `Controllers/Api/` holds the JSON twins (`/api/ui`, see [UI API](https://pieroviano.github.io/BaGet/ui-api/)); no `*Workflow` class is left |
| `Mvc/PageHandlerAttribute` | Picks the action by `?handler=` (route value, then query), like `OnPostApprove`; an unknown handler is the default handler |
| `Mvc/LegacyPageAttribute` | The page name a controller action replaces (`/Admin/Users`): `Url.Page`, `asp-page` and `RedirectToPage` keep resolving; navigation, `ApiOperation.UiPage` and the coverage tests use it |
| `Mvc/ImplicitPageHandlerAttribute` | Marks the default `[PageHandler]` action that answers a form posted to the screen's own URL: it renders the screen; not a handler of the route contract |
| `Mvc/RepositoryPageAttribute` | The action acts on the request's repository: `/r/{repo}/` twin routes and the repository check (format, upload, not on virtual / remote) |
| `Mvc/UiControllerBase` and `AccountControllerBase`, `AdminControllerBase`, `AccessControllerBase`, `RepositoryControllerBase`, `RetentionControllerBase`, `ReplicationControllerBase` | Antiforgery on every POST (`[AutoValidateAntiforgeryToken]`) and the shared checks (admin or project-admin, `NullContext` redirect, maintenance gate on POST, password confirmation) in a fixed order |

In a view, link with `asp-controller` / `asp-action` (and `asp-route-handler="X"` for a named handler); an MVC view has no ambient page, so each
handler form names its controller and action. `AdminControllerBase.Flash(model)` carries `Message`/`Error` across a redirect.

## Links

[Configuration](https://pieroviano.github.io/BaGet/configuration/) · [Source](https://github.com/pieroviano/BaGet)
