# nuget.org

!!! warning
    This page is a work in progress!

[nuget.org](https://www.nuget.org/), also known as the "Gallery", is the defacto feed to host open
source packages. You should publish NuGet packages for your open-source projects here.

The Gallery is battle-tested and proven to scale well. You can find a guide on how to host
your own Gallery instance on [the Gallery's wiki](https://github.com/NuGet/NuGetGallery/wiki/Hosting-nuget.org's-v3-services).
You can find the Gallery's code on GitHub:

* [NuGet/NuGetGallery](https://github.com/NuGet/NuGetGallery) - the [nuget.org](https://nuget.org)
website and v2 APIs
* [NuGet/NuGet.Jobs](https://github.com/NuGet/NuGet.Jobs/) - the Gallery's jobs for things like validation and package statistics.
* [NuGet/NuGet.Services.Metadata](https://github.com/NuGet/NuGet.Services.Metadata/) - NuGet's v3 implementation
* [NuGet/ServerCommon](https://github.com/NuGet/ServerCommon) - common libraries used across NuGet's services

As you can tell, nuget.org is a complex beast. Hosting your own instance of the Gallery is not for the faint of heart.

# BaGet vs nuget.org

See [this issue](https://github.com/loic-sharma/BaGet/issues/71) for a deep-dive.

## Features

| Feature | nuget.org | BaGet |
| --- | --- | --- |
| V3 push, restore, search, autocomplete, registration | yes | yes |
| Symbol packages (`.snupkg`) and symbol server | yes | yes |
| V2 (OData) feed | yes | yes, partial OData (`$skip`/`$top` only) |
| Catalog | yes | yes, not on Azure Table |
| Deprecation and vulnerability info | yes | yes, set through BaGet's [API](../advanced/api.md) or manage page |
| Report abuse, package details URI templates | yes | yes |
| Accounts, organizations, package owners | yes | yes, opt-in |
| Scoped, expiring API keys | yes | yes, opt-in |
| ID prefix reservation (`verified`) | yes | per owner, no page to set it yet |
| Validation before a package is served | yes | yes, opt-in, in-process |
| Repository signing | yes | yes, opt-in |
| Download statistics | per day | totals only, plus a statistics page |
| Search sort (most downloaded, recently updated) | yes | web UI only, `Database` search provider only |
| Upload through the browser | yes | yes, with a preview step |
| Contact owners | yes | yes, needs SMTP |
| Two-factor sign-in, OpenID Connect sign-in, avatars | yes | yes, opt-in |
| Reading requires authentication (private feed) | no | no |
| Content moderation workflow | yes | reports only; owners and admins act on them |

* BaGet covers the v3 services ([NuGet/NuGet.Services.Metadata](https://github.com/NuGet/NuGet.Services.Metadata/)) and a subset of the Gallery
* Still missing: private (authenticated) reads, per-day statistics, a prefix-reservation page, malware and license scanning, moderation beyond abuse reports
* nuget.org's v3 implementation is static
    * Runs only on Windows
    * Highly tied to Azure
    * Scales reads to near infinity
    * Doesn't scale well for writes
    * Static JSON files are hosted on Azure Blob Storage
    * Served by a Content Delivery Network
    * Files are updated by `feed2catalog`, `catalog2registration`, `catalog2dnx`, and `catalog2lucene` jobs
* BaGet's v3 implementation is dynamic
    * Cross-platform implementation
    * Requests are served by a service that queries a database
    * Simpler architecture, which makes it easier to deploy and run small feeds
    * Easier to scale for writes
    * Harder to scale for reads
    * Easier to add new features
    * Harder to make as reliable