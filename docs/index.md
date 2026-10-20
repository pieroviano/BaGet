# BaGet Extension

BaGet Extension is a lightweight NuGet and symbol server. It is [open source](https://github.com/pieroviano/BaGet) (a fork of [loic-sharma/BaGet](https://github.com/loic-sharma/BaGet)), cross-platform, and cloud ready!

<p align="center">
  <img width="100%" src="BaGetSite.png">
</p>

## Run BaGet Extension

You can run BaGet Extension on your preferred platform:

* [On your computer](installation/local.md)
* [Docker](installation/docker.md)
* [Azure](installation/azure.md)
* [AWS](installation/aws.md)
* [Google Cloud](installation/gcp.md)
* [Alibaba Cloud (Aliyun)](installation/aliyun.md)
* [Windows, behind an IIS proxy](installation/iis-proxy.md)

## What BaGet Extension serves

BaGet Extension speaks the NuGet V3 protocol, with a V2 feed for older clients (on by default), and serves
symbols. Accounts with package owners and per-user API keys are on by default; a validation
pipeline with repository signing is opt-in. Both are set in its [configuration](configuration.md).
By default it listens on `http://localhost:6060` (`Kestrel:Endpoints`; `--urls` has no effect). The [server API](advanced/api.md) lists
every resource and page, and [BaGet Extension vs nuget.org](vs/nugetorg.md) compares the two.

## Import packages

* [From nuget.org](import/nugetorg.md)
* [From NuGet.Server](import/nugetserver.md)
* [From a local feed](import/local-feeds.md)

## Compare BaGet Extension

* [nuget.org](vs/nugetorg.md)
* [Artifactory](vs/artifactory.md)
* [Azure Artifacts](vs/azure-artifacts.md)
* [LiGet](vs/liget.md)
* [Local feeds](vs/local-feeds.md)
* [MyGet](vs/myget.md)
* [Nexus](vs/nexus.md)
* [NuGet.Server](vs/nugetserver.md)
* [TeamCity](vs/teamcity.md)

## Advanced

* [Architecture](advanced/architecture.md)
* [Server API](advanced/api.md)
* [Debugging BaGet Extension](advanced/debugging.md)

## BaGet Extension SDK

You can also use the [`BaGet.Protocol`](https://www.nuget.org/packages/BaGet.Protocol) package to interact with a NuGet server. For more information, please refer to the [BaGet Extension SDK](advanced/sdk.md) guide.

To browse these pages without a site, see [reading this folder](https://github.com/pieroviano/BaGet/tree/main/docs).

## Credits

BaGet Extension is based on [BaGet](https://github.com/loic-sharma/BaGet) (pronounced "baguette") by Loic Sharma,
released under the MIT licence. See [LICENSE](https://github.com/pieroviano/BaGet/blob/main/LICENSE) and
[Acknowledgments](https://github.com/pieroviano/BaGet/blob/main/Acknowledgments.md).
