# Server API

BaGet Extension implements the [NuGet V3 API](https://learn.microsoft.com/nuget/api/overview); clients find
every resource through the service index at `/v3/index.json`. This page lists what it serves, and
the endpoints BaGet Extension adds of its own.

## Service index resources

| Resource | URL | Notes |
| --- | --- | --- |
| `PackagePublish/2.0.0` | `/api/v2/package` | `PUT` push; `DELETE /{id}/{version}` unlists or hard deletes per [`PackageDeletionBehavior`](../configuration.md#enable-package-hard-deletions); `POST /{id}/{version}` relists |
| `SymbolPackagePublish/4.9.0` | `/api/v2/symbol` | portable PDBs that match the package's assemblies |
| `SearchQueryService` | `/v3/search` (also advertised as `/3.0.0-beta`, `/3.0.0-rc`) | results include `deprecation`, `vulnerabilities`, `owners`, `verified` |
| `SearchAutocompleteService` | `/v3/autocomplete` | also advertised as `/3.0.0-beta`, `/3.0.0-rc` |
| `RegistrationsBaseUrl` | `/v3/registration` | entries include `deprecation` and `vulnerabilities`; also advertised as `/3.0.0-beta`, `/3.0.0-rc` |
| `PackageBaseAddress/3.0.0` | `/v3/package` | |
| `PackageDetailsUriTemplate/5.1.0` | `/packages/{id}/{version}` | |
| `ReportAbuseUriTemplate/3.0.0-beta`, `-rc` | `/packages/{id}/{version}/report-abuse` | |
| `VulnerabilityInfo/6.7.0` | `/v3/vulnerabilities/index.json` | one page, `base` |
| `Catalog/3.0.0` | `/v3/catalog/index.json` | not with Azure Table Storage |
| `RepositorySignatures/5.0.0` | `/v3/repository-signatures/index.json` | only while [repository signing](../configuration.md#repository-signing) is configured |

The V2 (OData) feed is at `/api/v2`; see [NuGet V2 feed](../configuration.md#nuget-v2-feed).

## Other routes

| Route | Notes |
| --- | --- |
| `GET /v3/registration/{id}/index.json`, `/v3/registration/{id}/{version}.json` | registration index and leaf |
| `GET /v3/package/{id}/index.json`, `/v3/package/{id}/{version}/{id}.{version}.nupkg`, `.../{id}.nuspec` | versions, package, manifest |
| `GET /v3/package/{id}/{version}/readme`, `.../icon` | the package's readme and icon |
| `GET /v3/dependents` | packages that depend on a package; BaGet Extension's own, not in the NuGet protocol |
| `GET /api/download/symbols/{file}/{key}/{file2}`, `/api/download/symbols/{prefix}/{file}/{key}/{file2}` | symbol download (the symbol server URL) |
| `GET /v3/catalog/index.json`, `/v3/catalog/page{n}.json`, `/v3/catalog/data/{commitId}.json` | catalog index, page, leaf |
| `GET /v3/repository-signatures/{fingerprint}.crt` | the repository signing certificate |
| `GET /api/v2`, `/api/v2/$metadata`, `/api/v2/Packages()`, `/api/v2/Packages(Id='..',Version='..')`, `/api/v2/FindPackagesById()`, `/api/v2/Search()` | V2 feed; `404` when `Feeds:EnableV2` is `false`, while push and delete under `/api/v2/package` are unaffected |

## Deprecation and vulnerabilities

These change a package version, so they are authorised like a delete: the global `ApiKey`, or with
[accounts](../configuration.md#accounts-package-owners-and-api-keys) an owner's key with the
`package:unlist` scope, sent in the `X-NuGet-ApiKey` header.

| Request | Body |
| --- | --- |
| `PUT /v3/package/{id}/{version}/deprecation` | `{"reasons": ["Legacy"], "message": "...", "alternatePackageId": "...", "alternateVersionRange": "[2.0.0, )"}` |
| `DELETE /v3/package/{id}/{version}/deprecation` | |
| `PUT /v3/package/{id}/{version}/vulnerabilities` | `[{"advisoryUrl": "https://...", "severity": 2, "versionRange": "[1.0.0, 1.2.0)"}]`; `[]` clears them |

* `reasons`: one or more of `Legacy`, `CriticalBugs`, `Other`.
* `severity`: `0` low, `1` moderate, `2` high, `3` critical.
* An omitted `versionRange` means the version itself.

| Response | Meaning |
| --- | --- |
| `204` | changed |
| `400` | invalid body; the response says why |
| `401` / `403` | not authenticated / not allowed for this package |
| `404` | no such package version |
| `501` | the database cannot store this metadata (Azure Table Storage) |

Owners can do the same from the package's manage page.

## Validation status

With the [validation pipeline](../configuration.md#validate-packages-before-serving-them) on:

`GET /v3/package/{id}/{version}/validation`, with an owner's key or the global `ApiKey`:

```json
{
  "submissionId": "...",
  "status": "Quarantined",
  "steps": [
    { "type": "PackageEntries", "state": "Succeeded", "message": null },
    { "type": "PackageSignature", "state": "Failed", "message": "The package must be signed." },
    { "type": "Index", "state": "Failed", "message": "Quarantined: the package failed validation." }
  ]
}
```

`status` is `Validating`, `Available` or `Quarantined`.

## Web pages

| Page | |
| --- | --- |
| `/packages`, `/packages/{id}/{version}` | search and package details |
| `/upload` | browser upload (two steps, with a metadata preview) and push instructions for each client |
| `/stats` | totals, most downloaded packages, latest versions |
| `/terms`, `/privacy` | the operator's Markdown files from `Site:TermsOfUsePath` / `Site:PrivacyPolicyPath`; linked from the footer only when set |
| `/packages/{id}/{version}/report-abuse` | records a report for the package's owners |
| `/account/login`, `/account/register`, `/account/apikeys`, `/account/organizations`, `/account/packages`, `/account/settings`, `/account/twofactor` | with accounts on |
| `/profiles/{name}` | a user's or organization's packages, with accounts on |
| `/packages/{id}/manage` | owners: owners, unlist/relist, delete (per `PackageDeletionBehavior`), deprecation, abuse reports |
