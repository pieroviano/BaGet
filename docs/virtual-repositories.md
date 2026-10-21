# Virtual repositories

A **virtual** repository presents several repositories (local, [remote](remote-repositories.md) or other virtual ones) as one feed at `/r/{name}/v3/index.json`. Clients configure one source; the server decides which member answers. It stores nothing itself.

## Create and edit

Administrators: `/admin/repositories` > *New virtual repository* (a name; it starts empty), then `/admin/repositories/{name}`. Needs a relational database and accounts, like all [repository pages](repositories.md#admin-pages). Not available with Azure Table or `Search:Type` = `AzureSearch` (only `default` exists there).

| Control | Does |
|---|---|
| Members table | Resolution order, top to bottom. *Up* / *Down* reorder, *Remove* drops a member. Saved at once, audited |
| *Add a member* | Any other NuGet repository whose own members do not lead back to this one; added at the end |
| Deploy repository | The member that receives uploads: a local member, or a virtual member that itself deploys to a local one. A remote repository cannot be one. *None* = the repository takes no uploads |
| *Where would a package come from?* | Package id + version: asks each effective member in order and shows what it found (*stored here*, *its upstream has it and it would be fetched*, *not found*, *its upstream could not be asked*, *not asked: an earlier member answers*) and which member answers. Fetches and stores nothing; a remote member's upstream is asked only while no earlier member answers |

Rules: at most 50 members; a repository is listed once; a member must exist and be a NuGet repository; **nesting** is allowed, a nested virtual member is expanded in place (each repository counts once, at its first place), and a change that would make a virtual repository contain itself is refused with the reason. A repository that a virtual repository lists cannot be deleted until it is removed there (the delete page says where). Edits are refused with `503` while the server is read-only for maintenance. Members are stored by name in `Repository.Settings` (no schema change).

## What a client sees

| Request | Result |
|---|---|
| Download, `.nuspec`, readme, icon of a version | The **first member**, in order, that has that version. A remote member fetches and stores it (in itself) as it does when read directly; a member that fails (its upstream is down) is logged and skipped, so a later member can answer |
| Version list, registration index | The **union** of the members' versions; each version's data comes from the first member that has it. (A package id in a local member and on nuget.org therefore lists the versions of both) |
| Search | Merged, one result per package id: the first member's entry, the versions of all members (highest first, the highest reported) and the summed downloads. Order is total and independent of the page: *relevance* = id (case-insensitive), *downloads* = summed downloads then id; `totalHits` counts distinct ids; `skip`/`take` never repeat or drop a package. *Last updated* is not offered. Each member is read up to 10,000 results per query; beyond that the total is an estimate (the ids seen plus the member's unseen remainder) and deeper pages are cut off |
| Autocomplete, version enumeration, dependents | The same merge (ids by name; versions ascending; dependents by summed downloads, top 20) |
| Symbols | The first member that stores the PDB |
| V2 feed (`/r/{name}/api/v2`) | `FindPackagesById`, `Packages(Id,Version)`, `Packages()` and `Search()` read the members' stored packages the same way (the V2 feed never asks an upstream, here or elsewhere) |
| Vulnerability index | The advisories of the readable members, each once |
| Catalog | **Not served** (`404`, and the service index omits `Catalog/3.0.0`): a virtual repository records nothing, and a merged log would have no commit order a client could resume from. Use the members' catalogs |
| Service index | Like any repository's, with every URL inside `/r/{name}/` |
| Web pages | *Packages*, a package's page, *Upload* and *Browse* work; *Statistics*, *Report abuse*, *Manage package* and *Contact owners* answer `404` (a virtual repository has no owners, reports or counters of its own: use the member's page) |

Every link in an answer (registration, search, V2 entries, content) points into the virtual repository, so clients keep resolving through it.

## Writes

A write to a virtual repository **is a write to its deploy repository**: the request is moved there before anything else, so the access model, package ownership, validation and the catalog all apply to that repository.

| Request | Result |
|---|---|
| Push, symbol push, unlist, relist, deprecation, vulnerabilities (`PUT`/`POST`/`DELETE`), validation status | Handled in the deploy repository; needs **Deploy** (or the action asked) **there**. Deploy on the virtual repository itself is never granted (not even to administrators) and never needed. Without a deploy repository: `403`. An unlist or delete of a package stored in another member finds nothing in the deploy repository |
| Browser upload (`/r/{name}/upload`) | Stores in the deploy repository (the page says which); offered to those who can read the virtual repository and deploy to its deploy repository |

## Access

* Reading needs **Read on the virtual repository** (anonymous read, grants and project roles work as for any repository: [access control](access-control.md)).
* A request is answered **only from the members the caller may read** (per package too, by the grants' include/exclude patterns). A private member contributes nothing for a caller without Read on it: no error, no hint; a package only it has is `404`.
* Search and the V2 listing filter by repository, not by package pattern, like everywhere else.
* The *Effective access* table of a virtual repository shows *Deploy* as decided by the deploy repository.

## `default` and the mirror

`default` keeps its URL (`/`). With `Mirror:Enabled`, it behaves as a virtual repository of two members: its own packages, then the `mirror` remote repository (see [the mirror](remote-repositories.md#the-mirror)). What it fetches is stored **in `default`'s own rows**, exactly as before; `mirror` itself stays empty unless it is read at `/r/mirror/`. This is not a stored virtual repository (nothing to migrate, nothing to edit). To compose your own:

* a virtual repository with `default` as a member reads `default` as it reads itself, mirror fall-through included;
* a virtual repository with `mirror` as a member reads the mirror's own cache (separate from what `default` stored).

## Not covered

No per-member path rules (include/exclude patterns), no merged catalog, no *last updated* sort, and the V2 feed ignores `$filter`/`$orderby` as everywhere. Per-member resolution state of remote members (memory, health) is per node, as for [remote repositories](remote-repositories.md).
