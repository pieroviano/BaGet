# Repositories

A server holds one or more named repositories. Each has its own packages, owners, catalog and validations.

| Repository | Feed |
|---|---|
| `default` | `/v3/index.json` (unchanged; every existing install is this one) |
| any other | `/r/{name}/v3/index.json` |

Only **local NuGet** repositories can be created today.

## Web pages

The web pages work in every repository: the package list, package, statistics, report-abuse, manage, contact-owners, profile and upload pages are at their usual address for `default` and under `/r/{name}/` for any other (`/r/{name}/packages`, `/r/{name}/upload`, ...). Links on a page stay in the repository being browsed.

| Where | What |
|---|---|
| Header | A *Repository* menu appears on those pages when more than one repository is readable; it goes to the front page of the chosen one. Hidden with a single repository, and always with Azure Table or `Search:Type` = `AzureSearch` (only `default`) |
| Upload | `/r/{name}/upload` publishes to `{name}`; the page lists the repositories you can publish to and shows the push commands of the one open |
| My packages | The packages you own, grouped by repository |
| `/admin/repositories` | *Browse* and *Upload* links per repository |
| Service index | Each repository advertises its own `PackageDetailsUriTemplate` and `ReportAbuseUriTemplate` |

## Admin pages

Administrators manage them at `/admin/repositories` (menu: *Repositories*). Other users and anonymous requests get `404` or the sign-in page.

| Page | Does |
|---|---|
| `/admin/repositories` | Lists name, type, package count and feed address (with a copy button); creates a repository |
| `/admin/repositories/{name}` | Shows the feed and who effectively holds each action (see [Access control](access-control.md#admin-pages)); edits *Anonymous read* |
| `/admin/repositories/{name}/delete` | Deletes a repository that holds **no packages** |

Other administrator pages work across all repositories:

| Page | Does |
|---|---|
| `/admin/operations` | Runs *Rebuild the catalog* and *Import download counts* for a chosen repository as background jobs; shows each one's last status |
| `/admin/validation` | Pending and quarantined submissions of every repository: retry, release (index anyway, audited), reject (see [configuration](configuration.md#validation)) |
| `/admin/abuse` | Abuse reports of every repository (unresolved by default, `?all=true` for all): resolve or reopen; links each package to its Manage page in its own repository |

Rules:

* Names: 1 to 64 lowercase letters, digits, `-`, `.`, `_`; no leading or trailing `.`, no `..`; not a reserved word (`admin`, `api`, `r`, `v3`, ...). Invalid names are refused with the reason.
* A name is never changed after creation (it is in URLs and storage paths).
* `default` cannot be deleted.
* A repository that still holds packages, or submissions awaiting validation, cannot be deleted; delete the packages first and wait for the validations. Deleting removes its history rows too (catalog entries, owners, validations, abuse reports) and every file under `repos/{name}/` in storage (symbols, catalog files, staged uploads), so a repository re-created under the same name starts empty.
* Writes are refused with `503` while the server is read-only for maintenance (for example during a database copy).
* *Anonymous read* is enforced: off, anonymous callers get `401` (feeds) or the sign-in page, and others need a Read grant of their own. See [Access control](access-control.md).
* Creating needs a relational database and a repository-aware search: with Azure Table, or with `Search:Type` = `AzureSearch` (one global index), only `default` exists and other repositories already in the database are not served.
* The `AddRepositories` migration creates the `default` row itself, so it exists even when migrations are applied outside the app.
