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

Two kinds of caller: **API keys** (scripts, like [`/api/v1`](admin-api.md)) and **a person's interactive session** (a single-page application): a
**session token** from `POST /api/ui/session` for any origin, or, for an application served from BaGet's own origin, the **cookie plus an antiforgery header**
([Sessions](#sessions-for-a-single-page-application)). The bare cookie is never a credential and HTTP Basic is refused. Exception: an open server (accounts off, no `ApiKey`) lets anonymous callers upload, so any page a
visitor opens can post an upload there, as it already can to the protocol routes (CORS allows any origin); configure an `ApiKey` or turn accounts on.

| Caller | Rule |
|---|---|
| Session token in `Authorization: Bearer bgs.…` | The signed-in user, with the rights of their cookie session (every scope, no repository limit, administrator flag read per call). Re-validated on every use (account exists, not pending, not locked out, security stamp unchanged). An invalid or expired token is `401`. **Only `/api/ui` accepts it**: on a protocol route, `/api/v1` or the registry it is an invalid key |
| Cookie + `X-XSRF-TOKEN` header (any method) | Same as a session token, for a client on BaGet's own origin; the token comes from `GET /api/ui/antiforgery`. The cookie without a valid header is ignored: the request is anonymous |
| API key in `Authorization: Bearer <key>` or `X-NuGet-ApiKey: <key>` | Acts as the key's owner within its scopes and repository limit. An invalid key is `401` (never read as anonymous, never falls back to the cookie). A key beside a session is judged as a key |
| HTTP Basic | `401` with a message saying to use Bearer or `X-NuGet-ApiKey` (browsers cache Basic credentials and re-send them cross-site) |
| Anonymous | Public screens (home, package, statistics, legal, report abuse, profiles, browse); a protected screen is `401` |
| Administrator screens | An administrator's interactive session, or an administrator's key with `admin:api` and no repository limit. Anyone else: `401` (no credential), `403` (another user's session or key). The **global `ApiKey` is refused** (`403`: it has no owner). A project administrator's session reaches the screens of their project |
| Account self-service (sign-in/out, registration, password, two-factor, external logins, API keys, organizations, deletion) and package ownership screens (manage, contact owners, validation status, scan) | **Interactive sessions only**: a session token, or the cookie with the antiforgery header. An API key (a leaked CI key must not change the account) and an anonymous caller get `403` "use the web screen". They stay in the OpenAPI document, marked "Interactive session only" |

Same gates as the HTML screen: accounts on, mail configured, administrator, access model present, repository check (`r/{repo}`: `404` for an
unknown repository, a format mismatch or a repository the caller cannot read), `MaintenanceGate` (`503` + `Retry-After` on writes while read-only) and the
password the screen asks for, sent in the body (an account without a password confirms within five minutes of the external sign-in that created the session token). The HTML screens keep their cookie session and antiforgery token; they call the same code in process.

## Sessions for a single-page application

Endpoints under `/api/ui` (JSON bodies, answers are `ScreenResult<…>` with `Cache-Control: no-store`; `404` while accounts are off). They are not the twin of a screen
(the sign-in page sets a cookie) and carry no `[UiApi]`.

| Method and route | Body | Answer |
|---|---|---|
| `POST /session` | `{ userName, password }` | `model`: `{ accessToken, expiresAt, refreshToken, refreshExpiresAt, userName }`, or `{ twoFactorRequired: true, twoFactorTicket, twoFactorTicketExpiresAt }` for an account with two-factor. `401` wrong credentials (the same answer for an unknown user), `403` locked out / awaiting approval / email unconfirmed (only to the right password), `400` empty form |
| `POST /session/two-factor` | `{ ticket, code }` | the tokens; `401` for a wrong code (counted: five failures lock the account, `403`) or an expired ticket |
| `POST /session/recovery-code` | `{ ticket, recoveryCode }` | the tokens; each code works once |
| `POST /session/refresh` | `{ refreshToken }` | a new pair; the old refresh token stays valid until it expires (stateless); the authentication time is kept |
| `POST /session/sign-out` | `{ everywhere?, refreshToken? }` | `200`. Revokes nothing by itself (the client forgets its tokens); `everywhere: true` changes the account's security stamp, which ends **every** token and cookie session of the account (send the access token, or the refresh token in the body) |
| `GET /antiforgery` | | `model`: `{ headerName: "X-XSRF-TOKEN", requestToken }` and the antiforgery cookie; the token is bound to the signed-in user, so fetch it again after signing in or out |
| `GET /session/external/oidc?returnUrl=` | | a browser navigation: challenges the OpenID Connect provider. `returnUrl` must be a path of this server or an origin in `Cors:AllowedOrigins` (an empty list does **not** mean any origin here), else `400` |
| `GET /session/external-callback?returnUrl=` | | redirects to `returnUrl#access_token=…&expires_at=…&refresh_token=…&refresh_expires_at=…&user_name=…`, or `#two_factor_ticket=…&two_factor_expires_at=…&user_name=…`, `#notice=…` (account created, not usable yet) or `#error=…`. The fragment never reaches a server or a log; no cookie is kept |

**Tokens.** `bgs.` + ASP.NET Core data protection of `{ user id, security stamp, authentication time, expiry }`, one purpose per kind (access, refresh, two-factor ticket, so one cannot
stand for another); nothing is stored. Lifetimes: access `Accounts:SessionTokenLifetime` (15 min), refresh `Accounts:SessionRefreshLifetime` (14 days; a refresh issues a new one, so an active
client stays signed in), two-factor ticket 5 minutes. Send `Authorization: Bearer <accessToken>`; renew with `refresh` before `expiresAt`; keep the tokens in memory (or a secure store),
never in a URL. In a cluster every node needs the shared data protection key ring ([high availability](high-availability.md)).

**Revocation.** Every use re-reads the account: deleted, awaiting approval, locked out, or a different security stamp means `401`. Changing the password, enabling or resetting two-factor, or
`sign-out everywhere` change the stamp. When the **current** session itself caused the change (opening the two-factor screen generates the authenticator key; changing the password), the answer carries a
renewed pair in the headers `X-Session-Access-Token`, `X-Session-Access-Expires`, `X-Session-Refresh-Token`, `X-Session-Refresh-Expires` (exposed to CORS): replace the tokens you hold. Every
other session of the account ends.

**Hosting.** `Cors:AllowedOrigins` and `Frontend:*` are in [configuration](configuration.md#single-page-applications).

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

No twin: the external provider's challenge and callback (sign-in and re-authentication; a token client uses `/session/external/oidc`), and downloads that are
plain URLs (package files, symbol files, avatars). `UiApiParityTests` fails for any other screen and handler (GET or POST) of a view without a twin.

## OpenAPI and Scalar

`/openapi/ui.json` (this API: every twin, plain and `/api/ui/r/{repo}/`; security schemes `bearer` and `apiKey` only), `/openapi/v1.json` and
`/openapi/protocols.json`, viewer at `/scalar`; administrators only (everyone else `404`). See [testing the APIs with Scalar](scalar.md).

## Architecture

| Project | Holds |
|---|---|
| `BaGet.Identity.Application`, `BaGet.Web.Application` | Use cases (`<Area>UseCase`), the DTOs (the JSON `model`), business services. No MVC or Razor reference (`ApplicationLayeringTests`) |
| `BaGet.Identity.Api`, `BaGet.Web.V3` | The `[UiApi]` twins (`Controllers/UI`) and the other web APIs; no view controller, no view, no reference to a front end (`ApplicationLayeringTests`) |
| `BaGet.Api.Base` | What twins and screens share: the twin bases (`UiApiControllerBase` family), `[UiApi]`, `[RepositoryPage]`, `IScreenController`/`ScreenEndpoints`, `UiApiSecurityFilter`, `RequestPrincipalResolver` |
| `BaGet.Identity.FrontEnd`, `BaGet.Web.FrontEnd` | The view controllers (`Controllers/FrontEnd`), which bind the form, call the twin in process (`UiApiInvoker`) and render, and their views; `AddBaGetFrontEnd()` registers them |
| `BaGet.Frontend.Base` | MVC base of the screens: `UiControllerBase` family, `UiApiInvoker`, `RepositoryUrlHelper`, `NavLinkTagHelper`, the layout and shared partials |

A use case takes explicit inputs (caller, form, cancellation token), returns a `ScreenResult<T>` and sees no `HttpContext`, `ModelState` or
`TempData`. The twin's filters do not run in process, so the view controller bases run the same `ScreenGate` first.
Moved types are not forwarded (`[TypeForwardedTo]`): code compiled against an older BaGet must be recompiled.

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
| JSON twin | `BaGet.Web.V3/Controllers/UI` or `BaGet.Identity.Api/Controllers/UI` | `<Feature>UiApiController`; never `[ApiController]`, a `UiControllerBase` or `[AutoValidateAntiforgeryToken]`; it may not reference the view controller, so `[UiApi]` names the screen by text |
| View controller | `BaGet.Web.FrontEnd/Controllers/FrontEnd` or `BaGet.Identity.FrontEnd/Controllers/FrontEnd`, view in the same project's `Views/<Controller>/<Action>.cshtml` | Binds the form, `Api<Twin, Model>(api => ...)`, `Render(...)`; holds no use case |

Twin bases: `UiApiControllerBase` (public), `AccountUiApiControllerBase` (override `RequiresSignIn`/`RequiresEmail`; **interactive sessions only**: 403 over HTTP to a key and to an anonymous caller),
`AdminUiApiControllerBase` (administrator's session or `admin:api` key), `AccessUiApiControllerBase` (override `AuthorizeAsync`; writes `503` while read-only; retention, replication and
repository bases build on these). Every twin is visible in the OpenAPI `ui` document (the base sets `[ApiExplorerSettings(IgnoreApi = false)]`) and answers
`ActionResult<ScreenResult<T>>` through `Respond(...)`.

```csharp
[Route("api/ui")]
[RepositoryPage(NotOnVirtual = true)]          // same values as the view controller: adds api/ui/r/{repo}/...
public sealed class StatisticsUiApiController : UiApiControllerBase
{
    [HttpGet("stats"), UiApi("Statistics", "Index")]   // view controller without the suffix + its default GET action
    public async Task<ActionResult<ScreenResult<StatisticsViewModel>>> Get(CancellationToken ct)
        => Respond(await _useCase.GetAsync(ct));
}
```

Routes: GET `api/ui/<view path>`; default POST the same path; handler `X` = `api/ui/<path>/<x-kebab-case>` with `[UiApi("X", "Index", Handler = "X")]` (the view controller without the `Controller` suffix and its default GET action name the screen; `ApiOperation(summary, "X", "Index")` takes the same two strings); route values keep the
view's names. Bodies are `[FromBody]` JSON, uploads `[FromForm]` multipart. Public helpers are `[NonAction]` or non-public. The view action becomes
`=> Render(await Api<StatisticsUiApiController, StatisticsViewModel>(api => api.Get(ct)), "Index")` and keeps every attribute and its base (whose `ScreenGate` and
antiforgery already ran: the twin's filters do not run in process).

Checklist: the parity case (page and twin carry the same, statuses correspond) in `UiApiParityTests`; a case per caller kind in `UiApiAccessTests`
(anonymous, cookie ignored without the antiforgery header, session token, key, `admin:api` key, limited key, global key, Basic); no password or key in a response; regenerate `Contracts/ui-api.txt` with
`BAGET_UPDATE_CONTRACTS=1` (never hand-merge); `ui-routes.txt`, `ui-forms.txt` and `public-api.txt` stay unchanged; a `ScreenCatalog` member and a row with its `Controller/Action` token in [UI coverage](ui-coverage.md) for
a new screen (`ScreenCatalogTests` checks that every catalog entry, `[UiApi]`, `FormatLink` and `ApiOperation` name resolves to a view action: a typo is a failing test, not a compile error).
