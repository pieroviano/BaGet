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

Every screen is a controller action and a view; the package ships **no `@page` file**. URLs, `?handler=` form posts, page names (`asp-page`,
`Url.Page`, `RedirectToPage`) and every public type are unchanged from the Razor Pages era;
the old page models are `[Obsolete]` shims in `Compat/Pages/` (removed in the next major version) over the same workflows.

| Folder / type | Role |
|---|---|
| `Controllers/` | One controller per feature: `Account*`, `ProfileController`, `PackageManage`, `NpmManage`, `MavenManage`, `DockerManage`, `ContactOwners`, `ScanArtifact`, `ValidationStatusPage`, `AdminUsers`, `AdminOrganizations`, `AdminSettings`, `AdminPackages`, `AdminOperations`, `AdminGroups`, `AdminPermissions`, `AdminProjects`, `AdminAudit`, `AdminRepositories`, `AdminRetention`, `AdminScanning`, `AdminReplication` |
| `Views/<Controller>/<Action>.cshtml` | The screens; `Views/Shared` holds the partials (the old `Pages/...` partial paths forward to them). A host overrides a view with its own `Views/<Controller>/<Action>.cshtml` |
| `Workflows/`, `ViewModels/` | The logic (`internal` `*Workflow`, shared by controller and shim) and the models the views read |
| `Mvc/PageHandlerAttribute` | Picks the action by `?handler=` (route value, then query), like `OnPostApprove`; an unknown handler is the default handler |
| `Mvc/LegacyPageAttribute` | The page name a controller action replaces (`/Admin/Users`): `Url.Page`, `asp-page` and `RedirectToPage` keep resolving; navigation, `ApiOperation.UiPage` and the coverage tests use it |
| `Mvc/ImplicitPageHandlerAttribute` | Marks the default `[PageHandler]` action that answers a form posted to the screen's own URL: it renders the screen; not a handler of the route contract |
| `Mvc/RepositoryPageAttribute` | The action acts on the request's repository: `/r/{repo}/` twin routes and the repository check (format, upload, not on virtual / remote) |
| `Mvc/UiControllerBase` and `AccountControllerBase`, `AdminControllerBase`, `AccessControllerBase`, `RepositoryControllerBase`, `RetentionControllerBase`, `ReplicationControllerBase` | Antiforgery on every POST (`[AutoValidateAntiforgeryToken]`) and the shared checks (admin or project-admin, `NullContext` redirect, maintenance gate on POST, password confirmation) in the order the page-model bases ran them. The page-model bases (`AccountPageModel`, `AdminPageModel`, `AccessPageModel`, `RepositoryPageModel`, `RetentionPageModel`, `ReplicationPageModel`) remain `[Obsolete]` for compatibility |

In a view, link with `asp-controller` / `asp-action` (and `asp-route-handler="X"` for a named handler); an MVC view has no ambient page, so each
handler form names its controller and action. `AdminControllerBase.Flash(model)` carries `Message`/`Error` across a redirect.

## Links

[Configuration](https://pieroviano.github.io/BaGet/configuration/) · [Source](https://github.com/pieroviano/BaGet)
