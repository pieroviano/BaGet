# DevOps manual

Build, release, deploy, run and monitor BaGet Extension. Details are in the linked pages.

## 1. Build and test

| Task | Command |
|---|---|
| Build | `dotnet build BaGet.sln -p:GeneratePackageOnBuild=false` |
| Test | `dotnet test BaGet.sln -p:GeneratePackageOnBuild=false` |
| Publish the host | `dotnet publish src/BaGet -c Release -o artifacts -p:GeneratePackageOnBuild=false` |
| Local wrapper | `build.ps1` (clean, restore, build; `-Run`, `-Publish`) |

- Always pass `-p:GeneratePackageOnBuild=false`: otherwise a build packs into `Packages\`, a shared NuGet feed.
- Create the `Packages` folder on a clean checkout (`mkdir Packages`), or restore fails with `NU1301`.
- Needs the .NET 10 SDK. Tests are xUnit v3 on Microsoft.Testing.Platform.
- Projects `BaGet.Clients.Tests`, `BaGet.Storage.Tests`, `BaGet.Database.Tests`, `BaGet.Telemetry.Tests` and `BaGet.Ha.Tests` use Docker (Testcontainers) and skip without it.

## 2. CI and release (`.github/workflows`)

| Workflow | Trigger | Does |
|---|---|---|
| `main.yml` (Build) | push / pull request on `main`, manual | Build and test on Ubuntu |
| `docs.yml` (Docs) | push on `main` | `mkdocs build --strict`, deploys to GitHub Pages |
| `release.yml` (Release) | manual; inputs `releaseVersion`, `packageSource` | `verify` (tests), then in parallel: `BaGet` zip artifact, NuGet packages pushed with `--skip-duplicate`, Docker image to Docker Hub |

| Secret / variable | Used for |
|---|---|
| `NUGET_API_KEY` | Pushing packages to the package source |
| `DOCKER_USERNAME`, `DOCKER_PASSWORD` | Docker Hub login |
| `DOCKER_IMAGE` (variable) | Image name; default `<DOCKER_USERNAME>/baget` |

Pages must use the "GitHub Actions" source for the docs site.

## 3. Deploy

| Target | Page |
|---|---|
| Docker (image from `Dockerfile`: `aspnet:10.0`, port 80, `dotnet BaGet.dll`) | [Docker](installation/docker.md) |
| IIS | [IIS proxy](installation/iis-proxy.md) |
| Azure, AWS, Google Cloud, Alibaba Cloud | [Azure](installation/azure.md), [AWS](installation/aws.md), [Google Cloud](installation/gcp.md), [Aliyun](installation/aliyun.md) |
| Several nodes | [High availability](high-availability.md) |

Configuration is layered: `appsettings.json`, `appsettings.Overrides.json` (written by the Settings page; path `SettingsOverridesPath`), environment variables (`Section__Key`), command line. Keep `SettingsOverridesPath` and log files on a persistent volume. Full reference: [Configuration](configuration.md).

## 4. Run

- Listens on `Kestrel:Endpoints:Http:Url` (`http://localhost:6060` by default; override with `Kestrel__Endpoints__Http__Url`). `--urls` has no effect.
- Pending migrations apply at startup (`RunMigrationsAtStartup`).
- Behind a proxy, TLS ends at the proxy; forwarded headers and path base are honoured.
- Restrict served host names, and refuse private upstream addresses for remote repositories: [Configuration](configuration.md).

## 5. Monitor

| Signal | How |
|---|---|
| Liveness | `GET /health/live` (200 while the process answers) |
| Readiness | `GET /health/ready` (200 or 503 by database and storage; Redis only reported); point the load balancer here |
| Logs | Serilog: console, `logs/baget-.log` (daily, 14 kept), Windows event log (Warning+); levels live on the Settings page. No query strings or headers are logged |
| Traces, metrics, logs | OpenTelemetry export to an OTLP endpoint (`Telemetry:*`), live on the Settings page, with a status table and a connection test: [Telemetry](configuration.md#telemetry) |
| Cluster | `/admin/cluster`: nodes, leases, gate, health |
| Background jobs | `/admin/operations`, `/admin/retention`, `/admin/replication` |

## 6. Operate

| Task | How |
|---|---|
| Read-only during maintenance | In-memory gate; writes answer `503` + `Retry-After: 60`, reads work: [Configuration](configuration.md#read-only-maintenance-gate) |
| Move to another database | Settings page, "Copy all data": verified copy, gate closed until restart: [Moving to another database](configuration.md#moving-to-another-database) |
| Clean up old versions | [Retention](retention.md), with dry run and holds |
| Mirror to another server | [Replication](replication.md) |
| Scan for vulnerabilities | [Vulnerability scanning](vulnerability-scanning.md) |
| Automate administration | [Administration REST API](admin-api.md) with an `admin:api` key |
| Account recovery | `dotnet BaGet.dll account reset-password <user>`, `account promote|demote|approve <user>` |

## 7. Back up and upgrade

- Back up the database, the package store (file system folder or bucket), `appsettings.Overrides.json` and, in a cluster, the certificate in `Ha:KeyCertificatePath`.
- Upgrade: back up, deploy the new build or image, restart; migrations apply at startup. In a cluster, roll nodes one at a time behind `/health/ready`.
- After a database copy, restart every node.
- **Upgrade note (Identity tables renamed).** Migration `RenameAspNetTables` renames `AspNetUsers`, `AspNetRoles`, `AspNetUserRoles`, `AspNetUserClaims`, `AspNetUserLogins`, `AspNetUserTokens` and `AspNetRoleClaims` to `Users`, `Roles`, `UserRoles`, `UserClaims`, `UserLogins`, `UserTokens` and `RoleClaims` (keys, indexes and foreign keys follow). Back up first; external SQL, reports or views that name the old tables must be updated, and during a rolling upgrade of a cluster old nodes cannot run against the renamed tables. On SQLite the foreign keys are rebuilt outside a transaction (EF warns at startup): an interrupted migration needs the backup.
- **Upgrade note (page models removed).** The `[Obsolete]` Razor Page models of `BaGet.Identity` and `BaGet.Web.V3` (`LoginModel`, `UsersModel`, `IndexModel`, `AccountPageModel`, ...), `RepositoryPageFilter` and `RepositoryPageRouteConvention` no longer exist: code of a host that referenced them must move to the controllers, workflows and `RepositoryActionFilter`. URLs, forms, `?handler=` values and `asp-page` names are unchanged.
- **Upgrade note (ownership foreign keys).** The migration `AddOwnershipForeignKeys` adds foreign keys from repository, project, user and replication-configuration references to their rows. Before it does, it **deletes the rows that already pointed at nothing** (for example packages or API keys of a repository or user that no longer exists, and everything under them) and clears references to missing projects; such rows were invisible to the application. Back up first if you want to inspect them. From then on the database refuses an orphan, and deleting a repository, project or user is done by the application, which removes the dependents first.
  - **Rolling upgrade (cluster):** until every node runs the new build, do not delete an image, a project or a repository: the old code relies on cascades and cleanup the foreign keys now forbid (the delete fails with a constraint error, nothing is lost).
  - **Large databases:** the cleanup and the foreign-key validation scan whole tables. Migrations run with the provider's command timeout (30 s by default on SQL Server, PostgreSQL and MySQL); raise it in the connection string (`Command Timeout=600` for SQL Server and PostgreSQL, `Default Command Timeout=600` for MySQL) for the upgrade start, or apply the migrations with `dotnet ef database update` from a shell with such a connection string.
  - **MySQL DDL is not transactional:** if the migration fails half way, the foreign keys already added stay. Fix the cause, then drop the `FK_*_Repositories_RepositoryKey`, `FK_*_Projects_ProjectKey`, `FK_*_AspNetUsers_UserId`, `FK_ReplicationRuns_*`, `FK_ReplicationItems_*` and `FK_OciTags_OciRepositories_ImageKey` constraints that exist and start BaGet again (or restore the backup); the cleanup is repeatable. SQL Server, PostgreSQL and SQLite roll the whole migration back.
  - A database whose `default` repository row is missing (migrated by an early build of `AddRepositories` and never started) gets the row created first; rows of repository 1 are never treated as orphans.

## 8. High availability checklist

Shared SQL Server, PostgreSQL or MySQL; shared object storage (not the file system); Redis; `Ha:Enabled=true` with `Ha:ConnectionString`; the same `Ha:KeyPrefix` on every node; `Ha:KeyCertificatePath` set so data protection keys are encrypted. A node that breaks a rule does not start ([High availability](high-availability.md)).
