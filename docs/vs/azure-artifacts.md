# Azure Artifacts

[Azure Artifacts](https://learn.microsoft.com/en-us/azure/devops/artifacts/start-using-azure-artifacts) is the package management service of Azure DevOps (cloud service and Azure DevOps Server).

| Feature | Azure Artifacts | BaGet |
|---|---|---|
| Package formats | NuGet, npm, Maven (incl. Gradle), Python, Cargo, Universal Packages | NuGet only (`.nupkg`, `.snupkg`) |
| NuGet protocol | V3 feed URL (`.../nuget/v3/index.json`); legacy V2 (`.../nuget/v2`, PAT; Azure DevOps Services only, "only when you can't move to a newer client") | V3 service index, plus V2 OData feed `/api/v2` (on by default, `Feeds:EnableV2`) |
| Symbols | Symbol server, published by the `PublishSymbols@2` pipeline task; retention follows the producing build; `.snupkg` not supported | Symbol server in the same feed: push `.snupkg`, download by key; portable PDBs only |
| Upstream / proxy | Upstream sources (nuget.org, npmjs, ...); packages saved to the feed when a Collaborator+ installs them; custom upstreams only for npm; cannot publish a version that exists upstream | Optional pull-through mirror (`Mirror:Enabled`, off by default), single upstream, indexed copy kept; search does not query upstream |
| Multiple feeds / views | Many feeds (org or project scoped); views `@local`, `@prerelease`, `@release`, read-only; promotion only, no demotion | One feed per instance; no views |
| Private reads | Private feeds need Feed Reader or higher; public feeds only in public projects (public projects convert to private from 2027) | Not supported: read routes are unauthenticated |
| Permissions | Roles Reader, Collaborator, Contributor, Owner per feed; view visibility settable | Accounts, organizations, per-package owners; no per-feed roles |
| Accounts / API keys | Azure DevOps identities, personal access tokens | Local accounts, OIDC, TOTP 2FA, scoped expiring API keys, global `ApiKey` |
| Retention | Policy: max versions per package and days since last download; deletions kept 30 days in Recycle Bin; versions immutable (number never reusable) | No retention policy; delete = `Unlist` (default) or `HardDelete`; overwrite optional (`AllowPackageOverwrites`) |
| UI | Web UI in Azure DevOps | Web UI: search, details, upload, stats, manage |
| Hosting | Azure DevOps Services (SaaS) or Azure DevOps Server (on-prem; Universal Packages not in Server 2020/2022; Cargo not in Server 2020) | Self-hosted .NET 10 (Windows/Linux/macOS, Docker, IIS) |
| HA | Managed by Microsoft (SaaS); on-prem HA not verified | No documented multi-instance support |
| Licence / cost | 2 GiB free per organization, then tiered from $2/GiB down to $0.25/GiB; publishing blocked above the limit until billing is set up | MIT, free; infrastructure cost only |
| Maintenance | Microsoft product; docs updated 2026 | Fork of loic-sharma/BaGet, community maintained |

## When to choose which

- Azure Artifacts: already on Azure DevOps, or you need npm/Maven/Python/Cargo in one feed, views, per-feed roles or authenticated reads.
- BaGet: NuGet only, self-hosted, no per-GiB billing, V2 and V3 on one server.
- BaGet cannot restrict who reads packages; use a reverse proxy or network boundary if needed.

## Sources

Checked 2026-10.

- https://learn.microsoft.com/en-us/azure/devops/artifacts/start-using-azure-artifacts
- https://learn.microsoft.com/en-us/azure/devops/artifacts/concepts/feeds
- https://learn.microsoft.com/en-us/azure/devops/artifacts/concepts/views
- https://learn.microsoft.com/en-us/azure/devops/artifacts/concepts/upstream-sources
- https://learn.microsoft.com/en-us/azure/devops/artifacts/feeds/feed-permissions
- https://learn.microsoft.com/en-us/azure/devops/artifacts/how-to/delete-and-recover-packages
- https://learn.microsoft.com/en-us/azure/devops/pipelines/artifacts/symbols
- https://learn.microsoft.com/en-us/azure/devops/artifacts/nuget/nuget-exe
- https://learn.microsoft.com/en-us/nuget/create-packages/symbol-packages-snupkg
- https://azure.microsoft.com/en-us/pricing/details/devops/azure-devops-services/
