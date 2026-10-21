# Access control

Who may **read** a repository and who may **change** packages in it. Two layers, both required for a write:

| Layer | Decides | Where |
|---|---|---|
| Access model | Which repositories and package IDs a caller may read, deploy to, delete from, annotate, manage | this page |
| Package ownership | Which existing package IDs a user may change | [Accounts](configuration.md#accounts-package-owners-and-api-keys) |

Needs a relational database (SQLite, SQL Server, PostgreSQL, MySQL). With Azure Table there is no access model: everyone
reads, any authenticated caller writes subject to ownership, as before.

## Model

| Entity | Meaning |
|---|---|
| Group | A named set of users. Built-in: **Everyone** (every caller, anonymous included) and **Authenticated** (every caller with valid credentials); their membership is implicit |
| Permission target | Repository patterns (names or `*` globs), package-ID include patterns (default `*`) and exclude patterns, optionally owned by a project |
| Grant | A user, group or organization and the actions it holds through a target |
| Project | Groups repositories (`Repository.ProjectKey`); members hold a role: *Viewer* = Read; *Developer* = Read, Annotate, Deploy; *Admin* = all |
| Audit entry | One row per change to any of the above, and per *Anonymous read* switch |

| Action | Allows |
|---|---|
| `Read` | Feed, symbols, catalog, registrations, package pages of the repository |
| `Annotate` | Deprecation and vulnerabilities |
| `Deploy` | Push new packages and versions |
| `Delete` | Unlist, relist, delete |
| `Manage` | Owners of a package ID |

Actions are independent: `Deploy` does not imply `Read`. A caller holds an action when **any** applicable grant carries it.
Server administrators and the global API key hold every action everywhere. Include/exclude patterns narrow package-level
checks only: a repository-level question (service index, search) is allowed when some package could match.

## Upgrade defaults

The migration seeds what the server did before the model, so nothing changes until an administrator edits it:

| Target | Repositories | Principal | Actions |
|---|---|---|---|
| Anyone reads every repository | `*` | Everyone | Read |
| Authenticated users deploy | `*` | Authenticated | Annotate, Deploy, Delete, Manage |

Ownership still applies inside Deploy, Delete, Annotate and Manage. Both targets can be deleted and restored
(`IAccessAdministration.RestoreBuiltInsAsync`).

## Anonymous read

`Repository.AnonymousRead` (the *Anonymous read* switch of `/admin/repositories/{name}`) gates the **Everyone** group's
`Read` on that repository:

| Switch | Effect |
|---|---|
| on (default) | Everyone's Read grants apply. If no target grants Everyone Read on the repository any more, switching on creates the target `Anonymous read {name}` |
| off | Everyone's Read does not apply there; callers need a Read grant of their own (user, group, organization or project role) |

## What a client sees

| Caller | Private repository |
|---|---|
| Anonymous | `401` with `WWW-Authenticate: Basic realm="BaGet"` on every protocol route (`/` and `/r/{repo}/`; GET and HEAD) |
| Signed in, no Read | `404`, as for an unknown repository or package |
| With Read | served |

Pages follow the same check: an anonymous visitor is sent to sign in, a signed-in user without Read gets `404`, and the
repository switcher and search lists only show readable repositories. Search results are filtered per repository, not
per package: a Read grant narrowed by include/exclude patterns protects the per-package routes (registration, content),
but `search`, `autocomplete` and the V2 listings of a repository the caller may read show all its packages.

## Client setup

The password is an **API key** (an account's key, or the global `ApiKey`); the user name is the key's owner (any name for
the global key).

| Client | Setup |
|---|---|
| `dotnet` / NuGet.exe | `dotnet nuget add source https://host/r/second/v3/index.json -n second -u alice -p <key> --store-password-in-clear-text`; on Linux/macOS clear text is required |
| `NuGet.Config` | `<packageSourceCredentials><second><add key="Username" value="alice" /><add key="ClearTextPassword" value="<key>" /></second></packageSourceCredentials>` |
| Visual Studio | Sends the 401 challenge's credentials from *Tools > Options > NuGet Package Manager > Package Sources*, or prompts |
| PowerShellGet | `Register-PSRepository -Name second -SourceLocation <v2 url> -Credential (Get-Credential)`, and `-Credential` on `Install-Module` |
| Symbols (Visual Studio) | Add the same credentials to the symbol server entry (`/api/download/symbols`) |

## Request credentials

Reads accept, in order: `Authorization: Basic` (password = API key), `X-NuGet-ApiKey`, then the browser session.
Credentials that do not authenticate count as none: an open repository stays readable, a private one answers `401`.
Writes are unchanged: `X-NuGet-ApiKey`, authorized per package (`401` no key, `403` not permitted).

## Cost and cache

The model is held in memory as a snapshot, rebuilt after any committed change to it (`AccessModelSaveChangesInterceptor`
bumps `AccessModelVersion`) and at most every 30 s (other instances catch up within that time). A check against
50 targets and 20 groups costs a few microseconds (`AccessEvaluatorTests`); a user's group and organization membership
is loaded on first use and kept with the snapshot.
