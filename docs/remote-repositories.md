# Remote repositories

A **remote** repository caches another NuGet feed (nuget.org, another BaGet, Artifactory, Azure Artifacts, ...). A package it does not hold is fetched from the upstream the first time it is requested, stored, and served from there afterwards. Its feed is `/r/{name}/v3/index.json` like any repository's; clients cannot push to it.

## Create and edit

Administrators: `/admin/repositories` > *New remote repository*; later `/admin/repositories/{name}` (needs a relational database and accounts, like all [repository pages](repositories.md#admin-pages)).

| Setting | Default | Meaning |
|---|---|---|
| Upstream URL | required | A V3 `index.json` URL or a V2 feed URL (absolute `http`/`https`, no credentials inside) |
| Protocol | V3 | `V3` or `V2` |
| User name / password | none | HTTP Basic, sent to the upstream's own host only, never to hosts a service index or redirect points to. A token goes in the password. Stored protected with ASP.NET Core data protection (the host's key ring; back it up with the host), never shown again, never logged. Blank on save or on *Test connection* keeps the stored one while the URL's scheme/host/port and the user name are unchanged (one rule for both) (on the `mirror` repository only the origin counts: `Mirror:*` never sets a user name, so a changed timeout, `Legacy` or URL path keeps it; otherwise it is dropped and a new one must be typed); *Remove the stored password* clears it. A host without data protection refuses to store a password |
| Metadata cache period | 600 s | How long the upstream's version lists and metadata are reused. `0` asks every time. An expired answer is still served when the upstream cannot be reached |
| Negative cache period | 60 s | How long a `404` from the upstream is remembered. A failure to reach the upstream (network, 5xx, 401) is never remembered as "not found" |
| Download timeout | 600 s | Per HTTP request (V3 only; a V2 upstream is contacted by NuGet's own HTTP stack, with its default timeout) |
| Store artifacts locally | on | On: packages are stored (database and files) on first request; they and their local metadata are served while the upstream is down. Off: **pass-through**: metadata and downloads come from the upstream each time and nothing is kept, so search and the package pages stay empty |
| Offline mode | off | The upstream is never contacted: only stored packages and remembered answers are served |

The cache lives in the repository: cached packages are ordinary package rows of the remote repository (in `repos/{name}/` in storage), isolated from every other repository, and browsable through the repository-aware pages (`/r/{name}/`, `/r/{name}/packages/{id}`, search).

## What a client sees

| Request | Result |
|---|---|
| Version list, registration | Upstream's versions merged with the stored ones; cached for the metadata period |
| Package, `.nuspec` download | Stored copy, else fetched, stored (indexed like a push, never validated) and served; pass-through streams it |
| Push, symbol push (`PUT`) | `403`: the access model never grants *Deploy* on a remote repository, not even to administrators; the browser upload page does not list it |
| Delete, unlist, deprecate | Allowed per the access model (evict a cached package) |
| Read | As for any repository: [access control](access-control.md) |
| Unknown package | `404`, remembered for the negative period |

## Test connection, cache and purge

On `/admin/repositories/{name}`:

| Control | Does |
|---|---|
| *Test connection* | Asks the upstream with the values in the form, saved or not (blank password = the stored one, if the URL's origin and user name are unchanged), and saves nothing. V3: the URL must answer a service index (at most 1 MB is read); V2: any success. Redirects are followed only within the same origin (at most 5), never to another host. Shows the HTTP status and time; failures say which (refused credentials, HTTP status, unreachable) without URLs or credentials |
| Cache table | Stored package versions and ids; remembered answers; hits, upstream requests, `404`s answered from memory, expired answers served; the upstream's health (last answer, failing since, last error). Counters and remembered answers are per node and reset at restart |
| *Purge cache* | Deletes every stored package (database and files) and drops the remembered answers. Audited |
| *Purge package* | The same for one package id, one version or all of them. Audited |
| *Browse the cached packages* | The repository's front page |

Creating a remote repository, saving its upstream, *Test connection* and both purges **ask for your password again** (or a recent sign-in with the external provider, for an account without a password): a test saves nothing but makes the server send a request to a typed URL. The stored password is sent by *Test connection* only while the URL's origin and user name are unchanged (the rule saving applies), so testing another host never hands it to that server. A remote repository with cached packages cannot be deleted until its cache is purged. Creating, editing and purging are refused with `503` while the server is read-only for maintenance (for example during a [database copy](configuration.md)).

## Upstream addresses (SSRF)

An upstream URL makes **this server** request whatever the administrator typed: loopback, internal services, link-local addresses such as the cloud metadata endpoint `169.254.169.254`. Only administrators can create or edit remote repositories, and internal feeds are a normal use, so private addresses are **allowed by default**. If your administrators are less trusted than the host, set [`Remote:BlockPrivateAddresses`](configuration.md#remote-repositories-refuse-private-upstreams) to `true`: creating, saving and testing then refuse such upstreams, and every V3 connection re-checks the address it actually connects to (so DNS changes and redirects cannot get around it). Whatever the setting, *Test connection* never follows a redirect to another host and reads at most 1 MB.

## The mirror

`Mirror:Enabled` (see [configuration](configuration.md#enable-read-through-caching)) keeps working unchanged: the default repository fetches from `Mirror:PackageSource`, stores the packages in itself and asks the upstream every time. At startup the **`mirror`** remote repository is created from `Mirror:*` (or its URL, protocol and timeout are brought in line with them) and the default repository fetches **through its settings**, so credentials, an offline switch or cache periods set on `mirror` apply to the default repository too. Its URL, protocol and timeout stay owned by `Mirror:*` (read-only on the page). Packages mirrored into `default` are default packages; `mirror` has its own (initially empty) cache at `/r/mirror/`. If a *local* repository is already named `mirror`, it is left alone and `Mirror:*` is read directly.

!!! note
    Replace `IRemoteHttpClientFactory` to change how upstreams are contacted (proxy, certificates). Registering your own `HttpClient` no longer affects mirroring. V2 upstreams do not go through the factory: NuGet.Protocol contacts them itself (no proxy/certificate hook, the download timeout does not apply, credentials are given to NuGet's source credential for that host), with NuGet's on-disk HTTP cache switched off so the metadata period and *Purge cache* govern.

## Not covered yet

A remote repository can be a member of a [virtual repository](virtual-repositories.md) (an ordered group behind one URL); `default` with the mirror behaves as one, see [`default` and the mirror](virtual-repositories.md#default-and-the-mirror). Azure Table and `Search:Type` = `AzureSearch` serve the default repository only, so remote repositories cannot be created there.
