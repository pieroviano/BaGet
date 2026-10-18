# BaGet.Core

Contains BaGet's core logic. The important folders are:

* Authentication - API key authentication, and the `IPackageAuthorizationService` that accounts plug into
* Configuration - BaGet's options (`BaGetOptions`, `AccountsOptions`, `ValidationOptions`, ...)
* Content - Serves package content
* Entities - The database model, shared by every database provider
* Indexing - Logic to upload packages and symbols, and the `IPackageChangeHandler` hook told about every change
* Metadata - Builds registration (package metadata) responses
* Search - Used to search for packages
* ServiceIndex - The service index, which other projects add resources to through `IServiceIndexResourceProvider`
* Staging - `IPackageStagingService`: holds a browser upload (`/upload`) in storage between its preview and its confirmation
* Statistics - Feed-wide download statistics
* Storage - Stores the content of packages and symbols
* Upstream - Interactions with an upstream NuGet feed, for mirroring
* Validation - Validation of BaGet's own configuration (package validation is in `BaGet.Validation`)
