# Package formats

A repository has a **format**. NuGet and npm (local repositories, see [npm registry](npm-registry.md)) have a protocol today; Maven and
Docker follow. `/admin/formats` (administrators) lists what this server can host.

| Format | Served under | Feed address | Repository kinds | Storage folders | Status |
|---|---|---|---|---|---|
| NuGet | the repository base (`/v3/…`, `/api/v2`, symbols) | `v3/index.json` | local, remote, virtual | `packages` `symbols` `catalog` `validation` `staging` | done |
| npm | `npm/` | `npm/` | local, remote, virtual | `npm` | done ([details](npm-registry.md)) |
| Maven, Docker | | | | | not started |

The *repository base* is `/` for `default` and `/r/{name}/` for the others, so an npm repository `web` is at `/r/web/npm/`. A
route belongs to one format: asking a NuGet route of an npm repository (or an npm route of `default`, which is NuGet) is `404`.

## Module contract (`IFormatModule`, `BaGet.Core/Formats`)

A module only **describes** its format. Register it with `TryAddEnumerable(ServiceDescriptor.Singleton<IFormatModule, X>())`;
`FormatRegistry` refuses two modules for one format, a shared route prefix, a shared storage folder, or a claim on `repos`.

| Member | Used for |
|---|---|
| `Format`, `DisplayName` | identity |
| `RoutePrefix`, `FeedPath` | where its routes start; the address clients are given (admin pages) |
| `CreatableKinds` | what `IRepositoryService.CreateAsync` accepts (NuGet and npm: local, remote) |
| `SupportsVirtual` | whether virtual repositories of the format exist (`IVirtualRepositoryAdministration.CreateAsync(..., format)`, created with their members; the format's reads must merge and its writes move to the deploy repository) |
| `StorageFolders` | folders it owns below `RepositoryStorage.Prefix` |
| `CreateFields`, `CreateSettings` | extra inputs of the create form; turned into `Repository.Settings` (`ArgumentException` = message shown) |
| `RepositoryLinks` | links on the Repositories page (NuGet: Browse, Upload) |
| `CountArtifactsAsync`, `GetUsageAsync`, `StagePurgeAsync` | package counts; what blocks deleting a repository; rows removed with it |

What a format does per request is a scoped service beside the module, found with `provider.GetFormatService<T>(format)`
(null = not offered):

| Service | Phase | NuGet |
|---|---|---|
| `IFormatIndexer` (deploy) | npm P3 | `NuGetFormatIndexer` over `IPackageIndexingService`; npm has none (publishing needs the caller, so it is `INpmRegistry.PublishAsync`) |
| `IFormatSearchProvider` | npm P3 | `NuGetFormatSearchProvider` over `ISearchService`; `NpmFormatSearchProvider` over `INpmRegistry.SearchAsync` |
| `IFormatRetentionHooks` | P6 | not yet |
| `IFormatScanner` | P7 | not yet |

Routes go through `BaGetEndpointBuilder.MapRoute(..., format: module.Format)` with a pattern starting `"{RoutePrefix}/…"`; it
registers the default and `r/{repo}/` variants and tags the endpoint (`RepositoryRouteMetadata.Format`). Unmarked endpoints are
NuGet's. **Razor Pages** are served under `/r/{repo}/` for every repository and decide per page: `RepositoryPageRouteConvention.PageFormats`
names the format of a page (NuGet unless listed; the npm pages `/NpmPackages`, `/NpmPackage`, `/NpmUpload`, `/Packages/NpmManage`),
and `RepositoryPageFilter` answers 404 for a page of another format, except that the NuGet front page of a repository of another format
redirects to the module's `browse` link. Widened with npm: the upload picker, header switcher, My packages, API-key repository
limits and the access-model listings now cover every format (the `Format == NuGet` filters are gone). A virtual repository
is of one format and so are its members; a remote repository speaks the format's own upstream protocol (`RemoteProtocol`:
NuGet V3/V2, `Npm`), set from the format. Azure Table still serves `default` only.

## Data model (B1)

npm tables exist (migration `AddNpmTables` on SQLite, SQL Server, PostgreSQL, MySQL; no pending model change on any):

| Table | Columns | Keys |
|---|---|---|
| `NpmPackages` (repository-scoped) | `Name` (lower case, scope kept, 214), `Created`, `Modified` | unique `(RepositoryKey, Name)` |
| `NpmVersions` | `PackageKey`, `Version` (256), `Integrity` (sha512 SRI), `Shasum` (sha1 hex), `TarballSize`, `Manifest` (JSON, unbounded), `Deprecated`, `Published` | unique `(PackageKey, Version)`; cascade from package |
| `NpmDistTags` | `PackageKey`, `Tag` (128), `Version` | unique `(PackageKey, Tag)`; cascade; no FK to the version |

The packument is built from the manifests, so the registry answers what was published (without the tarball address, built per request).
Tarballs live in storage, not in rows. The npm registry needed no further schema change: its migration is `AddNpmTables`.
Package owners are the common `PackageOwner` rows (repository-scoped, keyed by the lower-case npm name).

Sketches for later phases (not built):

| Format | Tables | Notes |
|---|---|---|
| Maven | `MavenArtifacts` (repo, `GroupId`, `ArtifactId`), `MavenVersions` (version, snapshot flag, timestamp+build for snapshots), `MavenFiles` (name, classifier, extension, size, sha1/sha256/md5) | `maven-metadata.xml` is generated from versions, never stored; unique `(Repo, Group, Artifact)`, `(Artifact, Version)`, `(Version, FileName)`; GAV stays a string (index length on MySQL) |
| OCI | `OciRepositories` (repo, image path), `OciManifests` (digest, media type, size, `subject` digest for referrers), `OciTags` (tag to manifest digest, unique per image), `OciBlobs` (digest unique per registry, size, refcount), manifest-to-blob link table | blobs by digest, deduplicated across images; digests are `sha256:` + 64 hex (71); refcount drives garbage collection (P6) |

## Client authentication (B3)

| Client | Flow | Verified |
|---|---|---|
| npm | `//host/:_authToken=T` in `.npmrc`; every request after carries `Authorization: Bearer T`. Nothing is sent unprompted without a token; a `401` is final (`E401`, no retry). `npm whoami` is `GET /-/whoami`. `npm login` tries `POST /-/v1/login` (web login); on `404` it falls back to `PUT /-/user/org.couchdb.user:NAME` with `{_id,name,password,type:"user",roles,date}` and expects `{ok,id,rev,token}`; the token is then stored as `_authToken`. The registry therefore needs **bearer tokens** (access tokens) and the CouchDB user call | real npm 10.9.9, `NpmClientFlowTests` |
| Docker | `GET /v2/` answers `401` + `WWW-Authenticate: Bearer realm="…/token",service="…"`; `docker login` then sends `GET {realm}?account=U&client_id=docker&offline_token=true&service=S` with **Basic** (user and password or token), takes `{token,expires_in}` and retries `/v2/` with `Bearer`; pull and push ask `{realm}?scope=repository:NAME:pull,push&service=S` first. Plain HTTP only for `localhost` or an insecure registry. API keys do not fit; P5 adds the token endpoint | real Docker 27.5 in `docker:27-dind`, manual probe (not in the suite) |
| Maven | HTTP Basic from `settings.xml` (`<server><id>` matching the repository id; user, password or token). Maven sends credentials **after a `401`** with `WWW-Authenticate: Basic`; deploy (`PUT`) also challenges first. `401` with a Basic challenge is enough | documented from Maven's behaviour; verify with the `maven` image in P4 |

## Client test rigs (B6)

`tests/BaGet.Clients.Tests`: `ClientContainers.NodeAsync()` starts `node:22-alpine` once (idle, `sleep infinity`) and runs scripts in
it with an empty `$HOME`. `HostRegistry` is a real Kestrel server on `0.0.0.0` that records requests; the container reaches it as
`host.docker.internal:{port}` (an extra host `host-gateway`, so Linux works too). `WebApplicationFactory` has no socket, so P3-npm
hosts the real application on such a server for the CLI tests. The tests skip without a Docker daemon (as `BaGet.Database.Tests`).

`docker:dind`: **feasible** on Docker Desktop (WSL2): `docker:27-dind` with `--privileged`, `DOCKER_TLS_CERTDIR=` and
`--insecure-registry=<registry host:port>` reaches `docker info` in ~25 s and pulls from Docker Hub. A CI runner must allow privileged
containers; the P5 tests skip when it does not. Maven: the `maven` image needs no privilege.
