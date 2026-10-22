# UI coverage

Every operation the server offers (REST, CLI, service) is reachable from the web UI, with the gate of the operation.
This page maps each capability to its page. Pages in repository scope also exist as `/r/{name}/...`. "Admin" = an
administrator signed in (others get 404). "Password" = the administrator's password (or a fresh external sign-in) is
asked again. Every write calls `MaintenanceGate.ThrowIfClosed` (503 while closed) and, on package pages, refuses in
`IsReadOnlyMode`. Tests: `UiCoverageTests` and the page test of each area.

## NuGet protocol (per repository)

| Capability | Reached by | UI | Gate |
|---|---|---|---|
| Service index `v3/index.json` | REST | Admin Repositories / RepositoryEdit (feed URL + copy); Upload page (feed URLs) | read |
| Search, autocomplete | REST | Home and `/` search (filters, sort, paging); type-ahead on both search boxes | read |
| Package page, versions, dependents, readme, icon, download | REST | `/packages/{id}/{version}`, download link | read |
| Registration, flat container, `.nuspec` | REST | machine-only; the package page links the downloads | read |
| Push `.nupkg` | REST (`PUT api/v2/package`) | `/upload` (stage, review, publish) | Push |
| Push `.snupkg` / symbols | REST (`api/v2/symbol`) | `/upload` (symbol package) | Push |
| Symbol download | REST | machine-only (debugger); symbols listed and deleted on Manage | read |
| Unlist / relist / delete | REST (`DELETE`, `POST`) | Manage: Unlist, Relist, Delete; permanent delete asks Password | Manage (owner) |
| Deprecation (reasons, message, alternate ID and version range) | REST | Manage: Deprecate / Remove | Manage |
| Vulnerabilities | REST | Manage: add / remove | Manage |
| V2 feed | REST (`api/v2`) | Repositories: "V2 feed" link | read |
| Vulnerability index | REST | Repositories: "Vulnerabilities" link | read |
| Catalog index, pages, leaves | REST | Repositories: "Catalog" link (local repositories) | read |
| Catalog rebuild | CLI `catalog rebuild -r` | Admin Operations | Admin, audited |
| Repository signatures, certificate | REST | Repositories: "Signatures" link (local repositories) | read |
| Validation status of a submission | REST (`v3/package/{id}/{v}/validation`) | `/packages/{id}/validation`, linked from Manage | Push or Manage |
| Import downloads | CLI `import downloads -r` | Admin Operations | Admin, audited |
| Report abuse | page | `/packages/{id}/{v}/report-abuse` | read |

## npm registry (per repository, `npm/`)

| Capability | Reached by | UI | Gate |
|---|---|---|---|
| Search `-/v1/search` | REST | `/npm-packages` | read |
| Packument, tarball | REST | `/npm-packages/{name}` (versions, tags, downloads) | read |
| Publish | REST (`PUT {name}`) | `/npm-upload` (file, tag) | Push |
| Dist-tags set / remove | REST | NpmManage | Manage |
| Deprecate / undeprecate a version | REST | NpmManage | Manage |
| Unpublish a version / all | REST (`DELETE`) | NpmManage | Manage + Password |
| Owners | REST | NpmManage | Manage |
| Login / logout (tokens) | REST (`npm login`, `-/user/token`) | Account API keys (create, list, revoke; npm tokens are keys limited to a repository) | signed in |
| Ping, whoami, audit | REST | stateless handshakes; whoami is the signed-in profile, audit answers "none" | none |
| Web login `-/v1/login` | REST | not offered (answers 404, npm falls back to user and password) | - |

## Maven repository (per repository, `maven/`)

| Capability | Reached by | UI | Gate |
|---|---|---|---|
| Read file, checksum, `maven-metadata.xml` | REST (`GET`/`HEAD`) | `/maven-artifacts` (browse, search), `/maven-artifacts/{group}/{artifact}/{version}` (files with sizes and checksums, POM view, download links) | read |
| Deploy (`mvn deploy`, `PUT`) | REST | `/maven-upload` (coordinates, file, optional POM), `settings.xml` and `distributionManagement` snippets on browse and upload | Deploy |
| Delete a file | REST (`DELETE`) | MavenManage | Manage + Password |
| Delete a version / the artifact | service | MavenManage | Manage + Password |
| Snapshot clean-up (keep newest N builds) | service | MavenManage | Manage + Password |
| Owners | service | MavenManage | Manage |
| Policy, redeploy, snapshot builds kept | service | RepositoryEdit: Maven settings (audited) | Admin |
| Remote Maven repository: create (URL, credentials, periods, offline, store locally) | service | Repositories > *New remote repository* (format Maven) | Admin + Password |
| Remote Maven: edit upstream, *Test connection*, cache table, *Purge cache*, *Purge package* (`group:artifact`, version text) | service | RepositoryEdit (`/admin/repositories/{name}`) | Admin + Password |
| Remote Maven: read through (`GET`/`HEAD` of files, checksums, metadata) | REST | `/maven-artifacts` (the stored artifacts), snippets incl. the Central `<mirror>` | read |
| Virtual Maven repository: create, members, order, deploy repository, *Where would an artifact come from?* | service | Repositories > *New virtual repository* (format Maven); RepositoryEdit members and preview (`group:artifact`, any version) | Admin |
| Dependency / `settings.xml` / `<repositories>` / Gradle snippets | machine-only config | artifact and browse pages | read |

## Docker registry (per repository, `/v2/{repo}/`)

| Capability | Reached by | UI | Gate |
|---|---|---|---|
| Version check `GET /v2/` | REST | machine-only handshake (the browse page shows the registry address and `docker login`) | none (401 challenge) |
| Token service `GET /v2/token` (`docker login`) | REST | machine-only; Account API keys creates the key used as the password; the browse page shows the login, `curl` and daemon snippets | API key / anonymous |
| Pull: blobs (ranges), manifests, tag list, referrers | REST | `/docker-images` (browse, search), `/docker-images/{image}` (tags, manifests, platforms of an index), `?digest=` (config, layers with sizes, annotations, the document) | Read |
| Push: upload sessions (monolithic, chunked, mount), manifests | REST | machine-only (Docker CLI); the browse and image pages show the push, `buildx` and pull commands | Deploy |
| Delete a tag | REST (`DELETE manifests/{tag}`) | DockerManage | Delete + Password |
| Delete a manifest (with its tags) | REST (`DELETE manifests/{digest}`) | DockerManage | Delete + Password |
| Delete a blob from an image | REST (`DELETE blobs/{digest}`) | covered by *Remove unused layers* (a blob in use is refused) | Delete |
| Remove unused layers | service | DockerManage | Delete + Password |
| Delete the whole image | service | DockerManage | Delete + Password |
| Repository: create, list, anonymous read, delete | service | Repositories (Docker create form, Browse link), RepositoryEdit, RepositoryDelete | Admin |
| Remote Docker repository: create (URL, user name, password or token, periods, offline, store locally) | service | Repositories > *New remote repository* (format Docker) | Admin + Password |
| Remote Docker: edit upstream, *Test connection* (version check through the token flow), cache table (manifests, images, layer bytes), *Purge cache*, *Purge package* (image, optional tag or digest) | service | RepositoryEdit (`/admin/repositories/{name}`) | Admin + Password |
| Remote Docker: pull through (`GET`/`HEAD` of manifests, layers, tags) | REST | `/docker-images` (the cached images, pull commands), `/docker-images/{image}` | read |
| Virtual Docker repository: create, members, order, deploy repository, *Where would an image come from?* (image, tag or digest) | service | Repositories > *New virtual repository* (format Docker); RepositoryEdit members and preview | Admin |
| Virtual Docker: pull from the first member, merged tags, push to the deploy repository | REST | `/docker-images` (the members' images merged), `/docker-images/{image}` | read / Deploy |
| Docker garbage collection of a repository (dry run, run) | service (`IOciGarbageCollector`) | Admin Operations: *Docker garbage collection: dry run* and *Docker garbage collection* per local or remote Docker repository | Admin, audited |

## Repositories (Admin)

| Capability | Reached by | UI | Gate |
|---|---|---|---|
| List, counts, feed URL, links per format | service | Admin Repositories | Admin |
| Create local NuGet / npm / Maven / Docker | service | Repositories create forms | Admin |
| Create remote (URL, credentials, cache periods, offline) | service | Repositories remote form, RepositoryEdit | Admin + Password |
| Create virtual, members, order, upload target | service | Repositories virtual form, RepositoryEdit | Admin |
| Virtual preview, remote connection test | service | RepositoryEdit: Preview, Test | Admin (+ Password for test) |
| Purge remote cache / one package | service | RepositoryEdit | Admin + Password |
| Anonymous read, delete | service | RepositoryEdit, RepositoryDelete | Admin |
| Enable formats | settings | Admin Formats | Admin |

## Access model (Admin)

| Capability | Reached by | UI | Gate |
|---|---|---|---|
| Groups: create, rename, delete, members | `IAccessAdministration` | Groups, GroupDetail | Admin + Password |
| Permission targets: create, edit, delete, grants, restore built-ins | service | Permissions, PermissionDetail | Admin + Password |
| Preview ("who can do what") | service | Permissions / PermissionDetail / ProjectDetail: Preview; Account Access (own access) | Admin |
| Projects: create, members, repositories, delete | service | Projects, ProjectDetail | Admin + Password |
| Audit trail (filter actor, subject, dates) | service | Audit | Admin |

## Accounts, organizations, owners

| Capability | Reached by | UI | Gate |
|---|---|---|---|
| Register, confirm email, sign in (password, 2FA, recovery code, external) | pages | Account pages | anonymous |
| Forgot / reset password | pages | ForgotPassword, ResetPassword | anonymous |
| Change password / email, delete account | service | Account Settings | signed in + Password |
| Two-factor: enable, disable, reset | service | TwoFactor | signed in + Password |
| API keys: scopes, glob, expiry, repositories; revoke | service | Account API keys | signed in |
| Organizations: create, members | service | Account Organizations | signed in |
| Organizations: rename, delete, admin flag | service | Admin Organizations, OrganizationDetail | Admin + Password |
| Package owners add / remove, verified flag | service | Manage (verified: Admin) | Manage |
| Contact owners | service | `/packages/{id}/contact-owners` | signed in, SMTP |
| Profiles, packages of an owner | pages | `/profiles/{name}`, Account Packages | read |
| Users: create, invite, promote, demote | CLI `account promote\|demote`, service | Admin Users | Admin + Password |
| Reset password, with `--email` | CLI `account reset-password [--email]` | Admin Users: Generate a password (+ optional email) | Admin + Password |
| Send reset link | service | Admin Users | Admin + Password, SMTP |
| Delete user, user's packages and keys | service | DeleteUser, UserDetail, UserPackages | Admin + Password |

## Operations and server

| Capability | Reached by | UI | Gate |
|---|---|---|---|
| Validation queue: retry, release, reject | service | Admin Validation | Admin (+ Password) |
| Abuse reports: resolve, reopen | service | Admin Abuse; Manage for the owner | Admin / Manage |
| Settings (every options section), restart-required marks | `appsettings.Overrides.json` | Admin Settings | Admin + Password |
| Database copy (copy all data, status) | service | Admin Settings (database section) | Admin + Password |
| Telemetry settings, status, connection test | service | Admin Settings (telemetry section) | Admin + Password |
| Statistics | service | `/statistics` | read |
| Read-only gate banner | MaintenanceGate | every page | - |

## Not in the UI, on purpose

| Item | Why |
|---|---|
| `--urls`, host arguments | process start, not a runtime operation |
| Registration, flat container, symbol download, tarball bodies | machine formats; the pages link the downloads |
| External login link / unlink | accounts are created by the first external sign-in; there is no link-later operation on the server |
