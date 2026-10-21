# Artifactory

[JFrog Artifactory](https://jfrog.com/artifactory/) is a commercial, multi-format artifact repository manager; NuGet is one of its package types.

| Feature | Artifactory | BaGet Extension |
|---|---|---|
| Formats | Multi-format (Maven, npm, Docker, PyPI, NuGet, ...) | NuGet only (`.nupkg`, `.snupkg`) |
| NuGet V3 | Yes: `/api/nuget/v3/<repo>/index.json`; PackageBaseAddress only on repos with *Enforce Layout* (fixed at creation) | Yes: `/v3/index.json`; registration not paged |
| NuGet V2 | Yes: `/api/nuget/<repo>` | Yes: `/api/v2` OData, on by default (`Feeds:EnableV2`); `$filter`/`$orderby`/`$select`/`$expand` ignored |
| Symbols | Local, remote and virtual symbol repos; `.snupkg`; Microsoft PDB v7 and Portable PDB | Yes: `.snupkg` push, download endpoint; Portable PDB only |
| Proxy / upstream cache | Remote repos (nuget.org or another Artifactory); cached on demand | [Remote repositories](../remote-repositories.md): per-repository upstream (V3 or V2), Basic credentials, metadata and negative cache periods, offline mode, pass-through or stored copies, test connection, purge; plus the optional `Mirror:Enabled` mirror for the default repository. Stored copies are permanent until purged; search does not query upstream |
| Virtual / group feeds | Virtual repos combine local and remote behind one URL; push allowed | Not supported |
| Multiple feeds | Many repositories per instance | One feed per instance |
| Private (authenticated) reads | Yes: anonymous access configurable (JFrog recommends off; anonymous removed from default permissions since 7.84.3); *Force Authentication* per repo | Not supported: read routes are open |
| Permissions model | Permission targets (repos + users/groups); Permissions V2 from 7.77.2; project RBAC | Package ownership (first pusher, owners, organizations); no per-feed read permissions |
| Accounts / API keys | Users, groups, access tokens; API key as `user:key` in NuGet clients | Accounts (on in shipped config), OpenID Connect, TOTP 2FA, scoped expiring API keys, global `ApiKey` |
| Storage / DB backends | Filestore: file system, S3, Azure Blob, GCS, NFS; DB: PostgreSQL (default since 7.84.7), MySQL, MariaDB, MSSQL, Oracle, Derby (not for production) | File system, Azure Blob, S3, Google Cloud, Aliyun OSS; SQLite, SQL Server, PostgreSQL, MySQL, Azure Table |
| UI | Admin and browse UI | Search, details, upload, stats, package management, account pages |
| Deployment | JFrog-hosted SaaS or self-managed | Self-hosted .NET 10 (Windows/Linux/macOS), Docker, IIS |
| HA / scale-out | Self-managed HA listed under Enterprise X; SaaS Enterprise X 99.9% uptime | No documented multi-instance support |
| Licence / cost | Commercial, see below | MIT, free; you pay only infrastructure |

Artifactory editions and pricing (pricing page):

| Plan | SaaS | Self-managed |
|---|---|---|
| Pro (SaaS) / Pro X (self-managed) | SaaS "Pro" $150/mo ($50 promo, monthly only); 25 GB base consumption, per-GB overage | Self-managed "Pro X" $27,000/yr, 1 server |
| Enterprise X | from $950/mo; 125 GB base | from $51,000/yr, 3 servers; HA |
| Enterprise + | custom | custom |

- SaaS is priced by storage and transfer consumption; self-managed by server count and project count, minimum one-year term.
- Free offerings per JFrog: Artifactory OSS (Java only), Community Edition for C/C++ (Conan and generic only), cloud trial. No free edition with NuGet was found.

## When to choose which

- Artifactory: one manager for several formats (Maven, npm, Docker, NuGet), virtual repos, authenticated reads, per-repo permissions, vendor-backed HA.
- BaGet Extension: a NuGet-only feed with no licence cost, a simple self-hosted deployment, and owner-based publishing with scoped API keys.
- Artifactory: when private reads or several feeds are mandatory; BaGet Extension has neither.
- BaGet Extension: when a free NuGet feed is the whole requirement; Artifactory NuGet has no free edition found.

## Sources

Checked 2026-10.

- <https://docs.jfrog.com/artifactory/docs/nuget-repositories>
- <https://jfrog.com/pricing/>
- <https://jfrog.com/community/open-source/>
- <https://docs.jfrog.com/administration/docs/permissions>
- <https://docs.jfrog.com/administration/docs/access-tokens>
- <https://docs.jfrog.com/installation/docs/artifactory-storage>
- <https://docs.jfrog.com/installation/docs/database-configuration>
- <https://docs.conan.io/2/tutorial/conan_repositories/setting_up_conan_remotes/artifactory/artifactory_ce_cpp.html>
- BaGet Extension side: this fork's code and docs.
