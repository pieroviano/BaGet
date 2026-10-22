# Maven repositories

A local **Maven** repository is a Maven 2 repository: `mvn deploy`, dependency resolution, `maven-metadata.xml`, checksums, SNAPSHOT
builds, a release/snapshot policy. Gradle's `maven-publish` and `maven { url }` use the same protocol. Remote (Maven Central proxy)
and virtual Maven repositories are not built yet.

## Create and use

| Step | How |
|---|---|
| Create | Administrators: `/admin/repositories` > *Maven* form (name, versions accepted, redeploy, snapshot builds kept; *Anonymous read*). Refused on Azure Table / Azure Search (default repository only) |
| Address | `https://host/r/{name}/maven/` (shown on the repository list and the browse page). `/maven/` of the root is never served: `default` is NuGet |
| Credentials | HTTP Basic: Maven sends the `settings.xml` `<server>` after a `401` challenge. Password = a user API key (user name = its owner) or the global `ApiKey` (any user name) |
| Deploy | `mvn deploy` with `<distributionManagement>` (snippets on the browse and upload pages), or `mvn deploy:deploy-file` |
| Resolve | `<repositories>` entry with the same `<id>` as the `<server>`; `mvn dependency:copy-dependencies`, `dependency:get`, Gradle |

Maven 3.8.1+ **blocks plain-http repositories** (a built-in mirror `maven-default-http-blocker`): serve BaGet over https, or override that mirror
in `settings.xml`.

## Protocol

| Request | Answer |
|---|---|
| `GET`/`HEAD` file | the stored file, `X-Checksum-Sha1/Md5/Sha256` headers |
| `GET` `…/{file}.sha1` `.md5` `.sha256` `.sha512` | computed once on the way in, kept in the file's row |
| `GET` `maven-metadata.xml` (group, artifact, version level) | generated from the rows, never stored; SNAPSHOT versions list the timestamped builds (`timestamp`, `buildNumber`, `snapshotVersions`) |
| `PUT` file | stored; checksum and metadata uploads are acknowledged (they are generated); `201`. A snapshot build is stored under its timestamped name and the build number advances |
| `DELETE` file | one file with its checksums (`204`); not Maven's protocol, but what scripts use |
| Refused | `409` policy refuses the version, or a release file already exists (unless *Redeploy* is on and the caller holds Delete); `400` name outside the layout; `413` over `Maven:MaxFileBytes` (1 GiB); `403` read-only mode |

A `.pom` deployed to a version is parsed (name, description, URL, licences, packaging, dependencies) for the pages and search; a POM that
does not parse still stores. Layout rules (`MavenNames`): ids use Maven's characters only, so a path never leaves its folder.

## Policy (per repository, *Edit repository > Maven settings*)

| Setting | Effect |
|---|---|
| Versions accepted | `Releases` (default), `Snapshots` or `Both`. A stricter policy deletes nothing stored |
| Redeploy releases | off: a release file is immutable (409). On: replaced by a caller who also holds Delete |
| Snapshot builds kept | 0 keeps all. N keeps the newest N builds of a snapshot version, deleting older ones on each new deploy; the manage page cleans up on demand |

## Pages

| Page | Address (under `/r/{name}/`) | Who |
|---|---|---|
| Browse | `/maven-artifacts?q=` | whoever may read the repository; lists only artifacts the visitor may read (`group:artifact`); `settings.xml` and `pom.xml` snippets |
| Artifact | `/maven-artifacts/{group}/{artifact}/{version?}` | readers of the artifact: versions, files with sizes and checksums, POM view, dependency snippets (Maven, Gradle), `mvn dependency:get` |
| Upload | `/maven-upload` | a signed-in user with Deploy (accounts on), or the API key typed into the form (accounts off): coordinates, packaging, classifier, file |
| Manage | `/maven-manage/{group}/{artifact}` | owners and administrators: owners, delete a file / a version / the artifact (password), snapshot clean-up |

Maven pages are 404 in a NuGet or npm repository and the other formats' pages are 404 in a Maven one. Access patterns (grants, include/exclude)
see the artifact as `group:artifact`. Writes are refused (503) while a database copy has the maintenance gate closed.

## Storage and data

Files: `maven/{group}/{artifact}/{version}/{file}` under the repository's prefix (`repos/{name}/` as for every non-default repository).
Tables `MavenArtifacts`, `MavenVersions`, `MavenFiles` (repository-scoped; migration `AddMavenTables`, all four providers). Owners are the
common `PackageOwner` rows keyed by `group:artifact`.

## Tests

`MavenLayoutTests` (Core), `MavenRepositoryTests`, `MavenPagesTests` (`BaGet.Tests`), and `MavenCliTests` (`BaGet.Clients.Tests`): the real
`mvn` 3.9 in `maven:3.9-eclipse-temurin-21` deploys and resolves releases and snapshots, hits the 409 rules, and signs in to a private
repository with `settings.xml` credentials. They skip without Docker and need the internet (the plugins come from Maven Central).
