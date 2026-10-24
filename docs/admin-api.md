# Administration REST API

`/api/v1` does what the administrator pages do, for scripts. One operation, one page: every endpoint is listed in
[UI coverage](ui-coverage.md) with its page, and the pages and the API call the same services, so rules cannot differ.
The OpenAPI 3 document is `GET /api/v1/openapi.json`; the signed-in list is the **REST API** page (`/admin/api`).

## Authentication

| Credential | Accepted when |
|---|---|
| API key as `Authorization: Bearer <key>`, as the Basic password, or `X-NuGet-ApiKey` | owner is an **administrator** (checked on every call), key has scope **`admin:api`**, key is not limited to repositories |
| The global `ApiKey` | one is configured (an open server with no key never admits anyone) |

- Create the key on **API keys** (the scope is offered to administrators only; demoting the owner disables the key at once).
- The browser session is **never** accepted, so a web page cannot drive the API through a signed-in browser.
- No credentials: `401` + `WWW-Authenticate: Bearer`. Valid but not allowed: `403`.
- `admin:api` is not in the scopes sessions and npm tokens receive.

## Confirmation

Where the page asks for the password again (**Confirmation: password** in the table), send the key owner's password in
`X-Confirm-Password`. Missing or wrong: `403` `confirmation-required` (a wrong one counts as a failed sign-in and can lock the
account out). An owner with no password: `403` `confirmation-unavailable`. The global key is its own confirmation.

| Page asks | API asks |
|---|---|
| remote repository create / save / test / purge, anonymous read of a remote repository | yes |
| access changes: grants, patterns, memberships, deletes; user create/promote/demote/group/key revoke | yes |
| retention policy create / save / run / delete, lift a hold; replication create / save / test / run / resume / delete; scanning policy; operation that deletes (`RequiresConfirmation`) | yes |
| create empty group / target / project, rename group, virtual members, repository delete, dry run, place a hold, start a scan | no (as the pages) |

## Conventions

| | |
|---|---|
| Errors | `application/problem+json`: `status`, `title`, `detail`, stable `code` (`not-found`, `invalid-request`, `conflict`, `confirmation-required`, `read-only`, `not-supported`, `unauthorized`, `forbidden`) |
| Status | `400` invalid input, `404` unknown, `409` state forbids it (repository not empty, last administrator, already running), `422` not supported here (no access model on Azure Table), `503` + `Retry-After` while the server is read-only (writes only) |
| Lists | `?page=1&pageSize=50` (max 200) answer `{items, page, pageSize, total}` |
| Names | repositories, groups, users… by name in the path; principals by name in bodies |
| Enums | by name (`"Remote"`, `"Read"`) |
| Created | `201` + `Location`; started jobs `202` (poll the status endpoint) |

## Resources

| Path | What |
|---|---|
| `/repositories`, `/formats` | list, create (local/remote/virtual, any format), anonymous read, delete; `/{name}/remote` (upstream, `test`, `cache`, `purge`, `purge-package`), `/{name}/virtual` (members, `preview`), `/{name}/access` |
| `/projects`, `/groups`, `/permission-targets`, `/audit` | the access model; `/permission-targets/preview` |
| `/users` | list, create, administrator flag, groups, keys, effective access |
| `/operations` | list, start, status (catalog rebuild, downloads import, Docker GC, vulnerability scan…) |
| `/retention` | `policies` (CRUD, `dry-run`, `run`), `holds` |
| `/replication` | `configs` (CRUD, `run`, `resume`, `stop`, `runs`), `test`, `runs/{key}`, `inventory/{repository}` (what a peer calls) |
| `/scanning` | overview, per-repository dashboard / results / policy / scan, per-project policy |

## Not offered over the API

| Left on the pages | Why |
|---|---|
| password reset, invitation mails, account deletion, organizations, validation queue, abuse reports, server settings | mail or destroy on someone's behalf, or carry secrets; use the pages |
| scanning one version | on the package page (needs Manage on the package, not a server administrator) |
| a project administrator's delegated pages | the API admits server administrators only |
| replication | arrives with its phase |

## Example

```sh
curl -H "Authorization: Bearer $KEY" https://baget.example/api/v1/repositories?kind=Remote
curl -X POST -H "Authorization: Bearer $KEY" -H "X-Confirm-Password: $PASSWORD" -H "Content-Type: application/json" \
  -d '{"name":"npmjs","format":"Npm","kind":"Remote","remote":{"url":"https://registry.npmjs.org/"}}' \
  https://baget.example/api/v1/repositories
```

Routes are mapped by `BaGetEndpointBuilder.MapAdminApiRoutes`; a host that maps its own endpoints must call it.
