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

| Caller | Rule |
|---|---|
| Browser session (cookie) | Reads freely; every write needs the antiforgery header (below), else `400` |
| API key (Basic password, Bearer, or `X-NuGet-ApiKey`) | Acts as the key's owner within its scopes and repository limit; no antiforgery; invalid credentials `401`; never falls back to the cookie |
| Administrator screens | Administrator session, or a key with `admin:api` and no repository limit (a project administrator reaches the screens of their project) |
| Account self-service (password, two-factor, external logins, API keys, sign-in/out, deletion), package ownership/listing/deletion pages | Cookie only: a key gets `403` |
| Anonymous | Only public screens (home, package, legal, statistics, sign-in/registration); else `401` |

Same gates as the HTML screen: accounts on, mail configured, administrator, access model present, repository check (`r/{repo}`: `404` for an
unknown repository, a format mismatch or a repository the caller cannot read), `MaintenanceGate` (`503` + `Retry-After` on writes while read-only) and the
password the screen asks for, sent in the body.

**Antiforgery.** `GET /api/ui/antiforgery` returns `{"headerName": "RequestVerificationToken", "requestToken": "..."}` and sets the
antiforgery cookie; send the token in that header on every `POST`/`PUT`/`PATCH`/`DELETE` made with the session.

## The answer: `ScreenResult<T>`

| Field | Meaning |
|---|---|
| `status` | `Ok`, `Invalid`, `NotFound`, `Forbidden`, `SignIn`, `Next` |
| `model` | What the screen renders (also on `Invalid`, so the form can be shown again) |
| `errors` | Field name to messages; `""` = the whole form |
| `next` | After a write: `screen` (page name such as `/Admin/Users`), `routeValues`, flash `message` or `error` |
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

No twin: the external provider's challenge and callback (they redirect the browser), the "sign in again" button, and downloads that are
plain URLs (package files, symbol files, avatars).

## OpenAPI and Scalar

`/openapi/ui.json` (this API), `/openapi/v1.json` and `/openapi/protocols.json`, viewer at `/scalar`; administrators only (everyone else `404`).
See [testing the APIs with Scalar](scalar.md).

## Architecture

| Project | Holds |
|---|---|
| `BaGet.Identity.Application`, `BaGet.Web.Application` | Use cases (`<Area>UseCase`), the DTOs (the JSON `model`), business services. No MVC or Razor reference (`ApplicationLayeringTests`) |
| `BaGet.Identity`, `BaGet.Web.V3` | Controllers: the `[UiApi]` twin (`Controllers/Api`, base `UiApiControllerBase` family) and the view controller, which binds the form, calls the twin in process (`UiApiInvoker`) and renders; views; MVC infrastructure |

A use case takes explicit inputs (caller, form, cancellation token), returns a `ScreenResult<T>` and sees no `HttpContext`, `ModelState` or
`TempData`. The twin's filters do not run in process, so the view controller bases run the same `ScreenGate` first.
Moved public types keep their name and namespace and are forwarded (`[TypeForwardedTo]`) from the old assembly.

## Not breaking

URLs, forms, `?handler=` values, redirects and status codes of the HTML screens are unchanged; the snapshots `ui-routes.txt`,
`ui-forms.txt` and `public-api.txt` (a superset check) guard it. Each screen's row in [UI coverage](ui-coverage.md) covers its twin.
