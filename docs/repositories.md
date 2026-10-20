# Repositories

A server holds one or more named repositories. Each has its own packages, owners, catalog and validations.

| Repository | Feed |
|---|---|
| `default` | `/v3/index.json` (unchanged; every existing install is this one) |
| any other | `/r/{name}/v3/index.json` |

Only **local NuGet** repositories can be created today.

## Admin pages

Administrators manage them at `/admin/repositories` (menu: *Repositories*). Other users and anonymous requests get `404` or the sign-in page.

| Page | Does |
|---|---|
| `/admin/repositories` | Lists name, type, package count and feed address (with a copy button); creates a repository |
| `/admin/repositories/{name}` | Shows the feed; edits *Anonymous read* |
| `/admin/repositories/{name}/delete` | Deletes an **empty** repository |

Rules:

* Names: 1 to 64 lowercase letters, digits, `-`, `.`, `_`; no leading or trailing `.`, no `..`; not a reserved word (`admin`, `api`, `r`, `v3`, ...). Invalid names are refused with the reason.
* A name is never changed after creation (it is in URLs and storage paths).
* `default` cannot be deleted.
* A repository that still holds packages cannot be deleted; delete its packages first. Deleting a repository together with its content is not available yet.
* Writes are refused with `503` while the server is read-only for maintenance (for example during a database copy).
* *Anonymous read* is stored but not enforced yet.
* Creating needs a relational database; with Azure Table only `default` exists.
