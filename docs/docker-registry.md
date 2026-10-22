# Docker registry

A local **Docker** repository is an OCI registry (Distribution Spec v1.1): `docker login`, `push`, `pull`, multi-platform indexes,
referrers (signatures, SBOMs), tag listing, deletes. Remote (Docker Hub proxy) and virtual Docker repositories are not built yet.

## Create and use

| Step | How |
|---|---|
| Create | Administrators: `/admin/repositories` > *Docker* form (name, *Anonymous read*). Needs a store that streams: `Storage:Type` FileSystem, AwsS3, AzureBlobStorage, GoogleCloud or AliyunOss ([streaming storage](formats.md#streaming-storage-b2)); refused otherwise, and on Azure Table / Azure Search (default repository only) |
| Image name | `{host}/{repository}/{image}[:{tag}]`: the BaGet repository is the **first path segment**, the rest is the image (`team/app`, any depth). `docker push localhost:6060/libs/team/app:1.0` pushes image `team/app` into repository `libs`. The registry is at the server root (`/v2/`), not under `/r/{name}/` |
| Login | `docker login host` with your user name and an **API key** as the password (create one on *API keys*). The API key never leaves the login: the registry hands Docker a short-lived token |
| Plain http | Docker only talks http to `localhost` and to registries listed in `daemon.json` `insecure-registries`; the browse page shows the entry for this host |
| Push / pull | `docker tag` + `docker push`, `docker pull`, `docker buildx build --platform linux/amd64,linux/arm64 --push` (snippets on the browse page) |

## Access

pull = Read, push = Deploy, delete = Delete on the image:

| Action | Needs | Notes |
|---|---|---|
| `pull` | **Read** on the repository and the image | anonymous pulls work where *Anonymous read* is on (Docker takes an anonymous token) |
| `push` | **Deploy** | `docker push` also checks which layers exist, so the user needs Read too |
| `delete` | **Delete** | manifests, tags, blobs |

The access model sees the image name (`team/app`) as the package id, so include/exclude patterns (`team/*`) work. A key still needs its
own scope: `package:push` or `package:pushversion` to push, `package:unlist` to delete; its package glob must match the image. There is no
ownership: whoever holds the action may use it. Deploy is never granted on a remote or virtual repository.

### Token flow (`docker login`)

| Request | Answer |
|---|---|
| `GET /v2/` | `200 {}` for a caller the registry knows (token or API key); `401` + `WWW-Authenticate: Bearer realm="https://host/v2/token",service="BaGet"` for an anonymous one. Every response carries `Docker-Distribution-API-Version: registry/2.0` |
| `GET /v2/token?service=BaGet&scope=repository:{repo}/{image}:pull,push` | `{"token","access_token","expires_in","issued_at"}`. Basic credentials are optional: an API key as the password (a user name is checked against the key's owner); wrong ones are `401`. The token grants, per requested image of an existing Docker repository, the requested actions **the caller holds** (possibly none) |
| a registry route without a covering token | `401` + `Bearer … scope="repository:{repo}/{image}:pull,push"`, plus `error="insufficient_scope"` (a token that does not cover it) or `error="invalid_token"` (expired, tampered with). The same answer for a repository that does not exist and one the caller may not see |
| a registry route with an API key (Basic or Bearer) | judged directly, without a token (scripts, `curl`, the conformance suite). A key that may not read is `404`; one that may read but lacks the action is `403` |

Tokens are signed and encrypted with ASP.NET Core data protection, hold only the user name (for the log), the image scopes and the expiry,
never an API key, and last `Oci:TokenLifetime` (5 minutes). A revoked permission applies when the tokens issued before it expire.

## Protocol

| Request | Answer |
|---|---|
| `HEAD`/`GET /v2/{repo}/{image}/blobs/{digest}` | the blob, `Docker-Content-Digest`, `Accept-Ranges`; `Range` (`206`, `416`). Served only through an image that holds it |
| `POST …/blobs/uploads/` | `202` + `Location` (upload session), `Docker-Upload-UUID`, `Range: 0-0` |
| `POST …/blobs/uploads/?digest=` | monolithic upload (`201`) |
| `POST …/blobs/uploads/?mount={digest}&from={repo}/{image}` | `201` when the source image of the same repository holds the blob and the caller may pull it; otherwise a normal `202` session |
| `PATCH …/blobs/uploads/{ref}` | append a chunk (`202`); `Content-Range` start must equal the upload length, else `416` with the real `Range` |
| `PUT …/blobs/uploads/{ref}?digest=` | optional last chunk, SHA-256 verified while streaming, `201`. A wrong digest is `400 DIGEST_INVALID` and ends the session |
| `GET`/`DELETE …/blobs/uploads/{ref}` | `204` with the current `Range`; `204` cancel |
| `PUT …/manifests/{tag or digest}` | `201`; the `Content-Type` must be an OCI or Docker v2 manifest/index type; every config, layer or child manifest must already be in the image (`400 MANIFEST_BLOB_UNKNOWN`); the digest in the URL must match; max `Oci:MaxManifestBytes` (4 MiB, `413`); `OCI-Subject` header when there is a subject |
| `HEAD`/`GET …/manifests/{tag or digest}` | the bytes exactly as pushed, `Docker-Content-Digest` |
| `DELETE …/manifests/{digest}` | removes the manifest and its tags (`202`); `…/manifests/{tag}` removes the tag only |
| `DELETE …/blobs/{digest}` | removes the blob from the image (`202`); `405 UNSUPPORTED` while a manifest of the image uses it |
| `GET …/tags/list?n=&last=` | sorted tags, `Link: rel="next"` when `n` leaves more |
| `GET …/referrers/{digest}?artifactType=` | an OCI index of the manifests whose `subject` is the digest; `OCI-Filters-Applied` |

Errors are the specification's JSON (`{"errors":[{"code","message","detail"}]}`). A path that is not a registry endpoint is `404`, an
undefined method `405`, an invalid name `400 NAME_INVALID`. Repository names match case-insensitively; image names and digests are lower case,
tags are case-sensitive. Only `sha256` digests are stored. Writes are `403` in read-only mode and `503` while a database copy has the maintenance gate closed.

## Pages

| Page | Address (under `/r/{name}/`) | Who |
|---|---|---|
| Browse | `/docker-images?q=` | whoever may read the repository; lists only images the visitor may read; login, pull, push, buildx and daemon snippets |
| Image | `/docker-images/{image}` | readers of the image: tags, manifests (type, platform, layers, size). `?digest=` shows one manifest: config and layers with sizes, an index's platforms with links to each manifest, annotations, the document |
| Manage | `/docker-manage/{image}` | signed-in users with **Delete** on the image: delete a tag, a manifest (with its tags), remove unused layers (older than `Oci:PruneGrace`), delete the whole image; each asks the password |

Docker pages are 404 in other formats' repositories and theirs are 404 in a Docker one. There is no upload page: images are pushed with the CLI (and the NuGet upload page does not offer Docker repositories).

## Storage and data

Blobs are stored once per repository, by digest: `docker/blobs/sha256/{ab}/{digest}` under the repository's prefix, shared by every image of the
repository. Manifests are rows (their bytes are the digest). Tables, migration `AddOciTables` on all four providers:

| Table | Holds |
|---|---|
| `OciRepositories` (repository-scoped) | the images (`team/app`), unique per repository |
| `OciManifests` | digest, media type, artifact type, subject digest, size, the JSON as pushed; unique per image |
| `OciTags` | tag (case-sensitive) to manifest, unique per image; deleted with the manifest |
| `OciBlobs` (repository-scoped) | digest, size, `RefCount` (images holding it); the file goes when it reaches 0 |
| `OciBlobLinks` | which image holds which blob, and how many of its manifests use it |
| `OciUploads` | open upload sessions (reference, storage upload id); idle ones expire after `Oci:UploadExpiry` when the next upload starts |

A blob is served under an image only through its link, so restricting `team/secret` cannot be bypassed by asking another image for its digest.
Deleting a repository needs it empty of images. Garbage collection beyond *Remove unused layers* arrives with the retention engine (P6).

## Configuration

| Setting | Default | Meaning |
|---|---|---|
| `Oci:TokenLifetime` | `00:05:00` | validity of a token from `/v2/token` (at least 10 s). Live |
| `Oci:Service` | `BaGet` | the `service` of the challenge, checked at the token endpoint. Live |
| `Oci:MaxManifestBytes` | `4194304` | largest manifest or index. Live |
| `Oci:UploadExpiry` | `1.00:00:00` | idle time after which an upload session is discarded. Live |
| `Oci:PruneGrace` | `01:00:00` | a layer no manifest uses is kept this long before *Remove unused layers* may remove it. Live |

Kestrel has no request body limit; behind IIS a single `PATCH` is capped (`IISServerOptions.MaxRequestBodySize`, `maxAllowedContentLength`): Docker
pushes a layer in one `PATCH`, so raise both ([streaming storage](formats.md#streaming-storage-b2)). Reverse proxies need `client_max_body_size 0` and
must pass `Authorization` and the `Host` header (the token URL is built from it).

## Tests

`OciNamesTests` (Core: names, paths, scopes, manifest reader), `OciRegistryTests` (HTTP protocol, token flow, access), `DockerPagesTests`
(`BaGet.Tests`), and `DockerCliTests` (`BaGet.Clients.Tests`): the real Docker 27 in a privileged `docker:27-dind` logs in, pushes and pulls
multi-layer and 96 MB images, a multi-platform index, public and private repositories, and the `opencontainers/distribution-spec` v1.1.0
conformance suite (push, pull, content discovery, content management) runs against the same server. They skip without a Docker daemon that
allows privileged containers; the images are built `FROM scratch`, so they need no internet (the conformance image comes from `ghcr.io`).
