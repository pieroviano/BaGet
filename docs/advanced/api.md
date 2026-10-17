# Server API

BaGet implements the [NuGet V3 API](https://learn.microsoft.com/nuget/api/overview); clients find
every resource through the service index at `/v3/index.json`. This page lists what it serves, and
the endpoints BaGet adds of its own.

## Service index resources

| Resource | URL | Notes |
| --- | --- | --- |
| `PackagePublish/2.0.0` | `/api/v2/package` | push, delete (unlist), relist |
| `SymbolPackagePublish/4.9.0` | `/api/v2/symbol` | portable PDBs that match the package's assemblies |
| `SearchQueryService` | `/v3/search` | results include `deprecation`, `vulnerabilities`, `owners`, `verified` |
| `SearchAutocompleteService` | `/v3/autocomplete` | |
| `RegistrationsBaseUrl` | `/v3/registration` | entries include `deprecation` and `vulnerabilities` |
| `PackageBaseAddress/3.0.0` | `/v3/package` | |
| `PackageDetailsUriTemplate/5.1.0` | `/packages/{id}/{version}` | |
| `ReportAbuseUriTemplate/3.0.0-beta`, `-rc` | `/packages/{id}/{version}/report-abuse` | |
| `VulnerabilityInfo/6.7.0` | `/v3/vulnerabilities/index.json` | one page, `base` |
| `Catalog/3.0.0` | `/v3/catalog/index.json` | not with Azure Table Storage |
| `RepositorySignatures/5.0.0` | `/v3/repository-signatures/index.json` | only while [repository signing](../configuration.md#repository-signing) is configured |

The V2 (OData) feed is at `/api/v2`; see [NuGet V2 feed](../configuration.md#nuget-v2-feed).

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
| `/packages/{id}/{version}/report-abuse` | records a report for the package's owners |
| `/account/login`, `/account/register`, `/account/apikeys`, `/account/organizations` | with accounts on |
| `/profiles/{name}` | a user's or organization's packages, with accounts on |
| `/packages/{id}/manage` | owners: owners, unlist/relist, deprecation, abuse reports |
