# Extension.BaGet.Identity.Base

The MVC infrastructure behind the screens of [BaGet Extension](https://pieroviano.github.io/BaGet/): controller bases, the `?handler=` selector,
repository-page routing, the in-process calls to the JSON UI API (`/api/ui`) and request credentials. It has no controller and no view of its own:
the screens are in `Extension.BaGet.Identity.Api`, which references this package (so do hosts through `Extension.BaGet.Web.V3`).

```powershell
dotnet add package Extension.BaGet.Identity.Base
```

Reference it directly only to write your own screens or JSON twins on the same bases; the business services are in
`Extension.BaGet.Identity.Application` (namespace `BaGet.Identity.Application.*`).

## Contents

| Folder / type | Role |
|---|---|
| `Controllers/Base/UiControllerBase` and `AccountControllerBase`, `AdminControllerBase`, `AccessControllerBase`, `RepositoryControllerBase`, `RetentionControllerBase`, `ReplicationControllerBase` | Bases of the view controllers: antiforgery on every POST (`[AutoValidateAntiforgeryToken]`) and the shared checks (`ScreenGate`: admin or project admin, `NullContext` redirect, maintenance gate on POST, password confirmation) in a fixed order. `AdminControllerBase.Flash(model)` carries `Message`/`Error` across a redirect |
| `Controllers/Base/UiApiControllerBase` and `AccountUiApiControllerBase`, `AdminUiApiControllerBase`, `AccessUiApiControllerBase` | Bases of the JSON twins (`/api/ui`, see [UI API](https://pieroviano.github.io/BaGet/ui-api/)); visible in the OpenAPI `ui` document |
| `Mvc/Attributes/PageHandlerAttribute` | Picks the action by `?handler=` (route value, then query); an unknown handler is the default handler. A screen's actions are those of one controller sharing their route templates |
| `Mvc/Attributes/ImplicitPageHandlerAttribute` | The default POST that renders the screen (a form posted to its own URL); not in the route contract |
| `Mvc/Attributes/FallbackHandlerAttribute` | The `NoHandler` action of a manage controller (a stray POST answers 404); not a handler of the screen |
| `Mvc/Attributes/RepositoryPageAttribute` | The action acts on the request's repository: `/r/{repo}/` twin routes and the repository check (format, upload, not on virtual / remote) |
| `Mvc/Attributes/RequiresSignInAttribute`, `RequiresEmailAttribute` | Account screen gates |
| `Mvc/Screens` | `ScreenOutcome`, `ScreenLinks`, `AccountSupport`, `AccessSupport`, `PackageControllerSupport`: shared screen rules |
| `Mvc/UI/ScreenEndpoints` | Whether an action is a screen (an action of a `UiControllerBase`, not a `[FallbackHandler]`) and which one (`ScreenRef`: controller + default GET action); used by navigation, scanning refusals, repository resolution and the coverage tests |
| `Mvc/UI/UiApiAttribute`, `UiApiInvoker` | `[UiApi(typeof(XController), nameof(XController.Index), Handler = ...)]` names a twin's screen; a view controller calls its twin in process |
| `Mvc/UI/UiApiSecurityFilter` | `/api/ui` over HTTP: API keys only (Bearer or `X-NuGet-ApiKey`); no cookie, no Basic, no antiforgery |
| `Security/RequestPrincipalResolver` | Request credentials: API key (Basic password, Bearer, `X-NuGet-ApiKey`), else the cookie session (never on writes) |

## Layout

One type per file, no nested types, namespace = `BaGet.Identity` + folder (`CodeLayoutTests`).

## Links

[UI API](https://pieroviano.github.io/BaGet/ui-api/) · [Source](https://github.com/pieroviano/BaGet)
