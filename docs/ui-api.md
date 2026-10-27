# UI API (`/api/ui`)

Every web screen has a JSON twin under `/api/ui/...` that does the work and answers the model the screen renders. A frontend (Angular, React,
a script) can be written against `/api/ui` alone; the HTML screens call the same code in process and only render the answer, so the two
cannot differ. Try it in the browser with [Scalar](scalar.md); the administration API for scripts is [`/api/v1`](admin-api.md).

## Routes

| Screen | Twin |
|---|---|
| GET of a screen (`/admin/users`) | `GET /api/ui/admin/users` |
| POST of the screen's own form | `POST` the same path |
| Named handler (`?handler=Approve`) | `POST` (or `GET`) `/api/ui/<path>/approve` (kebab case; catch-all routes put the handler first: `/api/ui/npm-manage/{handler}/{*id}`) |
| Repository screen (`/r/{repo}/...`) | `/api/ui/r/{repo}/<path>` |

Route values keep the screen's names (`{id}`, `{name}`, `{version}`). The complete list is the OpenAPI document `ui`
(`/openapi/ui.json`) and the snapshot `tests/BaGet.Tests/Contracts/ui-api.txt`. Bodies are JSON; a file upload is multipart.

## Who may call

Like [`/api/v1`](admin-api.md): **API keys only**. The browser session cookie is never a credential on `/api/ui`, and there is no antiforgery token
(nothing ambient for a cross-site page to ride on). Exception: an open server (accounts off, no `ApiKey`) lets anonymous callers upload, so any page a
visitor opens can post an upload there, as it already can to the protocol routes (CORS allows any origin); configure an `ApiKey` or turn accounts on.

| Caller | Rule |
|---|---|
| API key in `Authorization: Bearer <key>` or `X-NuGet-ApiKey: <key>` | Acts as the key's owner within its scopes and repository limit. An invalid key is `401` (never read as anonymous, never falls back to the cookie) |
| HTTP Basic | `401` with a message saying to use Bearer or `X-NuGet-ApiKey` (browsers cache Basic credentials and re-send them cross-site) |
| Browser session (cookie) | Ignored: the request is anonymous |
| Anonymous | Public screens (home, package, statistics, legal, report abuse, profiles, browse); a protected screen is `401` |
| Administrator screens | An administrator's key with `admin:api` and no repository limit. Anyone else: `401` (no key), `403` (another key). The **global `ApiKey` is refused** (`403`: it has no owner; use an administrator's key). A project administrator reaches the screens of their project in the web UI only |
| Account self-service (sign-in/out, registration, password, two-factor, external logins, API keys, organizations, deletion) and package ownership screens (manage, contact owners, validation status, scan) | **Web UI only**: `403` "use the web screen" to every caller. They stay in the OpenAPI document, marked "Screen only" |

Same gates as the HTML screen: accounts on, mail configured, administrator, access model present, repository check (`r/{repo}`: `404` for an
unknown repository, a format mismatch or a repository the caller cannot read), `MaintenanceGate` (`503` + `Retry-After` on writes while read-only) and the
password the screen asks for, sent in the body. The HTML screens keep their cookie session and antiforgery token; they call the same code in process.

## The answer: `ScreenResult<T>`

| Field | Meaning |
|---|---|
| `status` | `Ok`, `Invalid`, `NotFound`, `Forbidden`, `SignIn`, `Next` |
| `model` | What the screen renders (also on `Invalid`, so the form can be shown again) |
| `errors` | Field name to messages; `""` = the whole form |
| `next` | After a write: `screen` (`{ "controller": "AdminUsers", "action": "Index" }`), `routeValues`, flash `message` or `error` |
| `statusCode` | Set when the screen itself answers with a code (an upload refused with `403`, a missing package explained with `404`) |

| `status` | HTTP |
|---|---|
| `Ok`, `Next` | `200` (or `statusCode`) |
| `Invalid` | `400`, body still holds `model` and `errors` |
| `Forbidden` | `403` |
| `SignIn` | `401` with `WWW-Authenticate: Bearer` |
| `NotFound` | `404` (also for "you may not know it exists") |
| read-only gate closed | `503` + `Retry-After` (problem details) |

JSON is camelCase, enums by name, nulls left out. A response never carries a password, a protected password, a connection string, a
security stamp or an API key value (except the key just created, shown once).

## Browser-only steps

No twin: the external provider's challenge and callback (sign-in and re-authentication), and downloads that are
plain URLs (package files, symbol files, avatars). `UiApiParityTests` fails for any other screen and handler (GET or POST) of a view without a twin.

## OpenAPI and Scalar

`/openapi/ui.json` (this API: every twin, plain and `/api/ui/r/{repo}/`; security schemes `bearer` and `apiKey` only), `/openapi/v1.json` and
`/openapi/protocols.json`, viewer at `/scalar`; administrators only (everyone else `404`). See [testing the APIs with Scalar](scalar.md).

## Architecture

| Project | Holds |
|---|---|
| `BaGet.Identity.Application`, `BaGet.Web.Application` | Use cases (`<Area>UseCase`), the DTOs (the JSON `model`), business services. No MVC or Razor reference (`ApplicationLayeringTests`) |
| `BaGet.Identity`, `BaGet.Web.V3` | Controllers: the `[UiApi]` twin (`Controllers/UI`, base `UiApiControllerBase` family in `Controllers/Base`) and the view controller, which binds the form, calls the twin in process (`UiApiInvoker`) and renders; views; MVC infrastructure |

A use case takes explicit inputs (caller, form, cancellation token), returns a `ScreenResult<T>` and sees no `HttpContext`, `ModelState` or
`TempData`. The twin's filters do not run in process, so the view controller bases run the same `ScreenGate` first.
Moved public types keep their name and namespace and are forwarded (`[TypeForwardedTo]`) from the old assembly.

## Not breaking

URLs, forms, `?handler=` values, redirects and status codes of the HTML screens are unchanged; the snapshots `ui-routes.txt`,
`ui-forms.txt` and `public-api.txt` (a superset check) guard it. Each screen's row in [UI coverage](ui-coverage.md) covers its twin.

## Adding a screen

Worked examples: `LegalController` / `LegalUiApiController` / `LegalUseCase`, `StatisticsController` / `StatisticsUiApiController` / `StatisticsUseCase`;
test twins for each base at the end of `tests/BaGet.Tests/UiApiAccessTests.cs`.

| Piece | Where | Rule |
|---|---|---|
| Use case | `BaGet.Web.Application/UseCases` or `BaGet.Identity.Application/UseCases/<area>` | Scoped `<Area>UseCase`, one method per GET and handler; inputs explicit (caller `ClaimsPrincipal`, form DTO, `CancellationToken`); returns `ScreenResult<T>`; sees no `HttpContext`, `ModelState`, `TempData`; validates its form itself; a write calls `MaintenanceGate.ThrowIfClosed`; absolute links from `IUrlGenerator`; no secret in a DTO |
| DTO | same project, `Models/<area>` (namespace = folder) | Flat, no EF entities; one type per file |
| Registration | `AddBaGetWebUseCases`, or `AddBaGetAccounts` after its `Enabled` check | `TryAddScoped<<Area>UseCase>()` |
| JSON twin | `BaGet.Web.V3/Controllers/UI` or `BaGet.Identity/Controllers/UI` | `<Feature>UiApiController`; never `[ApiController]`, a `UiControllerBase` or `[AutoValidateAntiforgeryToken]` |
| View controller | unchanged place | Binds the form, `Api<Twin, Model>(api => ...)`, `Render(...)`; holds no use case |

Twin bases: `UiApiControllerBase` (public), `AccountUiApiControllerBase` (override `RequiresSignIn`/`RequiresEmail`; **screen only**: 403 over HTTP),
`AdminUiApiControllerBase` (administrator key), `AccessUiApiControllerBase` (override `AuthorizeAsync`; writes `503` while read-only; retention, replication and
repository bases build on these). Every twin is visible in the OpenAPI `ui` document (the base sets `[ApiExplorerSettings(IgnoreApi = false)]`) and answers
`ActionResult<ScreenResult<T>>` through `Respond(...)`.

```csharp
[Route("api/ui")]
[RepositoryPage(NotOnVirtual = true)]          // same values as the view controller: adds api/ui/r/{repo}/...
public sealed class StatisticsUiApiController : UiApiControllerBase
{
    [HttpGet("stats"), UiApi(typeof(StatisticsController), nameof(StatisticsController.Index))]
    public async Task<ActionResult<ScreenResult<StatisticsViewModel>>> Get(CancellationToken ct)
        => Respond(await _useCase.GetAsync(ct));
}
```

Routes: GET `api/ui/<view path>`; default POST the same path; handler `X` = `api/ui/<path>/<x-kebab-case>` with `[UiApi(typeof(XController), nameof(XController.Index), Handler = "X")]` (the view controller and its default GET action name the screen); route values keep the
view's names. Bodies are `[FromBody]` JSON, uploads `[FromForm]` multipart. Public helpers are `[NonAction]` or non-public. The view action becomes
`=> Render(await Api<StatisticsUiApiController, StatisticsViewModel>(api => api.Get(ct)), "Index")` and keeps every attribute and its base (whose `ScreenGate` and
antiforgery already ran: the twin's filters do not run in process).

Checklist: the parity case (page and twin carry the same, statuses correspond) in `UiApiParityTests`; a case per caller kind in `UiApiAccessTests`
(anonymous, cookie ignored, key, `admin:api` key, limited key, global key, Basic); no password or key in a response; regenerate `Contracts/ui-api.txt` with
`BAGET_UPDATE_CONTRACTS=1` (never hand-merge); `ui-routes.txt`, `ui-forms.txt` and `public-api.txt` stay unchanged; a `ScreenCatalog` member and a row with its `Controller/Action` token in [UI coverage](ui-coverage.md) for
a new screen (`ScreenCatalogTests` checks that every catalog entry, `[UiApi]`, `FormatLink` and `ApiOperation` target resolves to a view action).
