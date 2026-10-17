# BaGet

BaGet (pronounced "baguette") is a lightweight NuGet and symbol server. It is [open source](https://github.com/loic-sharma/BaGet), cross-platform, and cloud ready!

<p align="center">
  <img width="100%" src="https://user-images.githubusercontent.com/737941/50140219-d8409700-0258-11e9-94c9-dad24d2b48bb.png">
</p>

## Run BaGet

You can run BaGet on your preferred platform:

* [On your computer](installation/local.md)
* [Docker](installation/docker.md)
* [Azure](installation/azure.md)
* [AWS](installation/aws.md)
* [Google Cloud](installation/gcp.md)
* [Alibaba Cloud (Aliyun)](installation/aliyun.md)

## What BaGet serves

BaGet speaks the NuGet V3 protocol, with a V2 feed for older clients, and serves symbols. Accounts
with package owners and per-user API keys, and a validation pipeline with repository signing, can
be switched on in its [configuration](configuration.md). The [server API](advanced/api.md) lists
every resource and page, and [BaGet vs nuget.org](vs/nugetorg.md) compares the two.

## BaGet SDK

You can also use the [`BaGet.Protocol`](https://www.nuget.org/packages/BaGet.Protocol) package to interact with a NuGet server. For more information, please refer to the [BaGet SDK](advanced/sdk.md) guide.
