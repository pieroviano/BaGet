# LiGet

!!! warning
    This page is a work in progress!

[LiGet](https://github.com/ai-traders/liget) is a NuGet server created with a linux-first approach.

* LiGet
    * Strong support for Paket
    * Only supports NuGet's v2 APIs (missing verified packages, signed packages, etc...)
    * Stores all packages' metadata using a single JSON file
* BaGet
    * Supports NuGet's v3 APIs
    * Stores packages' metadata in a database
    * Can mirror nuget.org on demand (read-through)
    * Serves a V2 feed at `/api/v2`, and supports repository signing and verified owners
    * Can run on Azure