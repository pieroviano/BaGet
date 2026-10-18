# Nullable conversion report

Source: `D:\CommonLibrary\BaGet\BaGet.sln`  
Generated: 2026-10-18 11:28

## Summary

| | Before | After |
|---|---|---|
| Compile errors | 0 | 0 |
| Nullable warnings | 1219 | 801 |
| Other warnings | 11 | 11 |

- Annotations: 1302
- Signature alignments: 157
- Conditional accesses: 156
- Guards: 0
- `var` rewrites: 4
- Silenced with `#pragma`: 714 region(s) in 168 file(s)
- Iterations: 20 (**did not converge**)
- Files written: 373

## Projects

| Project | Kind | Added | Replaced | Kept |
|---|---|---|---|---|
| `samples\BaGet.Protocol.Samples.Tests\BaGet.Protocol.Samples.Tests.csproj` | test (PackageReference xunit.v3) | LangVersion = latest<br>Nullable = annotations | NoWarn: IDE0007 -> IDE0007;CS8600;CS8601;CS8602;CS8603;CS8604;CS8605;CS8607;CS8608;CS8609;CS8610;CS8611;CS8612;CS8613;CS8614;CS8615;CS8616;CS8617;CS8618;CS8619;CS8620;CS8621;CS8622;CS8624;CS8625;CS8629;CS8631;CS8633;CS8634;CS8643;CS8644;CS8645;CS8653;CS8654;CS8655;CS8667;CS8670;CS8714;CS8762;CS8764;CS8765;CS8766;CS8767;CS8768;CS8769;CS8770;CS8774;CS8775;CS8776;CS8777;CS9264 | — |
| `samples\BaGetWebApplication\BaGetWebApplication.csproj` | production | LangVersion = latest<br>Nullable = enable | — | — |
| `src\BaGet.Aliyun\BaGet.Aliyun.csproj` | production | Nullable = enable | — | LangVersion = latest |
| `src\BaGet.Aws\BaGet.Aws.csproj` | production | Nullable = enable | — | LangVersion = latest |
| `src\BaGet.Azure\BaGet.Azure.csproj` | production | Nullable = enable | — | LangVersion = latest |
| `src\BaGet.Catalog\BaGet.Catalog.csproj` | production | Nullable = enable | — | LangVersion = latest |
| `src\BaGet.Core\BaGet.Core.csproj` | production | Nullable = enable | — | LangVersion = latest |
| `src\BaGet.Database.MySql\BaGet.Database.MySql.csproj` | production | Nullable = enable | — | LangVersion = latest |
| `src\BaGet.Database.PostgreSql\BaGet.Database.PostgreSql.csproj` | production | Nullable = enable | — | LangVersion = latest |
| `src\BaGet.Database.Sqlite\BaGet.Database.Sqlite.csproj` | production | Nullable = enable | — | LangVersion = latest |
| `src\BaGet.Database.SqlServer\BaGet.Database.SqlServer.csproj` | production | Nullable = enable | — | LangVersion = latest |
| `src\BaGet.Gcp\BaGet.Gcp.csproj` | production | Nullable = enable | — | LangVersion = latest |
| `src\BaGet.Identity\BaGet.Identity.csproj` | production | Nullable = enable | — | LangVersion = latest |
| `src\BaGet.Protocol\BaGet.Protocol.csproj` | production | Nullable = enable | — | LangVersion = latest |
| `src\BaGet.Validation\BaGet.Validation.csproj` | production | Nullable = enable | — | LangVersion = latest |
| `src\BaGet.Web.V2\BaGet.Web.V2.csproj` | production | Nullable = enable | — | LangVersion = latest |
| `src\BaGet.Web\BaGet.Web.csproj` | production | Nullable = enable | — | LangVersion = latest |
| `src\BaGet\BaGet.csproj` | production | Nullable = enable | — | LangVersion = latest |
| `tests\BaGet.Azure.Tests\BaGet.Azure.Tests.csproj` | production | Nullable = enable | — | LangVersion = latest |
| `tests\BaGet.Core.Tests\BaGet.Core.Tests.csproj` | production | Nullable = enable | — | LangVersion = latest |
| `tests\BaGet.Protocol.Tests\BaGet.Protocol.Tests.csproj` | production | Nullable = enable | — | LangVersion = latest |
| `tests\BaGet.Tests\BaGet.Tests.csproj` | production | Nullable = enable | — | LangVersion = latest |
| `tests\BaGet.Web.Tests\BaGet.Web.Tests.csproj` | production | Nullable = enable | — | LangVersion = latest |

## How the copy loaded

An expression whose type does not resolve is never annotated, so a project that does not compile converts less.

| Configuration | References | Pre-existing errors |
|---|---|---|
| BaGet.Protocol | 172 | 0 |
| BaGet.Core | 201 | 0 |
| BaGet.Database.Sqlite | 225 | 0 |
| BaGet.Aws | 204 | 0 |
| BaGet.Catalog | 321 | 0 |
| BaGet.Web.V2 | 321 | 0 |
| BaGet.Database.MySql | 222 | 0 |
| BaGet.Gcp | 211 | 0 |
| BaGet.Core.Tests | 221 | 0 |
| BaGet.Identity | 334 | 0 |
| BaGet.Validation | 321 | 0 |
| BaGet.Web | 336 | 0 |
| BaGet.Database.SqlServer | 240 | 0 |
| BaGet.Database.PostgreSql | 222 | 0 |
| BaGet.Aliyun | 202 | 0 |
| BaGet.Azure | 215 | 0 |
| BaGet | 407 | 0 |
| BaGet.Web.Tests | 349 | 0 |
| BaGet.Azure.Tests | 229 | 0 |
| BaGet.Tests | 410 | 0 |
| BaGet.Protocol.Tests | 186 | 0 |
| BaGetWebApplication | 341 | 0 |

## Public API changes

720 signature(s) reachable from outside their assembly changed. Each is a breaking change for a consumer compiled against the old signature.

| File | Diagnostic | Change |
|---|---|---|
| `AliyunStorageOptions.cs` | CS8618 | AccessKey: string -> string? |
| `AliyunStorageOptions.cs` | CS8618 | AccessKeySecret: string -> string? |
| `AliyunStorageOptions.cs` | CS8618 | Endpoint: string -> string? |
| `AliyunStorageOptions.cs` | CS8618 | Bucket: string -> string? |
| `AliyunStorageOptions.cs` | CS8618 | Prefix: string -> string? |
| `S3StorageOptions.cs` | CS8618 | AccessKey: string -> string? |
| `S3StorageOptions.cs` | CS8618 | SecretKey: string -> string? |
| `S3StorageOptions.cs` | CS8618 | Region: string -> string? |
| `S3StorageOptions.cs` | CS8618 | Bucket: string -> string? |
| `S3StorageOptions.cs` | CS8618 | Prefix: string -> string? |
| `S3StorageOptions.cs` | CS8618 | AssumeRoleArn: string -> string? |
| `AzureBlobStorageOptions.cs` | CS8618 | ConnectionString: string -> string? |
| `AzureBlobStorageOptions.cs` | CS8618 | AccountName: string -> string? |
| `AzureBlobStorageOptions.cs` | CS8618 | AccessKey: string -> string? |
| `AzureBlobStorageOptions.cs` | CS8618 | Container: string -> string? |
| `AzureSearchOptions.cs` | CS8618 | AccountName: string -> string? |
| `AzureSearchOptions.cs` | CS8618 | ApiKey: string -> string? |
| `AzureTableOptions.cs` | CS8618 | ConnectionString: string -> string? |
| `AzureSearchService.cs` | CS8767 | FindDependentsAsync: align with ISearchService.FindDependentsAsync |
| `PackageDocument.cs` | CS8601 | Id: string -> string? |
| `PackageDocument.cs` | CS8618 | NormalizedId: string -> string? |
| `PackageDocument.cs` | CS8618 | Version: string -> string? |
| `PackageDocument.cs` | CS8601 | Description: string -> string? |
| `PackageDocument.cs` | CS8601 | Authors: string[] -> string[]? |
| `PackageDocument.cs` | CS8618 | IconUrl: string -> string? |
| `PackageDocument.cs` | CS8618 | LicenseUrl: string -> string? |
| `PackageDocument.cs` | CS8618 | ProjectUrl: string -> string? |
| `PackageDocument.cs` | CS8601 | Summary: string -> string? |
| `PackageDocument.cs` | CS8601 | Tags: string[] -> string[]? |
| `PackageDocument.cs` | CS8601 | Title: string -> string? |
| `PackageDocument.cs` | CS8618 | Versions: string[] -> string[]? |
| `PackageDocument.cs` | CS8618 | VersionDownloads: string[] -> string[]? |
| `PackageDocument.cs` | CS8619 | string[] -> string?[] |
| `PackageDocument.cs` | CS8619 | string[] -> string?[] |
| `PackageDocument.cs` | CS8618 | Frameworks: string[] -> string[]? |
| `PackageDocument.cs` | CS8618 | SearchFilters: string -> string? |
| `PackageDocument.cs` | CS8618 | Key: string -> string? |
| `PackageDocument.cs` | CS8618 | Dependencies: string?[] -> string?[]? |
| `PackageDocument.cs` | CS8618 | PackageTypes: string?[] -> string?[]? |
| `PackageDocument.cs` | CS8766 | IKeyedDocument.Key: widen to match KeyedDocument.Key |
| `PackageEntity.cs` | CS8618 | Id: string -> string? |
| `PackageEntity.cs` | CS8618 | NormalizedVersion: string -> string? |
| `PackageEntity.cs` | CS8618 | OriginalVersion: string -> string? |
| `PackageEntity.cs` | CS8618 | Authors: string -> string? |
| `PackageEntity.cs` | CS8601 | Description: string -> string? |
| `PackageEntity.cs` | CS8601 | Language: string -> string? |
| `PackageEntity.cs` | CS8601 | MinClientVersion: string -> string? |
| `PackageEntity.cs` | CS8601 | ReleaseNotes: string -> string? |
| `PackageEntity.cs` | CS8601 | Summary: string -> string? |
| `PackageEntity.cs` | CS8601 | Title: string -> string? |
| `PackageEntity.cs` | CS8618 | IconUrl: string -> string? |
| `PackageEntity.cs` | CS8618 | LicenseUrl: string -> string? |
| `PackageEntity.cs` | CS8618 | ProjectUrl: string -> string? |
| `PackageEntity.cs` | CS8618 | RepositoryUrl: string -> string? |
| `PackageEntity.cs` | CS8601 | RepositoryType: string -> string? |
| `PackageEntity.cs` | CS8618 | Tags: string -> string? |
| `PackageEntity.cs` | CS8618 | Dependencies: string -> string? |
| `PackageEntity.cs` | CS8618 | PackageTypes: string -> string? |
| `PackageEntity.cs` | CS8618 | TargetFrameworks: string -> string? |
| `PackageEntity.cs` | CS8601 | Id: string -> string? |
| `PackageEntity.cs` | CS8601 | VersionRange: string -> string? |
| `PackageEntity.cs` | CS8601 | TargetFramework: string -> string? |
| `PackageEntity.cs` | CS8601 | Name: string -> string? |
| `PackageEntity.cs` | CS8601 | Version: string -> string? |
| `TablePackageDatabase.cs` | CS8767 | ExistsAsync: align with IPackageDatabase.ExistsAsync |
| `TablePackageDatabase.cs` | CS8767 | FindAsync: align with IPackageDatabase.FindAsync |
| `TablePackageDatabase.cs` | CS8613 | FindOrNullAsync: align with IPackageDatabase.FindOrNullAsync |
| `TablePackageDatabase.cs` | CS8603 | FindOrNullAsync: Package -> Package? |
| `TablePackageDatabase.cs` | CS8767 | HardDeletePackageAsync: align with IPackageDatabase.HardDeletePackageAsync |
| `TablePackageDatabase.cs` | CS8767 | ExistsAsync: align with IPackageDatabase.ExistsAsync |
| `TablePackageDatabase.cs` | CS8767 | RelistPackageAsync: align with IPackageDatabase.RelistPackageAsync |
| `TablePackageDatabase.cs` | CS8767 | UnlistPackageAsync: align with IPackageDatabase.UnlistPackageAsync |
| `TablePackageDatabase.cs` | CS8767 | SetDeprecationAsync: align with IPackageDatabase.SetDeprecationAsync |
| `TablePackageDatabase.cs` | CS8767 | RemoveDeprecationAsync: align with IPackageDatabase.RemoveDeprecationAsync |
| `TableSearchService.cs` | CS8767 | FindDependentsAsync: align with ISearchService.FindDependentsAsync |
| `CatalogDatabase.cs` | CS8603 | FindOrNullAsync: CatalogEntry -> CatalogEntry? |
| `CatalogDatabase.cs` | CS8613 | FindOrNullAsync: align with ICatalogDatabase.FindOrNullAsync |
| `CatalogReader.cs` | CS8603 | GetPageOrNullAsync: CatalogPage -> CatalogPage? |
| `CatalogReader.cs` | CS8603 | GetLeafOrNullAsync: string -> string? |
| `ICatalogDatabase.cs` | CS8613 | ICatalogDatabase.FindOrNullAsync: widen to match CatalogDatabase.FindOrNullAsync |
| `ApiKeyAuthenticationService.cs` | CS8767 | AuthenticateAsync: align with IAuthenticationService.AuthenticateAsync |
| `BaGetPrincipal.cs` | CS8601 | UserId: string -> string? |
| `BaGetPrincipal.cs` | CS8601 | UserName: string -> string? |
| `IAuthenticationService.cs` | CS8603 | AuthenticatePrincipalAsync: BaGetPrincipal -> BaGetPrincipal? |
| `IPackageAuthorizationService.cs` | CS8767 | OnPushedAsync: align with IPackageAuthorizationService.OnPushedAsync |
| `IPackageAuthorizationService.cs` | CS8767 | AuthorizeAsync: align with IPackageAuthorizationService.AuthorizeAsync |
| `AccountsOptions.cs` | CS8618 | InitialAdministrators: string -> string? |
| `AccountsOptions.cs` | CS8618 | Authority: string -> string? |
| `AccountsOptions.cs` | CS8618 | ClientId: string -> string? |
| `AccountsOptions.cs` | CS8618 | ClientSecret: string -> string? |
| `BaGetOptions.cs` | CS8618 | ApiKey: string -> string? |
| `BaGetOptions.cs` | CS8618 | PathBase: string -> string? |
| `BaGetOptions.cs` | CS8618 | AllowedHosts: string -> string? |
| `BaGetOptions.cs` | CS8618 | Urls: string -> string? |
| `BaGetOptions.cs` | CS8618 | Database: DatabaseOptions -> DatabaseOptions? |
| `BaGetOptions.cs` | CS8618 | Storage: StorageOptions -> StorageOptions? |
| `BaGetOptions.cs` | CS8618 | Search: SearchOptions -> SearchOptions? |
| `BaGetOptions.cs` | CS8618 | Mirror: MirrorOptions -> MirrorOptions? |
| `BaGetOptions.cs` | CS8618 | Feeds: FeedsOptions -> FeedsOptions? |
| `BaGetOptions.cs` | CS8618 | Accounts: AccountsOptions -> AccountsOptions? |
| `BaGetOptions.cs` | CS8618 | Email: EmailOptions -> EmailOptions? |
| `BaGetOptions.cs` | CS8618 | Site: SiteOptions -> SiteOptions? |
| `BaGetOptions.cs` | CS8618 | Validation: ValidationOptions -> ValidationOptions? |
| `DatabaseOptions.cs` | CS8618 | Type: string -> string? |
| `DatabaseOptions.cs` | CS8618 | ConnectionString: string -> string? |
| `DatabaseOptions.cs` | CS8618 | MySqlServerVersion: string -> string? |
| `EmailOptions.cs` | CS8618 | Host: string -> string? |
| `EmailOptions.cs` | CS8618 | UserName: string -> string? |
| `EmailOptions.cs` | CS8618 | Password: string -> string? |
| `EmailOptions.cs` | CS8618 | From: string -> string? |
| `FileSystemStorageOptions.cs` | CS8618 | Path: string -> string? |
| `MirrorOptions.cs` | CS8618 | PackageSource: Uri -> Uri? |
| `SearchOptions.cs` | CS8618 | Type: string -> string? |
| `SettingsCatalog.cs` | CS8603 | Find: SettingDescriptor -> SettingDescriptor? |
| `SettingsOverridesStore.cs` | CS8604 | KeyValuePair<TValue>: string -> string? |
| `SettingsService.cs` | CS8603 | OverridesPath: string -> string? |
| `SettingsService.cs` | CS8618 | OverridesError: string -> string? |
| `SiteOptions.cs` | CS8765 | IsValid: align with ValidationAttribute.IsValid |
| `SiteOptions.cs` | CS8603 | IsValid: ValidationResult -> ValidationResult? |
| `SiteOptions.cs` | CS8618 | TermsOfUsePath: string -> string? |
| `SiteOptions.cs` | CS8618 | PrivacyPolicyPath: string -> string? |
| `SiteOptions.cs` | CS8618 | PublicUrl: string -> string? |
| `StorageOptions.cs` | CS8618 | Type: string -> string? |
| `ValidationOptions.cs` | CS8618 | RepositorySigningCertificatePath: string -> string? |
| `ValidationOptions.cs` | CS8618 | RepositorySigningCertificatePassword: string -> string? |
| `ValidationOptions.cs` | CS8618 | RepositorySigningServiceIndexUrl: string -> string? |
| `DefaultPackageContentService.cs` | CS8603 | GetPackageVersionsOrNullAsync: PackageVersionsResponse -> PackageVersionsResponse? |
| `DefaultPackageContentService.cs` | CS8603 | GetPackageContentStreamOrNullAsync: Stream -> Stream? |
| `DefaultPackageContentService.cs` | CS8603 | GetPackageManifestStreamOrNullAsync: Stream -> Stream? |
| `DefaultPackageContentService.cs` | CS8603 | GetPackageReadmeStreamOrNullAsync: Stream -> Stream? |
| `DefaultPackageContentService.cs` | CS8603 | GetPackageIconStreamOrNullAsync: Stream -> Stream? |
| `DefaultPackageContentService.cs` | CS8767 | GetPackageReadmeStreamOrNullAsync: align with IPackageContentService.GetPackageReadmeStreamOrNullAsync |
| `IPackageContentService.cs` | CS8613 | IPackageContentService.GetPackageVersionsOrNullAsync: widen to match DefaultPackageContentService.GetPackageVersionsOrNullAsync |
| `IPackageContentService.cs` | CS8613 | IPackageContentService.GetPackageContentStreamOrNullAsync: widen to match DefaultPackageContentService.GetPackageContentStreamOrNullAsync |
| `IPackageContentService.cs` | CS8613 | IPackageContentService.GetPackageManifestStreamOrNullAsync: widen to match DefaultPackageContentService.GetPackageManifestStreamOrNullAsync |
| `IPackageContentService.cs` | CS8613 | IPackageContentService.GetPackageReadmeStreamOrNullAsync: widen to match DefaultPackageContentService.GetPackageReadmeStreamOrNullAsync |
| `IPackageContentService.cs` | CS8613 | IPackageContentService.GetPackageIconStreamOrNullAsync: widen to match DefaultPackageContentService.GetPackageIconStreamOrNullAsync |
| `AbstractContext.cs` | CS8618 | Packages: DbSet<Package> -> DbSet<Package>? |
| `AbstractContext.cs` | CS8618 | PackageDependencies: DbSet<PackageDependency> -> DbSet<PackageDependency>? |
| `AbstractContext.cs` | CS8618 | PackageTypes: DbSet<PackageType> -> DbSet<PackageType>? |
| `AbstractContext.cs` | CS8618 | TargetFrameworks: DbSet<TargetFramework> -> DbSet<TargetFramework>? |
| `AbstractContext.cs` | CS8618 | PackageDeprecations: DbSet<PackageDeprecation> -> DbSet<PackageDeprecation>? |
| `AbstractContext.cs` | CS8618 | PackageVulnerabilities: DbSet<PackageVulnerability> -> DbSet<PackageVulnerability>? |
| `AbstractContext.cs` | CS8618 | CatalogLeafs: DbSet<CatalogEntry> -> DbSet<CatalogEntry>? |
| `AbstractContext.cs` | CS8618 | Organizations: DbSet<Organization> -> DbSet<Organization>? |
| `AbstractContext.cs` | CS8618 | OrganizationMembers: DbSet<OrganizationMember> -> DbSet<OrganizationMember>? |
| `AbstractContext.cs` | CS8618 | PackageOwners: DbSet<PackageOwner> -> DbSet<PackageOwner>? |
| `AbstractContext.cs` | CS8618 | ApiKeys: DbSet<ApiKey> -> DbSet<ApiKey>? |
| `AbstractContext.cs` | CS8618 | PackageValidations: DbSet<PackageValidation> -> DbSet<PackageValidation>? |
| `AbstractContext.cs` | CS8618 | AbuseReports: DbSet<AbuseReport> -> DbSet<AbuseReport>? |
| `AbuseReport.cs` | CS8618 | PackageId: string -> string? |
| `AbuseReport.cs` | CS8618 | PackageVersion: string -> string? |
| `AbuseReport.cs` | CS8618 | Reason: string -> string? |
| `AbuseReport.cs` | CS8618 | Details: string -> string? |
| `AbuseReport.cs` | CS8618 | ReporterEmail: string -> string? |
| `Accounts.cs` | CS8618 | Name: string -> string? |
| `Accounts.cs` | CS8618 | Organization: Organization -> Organization? |
| `Accounts.cs` | CS8618 | UserId: string -> string? |
| `Accounts.cs` | CS8618 | PackageId: string -> string? |
| `Accounts.cs` | CS8618 | OwnerName: string -> string? |
| `Accounts.cs` | CS8618 | UserId: string -> string? |
| `Accounts.cs` | CS8618 | Description: string -> string? |
| `Accounts.cs` | CS8618 | HashedKey: string -> string? |
| `Accounts.cs` | CS8618 | Scopes: string[] -> string[]? |
| `Accounts.cs` | CS8618 | PackageGlobPattern: string -> string? |
| `CatalogEntry.cs` | CS8618 | CommitId: string -> string? |
| `CatalogEntry.cs` | CS8618 | Type: string -> string? |
| `CatalogEntry.cs` | CS8618 | PackageId: string -> string? |
| `CatalogEntry.cs` | CS8618 | PackageVersion: string -> string? |
| `StringArrayToJsonConverter.cs` | CS8620 | StringArrayToJsonConverter -> ValueConverter<string[]?, string>? |
| `UriToStringConverter.cs` | CS8620 | UriToStringConverter -> ValueConverter<Uri?, string>? |
| `IContext.cs` | CS8766 | IContext.Packages: widen to match AbstractContext.Packages |
| `IContext.cs` | CS8766 | IContext.PackageDependencies: widen to match AbstractContext.PackageDependencies |
| `IContext.cs` | CS8766 | IContext.PackageDeprecations: widen to match AbstractContext.PackageDeprecations |
| `IContext.cs` | CS8766 | IContext.PackageVulnerabilities: widen to match AbstractContext.PackageVulnerabilities |
| `IContext.cs` | CS8766 | IContext.CatalogLeafs: widen to match AbstractContext.CatalogLeafs |
| `IContext.cs` | CS8766 | IContext.Organizations: widen to match AbstractContext.Organizations |
| `IContext.cs` | CS8766 | IContext.OrganizationMembers: widen to match AbstractContext.OrganizationMembers |
| `IContext.cs` | CS8766 | IContext.PackageOwners: widen to match AbstractContext.PackageOwners |
| `IContext.cs` | CS8766 | IContext.ApiKeys: widen to match AbstractContext.ApiKeys |
| `IContext.cs` | CS8766 | IContext.PackageValidations: widen to match AbstractContext.PackageValidations |
| `IContext.cs` | CS8766 | IContext.AbuseReports: widen to match AbstractContext.AbuseReports |
| `NullContext.cs` | CS8767 | Packages: align with IContext.Packages |
| `NullContext.cs` | CS8767 | PackageDependencies: align with IContext.PackageDependencies |
| `NullContext.cs` | CS8767 | PackageDeprecations: align with IContext.PackageDeprecations |
| `NullContext.cs` | CS8767 | PackageVulnerabilities: align with IContext.PackageVulnerabilities |
| `NullContext.cs` | CS8767 | CatalogLeafs: align with IContext.CatalogLeafs |
| `NullContext.cs` | CS8767 | Organizations: align with IContext.Organizations |
| `NullContext.cs` | CS8767 | OrganizationMembers: align with IContext.OrganizationMembers |
| `NullContext.cs` | CS8767 | PackageOwners: align with IContext.PackageOwners |
| `NullContext.cs` | CS8767 | ApiKeys: align with IContext.ApiKeys |
| `NullContext.cs` | CS8767 | PackageValidations: align with IContext.PackageValidations |
| `NullContext.cs` | CS8767 | AbuseReports: align with IContext.AbuseReports |
| `Package.cs` | CS8601 | Id: string -> string? |
| `Package.cs` | CS8618 | Authors: string[] -> string[]? |
| `Package.cs` | CS8601 | Description: string -> string? |
| `Package.cs` | CS8618 | ReleaseNotes: string -> string? |
| `Package.cs` | CS8601 | Language: string -> string? |
| `Package.cs` | CS8601 | MinClientVersion: string -> string? |
| `Package.cs` | CS8601 | Summary: string -> string? |
| `Package.cs` | CS8601 | Title: string -> string? |
| `Package.cs` | CS8618 | IconUrl: Uri -> Uri? |
| `Package.cs` | CS8618 | LicenseUrl: Uri -> Uri? |
| `Package.cs` | CS8618 | ProjectUrl: Uri -> Uri? |
| `Package.cs` | CS8625 | RepositoryUrl: Uri -> Uri? |
| `Package.cs` | CS8625 | RepositoryType: string -> string? |
| `Package.cs` | CS8601 | Tags: string[] -> string[]? |
| `Package.cs` | CS8618 | RowVersion: byte[] -> byte[]? |
| `Package.cs` | CS8618 | Dependencies: List<PackageDependency> -> List<PackageDependency>? |
| `Package.cs` | CS8618 | PackageTypes: List<PackageType> -> List<PackageType>? |
| `Package.cs` | CS8618 | TargetFrameworks: List<TargetFramework> -> List<TargetFramework>? |
| `Package.cs` | CS8618 | Deprecation: PackageDeprecation -> PackageDeprecation? |
| `Package.cs` | CS8618 | Vulnerabilities: List<PackageVulnerability> -> List<PackageVulnerability>? |
| `Package.cs` | CS8618 | NormalizedVersionString: string -> string? |
| `Package.cs` | CS8601 | OriginalVersionString: string -> string? |
| `PackageDependency.cs` | CS8625 | Id: string -> string? |
| `PackageDependency.cs` | CS8625 | VersionRange: string -> string? |
| `PackageDependency.cs` | CS8601 | TargetFramework: string -> string? |
| `PackageDependency.cs` | CS8618 | Package: Package -> Package? |
| `PackageDeprecation.cs` | CS8618 | Reasons: string[] -> string[]? |
| `PackageDeprecation.cs` | CS8618 | Message: string -> string? |
| `PackageDeprecation.cs` | CS8618 | AlternatePackageId: string -> string? |
| `PackageDeprecation.cs` | CS8618 | AlternateVersionRange: string -> string? |
| `PackageDeprecation.cs` | CS8618 | Package: Package -> Package? |
| `PackageType.cs` | CS8618 | Name: string -> string? |
| `PackageType.cs` | CS8618 | Version: string -> string? |
| `PackageType.cs` | CS8618 | Package: Package -> Package? |
| `PackageValidation.cs` | CS8618 | SubmissionId: string -> string? |
| `PackageValidation.cs` | CS8618 | PackageId: string -> string? |
| `PackageValidation.cs` | CS8618 | PackageVersion: string -> string? |
| `PackageValidation.cs` | CS8618 | Package: Package -> Package? |
| `PackageValidation.cs` | CS8618 | Type: string -> string? |
| `PackageValidation.cs` | CS8618 | Message: string -> string? |
| `PackageVulnerability.cs` | CS8618 | AdvisoryUrl: string -> string? |
| `PackageVulnerability.cs` | CS8618 | VersionRange: string -> string? |
| `PackageVulnerability.cs` | CS8618 | Package: Package -> Package? |
| `TargetFramework.cs` | CS8618 | Moniker: string -> string? |
| `TargetFramework.cs` | CS8618 | Package: Package -> Package? |
| `DependencyInjectionExtensions.Providers.cs` | CS8603 | GetServiceFromProviders: TService -> TService? |
| `FrameworkCompatibilityService.cs` | CS8619 | Dictionary<string, NuGetFramework> -> Dictionary<string, NuGetFramework?> |
| `FrameworkCompatibilityService.cs` | CS8604 | HashSet<T>: string -> string? |
| `FrameworkCompatibilityService.cs` | CS8604 | HashSet<T>: string -> string? |
| `FrameworkCompatibilityService.cs` | CS8619 | IReadOnlyList<string> -> List<string?> |
| `FrameworkCompatibilityService.cs` | CS8619 | IReadOnlyList<string> -> List<string?> |
| `PackageDeletionService.cs` | CS8767 | TryDeletePackageAsync: align with IPackageDeletionService.TryDeletePackageAsync |
| `PackageDeletionService.cs` | CS8767 | TryHardDeletePackageAsync: align with IPackageDeletionService.TryHardDeletePackageAsync |
| `PackageIndexingService.cs` | CS8767 | IndexAsync: align with IPackageIndexingService.IndexAsync |
| `SymbolIndexingService.cs` | CS8603 | GetSymbolPackagePdbPathsOrNullAsync: IReadOnlyList<string> -> IReadOnlyList<string>? |
| `SymbolIndexingService.cs` | CS8603 | ExtractPortablePdbOrNullAsync: PortablePdb -> PortablePdb? |
| `SymbolIndexingService.cs` | CS8767 | IndexAsync: align with ISymbolIndexingService.IndexAsync |
| `IPackageDatabase.cs` | CS8613 | IPackageDatabase.FindOrNullAsync: widen to match PackageDatabase.FindOrNullAsync |
| `IPackageService.cs` | CS8613 | IPackageService.FindPackageOrNullAsync: widen to match PackageService.FindPackageOrNullAsync |
| `IUrlGenerator.cs` | CS8766 | IUrlGenerator.GetServiceIndexUrl: widen to match BaGetUrlGenerator.GetServiceIndexUrl |
| `IUrlGenerator.cs` | CS8766 | IUrlGenerator.GetPackagePublishResourceUrl: widen to match BaGetUrlGenerator.GetPackagePublishResourceUrl |
| `IUrlGenerator.cs` | CS8766 | IUrlGenerator.GetSymbolPublishResourceUrl: widen to match BaGetUrlGenerator.GetSymbolPublishResourceUrl |
| `IUrlGenerator.cs` | CS8766 | IUrlGenerator.GetSearchResourceUrl: widen to match BaGetUrlGenerator.GetSearchResourceUrl |
| `IUrlGenerator.cs` | CS8766 | IUrlGenerator.GetAutocompleteResourceUrl: widen to match BaGetUrlGenerator.GetAutocompleteResourceUrl |
| `IUrlGenerator.cs` | CS8766 | IUrlGenerator.GetV2FeedUrl: widen to match BaGetUrlGenerator.GetV2FeedUrl |
| `IUrlGenerator.cs` | CS8766 | IUrlGenerator.GetRegistrationIndexUrl: widen to match BaGetUrlGenerator.GetRegistrationIndexUrl |
| `IUrlGenerator.cs` | CS8766 | IUrlGenerator.GetRegistrationLeafUrl: widen to match BaGetUrlGenerator.GetRegistrationLeafUrl |
| `IUrlGenerator.cs` | CS8766 | IUrlGenerator.GetPackageVersionsUrl: widen to match BaGetUrlGenerator.GetPackageVersionsUrl |
| `IUrlGenerator.cs` | CS8766 | IUrlGenerator.GetPackageDownloadUrl: widen to match BaGetUrlGenerator.GetPackageDownloadUrl |
| `IUrlGenerator.cs` | CS8766 | IUrlGenerator.GetPackageManifestDownloadUrl: widen to match BaGetUrlGenerator.GetPackageManifestDownloadUrl |
| `IUrlGenerator.cs` | CS8766 | IUrlGenerator.GetPackageIconDownloadUrl: widen to match BaGetUrlGenerator.GetPackageIconDownloadUrl |
| `BaGetPackageMetadata.cs` | CS8618 | PackageTypes: IReadOnlyList<string> -> IReadOnlyList<string>? |
| `BaGetPackageMetadata.cs` | CS8618 | ReleaseNotes: string -> string? |
| `BaGetPackageMetadata.cs` | CS8618 | RepositoryUrl: string -> string? |
| `BaGetPackageMetadata.cs` | CS8618 | RepositoryType: string -> string? |
| `BaGetPackageMetadata.cs` | CS8619 | IReadOnlyList<string> -> List<string?> |
| `BaGetRegistrationIndexPage.cs` | CS8618 | ItemsOrNull: IReadOnlyList<BaGetRegistrationIndexPageItem> -> IReadOnlyList<BaGetRegistrationIndexPageItem>? |
| `BaGetRegistrationIndexPageItem.cs` | CS8618 | PackageMetadata: BaGetPackageMetadata -> BaGetPackageMetadata? |
| `BaGetRegistrationIndexResponse.cs` | CS8618 | Pages: IReadOnlyList<BaGetRegistrationIndexPage> -> IReadOnlyList<BaGetRegistrationIndexPage>? |
| `DefaultPackageMetadataService.cs` | CS8603 | GetRegistrationIndexOrNullAsync: BaGetRegistrationIndexResponse -> BaGetRegistrationIndexResponse? |
| `DefaultPackageMetadataService.cs` | CS8603 | GetRegistrationLeafOrNullAsync: RegistrationLeafResponse -> RegistrationLeafResponse? |
| `IPackageMetadataService.cs` | CS8613 | IPackageMetadataService.GetRegistrationIndexOrNullAsync: widen to match DefaultPackageMetadataService.GetRegistrationIndexOrNullAsync |
| `IPackageMetadataService.cs` | CS8613 | IPackageMetadataService.GetRegistrationLeafOrNullAsync: widen to match DefaultPackageMetadataService.GetRegistrationLeafOrNullAsync |
| `PackageDatabase.cs` | CS8619 | Task<Package> -> Task<Package?> |
| `PackageDatabase.cs` | CS8619 | Task<Package> -> Task<Package?> |
| `PackageDatabase.cs` | CS8767 | FindAsync: align with IPackageDatabase.FindAsync |
| `PackageDatabase.cs` | CS8767 | ExistsAsync: align with IPackageDatabase.ExistsAsync |
| `PackageDatabase.cs` | CS8767 | FindOrNullAsync: align with IPackageDatabase.FindOrNullAsync |
| `PackageDatabase.cs` | CS8767 | HardDeletePackageAsync: align with IPackageDatabase.HardDeletePackageAsync |
| `PackageDatabase.cs` | CS8767 | ExistsAsync: align with IPackageDatabase.ExistsAsync |
| `PackageDatabase.cs` | CS8767 | UnlistPackageAsync: align with IPackageDatabase.UnlistPackageAsync |
| `PackageDatabase.cs` | CS8767 | RelistPackageAsync: align with IPackageDatabase.RelistPackageAsync |
| `PackageDatabase.cs` | CS8767 | SetDeprecationAsync: align with IPackageDatabase.SetDeprecationAsync |
| `PackageDatabase.cs` | CS8767 | RemoveDeprecationAsync: align with IPackageDatabase.RemoveDeprecationAsync |
| `PackageService.cs` | CS8603 | FindPackageOrNullAsync: Package -> Package? |
| `PackageService.cs` | CS8767 | FindPackageOrNullAsync: align with IPackageService.FindPackageOrNullAsync |
| `AutocompleteRequest.cs` | CS8618 | PackageType: string -> string? |
| `AutocompleteRequest.cs` | CS8618 | Query: string -> string? |
| `DatabaseSearchService.cs` | CS8619 | IQueryable<string> -> IQueryable<string?> |
| `DatabaseSearchService.cs` | CS8767 | FindDependentsAsync: align with ISearchService.FindDependentsAsync |
| `DependentsResponse.cs` | CS8618 | Data: IReadOnlyList<PackageDependent> -> IReadOnlyList<PackageDependent>? |
| `DependentsResponse.cs` | CS8618 | Id: string -> string? |
| `DependentsResponse.cs` | CS8618 | Description: string -> string? |
| `ISearchResponseBuilder.cs` | CS8620 | IReadOnlyList<string> -> List<string?> |
| `NullSearchService.cs` | CS8767 | FindDependentsAsync: align with ISearchService.FindDependentsAsync |
| `SearchRequest.cs` | CS8618 | PackageType: string -> string? |
| `SearchRequest.cs` | CS8618 | Framework: string -> string? |
| `SearchRequest.cs` | CS8618 | Query: string -> string? |
| `SearchResponseBuilder.cs` | CS8614 | BuildAutocomplete: align with ISearchResponseBuilder.BuildAutocomplete |
| `VersionsRequest.cs` | CS8618 | PackageId: string -> string? |
| `IPackageStagingService.cs` | CS8613 | IPackageStagingService.OpenAsync: widen to match StoragePackageStagingService.OpenAsync |
| `StoragePackageStagingService.cs` | CS8767 | StageAsync: align with IPackageStagingService.StageAsync |
| `StoragePackageStagingService.cs` | CS8767 | OpenAsync: align with IPackageStagingService.OpenAsync |
| `StoragePackageStagingService.cs` | CS8603 | OpenAsync: Stream -> Stream? |
| `StoragePackageStagingService.cs` | CS8767 | RemoveAsync: align with IPackageStagingService.RemoveAsync |
| `StoragePackageStagingService.cs` | CS8603 | OpenOrNullAsync: Stream -> Stream? |
| `StoragePackageStagingService.cs` | CS8767 | RemoveAsync: align with IPackageStagingService.RemoveAsync |
| `StatisticsService.cs` | CS8618 | TopPackages: IReadOnlyList<PackageDownloads> -> IReadOnlyList<PackageDownloads>? |
| `StatisticsService.cs` | CS8618 | RecentVersions: IReadOnlyList<PackageDownloads> -> IReadOnlyList<PackageDownloads>? |
| `StatisticsService.cs` | CS8601 | Id: string -> string? |
| `StatisticsService.cs` | CS8601 | Version: string -> string? |
| `StatisticsService.cs` | CS8620 | IReadOnlyCollection<string>? -> List<string?> |
| `IPackageStorageService.cs` | CS8613 | IPackageStorageService.GetPackageStreamAsync: widen to match PackageStorageService.GetPackageStreamAsync |
| `IPackageStorageService.cs` | CS8613 | IPackageStorageService.GetNuspecStreamAsync: widen to match PackageStorageService.GetNuspecStreamAsync |
| `IPackageStorageService.cs` | CS8613 | IPackageStorageService.GetReadmeStreamAsync: widen to match PackageStorageService.GetReadmeStreamAsync |
| `IPackageStorageService.cs` | CS8613 | IPackageStorageService.GetIconStreamAsync: widen to match PackageStorageService.GetIconStreamAsync |
| `ISymbolStorageService.cs` | CS8613 | ISymbolStorageService.GetPortablePdbContentStreamOrNullAsync: widen to match SymbolStorageService.GetPortablePdbContentStreamOrNullAsync |
| `PackageStorageService.cs` | CS8603 | GetStreamAsync: Stream -> Stream? |
| `PackageStorageService.cs` | CS8603 | GetPackageStreamAsync: Stream -> Stream? |
| `PackageStorageService.cs` | CS8603 | GetNuspecStreamAsync: Stream -> Stream? |
| `PackageStorageService.cs` | CS8603 | GetReadmeStreamAsync: Stream -> Stream? |
| `PackageStorageService.cs` | CS8603 | GetIconStreamAsync: Stream -> Stream? |
| `PackageStorageService.cs` | CS8604 | Func<T1>: string -> string? |
| `SymbolStorageService.cs` | CS8603 | GetPortablePdbContentStreamOrNullAsync: Stream -> Stream? |
| `DisabledUpstreamClient.cs` | CS8613 | DownloadPackageOrNullAsync: align with IUpstreamClient.DownloadPackageOrNullAsync |
| `DisabledUpstreamClient.cs` | CS8767 | DownloadPackageOrNullAsync: align with IUpstreamClient.DownloadPackageOrNullAsync |
| `V2UpstreamClient.cs` | CS8603 | DownloadPackageOrNullAsync: Stream -> Stream? |
| `V2UpstreamClient.cs` | CS8767 | DownloadPackageOrNullAsync: align with IUpstreamClient.DownloadPackageOrNullAsync |
| `V3UpstreamClient.cs` | CS8603 | DownloadPackageOrNullAsync: Stream -> Stream? |
| `V3UpstreamClient.cs` | CS8767 | DownloadPackageOrNullAsync: align with IUpstreamClient.DownloadPackageOrNullAsync |
| `IUpstreamClient.cs` | CS8613 | IUpstreamClient.DownloadPackageOrNullAsync: widen to match V2UpstreamClient.DownloadPackageOrNullAsync |
| `RequiredIfAttribute.cs` | CS8618 | OtherPropertyDisplayName: string -> string? |
| `RequiredIfAttribute.cs` | CS8765 | IsValid: align with ValidationAttribute.IsValid |
| `RequiredIfAttribute.cs` | CS8603 | IsValid: ValidationResult -> ValidationResult? |
| `RequiredIfAttribute.cs` | CS8601 | OtherPropertyValue: object -> object? |
| `ValidateBaGetOptions.cs` | CS8767 | Validate: align with IValidateOptions.Validate |
| `GoogleCloudStorageOptions.cs` | CS8618 | BucketName: string -> string? |
| `AccountAuthenticationService.cs` | CS8767 | AuthenticateAsync: align with IAuthenticationService.AuthenticateAsync |
| `AccountAuthenticationService.cs` | CS8613 | AuthenticatePrincipalAsync: align with IAuthenticationService.AuthenticatePrincipalAsync |
| `AccountAuthenticationService.cs` | CS8603 | AuthenticatePrincipalAsync: BaGetPrincipal -> BaGetPrincipal? |
| `AccountDeletionService.cs` | CS8620 | IReadOnlyList<string> -> List<string?> |
| `AccountDeletionService.cs` | CS8604 | List<T>: string -> string? |
| `AccountDeletionService.cs` | CS8620 | IReadOnlyList<string> -> List<string?> |
| `AccountDeletionService.cs` | CS8604 | List<T>: string -> string? |
| `AccountDeletionService.cs` | CS8620 | IReadOnlyList<string> -> List<string?> |
| `AccountDeletionService.cs` | CS8620 | IReadOnlyList<string> -> IReadOnlyList<string?> |
| `AccountDeletionService.cs` | CS8620 | IReadOnlyList<string> -> IReadOnlyList<string?> |
| `AccountDeletionService.cs` | CS8604 | List<T>: string -> string? |
| `AccountDeletionService.cs` | CS8620 | IReadOnlyList<string> -> List<string?> |
| `AccountRecoveryService.cs` | CS8603 | ResetPasswordAsync: string -> string? |
| `ApiKeyService.cs` | CS8603 | FindValidOrNullAsync: ApiKey -> ApiKey? |
| `AvatarService.cs` | CS8620 | IEnumerable<string> -> List<string?> |
| `OwnershipAuthorizationService.cs` | CS8767 | OnPushedAsync: align with IPackageAuthorizationService.OnPushedAsync |
| `OwnershipAuthorizationService.cs` | CS8767 | AuthorizeAsync: align with IPackageAuthorizationService.AuthorizeAsync |
| `PackageOwnershipService.cs` | CS8619 | Task<IReadOnlyList<string>> -> List<string?> |
| `PackageOwnershipService.cs` | CS8619 | Task<IReadOnlyList<string>> -> List<string?> |
| `PackageOwnershipService.cs` | CS8619 | Task<IReadOnlyList<Organization>> -> List<Organization?> |
| `AccountPageModel.cs` | CS8603 | DecodeToken: string -> string? |
| `ApiKeys.cshtml.cs` | CS8618 | Description: string -> string? |
| `ApiKeys.cshtml.cs` | CS8618 | CreatedKey: string -> string? |
| `ForgotPassword.cshtml.cs` | CS8618 | Identifier: string -> string? |
| `Login.cshtml.cs` | CS8618 | UserName: string -> string? |
| `Login.cshtml.cs` | CS8618 | Password: string -> string? |
| `Login.cshtml.cs` | CS8618 | ReturnUrl: string -> string? |
| `Login.cshtml.cs` | CS8618 | Error: string -> string? |
| `Login.cshtml.cs` | CS8618 | Notice: string -> string? |
| `Login.cshtml.cs` | CS8603 | ChooseUserNameAsync: string -> string? |
| `LoginWith2fa.cshtml.cs` | CS8618 | Code: string -> string? |
| `LoginWith2fa.cshtml.cs` | CS8618 | ReturnUrl: string -> string? |
| `LoginWith2fa.cshtml.cs` | CS8618 | Error: string -> string? |
| `LoginWithRecoveryCode.cshtml.cs` | CS8618 | RecoveryCode: string -> string? |
| `LoginWithRecoveryCode.cshtml.cs` | CS8618 | ReturnUrl: string -> string? |
| `LoginWithRecoveryCode.cshtml.cs` | CS8618 | Error: string -> string? |
| `Organizations.cshtml.cs` | CS8618 | Message: string -> string? |
| `Organizations.cshtml.cs` | CS8618 | Name: string -> string? |
| `Organizations.cshtml.cs` | CS8618 | Members: IReadOnlyList<(string UserName, bool IsAdmin)> -> IReadOnlyList<(string UserName, bool IsAdmin)>? |
| `Packages.cshtml.cs` | CS8618 | Id: string -> string? |
| `Packages.cshtml.cs` | CS8601 | LatestVersion: string -> string? |
| `Reauthenticate.cshtml.cs` | CS8618 | ReturnUrl: string -> string? |
| `Reauthenticate.cshtml.cs` | CS8618 | Error: string -> string? |
| `Register.cshtml.cs` | CS8618 | UserName: string -> string? |
| `Register.cshtml.cs` | CS8618 | Email: string -> string? |
| `Register.cshtml.cs` | CS8618 | Password: string -> string? |
| `Register.cshtml.cs` | CS8618 | ConfirmPassword: string -> string? |
| `Register.cshtml.cs` | CS8618 | ReturnUrl: string -> string? |
| `ResetPassword.cshtml.cs` | CS8618 | UserId: string -> string? |
| `ResetPassword.cshtml.cs` | CS8618 | Code: string -> string? |
| `ResetPassword.cshtml.cs` | CS8618 | Password: string -> string? |
| `ResetPassword.cshtml.cs` | CS8618 | ConfirmPassword: string -> string? |
| `ResetPassword.cshtml.cs` | CS8618 | Error: string -> string? |
| `Settings.cshtml.cs` | CS8601 | Email: string -> string? |
| `Settings.cshtml.cs` | CS8618 | Message: string -> string? |
| `Settings.cshtml.cs` | CS8618 | Error: string -> string? |
| `Settings.cshtml.cs` | CS8618 | Blockers: DeletionBlockers -> DeletionBlockers? |
| `TwoFactor.cshtml.cs` | CS8618 | SharedKey: string -> string? |
| `TwoFactor.cshtml.cs` | CS8618 | QrCodeSvg: string -> string? |
| `TwoFactor.cshtml.cs` | CS8618 | Error: string -> string? |
| `TwoFactor.cshtml.cs` | CS8618 | Message: string -> string? |
| `DeleteUser.cshtml.cs` | CS8618 | Name: string -> string? |
| `DeleteUser.cshtml.cs` | CS8618 | Preview: AdminDeletionPreview -> AdminDeletionPreview? |
| `DeleteUser.cshtml.cs` | CS8618 | Error: string -> string? |
| `Settings.cshtml.cs` | CS8603 | OverridesPath: string -> string? |
| `Settings.cshtml.cs` | CS8618 | Message: string -> string? |
| `Settings.cshtml.cs` | CS8618 | ErrorSection: string -> string? |
| `Settings.cshtml.cs` | CS8601 | LoadError: string -> string? |
| `UserPackages.cshtml.cs` | CS8601 | Name: string -> string? |
| `Users.cshtml.cs` | CS8618 | Q: string -> string? |
| `Users.cshtml.cs` | CS8601 | CurrentUserName: string -> string? |
| `Users.cshtml.cs` | CS8618 | GeneratedPassword: string -> string? |
| `Users.cshtml.cs` | CS8618 | GeneratedFor: string -> string? |
| `Users.cshtml.cs` | CS8618 | Message: string -> string? |
| `Users.cshtml.cs` | CS8618 | Error: string -> string? |
| `Users.cshtml.cs` | CS8603 | RefuseAsync: string -> string? |
| `ContactOwners.cshtml.cs` | CS8618 | Id: string -> string? |
| `ContactOwners.cshtml.cs` | CS8618 | Message: string -> string? |
| `ContactOwners.cshtml.cs` | CS8601 | PackageId: string -> string? |
| `ContactOwners.cshtml.cs` | CS8618 | Error: string -> string? |
| `ContactOwners.cshtml.cs` | CS8603 | LoadAsync: IReadOnlyList<PackageOwner> -> IReadOnlyList<PackageOwner>? |
| `Manage.cshtml.cs` | CS8618 | Id: string -> string? |
| `Manage.cshtml.cs` | CS8618 | Message: string -> string? |
| `Manage.cshtml.cs` | CS8603 | RefuseUnconfirmedAsync: IActionResult -> IActionResult? |
| `Profile.cshtml.cs` | CS8601 | Name: string -> string? |
| `Profile.cshtml.cs` | CS8619 | IReadOnlyList<string> -> List<string?> |
| `Profile.cshtml.cs` | CS8601 | AvatarUrl: string -> string? |
| `Profile.cshtml.cs` | CS8619 | IReadOnlyList<string> -> List<string?> |
| `SessionPrincipalFactory.cs` | CS8603 | CreateAsync: BaGetPrincipal -> BaGetPrincipal? |
| `NullCatalogClient.cs` | CS8767 | GetPageAsync: align with ICatalogClient.GetPageAsync |
| `NullCatalogClient.cs` | CS8767 | GetPackageDeleteLeafAsync: align with ICatalogClient.GetPackageDeleteLeafAsync |
| `NullCatalogClient.cs` | CS8767 | GetPackageDetailsLeafAsync: align with ICatalogClient.GetPackageDetailsLeafAsync |
| `RawCatalogClient.cs` | CS8767 | GetPageAsync: align with ICatalogClient.GetPageAsync |
| `RawCatalogClient.cs` | CS8767 | GetPackageDeleteLeafAsync: align with ICatalogClient.GetPackageDeleteLeafAsync |
| `RawCatalogClient.cs` | CS8767 | GetPackageDetailsLeafAsync: align with ICatalogClient.GetPackageDetailsLeafAsync |
| `PackageDependencyRangeJsonConverter.cs` | CS8603 | Read: string -> string? |
| `StringOrStringArrayJsonConverter.cs` | CS8604 | List<T>: string -> string? |
| `ServiceIndexModelExtensions.cs` | CS8603 | GetResourceUrl: string -> string? |
| `ServiceIndexModelExtensions.cs` | CS8619 | IReadOnlyList<string> -> List<string?> |
| `ServiceIndexModelExtensions.cs` | CS8619 | IReadOnlyList<string> -> IReadOnlyList<string?> |
| `ServiceIndexModelExtensions.cs` | CS8603 | GetCatalogResourceUrl: string -> string? |
| `ServiceIndexModelExtensions.cs` | CS8603 | GetSearchAutocompleteResourceUrl: string -> string? |
| `ServiceIndexModelExtensions.cs` | CS8619 | IReadOnlyList<string> -> IReadOnlyList<string?> |
| `ServiceIndexModelExtensions.cs` | CS8619 | IReadOnlyList<string> -> IReadOnlyList<string?> |
| `ServiceIndexModelExtensions.cs` | CS8603 | GetRequiredResourceUrl: string -> string? |
| `ServiceIndexModelExtensions.cs` | CS8603 | GetPackageContentResourceUrl: string -> string? |
| `ServiceIndexModelExtensions.cs` | CS8603 | GetPackageMetadataResourceUrl: string -> string? |
| `ServiceIndexModelExtensions.cs` | CS8603 | GetSearchQueryResourceUrl: string -> string? |
| `AlternatePackage.cs` | CS8618 | Url: string -> string? |
| `AlternatePackage.cs` | CS8618 | Type: string -> string? |
| `AlternatePackage.cs` | CS8618 | Id: string -> string? |
| `AlternatePackage.cs` | CS8618 | Range: string -> string? |
| `AutocompleteContext.cs` | CS8618 | Vocab: string -> string? |
| `AutocompleteResponse.cs` | CS8618 | Context: AutocompleteContext -> AutocompleteContext? |
| `AutocompleteResponse.cs` | CS8618 | Data: IReadOnlyList<string> -> IReadOnlyList<string>? |
| `AutocompleteResponse.cs` | CS8619 | IReadOnlyList<string> -> IReadOnlyList<string?> |
| `CatalogIndex.cs` | CS8618 | Items: List<CatalogPageItem> -> List<CatalogPageItem>? |
| `CatalogLeaf.cs` | CS8618 | CatalogLeafUrl: string -> string? |
| `CatalogLeaf.cs` | CS8618 | Type: IReadOnlyList<string> -> IReadOnlyList<string>? |
| `CatalogLeaf.cs` | CS8618 | CommitId: string -> string? |
| `CatalogLeaf.cs` | CS8618 | PackageId: string -> string? |
| `CatalogLeaf.cs` | CS8618 | PackageVersion: string -> string? |
| `CatalogLeafItem.cs` | CS8618 | CatalogLeafUrl: string -> string? |
| `CatalogLeafItem.cs` | CS8618 | Type: string -> string? |
| `CatalogLeafItem.cs` | CS8618 | PackageId: string -> string? |
| `CatalogLeafItem.cs` | CS8618 | PackageVersion: string -> string? |
| `CatalogPage.cs` | CS8618 | Items: List<CatalogLeafItem> -> List<CatalogLeafItem>? |
| `CatalogPage.cs` | CS8618 | CatalogIndexUrl: string -> string? |
| `CatalogPageItem.cs` | CS8618 | CatalogPageUrl: string -> string? |
| `DependencyGroupItem.cs` | CS8618 | TargetFramework: string -> string? |
| `DependencyGroupItem.cs` | CS8618 | Dependencies: List<DependencyItem> -> List<DependencyItem>? |
| `DependencyItem.cs` | CS8618 | Id: string -> string? |
| `DependencyItem.cs` | CS8618 | Range: string -> string? |
| `ICatalogLeafItem.cs` | CS8766 | ICatalogLeafItem.PackageId: widen to match CatalogLeafItem.PackageId |
| `ICatalogLeafItem.cs` | CS8766 | ICatalogLeafItem.PackageVersion: widen to match CatalogLeafItem.PackageVersion |
| `PackageDeprecation.cs` | CS8618 | CatalogLeafUrl: string -> string? |
| `PackageDeprecation.cs` | CS8618 | Reasons: IReadOnlyList<string> -> IReadOnlyList<string>? |
| `PackageDeprecation.cs` | CS8618 | Message: string -> string? |
| `PackageDeprecation.cs` | CS8618 | AlternatePackage: AlternatePackage -> AlternatePackage? |
| `PackageDetailsCatalogLeaf.cs` | CS8618 | Authors: string -> string? |
| `PackageDetailsCatalogLeaf.cs` | CS8618 | Copyright: string -> string? |
| `PackageDetailsCatalogLeaf.cs` | CS8618 | DependencyGroups: List<DependencyGroupItem> -> List<DependencyGroupItem>? |
| `PackageDetailsCatalogLeaf.cs` | CS8618 | Description: string -> string? |
| `PackageDetailsCatalogLeaf.cs` | CS8618 | IconUrl: string -> string? |
| `PackageDetailsCatalogLeaf.cs` | CS8618 | Language: string -> string? |
| `PackageDetailsCatalogLeaf.cs` | CS8618 | LicenseUrl: string -> string? |
| `PackageDetailsCatalogLeaf.cs` | CS8618 | MinClientVersion: string -> string? |
| `PackageDetailsCatalogLeaf.cs` | CS8618 | PackageHash: string -> string? |
| `PackageDetailsCatalogLeaf.cs` | CS8618 | PackageHashAlgorithm: string -> string? |
| `PackageDetailsCatalogLeaf.cs` | CS8618 | ProjectUrl: string -> string? |
| `PackageDetailsCatalogLeaf.cs` | CS8618 | ReleaseNotes: string -> string? |
| `PackageDetailsCatalogLeaf.cs` | CS8618 | Summary: string -> string? |
| `PackageDetailsCatalogLeaf.cs` | CS8618 | Tags: List<string> -> List<string>? |
| `PackageDetailsCatalogLeaf.cs` | CS8618 | Title: string -> string? |
| `PackageDetailsCatalogLeaf.cs` | CS8618 | VerbatimVersion: string -> string? |
| `PackageDetailsCatalogLeaf.cs` | CS8618 | LicenseExpression: string -> string? |
| `PackageDetailsCatalogLeaf.cs` | CS8618 | LicenseFile: string -> string? |
| `PackageDetailsCatalogLeaf.cs` | CS8618 | IconFile: string -> string? |
| `PackageMetadata.cs` | CS8618 | CatalogLeafUrl: string -> string? |
| `PackageMetadata.cs` | CS8618 | PackageId: string -> string? |
| `PackageMetadata.cs` | CS8618 | Version: string -> string? |
| `PackageMetadata.cs` | CS8618 | Authors: string -> string? |
| `PackageMetadata.cs` | CS8618 | DependencyGroups: IReadOnlyList<DependencyGroupItem> -> IReadOnlyList<DependencyGroupItem>? |
| `PackageMetadata.cs` | CS8618 | Deprecation: PackageDeprecation -> PackageDeprecation? |
| `PackageMetadata.cs` | CS8618 | Description: string -> string? |
| `PackageMetadata.cs` | CS8618 | IconUrl: string -> string? |
| `PackageMetadata.cs` | CS8618 | Language: string -> string? |
| `PackageMetadata.cs` | CS8618 | LicenseUrl: string -> string? |
| `PackageMetadata.cs` | CS8618 | MinClientVersion: string -> string? |
| `PackageMetadata.cs` | CS8618 | PackageContentUrl: string -> string? |
| `PackageMetadata.cs` | CS8618 | ProjectUrl: string -> string? |
| `PackageMetadata.cs` | CS8618 | Summary: string -> string? |
| `PackageMetadata.cs` | CS8618 | Tags: IReadOnlyList<string> -> IReadOnlyList<string>? |
| `PackageMetadata.cs` | CS8618 | Title: string -> string? |
| `PackageMetadata.cs` | CS8618 | Vulnerabilities: IReadOnlyList<PackageVulnerabilityMetadata> -> IReadOnlyList<PackageVulnerabilityMetadata>? |
| `PackageVersionsResponse.cs` | CS8618 | Versions: IReadOnlyList<string> -> IReadOnlyList<string>? |
| `PackageVulnerabilityMetadata.cs` | CS8618 | AdvisoryUrl: string -> string? |
| `PackageVulnerabilityMetadata.cs` | CS8618 | Severity: string -> string? |
| `RegistrationIndexPage.cs` | CS8618 | RegistrationPageUrl: string -> string? |
| `RegistrationIndexPage.cs` | CS8618 | ItemsOrNull: IReadOnlyList<RegistrationIndexPageItem> -> IReadOnlyList<RegistrationIndexPageItem>? |
| `RegistrationIndexPage.cs` | CS8618 | Lower: string -> string? |
| `RegistrationIndexPage.cs` | CS8618 | Upper: string -> string? |
| `RegistrationIndexPageItem.cs` | CS8618 | RegistrationLeafUrl: string -> string? |
| `RegistrationIndexPageItem.cs` | CS8618 | PackageMetadata: PackageMetadata -> PackageMetadata? |
| `RegistrationIndexPageItem.cs` | CS8618 | PackageContentUrl: string -> string? |
| `RegistrationIndexResponse.cs` | CS8618 | RegistrationIndexUrl: string -> string? |
| `RegistrationIndexResponse.cs` | CS8618 | Type: IReadOnlyList<string> -> IReadOnlyList<string>? |
| `RegistrationIndexResponse.cs` | CS8618 | Pages: IReadOnlyList<RegistrationIndexPage> -> IReadOnlyList<RegistrationIndexPage>? |
| `RegistrationLeafResponse.cs` | CS8618 | RegistrationLeafUrl: string -> string? |
| `RegistrationLeafResponse.cs` | CS8618 | Type: IReadOnlyList<string> -> IReadOnlyList<string>? |
| `RegistrationLeafResponse.cs` | CS8618 | PackageContentUrl: string -> string? |
| `RegistrationLeafResponse.cs` | CS8618 | RegistrationIndexUrl: string -> string? |
| `SearchContext.cs` | CS8618 | Vocab: string -> string? |
| `SearchContext.cs` | CS8618 | Base: string -> string? |
| `SearchResponse.cs` | CS8618 | Context: SearchContext -> SearchContext? |
| `SearchResponse.cs` | CS8618 | Data: IReadOnlyList<SearchResult> -> IReadOnlyList<SearchResult>? |
| `SearchResult.cs` | CS8618 | PackageId: string -> string? |
| `SearchResult.cs` | CS8618 | Version: string -> string? |
| `SearchResult.cs` | CS8618 | Description: string -> string? |
| `SearchResult.cs` | CS8618 | Authors: IReadOnlyList<string> -> IReadOnlyList<string>? |
| `SearchResult.cs` | CS8618 | IconUrl: string -> string? |
| `SearchResult.cs` | CS8618 | LicenseUrl: string -> string? |
| `SearchResult.cs` | CS8618 | PackageTypes: IReadOnlyList<SearchResultPackageType> -> IReadOnlyList<SearchResultPackageType>? |
| `SearchResult.cs` | CS8618 | ProjectUrl: string -> string? |
| `SearchResult.cs` | CS8618 | RegistrationIndexUrl: string -> string? |
| `SearchResult.cs` | CS8618 | Summary: string -> string? |
| `SearchResult.cs` | CS8618 | Tags: IReadOnlyList<string> -> IReadOnlyList<string>? |
| `SearchResult.cs` | CS8618 | Title: string -> string? |
| `SearchResult.cs` | CS8618 | Versions: IReadOnlyList<SearchResultVersion> -> IReadOnlyList<SearchResultVersion>? |
| `SearchResult.cs` | CS8618 | Deprecation: PackageDeprecation -> PackageDeprecation? |
| `SearchResult.cs` | CS8618 | Vulnerabilities: IReadOnlyList<PackageVulnerabilityMetadata> -> IReadOnlyList<PackageVulnerabilityMetadata>? |
| `SearchResult.cs` | CS8618 | Owners: IReadOnlyList<string> -> IReadOnlyList<string>? |
| `SearchResult.cs` | CS8619 | IReadOnlyList<string> -> List<string?> |
| `SearchResultPackageType.cs` | CS8618 | Name: string -> string? |
| `SearchResultVersion.cs` | CS8618 | RegistrationLeafUrl: string -> string? |
| `SearchResultVersion.cs` | CS8618 | Version: string -> string? |
| `ServiceIndexItem.cs` | CS8618 | ResourceUrl: string -> string? |
| `ServiceIndexItem.cs` | CS8618 | Type: string -> string? |
| `ServiceIndexItem.cs` | CS8618 | Comment: string -> string? |
| `ServiceIndexResponse.cs` | CS8618 | Version: string -> string? |
| `ServiceIndexResponse.cs` | CS8618 | Resources: IReadOnlyList<ServiceIndexItem> -> IReadOnlyList<ServiceIndexItem>? |
| `NuGetClient.cs` | CS8603 | GetPackageMetadataAsync: PackageMetadata -> PackageMetadata? |
| `NuGetClient.cs` | CS8603 | SearchAsync: IReadOnlyList<SearchResult> -> IReadOnlyList<SearchResult>? |
| `NuGetClient.cs` | CS8603 | SearchAsync: IReadOnlyList<SearchResult> -> IReadOnlyList<SearchResult>? |
| `NuGetClient.cs` | CS8603 | SearchAsync: IReadOnlyList<SearchResult> -> IReadOnlyList<SearchResult>? |
| `NuGetClient.cs` | CS8603 | SearchAsync: IReadOnlyList<SearchResult> -> IReadOnlyList<SearchResult>? |
| `NuGetClient.cs` | CS8603 | AutocompleteAsync: IReadOnlyList<string> -> IReadOnlyList<string>? |
| `NuGetClient.cs` | CS8603 | AutocompleteAsync: IReadOnlyList<string> -> IReadOnlyList<string>? |
| `NuGetClient.cs` | CS8603 | AutocompleteAsync: IReadOnlyList<string> -> IReadOnlyList<string>? |
| `NuGetClient.cs` | CS8603 | AutocompleteAsync: IReadOnlyList<string> -> IReadOnlyList<string>? |
| `NuGetClient.cs` | CS8619 | Task<IReadOnlyList<string>?> -> IReadOnlyList<string?>? |
| `NuGetClient.cs` | CS8619 | Task<IReadOnlyList<string>?> -> IReadOnlyList<string?>? |
| `NuGetClient.cs` | CS8619 | Task<IReadOnlyList<string>?> -> IReadOnlyList<string?>? |
| `NuGetClient.cs` | CS8619 | Task<IReadOnlyList<string>?> -> IReadOnlyList<string?>? |
| `NuGetClientFactory.cs` | CS8619 | Task<ServiceIndexResponse> -> Task<ServiceIndexResponse?> |
| `NuGetClientFactory.cs` | CS8619 | Task<IPackageContentClient> -> Task<IPackageContentClient?> |
| `NuGetClientFactory.cs` | CS8619 | Task<IPackageMetadataClient> -> Task<IPackageMetadataClient?> |
| `NuGetClientFactory.cs` | CS8619 | Task<IReadOnlyList<ISearchClient>> -> Task<IReadOnlyList<ISearchClient>?> |
| `NuGetClientFactory.cs` | CS8619 | Task<IReadOnlyList<IAutocompleteClient>> -> Task<IReadOnlyList<IAutocompleteClient>?> |
| `NuGetClientFactory.cs` | CS8619 | Task<ICatalogClient> -> Task<ICatalogClient?> |
| `IPackageContentClient.cs` | CS8613 | IPackageContentClient.DownloadPackageOrNullAsync: widen to match RawPackageContentClient.DownloadPackageOrNullAsync |
| `IPackageContentClient.cs` | CS8613 | IPackageContentClient.DownloadPackageManifestOrNullAsync: widen to match RawPackageContentClient.DownloadPackageManifestOrNullAsync |
| `PackageContentClient.cs` | CS8603 | DownloadPackageOrNullAsync: Stream -> Stream? |
| `PackageContentClient.cs` | CS8603 | DownloadPackageManifestOrNullAsync: Stream -> Stream? |
| `RawPackageContentClient.cs` | CS8603 | DownloadPackageOrNullAsync: Stream -> Stream? |
| `RawPackageContentClient.cs` | CS8603 | DownloadPackageManifestOrNullAsync: Stream -> Stream? |
| `RawPackageContentClient.cs` | CS8767 | DownloadPackageOrNullAsync: align with IPackageContentClient.DownloadPackageOrNullAsync |
| `RawPackageMetadataClient.cs` | CS8767 | GetRegistrationPageAsync: align with IPackageMetadataClient.GetRegistrationPageAsync |
| `IServiceIndexClient.cs` | CS8613 | IServiceIndexClient.GetAsync: widen to match ServiceIndexClient.GetAsync |
| `RawServiceIndexClient.cs` | CS8613 | GetAsync: align with IServiceIndexClient.GetAsync |
| `ServiceIndexClient.cs` | CS8603 | GetAsync: ServiceIndexResponse -> ServiceIndexResponse? |
| `IPackageValidator.cs` | CS8601 | Message: string -> string? |
| `PackageValidationRunner.cs` | CS8619 | Task<IReadOnlyList<string>> -> List<string?> |
| `PackageValidationRunner.cs` | CS8603 | IndexAsync: string -> string? |
| `RepositorySignaturesController.cs` | CS8618 | SigningCertificates: IReadOnlyList<RepositorySigningCertificate> -> IReadOnlyList<RepositorySigningCertificate>? |
| `RepositorySignaturesController.cs` | CS8618 | Fingerprints: IReadOnlyDictionary<string, string> -> IReadOnlyDictionary<string, string>? |
| `RepositorySignaturesController.cs` | CS8618 | Subject: string -> string? |
| `RepositorySignaturesController.cs` | CS8618 | Issuer: string -> string? |
| `RepositorySignaturesController.cs` | CS8618 | ContentUrl: string -> string? |
| `RepositorySignaturesController.cs` | CS8618 | SubmissionId: string -> string? |
| `RepositorySignaturesController.cs` | CS8618 | Status: string -> string? |
| `RepositorySignaturesController.cs` | CS8618 | Steps: IReadOnlyList<ValidationStep> -> IReadOnlyList<ValidationStep>? |
| `RepositorySignaturesController.cs` | CS8618 | Type: string -> string? |
| `RepositorySignaturesController.cs` | CS8618 | State: string -> string? |
| `RepositorySignaturesController.cs` | CS8618 | Message: string -> string? |
| `RepositorySigner.cs` | CS8603 | GetPublicCertificateOrNull: X509Certificate2 -> X509Certificate2? |
| `BaGetUrlGenerator.cs` | CS8603 | GetServiceIndexUrl: string -> string? |
| `BaGetUrlGenerator.cs` | CS8603 | GetPackagePublishResourceUrl: string -> string? |
| `BaGetUrlGenerator.cs` | CS8603 | GetSymbolPublishResourceUrl: string -> string? |
| `BaGetUrlGenerator.cs` | CS8603 | GetSearchResourceUrl: string -> string? |
| `BaGetUrlGenerator.cs` | CS8603 | GetAutocompleteResourceUrl: string -> string? |
| `BaGetUrlGenerator.cs` | CS8767 | GetRegistrationIndexUrl: align with IUrlGenerator.GetRegistrationIndexUrl |
| `BaGetUrlGenerator.cs` | CS8603 | GetRegistrationIndexUrl: string -> string? |
| `BaGetUrlGenerator.cs` | CS8767 | GetRegistrationLeafUrl: align with IUrlGenerator.GetRegistrationLeafUrl |
| `BaGetUrlGenerator.cs` | CS8603 | GetRegistrationLeafUrl: string -> string? |
| `BaGetUrlGenerator.cs` | CS8603 | GetPackageVersionsUrl: string -> string? |
| `BaGetUrlGenerator.cs` | CS8767 | GetPackageDownloadUrl: align with IUrlGenerator.GetPackageDownloadUrl |
| `BaGetUrlGenerator.cs` | CS8603 | GetPackageDownloadUrl: string -> string? |
| `BaGetUrlGenerator.cs` | CS8603 | GetPackageManifestDownloadUrl: string -> string? |
| `BaGetUrlGenerator.cs` | CS8767 | GetPackageIconDownloadUrl: align with IUrlGenerator.GetPackageIconDownloadUrl |
| `BaGetUrlGenerator.cs` | CS8603 | GetPackageIconDownloadUrl: string -> string? |
| `BaGetUrlGenerator.cs` | CS8603 | GetV2FeedUrl: string -> string? |
| `BaGetUrlGenerator.cs` | CS8767 | GetCatalogLeafUrl: align with IUrlGenerator.GetCatalogLeafUrl |
| `PackageMetadataAdminController.cs` | CS8603 | AuthorizeAsync: IActionResult -> IActionResult? |
| `PackageMetadataAdminController.cs` | CS8618 | Reasons: IReadOnlyList<string> -> IReadOnlyList<string>? |
| `PackageMetadataAdminController.cs` | CS8618 | Message: string -> string? |
| `PackageMetadataAdminController.cs` | CS8618 | AlternatePackageId: string -> string? |
| `PackageMetadataAdminController.cs` | CS8618 | AlternateVersionRange: string -> string? |
| `PackageMetadataAdminController.cs` | CS8618 | AdvisoryUrl: string -> string? |
| `PackageMetadataAdminController.cs` | CS8618 | VersionRange: string -> string? |
| `PackagePublishController.cs` | CS8603 | AuthorizeManageAsync: IActionResult -> IActionResult? |
| `VulnerabilityController.cs` | CS8618 | Name: string -> string? |
| `VulnerabilityController.cs` | CS8618 | Url: string -> string? |
| `VulnerabilityController.cs` | CS8618 | Url: string -> string? |
| `VulnerabilityController.cs` | CS8618 | Versions: string -> string? |
| `HttpRequestExtensions.cs` | CS8603 | ReadPackageIdOrNull: string -> string? |
| `HttpRequestExtensions.cs` | CS8603 | GetApiKey: string -> string? |
| `NavLinkTagHelper.cs` | CS8618 | Page: string -> string? |
| `PackageDependencies.cs` | CS8618 | Name: string -> string? |
| `PackageDependencies.cs` | CS8618 | Dependencies: IReadOnlyList<DependencyModel> -> IReadOnlyList<DependencyModel>? |
| `PackageDependencies.cs` | CS8618 | PackageId: string -> string? |
| `PackageDependencies.cs` | CS8618 | VersionSpec: string -> string? |
| `Index.cshtml.cs` | CS8618 | Query: string -> string? |
| `Index.cshtml.cs` | CS8618 | Packages: IReadOnlyList<SearchResult> -> IReadOnlyList<SearchResult>? |
| `Index.cshtml.cs` | CS8618 | Totals: FeedStatistics -> FeedStatistics? |
| `LegalPageModel.cs` | CS8618 | Content: HtmlString -> HtmlString? |
| `LegalPageModel.cs` | CS8603 | ConfiguredPath: string -> string? |
| `LegalPageModel.cs` | CS8603 | ConfiguredPath: string -> string? |
| `LegalPageModel.cs` | CS8764 | LegalPageModel.ConfiguredPath: widen to match TermsModel.ConfiguredPath |
| `Package.cshtml.cs` | CS8618 | Package: Package -> Package? |
| `Package.cshtml.cs` | CS8618 | UsedBy: IReadOnlyList<PackageDependent> -> IReadOnlyList<PackageDependent>? |
| `Package.cshtml.cs` | CS8618 | DependencyGroups: IReadOnlyList<DependencyGroupModel> -> IReadOnlyList<DependencyGroupModel>? |
| `Package.cshtml.cs` | CS8618 | Versions: IReadOnlyList<VersionModel> -> IReadOnlyList<VersionModel>? |
| `Package.cshtml.cs` | CS8618 | Readme: HtmlString -> HtmlString? |
| `Package.cshtml.cs` | CS8619 | IReadOnlyList<string> -> List<string?> |
| `Package.cshtml.cs` | CS8618 | IconUrl: string -> string? |
| `Package.cshtml.cs` | CS8618 | LicenseUrl: string -> string? |
| `Package.cshtml.cs` | CS8618 | PackageDownloadUrl: string -> string? |
| `Package.cshtml.cs` | CS8618 | PackageExplorerUrl: string -> string? |
| `Package.cshtml.cs` | CS8603 | GetReadmeHtmlStringOrNullAsync: HtmlString -> HtmlString? |
| `Package.cshtml.cs` | CS8618 | Version: NuGetVersion -> NuGetVersion? |
| `ReportAbuse.cshtml.cs` | CS8618 | Id: string -> string? |
| `ReportAbuse.cshtml.cs` | CS8618 | Version: string -> string? |
| `ReportAbuse.cshtml.cs` | CS8618 | Reason: string -> string? |
| `ReportAbuse.cshtml.cs` | CS8618 | Details: string -> string? |
| `ReportAbuse.cshtml.cs` | CS8618 | ReporterEmail: string -> string? |
| `Statistics.cshtml.cs` | CS8618 | Statistics: FeedStatistics -> FeedStatistics? |
| `Upload.cshtml.cs` | CS8618 | Id: string -> string? |
| `Upload.cshtml.cs` | CS8618 | Staged: StagedUpload -> StagedUpload? |
| `Upload.cshtml.cs` | CS8618 | Message: string -> string? |
| `Upload.cshtml.cs` | CS8618 | Published: StagedUpload -> StagedUpload? |
| `Upload.cshtml.cs` | CS8603 | AuthenticateAsync: BaGetPrincipal -> BaGetPrincipal? |
| `Upload.cshtml.cs` | CS8603 | ReadOrNullAsync: StagedUpload -> StagedUpload? |
| `Upload.cshtml.cs` | CS8618 | RetryToken: string -> string? |
| `Upload.cshtml.cs` | CS8618 | Token: string -> string? |
| `Upload.cshtml.cs` | CS8618 | Id: string -> string? |
| `Upload.cshtml.cs` | CS8618 | Version: string -> string? |
| `Upload.cshtml.cs` | CS8601 | Description: string -> string? |
| `Upload.cshtml.cs` | CS8601 | License: string -> string? |
| `Upload.cshtml.cs` | CS8618 | LicenseUrl: string -> string? |
| `Upload.cshtml.cs` | CS8618 | Readme: HtmlString -> HtmlString? |
| `Upload.cshtml.cs` | CS8601 | TargetPackageMessage: string -> string? |
| `ReadmeMarkdown.cs` | CS8603 | ToHtml: HtmlString -> HtmlString? |
| `ReadmeMarkdown.cs` | CS8603 | ToSafeHtml: HtmlString -> HtmlString? |
| `ConfigureBaGetOptions.cs` | CS8604 | HashSet<T>: string -> string? |
| `ConfigureBaGetOptions.cs` | CS8604 | HashSet<T>: string -> string? |
| `ConfigureBaGetOptions.cs` | CS8604 | HashSet<T>: string -> string? |
| `ConfigureBaGetOptions.cs` | CS8767 | Validate: align with IValidateOptions.Validate |
| `ModelTests.cs` | CS8618 | OriginalType: Type -> Type? |
| `ModelTests.cs` | CS8618 | DerivedType: Type -> Type? |
| `BaGetServiceIndexTests.cs` | CS8603 | UrlOf: string -> string? |
| `BaGetServiceIndexTests.cs` | CS8619 | Task<string[]> -> string?[]? |
| `BaGetServiceIndexTests.cs` | CS8603 | Types: string[] -> string[]? |
| `FileStorageServiceTests.cs` | CS8603 | OutsideStorePathData: IEnumerable<string> -> IEnumerable<string>? |
| `SettingsServiceTests.cs` | CS8620 | Dictionary<string, string> -> IEnumerable<KeyValuePair<string, string?>>? |
| `SettingsServiceTests.cs` | CS8620 | Dictionary<string, string> -> IEnumerable<KeyValuePair<string, string?>>? |
| `RecordingLogger.cs` | CS8767 | Log: align with ILogger.Log |
| `RecordingLogger.cs` | CS8601 | Exception: Exception -> Exception? |
| `CountingHttpMessageHandler.cs` | CS8604 | List<T>: Uri -> Uri? |
| `RecordingHttpMessageHandler.cs` | CS8604 | List<T>: Uri -> Uri? |
| `AccountReauthenticationTests.cs` | CS8603 | FindAsync: BaGetUser -> BaGetUser? |
| `AccountReauthenticationTests.cs` | CS8603 | EmailAsync: string -> string? |
| `AccountReviewFixesTests.cs` | CS8603 | FindAsync: BaGetUser -> BaGetUser? |
| `AccountSettingsTests.cs` | CS8619 | Task<BaGetUser> -> Task<BaGetUser?> |
| `AccountSettingsTests.cs` | CS8619 | Task<string> -> Task<string?> |
| `AccountsWithoutGlobalKeyTests.cs` | CS8604 | List<T>: string -> string? |
| `AccountsWithoutGlobalKeyTests.cs` | CS8619 | Task<IReadOnlyList<string>> -> List<string?> |
| `BrowserUploadTests.cs` | CS8619 | Task<IReadOnlyList<string>> -> List<string?> |
| `EmailTests.cs` | CS8620 | Dictionary<string, string> -> IEnumerable<KeyValuePair<string, string?>>? |
| `PackageManagementPageTests.cs` | CS8619 | Task<Package> -> Task<Package?> |
| `AdminTestSupport.cs` | CS8603 | FindAsync: BaGetUser -> BaGetUser? |
| `CapturingEmailSender.cs` | CS8767 | SendAsync: align with IEmailMessageSender.SendAsync |
| `HttpSourceResourceProviderTestHost.cs` | CS8609 | TryCreate: align with ResourceProvider.TryCreate |
| `HttpSourceResourceProviderTestHost.cs` | CS8604 | Tuple<T2>: INuGetResource -> INuGetResource? |
| `TestableHttpSource.cs` | CS8619 | Func<Task<HttpHandlerResource>> -> Task<HttpHandlerResource?> |
| `TestableHttpSource.cs` | CS8620 | Func<Task<HttpHandlerResource>> -> Func<Task<HttpHandlerResource>?> |
| `TestResources.cs` | CS8603 | GetResourceStream: MemoryStream -> MemoryStream? |
| `XunitLogger.cs` | CS8767 | Log: align with ILogger.Log |
| `ValidationIntegrationTests.cs` | CS8603 | StatusAsync: string -> string? |
| `ValidationIntegrationTests.cs` | CS8603 | WaitForResultAsync: string -> string? |

## Dereferences made conditional

These dereferenced a value that may be null, and now yield null instead of throwing. **That is a change in behaviour**: the code threw a NullReferenceException at each of these points before, and the null now travels on to whatever receives the result. Worth reading as a set.

| File | Diagnostic | Change |
|---|---|---|
| `CatalogDatabase.cs` | CS8602 | _context.CatalogLeafs?.Add |
| `CatalogReader.cs` | CS8602 | leaf?["@id"] (and 1 later in the member) |
| `CatalogRebuilder.cs` | CS8602 | id?.ToLowerInvariant |
| `SettingsService.cs` | CS8602 | method?.Invoke |
| `DependencyInjectionExtensions.cs` | CS8602 | assembly?.GetName (and 1 later in the member) |
| `DependencyInjectionExtensions.cs` | CS8602 | options.Value.PackageSource?.ToString |
| `FrameworkCompatibilityService.cs` | CS8602 | f?.Framework |
| `FrameworkCompatibilityService.cs` | CS8602 | f?.GetShortFolderName |
| `FrameworkCompatibilityService.cs` | CS8602 | possibleFramework?.Framework (and 2 later in the member) |
| `PackageIndexingService.cs` | CS8602 | packageStream?.Position |
| `RegistrationBuilder.cs` | CS8604 | package.PackageTypes?.Select |
| `PackageDatabase.cs` | CS8602 | _context.Packages?.Add |
| `PackageDatabase.cs` | CS8602 | _context.PackageDependencies?.RemoveRange |
| `PackageDatabase.cs` | CS8602 | _context.PackageDeprecations?.Remove |
| `PackageDatabase.cs` | CS8602 | _context.PackageVulnerabilities?.RemoveRange |
| `PackageDatabase.cs` | CS8602 | _context.PackageDeprecations?.Remove |
| `PackageDatabase.cs` | CS8602 | _context.PackageDeprecations?.Remove |
| `PackageDatabase.cs` | CS8602 | _context.PackageVulnerabilities?.RemoveRange |
| `DatabaseSearchService.cs` | CS8604 | _context.Packages?.Where |
| `DatabaseSearchService.cs` | CS8602 | response.Data?.Count (and 1 later in the member) |
| `DatabaseSearchService.cs` | CS8602 | r.PackageId?.ToLower |
| `DatabaseSearchService.cs` | CS8602 | request.PackageId?.ToLower |
| `DatabaseSearchService.cs` | CS8604 | search?.GroupBy |
| `DatabaseSearchService.cs` | CS8604 | query?.Where |
| `DatabaseSearchService.cs` | CS8604 | query?.Where |
| `DatabaseSearchService.cs` | CS8604 | query?.Where |
| `DatabaseSearchService.cs` | CS8604 | query?.Where |
| `DatabaseSearchService.cs` | CS8604 | query?.Where (and 1 later in the member) |
| `DatabaseSearchService.cs` | CS8604 | query?.Where |
| `StoragePackageStagingService.cs` | CS8602 | read.Metadata?.StagedAtUtc |
| `StatisticsService.cs` | CS8604 | _context.Packages?.AsNoTracking |
| `StatisticsService.cs` | CS8602 | id?.ToLower |
| `StatisticsService.cs` | CS8604 | _context.Packages?.AsNoTracking |
| `PackageStorageService.cs` | CS8602 | package.Id?.ToLowerInvariant |
| `PackageStorageService.cs` | CS8602 | package.NormalizedVersionString?.ToLowerInvariant |
| `PackageStorageService.cs` | CS8602 | id?.ToLowerInvariant |
| `PackageStorageService.cs` | CS8602 | id?.ToLowerInvariant |
| `V2UpstreamClient.cs` | CS8604 | group.Packages?.Select |
| `V3UpstreamClient.cs` | CS8604 | group.Dependencies?.Select |
| `DownloadsImporter.cs` | CS8604 | _context.Packages?.CountAsync |
| `DownloadsImporter.cs` | CS8602 | package.Id?.ToLowerInvariant |
| `DownloadsImporter.cs` | CS8602 | package.NormalizedVersionString?.ToLowerInvariant |
| `PackageDownloadsJsonSource.cs` | CS8602 | record[0]?.ToString |
| `PackageDownloadsJsonSource.cs` | CS8602 | record[1]?.Type |
| `PackageDownloadsJsonSource.cs` | CS8602 | token[0]?.ToString |
| `AccountDeletionService.cs` | CS8602 | user.UserName?.ToLowerInvariant |
| `AccountDeletionService.cs` | CS8602 | packageId?.ToLowerInvariant |
| `AccountDeletionService.cs` | CS8602 | user?.UserName |
| `AccountDeletionService.cs` | CS8602 | user.UserName?.ToLowerInvariant |
| `AccountDeletionService.cs` | CS8604 | _context.ApiKeys?.CountAsync |
| `AccountDeletionService.cs` | CS8604 | _context.ApiKeys?.Where (and 1 later in the member) |
| `AccountDeletionService.cs` | CS8602 | user?.UserName (and 2 later in the member) |
| `AccountDeletionService.cs` | CS8602 | user.UserName?.ToLowerInvariant |
| `AccountDeletionService.cs` | CS8604 | _context.PackageOwners?.Where (and 1 later in the member) |
| `AccountDeletionService.cs` | CS8602 | _context.Organizations?.RemoveRange (and 1 later in the member) |
| `AccountDeletionService.cs` | CS8602 | organization?.ToLowerInvariant |
| `AccountDeletionService.cs` | CS8604 | _context.PackageOwners?.AnyAsync |
| `AccountsServiceCollectionExtensions.cs` | CS8602 | cookies.ApplicationCookie?.Configure |
| `ApiKeyService.cs` | CS8602 | _context.ApiKeys?.Add |
| `ApiKeyService.cs` | CS8604 | _context.ApiKeys?.FirstOrDefaultAsync (and 1 later in the member) |
| `AvatarService.cs` | CS8602 | n?.ToLowerInvariant |
| `ContactRateLimiter.cs` | CS8602 | packageId?.ToLowerInvariant |
| `ContactRateLimiter.cs` | CS8602 | packageId?.ToLowerInvariant |
| `PackageOwnershipService.cs` | CS8602 | packageId?.ToLowerInvariant |
| `PackageOwnershipService.cs` | CS8602 | packageId?.ToLowerInvariant |
| `PackageOwnershipService.cs` | CS8602 | packageId?.ToLowerInvariant |
| `PackageOwnershipService.cs` | CS8604 | _context.PackageOwners?.AnyAsync |
| `PackageOwnershipService.cs` | CS8602 | name?.ToUpperInvariant (and 1 later in the member) |
| `PackageOwnershipService.cs` | CS8604 | _context.Organizations?.AnyAsync |
| `PackageOwnershipService.cs` | CS8602 | _context.PackageOwners?.Add |
| `PackageOwnershipService.cs` | CS8602 | packageId?.ToLowerInvariant |
| `PackageOwnershipService.cs` | CS8604 | _context.PackageOwners?.AnyAsync |
| `PackageOwnershipService.cs` | CS8602 | _context.Organizations?.Add |
| `PackageOwnershipService.cs` | CS8602 | _context.OrganizationMembers?.Add |
| `PackageOwnershipService.cs` | CS8602 | organizationName?.ToLowerInvariant |
| `PackageOwnershipService.cs` | CS8604 | _context.Organizations?.FirstOrDefaultAsync |
| `PackageOwnershipService.cs` | CS8602 | _context.OrganizationMembers?.Add |
| `AccountPageModel.cs` | CS8602 | root?.GetLeftPart |
| `ForgotPassword.cshtml.cs` | CS8602 | Identifier?.Trim |
| `LoginWith2fa.cshtml.cs` | CS8602 | Code?.Replace |
| `LoginWithRecoveryCode.cshtml.cs` | CS8602 | RecoveryCode?.Replace |
| `Packages.cshtml.cs` | CS8602 | User.Identity?.Name |
| `Packages.cshtml.cs` | CS8602 | ids?.Count |
| `Profile.cshtml.cs` | CS8602 | User.Identity?.Name |
| `Reauthenticate.cshtml.cs` | CS8602 | info?.Principal |
| `DeleteUser.cshtml.cs` | CS8602 | current?.UserName |
| `CatalogClient.cs` | CS8602 | client?.GetIndexAsync |
| `CatalogClient.cs` | CS8602 | client?.GetPageAsync |
| `CatalogClient.cs` | CS8602 | client?.GetPackageDetailsLeafAsync |
| `CatalogClient.cs` | CS8602 | client?.GetPackageDeleteLeafAsync |
| `CatalogProcessor.cs` | CS8602 | index.Items?.Count |
| `CatalogProcessor.cs` | CS8602 | page.Items?.Count |
| `FileCursor.cs` | CS8602 | data?.Value (and 1 later in the member) |
| `RawCatalogClient.cs` | CS8604 | leaf.Type?.FirstOrDefault |
| `EndpointFailover.cs` | CS8602 | failures?.Count (and 2 later in the member) |
| `CatalogModelExtensions.cs` | CS8602 | x.PackageId?.ToLowerInvariant |
| `CatalogModelExtensions.cs` | CS8604 | leaf.Type?.FirstOrDefault |
| `CatalogModelExtensions.cs` | CS8604 | leaf.Type?.FirstOrDefault |
| `PackageMetadataModelExtensions.cs` | CS8602 | package?.Version |
| `NuGetClient.cs` | CS8602 | _contentClient?.GetPackageVersionsOrNullAsync |
| `NuGetClient.cs` | CS8602 | _contentClient?.GetPackageVersionsOrNullAsync |
| `NuGetClient.cs` | CS8602 | _contentClient?.DownloadPackageOrNullAsync |
| `NuGetClient.cs` | CS8602 | _contentClient?.DownloadPackageManifestOrNullAsync |
| `NuGetClient.cs` | CS8602 | _contentClient?.GetPackageVersionsOrNullAsync |
| `NuGetClient.cs` | CS8602 | _metadataClient?.GetRegistrationIndexOrNullAsync (and 1 later in the member) |
| `NuGetClient.cs` | CS8602 | _metadataClient?.GetRegistrationIndexOrNullAsync (and 1 later in the member) |
| `NuGetClient.cs` | CS8602 | _searchClient?.SearchAsync |
| `NuGetClient.cs` | CS8602 | _searchClient?.SearchAsync |
| `NuGetClient.cs` | CS8602 | _searchClient?.SearchAsync |
| `NuGetClient.cs` | CS8602 | _searchClient?.SearchAsync |
| `NuGetClient.cs` | CS8602 | _autocompleteClient?.AutocompleteAsync |
| `NuGetClient.cs` | CS8602 | _autocompleteClient?.AutocompleteAsync |
| `NuGetClient.cs` | CS8602 | _autocompleteClient?.AutocompleteAsync |
| `NuGetClient.cs` | CS8602 | _autocompleteClient?.AutocompleteAsync |
| `NuGetClientFactory.cs` | CS8602 | _mutex?.WaitAsync (and 1 later in the member) |
| `PackageContentClient.cs` | CS8602 | client?.DownloadPackageOrNullAsync |
| `PackageContentClient.cs` | CS8602 | client?.DownloadPackageManifestOrNullAsync |
| `PackageContentClient.cs` | CS8602 | client?.GetPackageVersionsOrNullAsync |
| `RawPackageContentClient.cs` | CS8602 | packageId?.ToLowerInvariant |
| `PackageMetadataClient.cs` | CS8602 | client?.GetRegistrationIndexOrNullAsync |
| `PackageMetadataClient.cs` | CS8602 | client?.GetRegistrationPageAsync |
| `PackageMetadataClient.cs` | CS8602 | client?.GetRegistrationLeafAsync |
| `ValueConverterTests.cs` | CS8602 | _target?.ConvertToProvider |
| `ValueConverterTests.cs` | CS8602 | _target?.ConvertFromProvider |
| `ValueConverterTests.cs` | CS8602 | _target?.ConvertToProvider |
| `ValueConverterTests.cs` | CS8602 | _target?.ConvertFromProvider |
| `PackageArchiveReaderExtensionsTests.cs` | CS8602 | package.IconUrl?.AbsoluteUri |
| `PackageArchiveReaderExtensionsTests.cs` | CS8602 | package.LicenseUrl?.AbsoluteUri |
| `PackageArchiveReaderExtensionsTests.cs` | CS8602 | package.ProjectUrl?.AbsoluteUri |
| `PackageArchiveReaderExtensionsTests.cs` | CS8602 | package.RepositoryUrl?.AbsoluteUri |
| `PackageArchiveReaderExtensionsTests.cs` | CS8602 | package.RepositoryUrl?.AbsoluteUri |
| `PackageArchiveReaderExtensionsTests.cs` | CS8604 | package.Dependencies?.Select |
| `RegistrationBuilderTests.cs` | CS8602 | response.Pages?[0] (and 1 later in the member) |
| `RegistrationBuilderTests.cs` | CS8602 | response.Pages[0].ItemsOrNull?.Count (and 1 later in the member) |
| `RegistrationBuilderTests.cs` | CS8602 | response.Pages[0].ItemsOrNull?[index++] |
| `RegistrationBuilderTests.cs` | CS8602 | response.Pages[0].ItemsOrNull[index++].PackageMetadata?.Version |
| `SearchResponseBuilderTests.cs` | CS8604 | Assert.Single(response.Data).Versions?.Select |
| `SearchResponseBuilderTests.cs` | CS8604 | versions?.First (and 1 later in the member) |
| `SearchResponseBuilderTests.cs` | CS8604 | response.Data?.Select |
| `BaGetServiceIndexTests.cs` | CS8604 | response.Resources?.Select |
| `BaGetServiceIndexTests.cs` | CS8604 | response.Resources?.Single |
| `PackageDatabaseTests.cs` | CS8602 | result?.Dependencies (and 1 later in the member) |
| `PackageDatabaseTests.cs` | CS8602 | _context.Packages?.Add (and 1 later in the member) |
| `PackageIndexingServiceTests.cs` | CS8602 | _savedPackage?.HasReadme |
| `PackageIndexingServiceTests.cs` | CS8602 | _savedPackage?.HasReadme |
| `PackageIndexingServiceTests.cs` | CS8602 | _savedPackage?.HasEmbeddedIcon |
| `PackageIndexingServiceTests.cs` | CS8602 | _savedPackage?.HasEmbeddedIcon |
| `PackageIndexingServiceTests.cs` | CS8602 | archive.GetEntry(path)?.Open |
| `SettingsOverridesStoreTests.cs` | CS8602 | JsonNode.Parse(File.ReadAllText(_path))?.AsObject |
| `SettingsOverridesStoreTests.cs` | CS8602 | JsonNode.Parse(File.ReadAllText(_path))?.AsObject |
| `SettingsOverridesStoreTests.cs` | CS8602 | JsonNode.Parse(File.ReadAllText(_path))?.AsObject |
| `SettingsOverridesStoreTests.cs` | CS8602 | json["Items"]?.AsArray |
| `RequiredIfAttributeTests.cs` | CS8602 | result?.ErrorMessage |
| `RequiredIfAttributeTests.cs` | CS8602 | result?.ErrorMessage (and 3 later in the member) |
| `RequiredIfAttributeTests.cs` | CS8602 | result?.ErrorMessage |
| `RequiredIfAttributeTests.cs` | CS8602 | result?.ErrorMessage |

## Guards inserted

None.

## Signatures aligned with a base declaration

| File | Diagnostic | Change |
|---|---|---|
| `AzureSearchService.cs` | CS8767 | FindDependentsAsync: align with ISearchService.FindDependentsAsync |
| `PackageDocument.cs` | CS8766 | IKeyedDocument.Key: widen to match KeyedDocument.Key |
| `TablePackageDatabase.cs` | CS8767 | ExistsAsync: align with IPackageDatabase.ExistsAsync |
| `TablePackageDatabase.cs` | CS8767 | FindAsync: align with IPackageDatabase.FindAsync |
| `TablePackageDatabase.cs` | CS8613 | FindOrNullAsync: align with IPackageDatabase.FindOrNullAsync |
| `TablePackageDatabase.cs` | CS8767 | HardDeletePackageAsync: align with IPackageDatabase.HardDeletePackageAsync |
| `TablePackageDatabase.cs` | CS8767 | ExistsAsync: align with IPackageDatabase.ExistsAsync |
| `TablePackageDatabase.cs` | CS8767 | RelistPackageAsync: align with IPackageDatabase.RelistPackageAsync |
| `TablePackageDatabase.cs` | CS8767 | UnlistPackageAsync: align with IPackageDatabase.UnlistPackageAsync |
| `TablePackageDatabase.cs` | CS8767 | SetDeprecationAsync: align with IPackageDatabase.SetDeprecationAsync |
| `TablePackageDatabase.cs` | CS8767 | RemoveDeprecationAsync: align with IPackageDatabase.RemoveDeprecationAsync |
| `TableSearchService.cs` | CS8767 | FindDependentsAsync: align with ISearchService.FindDependentsAsync |
| `CatalogDatabase.cs` | CS8613 | FindOrNullAsync: align with ICatalogDatabase.FindOrNullAsync |
| `ICatalogDatabase.cs` | CS8613 | ICatalogDatabase.FindOrNullAsync: widen to match CatalogDatabase.FindOrNullAsync |
| `ApiKeyAuthenticationService.cs` | CS8767 | AuthenticateAsync: align with IAuthenticationService.AuthenticateAsync |
| `IPackageAuthorizationService.cs` | CS8767 | OnPushedAsync: align with IPackageAuthorizationService.OnPushedAsync |
| `IPackageAuthorizationService.cs` | CS8767 | AuthorizeAsync: align with IPackageAuthorizationService.AuthorizeAsync |
| `SettingsService.cs` | CS8767 | Set: align with IConfigurationProvider.Set |
| `SettingsService.cs` | CS8767 | GetChildKeys: align with IConfigurationProvider.GetChildKeys |
| `SiteOptions.cs` | CS8765 | IsValid: align with ValidationAttribute.IsValid |
| `DefaultPackageContentService.cs` | CS8767 | GetPackageReadmeStreamOrNullAsync: align with IPackageContentService.GetPackageReadmeStreamOrNullAsync |
| `IPackageContentService.cs` | CS8613 | IPackageContentService.GetPackageVersionsOrNullAsync: widen to match DefaultPackageContentService.GetPackageVersionsOrNullAsync |
| `IPackageContentService.cs` | CS8613 | IPackageContentService.GetPackageContentStreamOrNullAsync: widen to match DefaultPackageContentService.GetPackageContentStreamOrNullAsync |
| `IPackageContentService.cs` | CS8613 | IPackageContentService.GetPackageManifestStreamOrNullAsync: widen to match DefaultPackageContentService.GetPackageManifestStreamOrNullAsync |
| `IPackageContentService.cs` | CS8613 | IPackageContentService.GetPackageReadmeStreamOrNullAsync: widen to match DefaultPackageContentService.GetPackageReadmeStreamOrNullAsync |
| `IPackageContentService.cs` | CS8613 | IPackageContentService.GetPackageIconStreamOrNullAsync: widen to match DefaultPackageContentService.GetPackageIconStreamOrNullAsync |
| `IContext.cs` | CS8766 | IContext.Packages: widen to match AbstractContext.Packages |
| `IContext.cs` | CS8766 | IContext.PackageDependencies: widen to match AbstractContext.PackageDependencies |
| `IContext.cs` | CS8766 | IContext.PackageDeprecations: widen to match AbstractContext.PackageDeprecations |
| `IContext.cs` | CS8766 | IContext.PackageVulnerabilities: widen to match AbstractContext.PackageVulnerabilities |
| `IContext.cs` | CS8766 | IContext.CatalogLeafs: widen to match AbstractContext.CatalogLeafs |
| `IContext.cs` | CS8766 | IContext.Organizations: widen to match AbstractContext.Organizations |
| `IContext.cs` | CS8766 | IContext.OrganizationMembers: widen to match AbstractContext.OrganizationMembers |
| `IContext.cs` | CS8766 | IContext.PackageOwners: widen to match AbstractContext.PackageOwners |
| `IContext.cs` | CS8766 | IContext.ApiKeys: widen to match AbstractContext.ApiKeys |
| `IContext.cs` | CS8766 | IContext.PackageValidations: widen to match AbstractContext.PackageValidations |
| `IContext.cs` | CS8766 | IContext.AbuseReports: widen to match AbstractContext.AbuseReports |
| `NullContext.cs` | CS8767 | Packages: align with IContext.Packages |
| `NullContext.cs` | CS8767 | PackageDependencies: align with IContext.PackageDependencies |
| `NullContext.cs` | CS8767 | PackageDeprecations: align with IContext.PackageDeprecations |
| `NullContext.cs` | CS8767 | PackageVulnerabilities: align with IContext.PackageVulnerabilities |
| `NullContext.cs` | CS8767 | CatalogLeafs: align with IContext.CatalogLeafs |
| `NullContext.cs` | CS8767 | Organizations: align with IContext.Organizations |
| `NullContext.cs` | CS8767 | OrganizationMembers: align with IContext.OrganizationMembers |
| `NullContext.cs` | CS8767 | PackageOwners: align with IContext.PackageOwners |
| `NullContext.cs` | CS8767 | ApiKeys: align with IContext.ApiKeys |
| `NullContext.cs` | CS8767 | PackageValidations: align with IContext.PackageValidations |
| `NullContext.cs` | CS8767 | AbuseReports: align with IContext.AbuseReports |
| `FrameworkCompatibilityService.cs` | CS8621 | FindAllCompatibleFrameworks: align with Func.Invoke |
| `PackageDeletionService.cs` | CS8767 | TryDeletePackageAsync: align with IPackageDeletionService.TryDeletePackageAsync |
| `PackageDeletionService.cs` | CS8767 | TryHardDeletePackageAsync: align with IPackageDeletionService.TryHardDeletePackageAsync |
| `PackageIndexingService.cs` | CS8767 | IndexAsync: align with IPackageIndexingService.IndexAsync |
| `SymbolIndexingService.cs` | CS8767 | IndexAsync: align with ISymbolIndexingService.IndexAsync |
| `IPackageDatabase.cs` | CS8613 | IPackageDatabase.FindOrNullAsync: widen to match PackageDatabase.FindOrNullAsync |
| `IPackageService.cs` | CS8613 | IPackageService.FindPackageOrNullAsync: widen to match PackageService.FindPackageOrNullAsync |
| `IUrlGenerator.cs` | CS8766 | IUrlGenerator.GetServiceIndexUrl: widen to match BaGetUrlGenerator.GetServiceIndexUrl |
| `IUrlGenerator.cs` | CS8766 | IUrlGenerator.GetPackagePublishResourceUrl: widen to match BaGetUrlGenerator.GetPackagePublishResourceUrl |
| `IUrlGenerator.cs` | CS8766 | IUrlGenerator.GetSymbolPublishResourceUrl: widen to match BaGetUrlGenerator.GetSymbolPublishResourceUrl |
| `IUrlGenerator.cs` | CS8766 | IUrlGenerator.GetSearchResourceUrl: widen to match BaGetUrlGenerator.GetSearchResourceUrl |
| `IUrlGenerator.cs` | CS8766 | IUrlGenerator.GetAutocompleteResourceUrl: widen to match BaGetUrlGenerator.GetAutocompleteResourceUrl |
| `IUrlGenerator.cs` | CS8766 | IUrlGenerator.GetV2FeedUrl: widen to match BaGetUrlGenerator.GetV2FeedUrl |
| `IUrlGenerator.cs` | CS8766 | IUrlGenerator.GetRegistrationIndexUrl: widen to match BaGetUrlGenerator.GetRegistrationIndexUrl |
| `IUrlGenerator.cs` | CS8766 | IUrlGenerator.GetRegistrationLeafUrl: widen to match BaGetUrlGenerator.GetRegistrationLeafUrl |
| `IUrlGenerator.cs` | CS8766 | IUrlGenerator.GetPackageVersionsUrl: widen to match BaGetUrlGenerator.GetPackageVersionsUrl |
| `IUrlGenerator.cs` | CS8766 | IUrlGenerator.GetPackageDownloadUrl: widen to match BaGetUrlGenerator.GetPackageDownloadUrl |
| `IUrlGenerator.cs` | CS8766 | IUrlGenerator.GetPackageManifestDownloadUrl: widen to match BaGetUrlGenerator.GetPackageManifestDownloadUrl |
| `IUrlGenerator.cs` | CS8766 | IUrlGenerator.GetPackageIconDownloadUrl: widen to match BaGetUrlGenerator.GetPackageIconDownloadUrl |
| `IPackageMetadataService.cs` | CS8613 | IPackageMetadataService.GetRegistrationIndexOrNullAsync: widen to match DefaultPackageMetadataService.GetRegistrationIndexOrNullAsync |
| `IPackageMetadataService.cs` | CS8613 | IPackageMetadataService.GetRegistrationLeafOrNullAsync: widen to match DefaultPackageMetadataService.GetRegistrationLeafOrNullAsync |
| `PackageDatabase.cs` | CS8767 | FindAsync: align with IPackageDatabase.FindAsync |
| `PackageDatabase.cs` | CS8767 | ExistsAsync: align with IPackageDatabase.ExistsAsync |
| `PackageDatabase.cs` | CS8767 | FindOrNullAsync: align with IPackageDatabase.FindOrNullAsync |
| `PackageDatabase.cs` | CS8767 | HardDeletePackageAsync: align with IPackageDatabase.HardDeletePackageAsync |
| `PackageDatabase.cs` | CS8767 | ExistsAsync: align with IPackageDatabase.ExistsAsync |
| `PackageDatabase.cs` | CS8767 | UnlistPackageAsync: align with IPackageDatabase.UnlistPackageAsync |
| `PackageDatabase.cs` | CS8767 | RelistPackageAsync: align with IPackageDatabase.RelistPackageAsync |
| `PackageDatabase.cs` | CS8767 | SetDeprecationAsync: align with IPackageDatabase.SetDeprecationAsync |
| `PackageDatabase.cs` | CS8767 | RemoveDeprecationAsync: align with IPackageDatabase.RemoveDeprecationAsync |
| `PackageService.cs` | CS8767 | FindPackageOrNullAsync: align with IPackageService.FindPackageOrNullAsync |
| `DatabaseSearchService.cs` | CS8767 | FindDependentsAsync: align with ISearchService.FindDependentsAsync |
| `NullSearchService.cs` | CS8767 | FindDependentsAsync: align with ISearchService.FindDependentsAsync |
| `SearchResponseBuilder.cs` | CS8614 | BuildAutocomplete: align with ISearchResponseBuilder.BuildAutocomplete |
| `IPackageStagingService.cs` | CS8613 | IPackageStagingService.OpenAsync: widen to match StoragePackageStagingService.OpenAsync |
| `StoragePackageStagingService.cs` | CS8767 | StageAsync: align with IPackageStagingService.StageAsync |
| `StoragePackageStagingService.cs` | CS8767 | OpenAsync: align with IPackageStagingService.OpenAsync |
| `StoragePackageStagingService.cs` | CS8767 | RemoveAsync: align with IPackageStagingService.RemoveAsync |
| `StoragePackageStagingService.cs` | CS8767 | RemoveAsync: align with IPackageStagingService.RemoveAsync |
| `IPackageStorageService.cs` | CS8613 | IPackageStorageService.GetPackageStreamAsync: widen to match PackageStorageService.GetPackageStreamAsync |
| `IPackageStorageService.cs` | CS8613 | IPackageStorageService.GetNuspecStreamAsync: widen to match PackageStorageService.GetNuspecStreamAsync |
| `IPackageStorageService.cs` | CS8613 | IPackageStorageService.GetReadmeStreamAsync: widen to match PackageStorageService.GetReadmeStreamAsync |
| `IPackageStorageService.cs` | CS8613 | IPackageStorageService.GetIconStreamAsync: widen to match PackageStorageService.GetIconStreamAsync |
| `ISymbolStorageService.cs` | CS8613 | ISymbolStorageService.GetPortablePdbContentStreamOrNullAsync: widen to match SymbolStorageService.GetPortablePdbContentStreamOrNullAsync |
| `PackageStorageService.cs` | CS8622 | NuspecPath: align with Func.Invoke |
| `PackageStorageService.cs` | CS8622 | ReadmePath: align with Func.Invoke |
| `PackageStorageService.cs` | CS8622 | IconPath: align with Func.Invoke |
| `DisabledUpstreamClient.cs` | CS8613 | DownloadPackageOrNullAsync: align with IUpstreamClient.DownloadPackageOrNullAsync |
| `DisabledUpstreamClient.cs` | CS8767 | DownloadPackageOrNullAsync: align with IUpstreamClient.DownloadPackageOrNullAsync |
| `V2UpstreamClient.cs` | CS8767 | DownloadPackageOrNullAsync: align with IUpstreamClient.DownloadPackageOrNullAsync |
| `V2UpstreamClient.cs` | CS8621 | ToDependencies: align with Func.Invoke |
| `V2UpstreamClient.cs` | CS8621 | ToDependencies: align with Func.Invoke |
| `V3UpstreamClient.cs` | CS8767 | DownloadPackageOrNullAsync: align with IUpstreamClient.DownloadPackageOrNullAsync |
| `V3UpstreamClient.cs` | CS8621 | ToDependencies: align with Func.Invoke |
| `V3UpstreamClient.cs` | CS8621 | ToDependencies: align with Func.Invoke |
| `IUpstreamClient.cs` | CS8613 | IUpstreamClient.DownloadPackageOrNullAsync: widen to match V2UpstreamClient.DownloadPackageOrNullAsync |
| `RequiredIfAttribute.cs` | CS8765 | IsValid: align with ValidationAttribute.IsValid |
| `ValidateBaGetOptions.cs` | CS8767 | Validate: align with IValidateOptions.Validate |
| `AccountAuthenticationService.cs` | CS8767 | AuthenticateAsync: align with IAuthenticationService.AuthenticateAsync |
| `AccountAuthenticationService.cs` | CS8613 | AuthenticatePrincipalAsync: align with IAuthenticationService.AuthenticatePrincipalAsync |
| `NoOpEmailSender.cs` | CS8767 | SendAsync: align with IEmailMessageSender.SendAsync |
| `OwnershipAuthorizationService.cs` | CS8767 | OnPushedAsync: align with IPackageAuthorizationService.OnPushedAsync |
| `OwnershipAuthorizationService.cs` | CS8767 | AuthorizeAsync: align with IPackageAuthorizationService.AuthorizeAsync |
| `ContactOwners.cshtml.cs` | CS8622 | IsDeliverable: align with Func.Invoke |
| `SmtpEmailSender.cs` | CS8767 | SendAsync: align with IEmailMessageSender.SendAsync |
| `CatalogClient.cs` | CS8767 | GetPageAsync: align with ICatalogClient.GetPageAsync |
| `CatalogClient.cs` | CS8767 | GetPackageDetailsLeafAsync: align with ICatalogClient.GetPackageDetailsLeafAsync |
| `CatalogClient.cs` | CS8767 | GetPackageDeleteLeafAsync: align with ICatalogClient.GetPackageDeleteLeafAsync |
| `NullCatalogClient.cs` | CS8767 | GetPageAsync: align with ICatalogClient.GetPageAsync |
| `NullCatalogClient.cs` | CS8767 | GetPackageDeleteLeafAsync: align with ICatalogClient.GetPackageDeleteLeafAsync |
| `NullCatalogClient.cs` | CS8767 | GetPackageDetailsLeafAsync: align with ICatalogClient.GetPackageDetailsLeafAsync |
| `RawCatalogClient.cs` | CS8767 | GetPageAsync: align with ICatalogClient.GetPageAsync |
| `RawCatalogClient.cs` | CS8767 | GetPackageDeleteLeafAsync: align with ICatalogClient.GetPackageDeleteLeafAsync |
| `RawCatalogClient.cs` | CS8767 | GetPackageDetailsLeafAsync: align with ICatalogClient.GetPackageDetailsLeafAsync |
| `ICatalogLeafItem.cs` | CS8766 | ICatalogLeafItem.PackageId: widen to match CatalogLeafItem.PackageId |
| `ICatalogLeafItem.cs` | CS8766 | ICatalogLeafItem.PackageVersion: widen to match CatalogLeafItem.PackageVersion |
| `IPackageContentClient.cs` | CS8613 | IPackageContentClient.DownloadPackageOrNullAsync: widen to match RawPackageContentClient.DownloadPackageOrNullAsync |
| `IPackageContentClient.cs` | CS8613 | IPackageContentClient.DownloadPackageManifestOrNullAsync: widen to match RawPackageContentClient.DownloadPackageManifestOrNullAsync |
| `PackageContentClient.cs` | CS8613 | DownloadPackageOrNullAsync: align with IPackageContentClient.DownloadPackageOrNullAsync |
| `PackageContentClient.cs` | CS8613 | DownloadPackageManifestOrNullAsync: align with IPackageContentClient.DownloadPackageManifestOrNullAsync |
| `PackageContentClient.cs` | CS8767 | DownloadPackageOrNullAsync: align with IPackageContentClient.DownloadPackageOrNullAsync |
| `RawPackageContentClient.cs` | CS8767 | DownloadPackageOrNullAsync: align with IPackageContentClient.DownloadPackageOrNullAsync |
| `PackageMetadataClient.cs` | CS8767 | GetRegistrationPageAsync: align with IPackageMetadataClient.GetRegistrationPageAsync |
| `RawPackageMetadataClient.cs` | CS8767 | GetRegistrationPageAsync: align with IPackageMetadataClient.GetRegistrationPageAsync |
| `IServiceIndexClient.cs` | CS8613 | IServiceIndexClient.GetAsync: widen to match ServiceIndexClient.GetAsync |
| `RawServiceIndexClient.cs` | CS8613 | GetAsync: align with IServiceIndexClient.GetAsync |
| `BaGetUrlGenerator.cs` | CS8767 | GetRegistrationIndexUrl: align with IUrlGenerator.GetRegistrationIndexUrl |
| `BaGetUrlGenerator.cs` | CS8767 | GetRegistrationLeafUrl: align with IUrlGenerator.GetRegistrationLeafUrl |
| `BaGetUrlGenerator.cs` | CS8767 | GetPackageDownloadUrl: align with IUrlGenerator.GetPackageDownloadUrl |
| `BaGetUrlGenerator.cs` | CS8767 | GetPackageIconDownloadUrl: align with IUrlGenerator.GetPackageIconDownloadUrl |
| `BaGetUrlGenerator.cs` | CS8767 | GetCatalogLeafUrl: align with IUrlGenerator.GetCatalogLeafUrl |
| `LegalPageModel.cs` | CS8764 | LegalPageModel.ConfiguredPath: widen to match TermsModel.ConfiguredPath |
| `ConfigureBaGetOptions.cs` | CS8767 | Validate: align with IValidateOptions.Validate |
| `RecordingLogger.cs` | CS8767 | Log: align with ILogger.Log |
| `AdminPackageTests.cs` | CS8767 | Log: align with ILogger.Log |
| `BrowserUploadReviewTests.cs` | CS8767 | OnPushedAsync: align with IPackageAuthorizationService.OnPushedAsync |
| `BrowserUploadReviewTests.cs` | CS8767 | AuthorizeAsync: align with IPackageAuthorizationService.AuthorizeAsync |
| `BrowserUploadReviewTests.cs` | CS8767 | AuthorizeAsync: align with IPackageAuthorizationService.AuthorizeAsync |
| `BrowserUploadSecurityTests.cs` | CS8767 | StageAsync: align with IPackageStagingService.StageAsync |
| `BrowserUploadSecurityTests.cs` | CS8613 | OpenAsync: align with IPackageStagingService.OpenAsync |
| `BrowserUploadSecurityTests.cs` | CS8767 | RemoveAsync: align with IPackageStagingService.RemoveAsync |
| `BrowserUploadSecurityTests.cs` | CS8767 | RemoveAsync: align with IPackageStagingService.RemoveAsync |
| `BrowserUploadSecurityTests.cs` | CS8767 | StageAsync: align with IPackageStagingService.StageAsync |
| `EmailTests.cs` | CS8767 | Log: align with ILogger.Log |
| `HeaderLinksTests.cs` | CS8767 | Log: align with ILogger.Log |
| `SearchSortWebTests.cs` | CS8767 | FindDependentsAsync: align with ISearchService.FindDependentsAsync |
| `CapturingEmailSender.cs` | CS8767 | SendAsync: align with IEmailMessageSender.SendAsync |
| `HttpSourceResourceProviderTestHost.cs` | CS8609 | TryCreate: align with ResourceProvider.TryCreate |
| `XunitLogger.cs` | CS8767 | Log: align with ILogger.Log |

## Left over

413 diagnostic(s) nothing could be done about.

### CS8604 — 216

| Where | Why |
|---|---|
| `src\BaGet.Azure\Search\AzureSearchService.cs(84)` | 'string value' comes from metadata and would have to be annotated upstream |
| `src\BaGet.Azure\Table\TableJson.cs(22)` | 'string json' comes from metadata and would have to be annotated upstream |
| `src\BaGet.Catalog\CatalogRebuilder.cs(106)` | 'string value' comes from metadata and would have to be annotated upstream |
| `src\BaGet.Catalog\CatalogRebuilder.cs(81)` | 'string value' comes from metadata and would have to be annotated upstream |
| `src\BaGet.Catalog\CatalogRebuilder.cs(91)` | 'string value' comes from metadata and would have to be annotated upstream |
| `src\BaGet.Core\Configuration\Settings\SettingsOverridesStore.cs(137)` | 'string path' comes from metadata and would have to be annotated upstream |
| `src\BaGet.Core\Configuration\Settings\XmlDocumentation.cs(44)` | 'string path1' comes from metadata and would have to be annotated upstream |
| `src\BaGet.Core\Entities\Package.cs(21)` | 'string value' comes from metadata and would have to be annotated upstream |
| `src\BaGet.Core\Indexing\FrameworkCompatibilityService.cs(49)` | 'ConcurrentDictionary<TKey>' (NuGetFramework): the type parameter it is an argument for does not admit null |
| `src\BaGet.Core\Indexing\FrameworkCompatibilityService.cs(68)` | 'NuGet.Frameworks.NuGetFramework framework' comes from metadata and would have to be annotated upstream |
| `src\BaGet.Core\Metadata\RegistrationBuilder.cs(70)` | 'params string?[] value' comes from metadata and would have to be annotated upstream |
| `src\BaGet.Core\PackageDatabase.cs(130)` | 'System.Collections.Generic.IEnumerable<BaGet.Core.PackageDependency> entities' comes from metadata and would have to be annotated upstream |
| `src\BaGet.Core\PackageDatabase.cs(138)` | 'System.Collections.Generic.IEnumerable<BaGet.Core.PackageVulnerability> entities' comes from metadata and would have to be annotated upstream |
| `src\BaGet.Core\PackageDatabase.cs(206)` | 'System.Collections.Generic.IEnumerable<BaGet.Core.PackageVulnerability> entities' comes from metadata and would have to be annotated upstream |
| `src\BaGet.Core\Search\DatabaseSearchService.cs(110)` | could not resolve the method being called |
| `src\BaGet.Core\Search\DatabaseSearchService.cs(181)` | the dereference is in an expression body, where no statement fits |
| `src\BaGet.Core\Search\DatabaseSearchService.cs(269)` | the dereference is in an expression body, where no statement fits |
| `src\BaGet.Core\Search\DatabaseSearchService.cs(274)` | the dereference is in an expression body, where no statement fits |
| `src\BaGet.Core\Search\DatabaseSearchService.cs(64)` | the type argument for 'T' of 'List' is not written anywhere this conversion may edit |
| `src\BaGet.Core\Search\DatabaseSearchService.cs(78)` | 'Dictionary<TKey>' (string): the type parameter it is an argument for does not admit null |
| `src\BaGet.Core\Search\DatabaseSearchService.cs(82)` | 'Dictionary<TKey>' (string): the type parameter it is an argument for does not admit null |
| `src\BaGet.Core\Staging\StoragePackageStagingService.cs(294)` | 'string s' comes from metadata and would have to be annotated upstream |
| `src\BaGet.Core\Statistics\StatisticsService.cs(124)` | could not resolve the method being called |
| `src\BaGet.Core\Storage\FileStorageService.cs(26)` | 'string path' comes from metadata and would have to be annotated upstream |
| `src\BaGet.Core\Storage\FileStorageService.cs(64)` | 'string path' comes from metadata and would have to be annotated upstream |
| `src\BaGet.Core\Storage\PackageStorageService.cs(219)` | 'string path2' comes from metadata and would have to be annotated upstream |
| `src\BaGet.Core\Storage\PackageStorageService.cs(220)` | 'string path3' comes from metadata and would have to be annotated upstream |
| `src\BaGet.Core\Storage\PackageStorageService.cs(228)` | 'string path2' comes from metadata and would have to be annotated upstream |
| `src\BaGet.Core\Storage\PackageStorageService.cs(229)` | 'string path3' comes from metadata and would have to be annotated upstream |
| `src\BaGet.Core\Storage\PackageStorageService.cs(237)` | 'string path2' comes from metadata and would have to be annotated upstream |
| `src\BaGet.Core\Storage\PackageStorageService.cs(238)` | 'string path3' comes from metadata and would have to be annotated upstream |
| `src\BaGet.Core\Storage\PackageStorageService.cs(246)` | 'string path2' comes from metadata and would have to be annotated upstream |
| `src\BaGet.Core\Storage\PackageStorageService.cs(247)` | 'string path3' comes from metadata and would have to be annotated upstream |
| `src\BaGet.Core\Storage\SymbolStorageService.cs(54)` | 'string path1' comes from metadata and would have to be annotated upstream |
| `src\BaGet.Core\Upstream\DownloadsImporter.cs(44)` | the type argument for 'TKey' of 'Dictionary' is not written anywhere this conversion may edit |
| `src\BaGet.Core\Upstream\DownloadsImporter.cs(45)` | the type argument for 'TKey' of 'Dictionary' is not written anywhere this conversion may edit |
| `src\BaGet.Core\Upstream\DownloadsImporter.cs(60)` | the dereference is not inside a statement, so no guard can be placed |
| `src\BaGet.Core\Upstream\PackageDownloadsJsonSource.cs(53)` | 'string str' comes from metadata and would have to be annotated upstream |
| `src\BaGet.Core\Upstream\PackageDownloadsJsonSource.cs(53)` | 'record[0]' would have to be captured in a local first |
| `src\BaGet.Core\Upstream\PackageDownloadsJsonSource.cs(71)` | 'string value' comes from metadata and would have to be annotated upstream |
| `src\BaGet.Core\Upstream\PackageDownloadsJsonSource.cs(71)` | 'token[0]' would have to be captured in a local first |
| `src\BaGet.Identity\AccountAuthenticationService.cs(52)` | 'string userId' comes from metadata and would have to be annotated upstream |
| `src\BaGet.Identity\AccountDeletionService.cs(186)` | the type argument for 'T' of 'List' is not written anywhere this conversion may edit |
| `src\BaGet.Identity\AccountDeletionService.cs(237)` | 'string userId' comes from metadata and would have to be annotated upstream |
| `src\BaGet.Identity\AccountDeletionService.cs(304)` | 'UserManager<TUser>' (BaGetUser): the type parameter it is an argument for does not admit null |
| `src\BaGet.Identity\AccountRecoveryService.cs(34)` | 'string userName' comes from metadata and would have to be annotated upstream |
| `src\BaGet.Identity\AvatarService.cs(59)` | could not resolve the method being called |
| `src\BaGet.Identity\PackageOwnershipService.cs(71)` | 'await GetAccountNamesAsync(userId, userName, ca...' would have to be captured in a local first |
| `src\BaGet.Identity\PackageOwnershipService.cs(85)` | 'await GetAccountNamesAsync(userId, userName, ca...' would have to be captured in a local first |
| `src\BaGet.Identity\Pages\Account\AccountPageModel.cs(101)` | 'string input' comes from metadata and would have to be annotated upstream |
| `src\BaGet.Identity\Pages\Account\AccountPageModel.cs(149)` | 'UserManager<TUser>' (BaGetUser): the type parameter it is an argument for does not admit null |
| `src\BaGet.Identity\Pages\Account\ForgotPassword.cshtml.cs(38)` | 'string userName' comes from metadata and would have to be annotated upstream |
| `src\BaGet.Identity\Pages\Account\Login.cshtml.cs(51)` | 'string userName' comes from metadata and would have to be annotated upstream |
| `src\BaGet.Identity\Pages\Account\Login.cshtml.cs(51)` | 'string password' comes from metadata and would have to be annotated upstream |
| `src\BaGet.Identity\Pages\Account\LoginWith2fa.cshtml.cs(39)` | 'string code' comes from metadata and would have to be annotated upstream |
| `src\BaGet.Identity\Pages\Account\Organizations.cshtml.cs(91)` | 'string userId' comes from metadata and would have to be annotated upstream |
| `src\BaGet.Identity\Pages\Account\Packages.cshtml.cs(104)` | 'string value' comes from metadata and would have to be annotated upstream |
| `src\BaGet.Identity\Pages\Account\Packages.cshtml.cs(96)` | 'string value' comes from metadata and would have to be annotated upstream |
| `src\BaGet.Identity\Pages\Account\Profile.cshtml.cs(12)` | 'string stringToEscape' comes from metadata and would have to be annotated upstream |
| `src\BaGet.Identity\Pages\Account\Profile.cshtml.cs(12)` | the dereference is not inside a statement, so no guard can be placed |
| `src\BaGet.Identity\Pages\Account\Register.cshtml.cs(75)` | 'string password' comes from metadata and would have to be annotated upstream |
| `src\BaGet.Identity\Pages\Account\Register.cshtml.cs(94)` | 'string email' comes from metadata and would have to be annotated upstream |
| `src\BaGet.Identity\Pages\Account\ResetPassword.cshtml.cs(46)` | 'string userId' comes from metadata and would have to be annotated upstream |
| `src\BaGet.Identity\Pages\Account\ResetPassword.cshtml.cs(54)` | 'string newPassword' comes from metadata and would have to be annotated upstream |
| `src\BaGet.Identity\Pages\Account\TwoFactor.cshtml.cs(174)` | 'string stringToEscape' comes from metadata and would have to be annotated upstream |
| `src\BaGet.Identity\Pages\Account\TwoFactor.cshtml.cs(76)` | 'await UserManager.GenerateNewTwoFactorRecoveryC...' would have to be captured in a local first |
| `src\BaGet.Identity\Pages\Admin\Packages.cshtml.cs(40)` | the dereference is in an expression body, where no statement fits |
| `src\BaGet.Identity\Pages\Profiles\Profile.cshtml.cs(38)` | 'string userName' comes from metadata and would have to be annotated upstream |
| `src\BaGet.Identity\SmtpEmailSender.cs(106)` | 'string host' comes from metadata and would have to be annotated upstream |
| `src\BaGet.Identity\SmtpEmailSender.cs(109)` | 'string password' comes from metadata and would have to be annotated upstream |
| `src\BaGet.Identity\SmtpEmailSender.cs(57)` | 'string text' comes from metadata and would have to be annotated upstream |
| `src\BaGet.Identity\SmtpEmailSender.cs(83)` | 'string text' comes from metadata and would have to be annotated upstream |
| `src\BaGet.Protocol\Converters\StringOrStringArrayJsonConverter.cs(18)` | could not find the argument |
| `src\BaGet.Protocol\EndpointFailover.cs(79)` | 'System.Collections.Generic.IEnumerable<System.Exception> innerExceptions' comes from metadata and would have to be annotated upstream |
| `src\BaGet.Protocol\Extensions\CatalogModelExtensions.cs(102)` | 'string value' comes from metadata and would have to be annotated upstream |
| `src\BaGet.Protocol\Extensions\CatalogModelExtensions.cs(132)` | 'string value' comes from metadata and would have to be annotated upstream |
| `src\BaGet.Protocol\Extensions\PackageMetadataModelExtensions.cs(19)` | 'string value' comes from metadata and would have to be annotated upstream |
| `src\BaGet.Protocol\Extensions\PackageMetadataModelExtensions.cs(46)` | 'string value' comes from metadata and would have to be annotated upstream |
| `src\BaGet.Protocol\Extensions\PackageMetadataModelExtensions.cs(56)` | 'string value' comes from metadata and would have to be annotated upstream |
| `src\BaGet.Protocol\Extensions\SearchModelExtensions.cs(18)` | 'string value' comes from metadata and would have to be annotated upstream |
| `src\BaGet.Protocol\Extensions\SearchModelExtensions.cs(28)` | 'string value' comes from metadata and would have to be annotated upstream |
| `src\BaGet.Validation\PackageValidationRunner.cs(128)` | 'string value' comes from metadata and would have to be annotated upstream |
| `src\BaGet.Validation\RepositorySigner.cs(108)` | 'string path' comes from metadata and would have to be annotated upstream |
| `src\BaGet.Web.V2\V2FeedWriter.cs(179)` | 'object value' comes from metadata and would have to be annotated upstream |
| `src\BaGet.Web\BaGetUrlGenerator.cs(110)` | 'Microsoft.AspNetCore.Http.HttpContext httpContext' comes from metadata and would have to be annotated upstream |
| `src\BaGet.Web\BaGetUrlGenerator.cs(121)` | 'Microsoft.AspNetCore.Http.HttpContext httpContext' comes from metadata and would have to be annotated upstream |
| `src\BaGet.Web\BaGetUrlGenerator.cs(137)` | 'Microsoft.AspNetCore.Http.HttpContext httpContext' comes from metadata and would have to be annotated upstream |
| `src\BaGet.Web\BaGetUrlGenerator.cs(153)` | 'Microsoft.AspNetCore.Http.HttpContext httpContext' comes from metadata and would have to be annotated upstream |
| `src\BaGet.Web\BaGetUrlGenerator.cs(165)` | 'Microsoft.AspNetCore.Http.HttpContext httpContext' comes from metadata and would have to be annotated upstream |
| `src\BaGet.Web\BaGetUrlGenerator.cs(200)` | 'string stringToEscape' comes from metadata and would have to be annotated upstream |
| `src\BaGet.Web\BaGetUrlGenerator.cs(34)` | 'Microsoft.AspNetCore.Http.HttpContext httpContext' comes from metadata and would have to be annotated upstream |
| `src\BaGet.Web\BaGetUrlGenerator.cs(52)` | 'Microsoft.AspNetCore.Http.HttpContext httpContext' comes from metadata and would have to be annotated upstream |
| `src\BaGet.Web\BaGetUrlGenerator.cs(60)` | 'Microsoft.AspNetCore.Http.HttpContext httpContext' comes from metadata and would have to be annotated upstream |
| `src\BaGet.Web\BaGetUrlGenerator.cs(68)` | 'Microsoft.AspNetCore.Http.HttpContext httpContext' comes from metadata and would have to be annotated upstream |
| `src\BaGet.Web\BaGetUrlGenerator.cs(76)` | 'Microsoft.AspNetCore.Http.HttpContext httpContext' comes from metadata and would have to be annotated upstream |
| `src\BaGet.Web\BaGetUrlGenerator.cs(84)` | 'Microsoft.AspNetCore.Http.HttpContext httpContext' comes from metadata and would have to be annotated upstream |
| `src\BaGet.Web\BaGetUrlGenerator.cs(98)` | 'Microsoft.AspNetCore.Http.HttpContext httpContext' comes from metadata and would have to be annotated upstream |
| `src\BaGet.Web\Pages\Package.cshtml.cs(132)` | 'string stringToEscape' comes from metadata and would have to be annotated upstream |
| `src\BaGet\Program.cs(119)` | 'string userName' comes from metadata and would have to be annotated upstream |
| `src\BaGet\Program.cs(248)` | 'string path' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Core.Tests\Extensions\PackageArchiveReaderExtensionsTests.cs(102)` | 'System.Collections.IEnumerable collection' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Core.Tests\Extensions\PackageArchiveReaderExtensionsTests.cs(270)` | 'System.Collections.IEnumerable collection' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Core.Tests\Extensions\PackageArchiveReaderExtensionsTests.cs(283)` | 'System.Collections.Generic.IEnumerable<BaGet.Core.PackageDependency> collection' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Core.Tests\Extensions\PackageArchiveReaderExtensionsTests.cs(297)` | 'System.Collections.Generic.IEnumerable<BaGet.Core.PackageDependency> collection' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Core.Tests\Extensions\PackageArchiveReaderExtensionsTests.cs(325)` | 'System.Collections.Generic.IEnumerable<BaGet.Core.PackageType> collection' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Core.Tests\Extensions\PackageArchiveReaderExtensionsTests.cs(338)` | 'System.Collections.Generic.IEnumerable<BaGet.Core.PackageType> collection' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Core.Tests\Extensions\PackageArchiveReaderExtensionsTests.cs(344)` | 'System.Collections.Generic.IEnumerable<BaGet.Core.TargetFramework> collection' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Core.Tests\Extensions\PackageArchiveReaderExtensionsTests.cs(357)` | 'System.Collections.Generic.IEnumerable<BaGet.Core.TargetFramework> collection' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Core.Tests\Search\SearchResponseBuilderTests.cs(104)` | 'System.Collections.Generic.IEnumerable<BaGet.Protocol.Models.SearchResult> collection' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Core.Tests\Search\SearchResponseBuilderTests.cs(117)` | 'System.Collections.Generic.IEnumerable<BaGet.Protocol.Models.SearchResult> collection' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Core.Tests\Search\SearchResponseBuilderTests.cs(130)` | 'System.Collections.Generic.IEnumerable<BaGet.Protocol.Models.SearchResult> collection' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Core.Tests\Search\SearchResponseBuilderTests.cs(143)` | 'System.Collections.Generic.IEnumerable<BaGet.Protocol.Models.SearchResult> collection' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Core.Tests\Search\SearchResponseBuilderTests.cs(154)` | 'System.Collections.Generic.IEnumerable<BaGet.Protocol.Models.SearchResult> collection' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Core.Tests\Search\SearchResponseBuilderTests.cs(174)` | 'System.Collections.IEnumerable collection' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Core.Tests\Search\SearchResponseBuilderTests.cs(191)` | 'System.Collections.IEnumerable collection' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Core.Tests\Search\SearchResponseBuilderTests.cs(204)` | 'System.Collections.Generic.IEnumerable<BaGet.Protocol.Models.SearchResult> collection' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Core.Tests\Search\SearchResponseBuilderTests.cs(213)` | 'System.Collections.Generic.IEnumerable<string?> collection' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Core.Tests\Search\SearchResponseBuilderTests.cs(213)` | 'System.Collections.Generic.IEnumerable<string> collection' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Core.Tests\Search\SearchResponseBuilderTests.cs(227)` | 'System.Collections.Generic.IEnumerable<BaGet.Core.PackageDependent> collection' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Core.Tests\Search\SearchResponseBuilderTests.cs(62)` | 'System.Collections.Generic.IEnumerable<BaGet.Protocol.Models.SearchResult> collection' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Core.Tests\Search\SearchResponseBuilderTests.cs(75)` | 'System.Collections.Generic.IEnumerable<BaGet.Protocol.Models.SearchResult> collection' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Core.Tests\Search\SearchResponseBuilderTests.cs(88)` | 'System.Collections.Generic.IEnumerable<BaGet.Protocol.Models.SearchResult> collection' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Core.Tests\ServiceIndex\BaGetServiceIndexTests.cs(117)` | 'System.Collections.Generic.IEnumerable<BaGet.Protocol.Models.ServiceIndexItem> collection' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Core.Tests\ServiceIndex\BaGetServiceIndexTests.cs(67)` | 'System.Collections.Generic.IEnumerable<string> collection' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Core.Tests\ServiceIndex\BaGetServiceIndexTests.cs(78)` | 'System.Collections.Generic.IEnumerable<string> collection' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Core.Tests\ServiceIndex\BaGetServiceIndexTests.cs(84)` | 'System.Collections.Generic.IEnumerable<string> collection' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Core.Tests\ServiceIndex\BaGetServiceIndexTests.cs(85)` | 'System.Collections.Generic.IEnumerable<string> collection' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Core.Tests\Services\FileStorageServiceTests.cs(110)` | 'string path' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Core.Tests\Services\FileStorageServiceTests.cs(130)` | 'string path' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Core.Tests\Services\FileStorageServiceTests.cs(290)` | 'string path1' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Core.Tests\Services\PackageDatabaseTests.cs(129)` | 'System.Collections.Generic.IEnumerable<BaGet.Core.PackageDependency> collection' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Core.Tests\Services\PackageDatabaseTests.cs(130)` | 'System.Collections.Generic.IEnumerable<BaGet.Core.TargetFramework> collection' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Core.Tests\Services\PackageDatabaseTests.cs(131)` | 'System.Collections.Generic.IEnumerable<BaGet.Core.PackageType> collection' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Core.Tests\Services\PackageDatabaseTests.cs(203)` | 'System.Collections.Generic.IEnumerable<BaGet.Core.PackageDependency> collection' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Core.Tests\Services\PackageDatabaseTests.cs(204)` | 'System.Collections.Generic.IEnumerable<BaGet.Core.TargetFramework> collection' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Core.Tests\Services\PackageIndexingServiceTests.cs(356)` | 'byte[] bytes' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Core.Tests\Services\PackageIndexingServiceTests.cs(386)` | 'byte[] bytes' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Core.Tests\Services\PackageStorageServiceTests.cs(509)` | 'string path' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Core.Tests\Services\PackageStorageServiceTests.cs(561)` | 'System.IO.Stream stream' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Core.Tests\Services\StoragePackageStagingServiceTests.cs(303)` | 'System.IO.Stream stream' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Core.Tests\Settings\SettingsOverridesStoreTests.cs(112)` | 'bool condition' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Core.Tests\Settings\SettingsOverridesStoreTests.cs(113)` | 'Equal' infers its own type arguments, so there is nothing written to annotate |
| `tests\BaGet.Core.Tests\Settings\SettingsOverridesStoreTests.cs(114)` | 'Equal' infers its own type arguments, so there is nothing written to annotate |
| `tests\BaGet.Core.Tests\Settings\SettingsOverridesStoreTests.cs(126)` | 'string path' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Core.Tests\Settings\SettingsOverridesStoreTests.cs(134)` | 'string path' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Core.Tests\Settings\SettingsOverridesStoreTests.cs(148)` | 'string path' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Core.Tests\Settings\SettingsOverridesStoreTests.cs(63)` | 'System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<string, System.Text.Json.Nodes.JsonNode?>> collection' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Core.Tests\Settings\SettingsServiceTests.cs(225)` | 'System.Collections.Generic.IDictionary<string, string> dictionary' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Core.Tests\Settings\SettingsServiceTests.cs(50)` | 'string path2' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Core.Tests\Settings\SettingsServiceTests.cs(86)` | could not resolve the method being called |
| `tests\BaGet.Protocol.Tests\Converters\StringOrStringArrayJsonConverterTests.cs(20)` | 'System.Collections.Generic.IEnumerable<string> collection' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Protocol.Tests\Converters\StringOrStringArrayJsonConverterTests.cs(35)` | 'System.Collections.Generic.IEnumerable<string> collection' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Protocol.Tests\Converters\StringOrStringArrayJsonConverterTests.cs(49)` | 'System.Collections.IEnumerable collection' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Protocol.Tests\NuGetClientFactoryTests.cs(162)` | 'System.Collections.Generic.IEnumerable<BaGet.Protocol.Models.SearchResult> collection' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Protocol.Tests\NuGetClientFactoryTests.cs(174)` | 'System.Collections.Generic.IEnumerable<string?> collection' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Protocol.Tests\NuGetClientFactoryTests.cs(174)` | 'System.Collections.Generic.IEnumerable<string> collection' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Protocol.Tests\NuGetClientTests.cs(189)` | 'System.Collections.Generic.IEnumerable<BaGet.Protocol.Models.SearchResult> collection' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Protocol.Tests\NuGetClientTests.cs(193)` | 'System.Collections.Generic.IEnumerable<string> collection' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Protocol.Tests\NuGetClientTests.cs(201)` | 'System.Collections.Generic.IEnumerable<string> collection' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Protocol.Tests\RawAutocompleteClientTests.cs(25)` | 'System.Collections.Generic.IEnumerable<string?> collection' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Protocol.Tests\RawAutocompleteClientTests.cs(25)` | 'System.Collections.Generic.IEnumerable<string> collection' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Protocol.Tests\RawPackageMetadataClientTests.cs(109)` | 'System.Collections.Generic.IEnumerable<BaGet.Protocol.Models.RegistrationIndexPageItem> collection' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Protocol.Tests\RawSearchClientTests.cs(26)` | 'System.Collections.Generic.IEnumerable<BaGet.Protocol.Models.SearchResult> collection' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Protocol.Tests\RawSearchClientTests.cs(28)` | 'System.Collections.Generic.IEnumerable<string> collection' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Protocol.Tests\RawSearchClientTests.cs(31)` | 'System.Collections.Generic.IEnumerable<BaGet.Protocol.Models.SearchResultPackageType> collection' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Protocol.Tests\SearchFailoverTests.cs(196)` | 'System.Collections.Generic.IEnumerable<string?> collection' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Protocol.Tests\SearchFailoverTests.cs(196)` | 'System.Collections.Generic.IEnumerable<string> collection' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Protocol.Tests\SearchFailoverTests.cs(265)` | 'System.Collections.IEnumerable collection' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Protocol.Tests\SearchFailoverTests.cs(76)` | 'System.Collections.Generic.IEnumerable<BaGet.Protocol.Models.SearchResult> collection' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Tests\AccountDeletionAtomicityTests.cs(58)` | the type argument for 'TUser' of 'UserManager' is not written anywhere this conversion may edit |
| `tests\BaGet.Tests\AccountDeletionAtomicityTests.cs(70)` | 'System.Collections.Generic.IEnumerable<BaGet.Core.Organization> collection' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Tests\AccountReauthenticationTests.cs(98)` | the type argument for 'TUser' of 'UserManager' is not written anywhere this conversion may edit |
| `tests\BaGet.Tests\AccountSettingsTests.cs(506)` | the type argument for 'TUser' of 'UserManager' is not written anywhere this conversion may edit |
| `tests\BaGet.Tests\AccountSettingsTests.cs(606)` | 'System.Collections.IEnumerable collection' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Tests\AdminUserActionsTests.cs(94)` | the type argument for 'TUser' of 'UserManager' is not written anywhere this conversion may edit |
| `tests\BaGet.Tests\BaGetClientIntegrationTests.cs(102)` | 'System.Collections.IEnumerable collection' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Tests\BaGetClientIntegrationTests.cs(113)` | 'System.Collections.Generic.IEnumerable<string?> collection' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Tests\BaGetClientIntegrationTests.cs(113)` | 'System.Collections.Generic.IEnumerable<string> collection' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Tests\BaGetClientIntegrationTests.cs(125)` | 'System.Collections.IEnumerable collection' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Tests\BaGetClientIntegrationTests.cs(46)` | 'System.Collections.IEnumerable collection' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Tests\BaGetClientIntegrationTests.cs(47)` | 'System.Collections.IEnumerable collection' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Tests\BaGetClientIntegrationTests.cs(48)` | 'System.Collections.IEnumerable collection' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Tests\BaGetClientIntegrationTests.cs(49)` | 'System.Collections.IEnumerable collection' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Tests\BaGetClientIntegrationTests.cs(50)` | 'System.Collections.IEnumerable collection' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Tests\BaGetClientIntegrationTests.cs(51)` | 'System.Collections.IEnumerable collection' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Tests\BaGetClientIntegrationTests.cs(52)` | 'System.Collections.IEnumerable collection' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Tests\BaGetClientIntegrationTests.cs(53)` | 'System.Collections.IEnumerable collection' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Tests\BaGetClientIntegrationTests.cs(63)` | 'System.Collections.Generic.IEnumerable<BaGet.Protocol.Models.SearchResult> collection' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Tests\BaGetClientIntegrationTests.cs(64)` | 'System.Collections.Generic.IEnumerable<string> collection' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Tests\BaGetClientIntegrationTests.cs(65)` | 'System.Collections.Generic.IEnumerable<BaGet.Protocol.Models.SearchResultVersion> collection' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Tests\BaGetClientIntegrationTests.cs(82)` | 'System.Collections.IEnumerable collection' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Tests\BaGetClientIntegrationTests.cs(92)` | 'System.Collections.Generic.IEnumerable<string> collection' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Tests\BrowserUploadTests.cs(316)` | 'System.Collections.IEnumerable collection' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Tests\DatabaseSearchServiceTests.cs(110)` | could not resolve the method being called |
| `tests\BaGet.Tests\DatabaseSearchServiceTests.cs(157)` | 'System.Collections.Generic.IEnumerable<BaGet.Protocol.Models.SearchResult> collection' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Tests\DatabaseSearchServiceTests.cs(196)` | 'System.Collections.Generic.IEnumerable<BaGet.Protocol.Models.SearchResult> collection' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Tests\DatabaseSearchServiceTests.cs(211)` | 'System.Collections.IEnumerable collection' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Tests\DatabaseSearchServiceTests.cs(224)` | 'System.Collections.IEnumerable collection' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Tests\DatabaseSearchServiceTests.cs(233)` | 'System.Collections.IEnumerable collection' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Tests\DatabaseSearchServiceTests.cs(309)` | 'System.Collections.IEnumerable collection' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Tests\DatabaseSearchServiceTests.cs(322)` | 'System.Collections.IEnumerable collection' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Tests\DatabaseSearchServiceTests.cs(417)` | 'System.Collections.IEnumerable collection' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Tests\EmailTests.cs(154)` | the type argument for 'TUser' of 'UserManager' is not written anywhere this conversion may edit |
| `tests\BaGet.Tests\EmailTests.cs(170)` | the type argument for 'TUser' of 'UserManager' is not written anywhere this conversion may edit |
| `tests\BaGet.Tests\SettingsCatalogTests.cs(230)` | 'string message' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Tests\SettingsCatalogTests.cs(251)` | 'string subpath' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Tests\SettingsCatalogTests.cs(256)` | 'string path' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Tests\Support\Browser.cs(93)` | 'System.IO.Stream content' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Tests\Support\ExternalCookieFilter.cs(32)` | 'string value' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Tests\Support\ExternalCookieFilter.cs(33)` | 'string value' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Tests\Support\ExternalCookieFilter.cs(35)` | 'string value' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Tests\Support\ExternalCookieFilter.cs(36)` | 'string value' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Tests\Support\ExternalCookieFilter.cs(38)` | 'string value' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Tests\ValidationIntegrationTests.cs(257)` | 'string path' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Web.Tests\Pages\PackageModelFacts.cs(239)` | 'System.Collections.Generic.IEnumerable<BaGet.Web.DependencyModel> collection' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Web.Tests\Pages\PackageModelFacts.cs(277)` | 'System.Collections.Generic.IEnumerable<BaGet.Web.DependencyGroupModel> collection' comes from metadata and would have to be annotated upstream |

### CS8602 — 77

| Where | Why |
|---|---|
| `src\BaGet.Core\Extensions\DependencyInjectionExtensions.Providers.cs(44)` | 'config[DatabaseTypeKey]' would have to be captured in a local first |
| `src\BaGet.Core\Extensions\DependencyInjectionExtensions.Providers.cs(55)` | 'config[SearchTypeKey]' would have to be captured in a local first |
| `src\BaGet.Core\Extensions\DependencyInjectionExtensions.Providers.cs(66)` | 'config[StorageTypeKey]' would have to be captured in a local first |
| `src\BaGet.Core\Search\DatabaseSearchService.cs(104)` | the dereference is in an expression body, where no statement fits |
| `src\BaGet.Core\Search\DatabaseSearchService.cs(158)` | the dereference is in an expression body, where no statement fits |
| `src\BaGet.Core\Search\DatabaseSearchService.cs(247)` | the dereference is in an expression body, where no statement fits |
| `src\BaGet.Core\Statistics\StatisticsService.cs(119)` | the dereference is in an expression body, where no statement fits |
| `src\BaGet.Core\Upstream\PackageDownloadsJsonSource.cs(72)` | 'token[1]' would have to be captured in a local first |
| `src\BaGet.Identity\AccountDeletionService.cs(103)` | the dereference is in an expression body, where no statement fits |
| `src\BaGet.Identity\AccountDeletionService.cs(114)` | 'OrganizationOwnsPackagesAsync(organization.Name...' would have to be captured in a local first |
| `src\BaGet.Identity\AccountDeletionService.cs(212)` | the dereference is in an expression body, where no statement fits |
| `src\BaGet.Identity\AccountDeletionService.cs(225)` | 'OrganizationOwnsPackagesAsync(organization.Name...' would have to be captured in a local first |
| `src\BaGet.Identity\AccountDeletionService.cs(267)` | the dereference is in an expression body, where no statement fits |
| `src\BaGet.Identity\AccountDeletionService.cs(271)` | the dereference is in an expression body, where no statement fits |
| `src\BaGet.Identity\AccountDeletionService.cs(288)` | 'OrganizationOwnsPackagesAsync(organization.Name...' would have to be captured in a local first |
| `src\BaGet.Identity\AccountDeletionService.cs(330)` | the dereference is in an expression body, where no statement fits |
| `src\BaGet.Identity\AccountDeletionService.cs(331)` | the dereference is in an expression body, where no statement fits |
| `src\BaGet.Identity\AccountDeletionService.cs(339)` | the dereference is in an expression body, where no statement fits |
| `src\BaGet.Identity\AccountDeletionService.cs(357)` | the dereference is in an expression body, where no statement fits |
| `src\BaGet.Identity\AccountDeletionService.cs(95)` | the dereference is in an expression body, where no statement fits |
| `src\BaGet.Identity\AvatarService.cs(51)` | the dereference is in an expression body, where no statement fits |
| `src\BaGet.Identity\PackageOwnershipService.cs(102)` | the dereference is in an expression body, where no statement fits |
| `src\BaGet.Identity\PackageOwnershipService.cs(111)` | the dereference is in an expression body, where no statement fits |
| `src\BaGet.Identity\PackageOwnershipService.cs(134)` | the dereference is in an expression body, where no statement fits |
| `src\BaGet.Identity\PackageOwnershipService.cs(154)` | the dereference is in an expression body, where no statement fits |
| `src\BaGet.Identity\PackageOwnershipService.cs(169)` | the dereference is in an expression body, where no statement fits |
| `src\BaGet.Identity\PackageOwnershipService.cs(178)` | the dereference is in an expression body, where no statement fits |
| `src\BaGet.Identity\PackageOwnershipService.cs(210)` | the dereference is in an expression body, where no statement fits |
| `src\BaGet.Identity\PackageOwnershipService.cs(224)` | the dereference is in an expression body, where no statement fits |
| `src\BaGet.Identity\PackageOwnershipService.cs(248)` | the dereference is in an expression body, where no statement fits |
| `src\BaGet.Identity\PackageOwnershipService.cs(50)` | the dereference is in an expression body, where no statement fits |
| `src\BaGet.Identity\PackageOwnershipService.cs(63)` | the dereference is in an expression body, where no statement fits |
| `src\BaGet.Identity\PackageOwnershipService.cs(77)` | the dereference is in an expression body, where no statement fits |
| `src\BaGet.Identity\PackageOwnershipService.cs(91)` | the dereference is in an expression body, where no statement fits |
| `src\BaGet.Identity\Pages\Account\Organizations.cshtml.cs(85)` | 'await Ownership.GetOrganizationsAsync(userId, c...' would have to be captured in a local first |
| `src\BaGet.Identity\Pages\Account\Packages.cshtml.cs(68)` | the dereference is in an expression body, where no statement fits |
| `src\BaGet.Identity\Pages\Admin\Packages.cshtml.cs(40)` | the dereference is in an expression body, where no statement fits |
| `src\BaGet.Identity\Pages\Admin\Packages.cshtml.cs(41)` | the dereference is in an expression body, where no statement fits |
| `src\BaGet.Validation\ValidationWorker.cs(66)` | 'await runner.GetPendingSubmissionIdsAsync(cance...' would have to be captured in a local first |
| `tests\BaGet.Core.Tests\ServiceIndex\BaGetServiceIndexTests.cs(67)` | 'Types()' would have to be captured in a local first |
| `tests\BaGet.Core.Tests\ServiceIndex\BaGetServiceIndexTests.cs(78)` | 'Types()' would have to be captured in a local first |
| `tests\BaGet.Core.Tests\ServiceIndex\BaGetServiceIndexTests.cs(84)` | 'Types()' would have to be captured in a local first |
| `tests\BaGet.Core.Tests\ServiceIndex\BaGetServiceIndexTests.cs(85)` | 'Types()' would have to be captured in a local first |
| `tests\BaGet.Protocol.Tests\NuGetClientTests.cs(199)` | '_target.AutocompleteAsync()' would have to be captured in a local first |
| `tests\BaGet.Tests\AccountReauthenticationTests.cs(117)` | 'await FindAsync(app)' would have to be captured in a local first |
| `tests\BaGet.Tests\AccountReauthenticationTests.cs(178)` | 'await FindAsync(app)' would have to be captured in a local first |
| `tests\BaGet.Tests\AccountReviewFixesTests.cs(100)` | 'await FindAsync(app, "alice")' would have to be captured in a local first |
| `tests\BaGet.Tests\AccountReviewFixesTests.cs(156)` | 'await FindAsync(app, "alice")' would have to be captured in a local first |
| `tests\BaGet.Tests\AccountReviewFixesTests.cs(83)` | 'await FindAsync(app, "alice")' would have to be captured in a local first |
| `tests\BaGet.Tests\AccountSettingsTests.cs(230)` | 'await FindAsync(app, "alice")' would have to be captured in a local first |
| `tests\BaGet.Tests\AccountSettingsTests.cs(248)` | 'await FindAsync(app, "alice")' would have to be captured in a local first |
| `tests\BaGet.Tests\AccountSettingsTests.cs(254)` | 'await FindAsync(app, "alice")' would have to be captured in a local first |
| `tests\BaGet.Tests\AccountSettingsTests.cs(263)` | 'await FindAsync(app, "alice")' would have to be captured in a local first |
| `tests\BaGet.Tests\AccountSettingsTests.cs(267)` | 'await FindAsync(app, "alice")' would have to be captured in a local first |
| `tests\BaGet.Tests\AccountSettingsTests.cs(292)` | 'await FindAsync(app, "alice")' would have to be captured in a local first |
| `tests\BaGet.Tests\AccountSettingsTests.cs(321)` | 'await FindAsync(app, "alice")' would have to be captured in a local first |
| `tests\BaGet.Tests\AccountSettingsTests.cs(424)` | 'await FindAsync(app, "alice")' would have to be captured in a local first |
| `tests\BaGet.Tests\AccountSettingsTests.cs(438)` | 'await FindAsync(app, "alice")' would have to be captured in a local first |
| `tests\BaGet.Tests\AccountSettingsTests.cs(478)` | 'await FindAsync(app, "alice")' would have to be captured in a local first |
| `tests\BaGet.Tests\AccountSettingsTests.cs(481)` | 'await FindAsync(app, "alice")' would have to be captured in a local first |
| `tests\BaGet.Tests\AccountSettingsTests.cs(499)` | 'await FindAsync(app, "alice")' would have to be captured in a local first |
| `tests\BaGet.Tests\AdministratorTests.cs(69)` | 'await WithAsync(app, p => p.GetRequiredService<...' would have to be captured in a local first |
| `tests\BaGet.Tests\AdminPackageTests.cs(97)` | 'await FindAsync("Alice.Package", "1.0.0")' would have to be captured in a local first |
| `tests\BaGet.Tests\AdminUserActionsTests.cs(117)` | 'await FindAsync(app, "alice")' would have to be captured in a local first |
| `tests\BaGet.Tests\AllowedHostsIntegrationTests.cs(43)` | 'content["resources"]' would have to be captured in a local first |
| `tests\BaGet.Tests\AllowedHostsIntegrationTests.cs(73)` | 'content["resources"]' would have to be captured in a local first |
| `tests\BaGet.Tests\BaGetClientIntegrationTests.cs(100)` | '_client.AutocompleteAsync("PackageDoesNotExist")' would have to be captured in a local first |
| `tests\BaGet.Tests\BaGetClientIntegrationTests.cs(90)` | '_client.AutocompleteAsync()' would have to be captured in a local first |
| `tests\BaGet.Tests\BrowserUploadSecurityTests.cs(194)` | 'await s.GetRequiredService<UserManager<BaGetUse...' would have to be captured in a local first |
| `tests\BaGet.Tests\PackageManagementPageTests.cs(228)` | 'await s.GetRequiredService<UserManager<BaGetUse...' would have to be captured in a local first |
| `tests\BaGet.Tests\PackageManagementPageTests.cs(373)` | 'await FindAsync("Alice.Package", "1.0.0")' would have to be captured in a local first |
| `tests\BaGet.Tests\PackageManagementPageTests.cs(392)` | 'await FindAsync("Alice.Package", "1.0.0")' would have to be captured in a local first |
| `tests\BaGet.Tests\PackageManagementPageTests.cs(409)` | 'await FindAsync("Alice.Package", "1.0.0")' would have to be captured in a local first |
| `tests\BaGet.Tests\PathBaseIntegrationTests.cs(113)` | 'content["resources"]' would have to be captured in a local first |
| `tests\BaGet.Tests\PathBaseIntegrationTests.cs(43)` | 'content["resources"]' would have to be captured in a local first |
| `tests\BaGet.Tests\PathBaseIntegrationTests.cs(59)` | 'content["resources"]' would have to be captured in a local first |
| `tests\BaGet.Tests\PathBaseIntegrationTests.cs(94)` | 'content["resources"]' would have to be captured in a local first |

### CS8619 — 31

| Where | Why |
|---|---|
| `src\BaGet.Catalog\CatalogDatabase.cs(109)` | 'System.Threading.Tasks.Task<BaGet.Core.CatalogEntry>' is not spelled out anywhere this conversion may edit |
| `src\BaGet.Catalog\CatalogRebuilder.cs(106)` | '(string, string)' needed no change after all |
| `src\BaGet.Core\Configuration\Settings\SettingsForm.cs(123)` | could not resolve the type of the value |
| `src\BaGet.Core\Configuration\Settings\SettingsOverridesStore.cs(102)` | could not find what the value flows into |
| `src\BaGet.Core\Extensions\PackageArchiveReaderExtensions.cs(137)` | could not resolve the type of the value |
| `src\BaGet.Core\Extensions\PackageArchiveReaderExtensions.cs(142)` | could not resolve the type of the value |
| `src\BaGet.Core\Indexing\FrameworkCompatibilityService.cs(29)` | 'Dictionary<string, NuGetFramework?>' needed no change after all |
| `src\BaGet.Core\Upstream\Clients\DisabledUpstreamClient.cs(32)` | 'System.Threading.Tasks.Task<System.IO.Stream>' is not spelled out anywhere this conversion may edit |
| `src\BaGet.Identity\AccountDeletionService.cs(132)` | could not resolve the type of the value |
| `src\BaGet.Identity\AccountDeletionService.cs(186)` | could not find what the value flows into |
| `src\BaGet.Identity\PackageOwnershipService.cs(174)` | 'Task<IReadOnlyList<Organization>?>' needed no change after all |
| `src\BaGet.Identity\PackageOwnershipService.cs(66)` | 'Task<IReadOnlyList<string>?>' needed no change after all |
| `src\BaGet.Identity\PackageOwnershipService.cs(95)` | 'Task<IReadOnlyList<string>?>' needed no change after all |
| `src\BaGet.Identity\Pages\Admin\DeleteUser.cshtml.cs(122)` | could not resolve the type of the value |
| `src\BaGet.Identity\Pages\Admin\DeleteUser.cshtml.cs(125)` | could not resolve the type of the value |
| `src\BaGet.Identity\Pages\Admin\DeleteUser.cshtml.cs(131)` | could not resolve the type of the value |
| `src\BaGet.Identity\Pages\Admin\DeleteUser.cshtml.cs(134)` | could not resolve the type of the value |
| `src\BaGet.Protocol\Converters\StringOrStringArrayJsonConverter.cs(37)` | the signature is fixed by 'System.Text.Json.Serialization.JsonConverter<System.Collections.Generic.IReadOnlyList<string>>.Read(ref System.Text.Json.Utf8JsonReader, System.Type, System.Text.Json.JsonSerializerOptions)', declared outside the copy |
| `src\BaGet.Protocol\NuGetClient.cs(419)` | 'Task<IReadOnlyList<string>?>?' needed no change after all |
| `src\BaGet.Protocol\NuGetClient.cs(446)` | 'Task<IReadOnlyList<string>?>?' needed no change after all |
| `src\BaGet.Protocol\NuGetClient.cs(468)` | 'Task<IReadOnlyList<string>?>?' needed no change after all |
| `src\BaGet.Protocol\NuGetClient.cs(497)` | 'Task<IReadOnlyList<string>?>?' needed no change after all |
| `src\BaGet.Validation\PackageValidationRunner.cs(55)` | 'Task<IReadOnlyList<string>?>' needed no change after all |
| `tests\BaGet.Core.Tests\ServiceIndex\BaGetServiceIndexTests.cs(34)` | 'Task<string[]?>?' needed no change after all |
| `tests\BaGet.Core.Tests\ServiceIndex\BaGetServiceIndexTests.cs(34)` | 'Task<string[]>' needed no change after all |
| `tests\BaGet.Protocol.Tests\Support\CountingHttpMessageHandler.cs(29)` | 'IReadOnlyList<Uri>' needed no change after all |
| `tests\BaGet.Protocol.Tests\Support\RecordingHttpMessageHandler.cs(34)` | 'IReadOnlyList<Uri>' needed no change after all |
| `tests\BaGet.Tests\AccountsWithoutGlobalKeyTests.cs(109)` | 'Task<IReadOnlyList<string>?>' needed no change after all |
| `tests\BaGet.Tests\BrowserUploadSecurityTests.cs(157)` | 'System.Threading.Tasks.Task<System.IO.Stream>' is not spelled out anywhere this conversion may edit |
| `tests\BaGet.Tests\BrowserUploadTests.cs(439)` | 'Task<IReadOnlyList<string>?>' needed no change after all |
| `tests\BaGet.Tests\Support\TestableHttpSource.cs(16)` | 'Func<Task<HttpHandlerResource>?>' needed no change after all |

### CS8603 — 25

| Where | Why |
|---|---|
| `src\BaGet.Aliyun\AliyunApplicationExtensions.cs(29)` | the value is returned from a lambda, whose type comes from its target |
| `src\BaGet.Aws\AwsApplicationExtensions.cs(25)` | the value is returned from a lambda, whose type comes from its target |
| `src\BaGet.Azure\AzureApplicationExtensions.cs(114)` | the value is returned from a lambda, whose type comes from its target |
| `src\BaGet.Azure\AzureApplicationExtensions.cs(160)` | the value is returned from a lambda, whose type comes from its target |
| `src\BaGet.Azure\AzureApplicationExtensions.cs(167)` | the value is returned from a lambda, whose type comes from its target |
| `src\BaGet.Azure\AzureApplicationExtensions.cs(47)` | the value is returned from a lambda, whose type comes from its target |
| `src\BaGet.Azure\AzureApplicationExtensions.cs(54)` | the value is returned from a lambda, whose type comes from its target |
| `src\BaGet.Azure\AzureApplicationExtensions.cs(55)` | the value is returned from a lambda, whose type comes from its target |
| `src\BaGet.Azure\AzureApplicationExtensions.cs(62)` | the value is returned from a lambda, whose type comes from its target |
| `src\BaGet.Azure\AzureApplicationExtensions.cs(63)` | the value is returned from a lambda, whose type comes from its target |
| `src\BaGet.Azure\Table\TableJson.cs(22)` | 'Deserialize' (T): an unconstrained type parameter |
| `src\BaGet.Core\Entities\Converters\StringArrayToJsonConverter.cs(13)` | the value is returned from a lambda, whose type comes from its target |
| `src\BaGet.Core\Extensions\DependencyInjectionExtensions.cs(156)` | the value is returned from a lambda, whose type comes from its target |
| `src\BaGet.Core\Extensions\DependencyInjectionExtensions.Providers.cs(104)` | the value is returned from a lambda, whose type comes from its target |
| `src\BaGet.Core\Extensions\DependencyInjectionExtensions.Providers.cs(105)` | the value is returned from a lambda, whose type comes from its target |
| `src\BaGet.Core\Extensions\DependencyInjectionExtensions.Providers.cs(82)` | the value is returned from a lambda, whose type comes from its target |
| `src\BaGet.Core\Extensions\DependencyInjectionExtensions.Providers.cs(89)` | the value is returned from a lambda, whose type comes from its target |
| `src\BaGet.Core\Extensions\DependencyInjectionExtensions.Providers.cs(96)` | the value is returned from a lambda, whose type comes from its target |
| `src\BaGet.Core\Extensions\DependencyInjectionExtensions.Providers.cs(97)` | the value is returned from a lambda, whose type comes from its target |
| `src\BaGet.Gcp\GoogleCloudApplicationExtensions.cs(20)` | the value is returned from a lambda, whose type comes from its target |
| `src\BaGet.Protocol\Extensions\HttpClientExtensions.cs(35)` | 'GetFromJsonAsync' (TResult): an unconstrained type parameter |
| `src\BaGet.Protocol\Extensions\HttpClientExtensions.cs(61)` | 'GetFromJsonOrDefaultAsync' (TResult): an unconstrained type parameter |
| `src\BaGet.Protocol\Extensions\HttpClientExtensions.cs(68)` | 'GetFromJsonOrDefaultAsync' (TResult): an unconstrained type parameter |
| `src\BaGet.Web.V2\V2FeedController.cs(234)` | the value is returned from a lambda, whose type comes from its target |
| `tests\BaGet.Protocol.Tests\NuGetClientFactoryTests.cs(103)` | the value is returned from a lambda, whose type comes from its target |

### CS8620 — 22

| Where | Why |
|---|---|
| `src\BaGet.Core\Configuration\SiteOptions.cs(31)` | 'System.Collections.Generic.IEnumerable<string>?' is not spelled out anywhere this conversion may edit |
| `src\BaGet.Identity\ContactRateLimiter.cs(37)` | '(string Sender, string PackageId)' needed no change after all |
| `src\BaGet.Identity\ContactRateLimiter.cs(42)` | 'DateTime' needed no change after all |
| `src\BaGet.Identity\ContactRateLimiter.cs(54)` | '(string Sender, string PackageId)' needed no change after all |
| `src\BaGet.Identity\Pages\Account\Organizations.cshtml.cs(92)` | '(string, bool)' needed no change after all |
| `src\BaGet.Identity\Pages\Account\Reauthenticate.cshtml.cs(115)` | 'System.Collections.Generic.IEnumerable<System.Security.Claims.Claim>' is not spelled out anywhere this conversion may edit |
| `src\BaGet.Identity\Pages\Admin\Users.cshtml.cs(266)` | 'System.Collections.Generic.IReadOnlyDictionary<string, string>' is not spelled out anywhere this conversion may edit |
| `src\BaGet.Identity\Pages\Admin\Users.cshtml.cs(266)` | 'string' needed no change after all |
| `src\BaGet.Identity\Pages\Profiles\Profile.cshtml.cs(80)` | 'IEnumerable<string>' needed no change after all |
| `src\BaGet.Identity\Pages\Profiles\Profile.cshtml.cs(81)` | 'System.Collections.Generic.IReadOnlyDictionary<string, string>' is not spelled out anywhere this conversion may edit |
| `src\BaGet.Identity\Pages\Profiles\Profile.cshtml.cs(81)` | 'string' needed no change after all |
| `src\BaGet.Protocol\NuGetClient.cs(243)` | 'System.Collections.Generic.IEnumerable<BaGet.Protocol.Models.PackageMetadata>' is not spelled out anywhere this conversion may edit |
| `tests\BaGet.Core.Tests\Services\PackageServiceTests.cs(230)` | could not find what the value flows into |
| `tests\BaGet.Core.Tests\Services\PackageServiceTests.cs(297)` | could not find what the value flows into |
| `tests\BaGet.Core.Tests\Services\SymbolIndexingServiceTests.cs(337)` | could not find what the value flows into |
| `tests\BaGet.Core.Tests\Settings\SettingsOverridesStoreTests.cs(72)` | could not resolve the type of the value |
| `tests\BaGet.Core.Tests\Settings\SettingsOverridesStoreTests.cs(81)` | could not resolve the type of the value |
| `tests\BaGet.Core.Tests\Settings\SettingsServiceTests.cs(51)` | 'IReadOnlyDictionary<string, string>' needed no change after all |
| `tests\BaGet.Tests\DatabaseSearchServiceTests.cs(128)` | 'System.Collections.Generic.IEnumerable<string>' is not spelled out anywhere this conversion may edit |
| `tests\BaGet.Tests\PackageFrameworksTests.cs(95)` | 'IEnumerable<string>' needed no change after all |
| `tests\BaGet.Tests\PackageFrameworksTests.cs(95)` | 'IEnumerable<string>?' needed no change after all |
| `tests\BaGet.Tests\Support\TestableHttpSource.cs(37)` | 'Func<Task<HttpHandlerResource>?>' needed no change after all |

### CS8625 — 9

| Where | Why |
|---|---|
| `src\BaGet.Aws\S3StorageOptions.cs(13)` | could not find what the null was being converted to |
| `src\BaGet.Aws\S3StorageOptions.cs(9)` | could not find what the null was being converted to |
| `src\BaGet.Catalog\CatalogDatabase.cs(109)` | 'FromResult' infers its own type arguments, so there is nothing written to annotate |
| `src\BaGet.Core\Configuration\Settings\SettingsForm.cs(63)` | 'System.Collections.Generic.Dictionary<BaGet.Core.SettingDescriptor, object>.this[BaGet.Core.SettingDescriptor]' comes from metadata and would have to be annotated upstream |
| `src\BaGet.Core\Storage\NullStorageService.cs(29)` | 'FromResult' infers its own type arguments, so there is nothing written to annotate |
| `src\BaGet.Core\Storage\NullStorageService.cs(34)` | 'FromResult' infers its own type arguments, so there is nothing written to annotate |
| `src\BaGet.Core\Upstream\Clients\DisabledUpstreamClient.cs(32)` | 'FromResult' infers its own type arguments, so there is nothing written to annotate |
| `tests\BaGet.Protocol.Tests\Converters\PackageDependencyRangeJsonConverterTests.cs(71)` | 'Serialize' infers its own type arguments, so there is nothing written to annotate |
| `tests\BaGet.Tests\BrowserUploadSecurityTests.cs(157)` | 'FromResult' infers its own type arguments, so there is nothing written to annotate |

### CS8621 — 7

| Where | Why |
|---|---|
| `src\BaGet.Core\Extensions\DependencyInjectionExtensions.cs(130)` | 'lambda expression' is not declared in source we can edit |
| `src\BaGet.Core\Extensions\DependencyInjectionExtensions.cs(137)` | 'lambda expression' is not declared in source we can edit |
| `src\BaGet.Core\Indexing\FrameworkCompatibilityService.cs(35)` | 'lambda expression' is not declared in source we can edit |
| `src\BaGet.Identity\AvatarService.cs(54)` | 'lambda expression' is not declared in source we can edit |
| `src\BaGet.Identity\Pages\Account\Packages.cshtml.cs(88)` | 'lambda expression' is not declared in source we can edit |
| `src\BaGet.Identity\Pages\Admin\UserPackages.cshtml.cs(70)` | 'lambda expression' is not declared in source we can edit |
| `tests\BaGet.Tests\AccountsIntegrationTests.cs(195)` | 'lambda expression' is not declared in source we can edit |

### CS8634 — 7

| Where | Why |
|---|---|
| `src\BaGet.Core\Extensions\DependencyInjectionExtensions.cs(130)` | no fixer for this diagnostic |
| `src\BaGet.Core\Extensions\DependencyInjectionExtensions.cs(137)` | no fixer for this diagnostic |
| `src\BaGet\Program.cs(308)` | no fixer for this diagnostic |
| `src\BaGet\Program.cs(309)` | no fixer for this diagnostic |
| `src\BaGet\Program.cs(310)` | no fixer for this diagnostic |
| `src\BaGet\Program.cs(311)` | no fixer for this diagnostic |
| `src\BaGet\Program.cs(312)` | no fixer for this diagnostic |

### CS8714 — 6

| Where | Why |
|---|---|
| `src\BaGet.Core\Indexing\FrameworkCompatibilityService.cs(29)` | no fixer for this diagnostic |
| `src\BaGet.Identity\AvatarService.cs(54)` | no fixer for this diagnostic |
| `src\BaGet.Identity\Pages\Account\Packages.cshtml.cs(86)` | no fixer for this diagnostic |
| `src\BaGet.Identity\Pages\Admin\UserPackages.cshtml.cs(67)` | no fixer for this diagnostic |
| `tests\BaGet.Tests\AccountsIntegrationTests.cs(194)` | no fixer for this diagnostic |
| `tests\BaGet.Tests\AccountsIntegrationTests.cs(283)` | no fixer for this diagnostic |

### CS8601 — 5

| Where | Why |
|---|---|
| `src\BaGet.Core\Configuration\Settings\SettingsService.cs(93)` | 'System.Collections.Generic.Dictionary<string, string>.this[string]' comes from metadata and would have to be annotated upstream |
| `src\BaGet.Identity\SmtpEmailSender.cs(64)` | 'MimeKit.MimeMessage.Subject' comes from metadata and would have to be annotated upstream |
| `src\BaGet.Protocol\Search\RawSearchClient.cs(56)` | 'System.Collections.Generic.Dictionary<string, string>.this[string]' comes from metadata and would have to be annotated upstream |
| `src\BaGet.Protocol\Search\RawSearchClient.cs(57)` | 'System.Collections.Generic.Dictionary<string, string>.this[string]' comes from metadata and would have to be annotated upstream |
| `tests\BaGet.Core.Tests\Settings\SettingsServiceTests.cs(86)` | 'System.Collections.Generic.Dictionary<BaGet.Core.SettingDescriptor, object>.this[BaGet.Core.SettingDescriptor]' comes from metadata and would have to be annotated upstream |

### CS8633 — 5

| Where | Why |
|---|---|
| `tests\BaGet.Core.Tests\Support\RecordingLogger.cs(21)` | no fixer for this diagnostic |
| `tests\BaGet.Tests\AdminPackageTests.cs(440)` | no fixer for this diagnostic |
| `tests\BaGet.Tests\EmailTests.cs(233)` | no fixer for this diagnostic |
| `tests\BaGet.Tests\HeaderLinksTests.cs(181)` | no fixer for this diagnostic |
| `tests\BaGet.Tests\Support\XunitLogger.cs(20)` | no fixer for this diagnostic |

### CS8629 — 3

| Where | Why |
|---|---|
| `src\BaGet.Azure\Search\AzureSearchService.cs(107)` | no fixer for this diagnostic |
| `src\BaGet.Azure\Search\AzureSearchService.cs(141)` | no fixer for this diagnostic |
| `src\BaGet.Azure\Search\AzureSearchService.cs(254)` | no fixer for this diagnostic |

## Rolled back

These edits were applied and then undone, because the compiler was unhappier afterwards.

| File | Diagnostic | Change |
|---|---|---|
| `CatalogWriter.cs` | CS8602 | _source?.Read |
| `CatalogWriter.cs` | CS8602 | _source?.ReadAsync |
| `SettingsService.cs` | CS8602 | result?.Failed (and 1 later in the member) |
| `StringArrayToJsonConverter.cs` | CS8620 | StringArrayToJsonConverter -> ValueConverter<string[]?, string>? |
| `UriToStringConverter.cs` | CS8620 | UriToStringConverter -> ValueConverter<Uri?, string>? |
| `DependencyInjectionExtensions.Providers.cs` | CS8602 | config[DatabaseTypeKey]?.Equals |
| `DependencyInjectionExtensions.Providers.cs` | CS8602 | config[SearchTypeKey]?.Equals |
| `DependencyInjectionExtensions.Providers.cs` | CS8602 | config[StorageTypeKey]?.Equals |
| `DatabaseSearchService.cs` | CS8602 | o.PackageId?.ToLower |
| `DatabaseSearchService.cs` | CS8602 | p.Id?.ToLower |
| `DatabaseSearchService.cs` | CS8604 | p.Dependencies?.Any |
| `DatabaseSearchService.cs` | CS8602 | p.Id?.ToLower |
| `DatabaseSearchService.cs` | CS8604 | p.PackageTypes?.Any |
| `DatabaseSearchService.cs` | CS8604 | p.TargetFrameworks?.Any |
| `StatisticsService.cs` | CS8602 | p.Id?.ToLower |
| `PackageDownloadsJsonSource.cs` | CS8602 | token[1]?.ToObject<long> |
| `AccountDeletionService.cs` | CS8602 | user?.UserName (and 1 later in the member) |
| `AccountDeletionService.cs` | CS8602 | o.OwnerName?.ToLower |
| `AccountDeletionService.cs` | CS8602 | o.PackageId?.ToLower |
| `AccountDeletionService.cs` | CS8602 | o.OwnerName?.ToLower |
| `AccountDeletionService.cs` | CS8602 | user?.Id (and 3 later in the member) |
| `AccountDeletionService.cs` | CS8602 | o.OwnerName?.ToLower |
| `AccountDeletionService.cs` | CS8602 | m.Organization?.Name |
| `AccountDeletionService.cs` | CS8602 | o.OwnerName?.ToLower |
| `AvatarService.cs` | CS8602 | u.UserName?.ToLower |
| `PackageOwnershipService.cs` | CS8602 | o.PackageId?.ToLower |
| `PackageOwnershipService.cs` | CS8602 | m.Organization?.Name |
| `PackageOwnershipService.cs` | CS8602 | o.PackageId?.ToLower |
| `PackageOwnershipService.cs` | CS8602 | o.OwnerName?.ToLower |
| `PackageOwnershipService.cs` | CS8602 | o.OwnerName?.ToLower |
| `PackageOwnershipService.cs` | CS8602 | o.PackageId?.ToLower |
| `PackageOwnershipService.cs` | CS8602 | o.Name?.ToLower |
| `PackageOwnershipService.cs` | CS8602 | o.PackageId?.ToLower |
| `PackageOwnershipService.cs` | CS8602 | o.PackageId?.ToLower |
| `PackageOwnershipService.cs` | CS8602 | o.PackageId?.ToLower |
| `PackageOwnershipService.cs` | CS8602 | o?.Name |
| `PackageOwnershipService.cs` | CS8602 | m.Organization.Name?.ToLower |
| `PackageOwnershipService.cs` | CS8602 | o.Name?.ToLower |
| `PackageOwnershipService.cs` | CS8602 | m.Organization.Name?.ToLower |
| `Packages.cshtml.cs` | CS8602 | p.Id?.ToLower |
| `TwoFactor.cshtml.cs` | CS8602 | key?.Length (and 1 later in the member) |
| `Packages.cshtml.cs` | CS8604 | context.PackageOwners?.Any |
| `Packages.cshtml.cs` | CS8602 | o.PackageId?.ToLower |
| `Packages.cshtml.cs` | CS8602 | p.Id?.ToLower (and 1 later in the member) |
| `Packages.cshtml.cs` | CS8602 | p.Id?.ToLower |
| `NuGetClient.cs` | CS8604 | versions.Versions?.Any |
| `SettingsOverridesStoreTests.cs` | CS8620 | Dictionary<string, string> -> IEnumerable<KeyValuePair<string, string?>>? |
| `SettingsOverridesStoreTests.cs` | CS8620 | Dictionary<string, string> -> IEnumerable<KeyValuePair<string, string?>>? |
| `SettingsOverridesStoreTests.cs` | CS8620 | Dictionary<string, string> -> IEnumerable<KeyValuePair<string, string?>>? |
| `SettingsServiceTests.cs` | CS8620 | Dictionary<string, string> -> IEnumerable<KeyValuePair<string, string?>>? |
| `SettingsServiceTests.cs` | CS8620 | Dictionary<string, string> -> IEnumerable<KeyValuePair<string, string?>>? |
| `SettingsServiceTests.cs` | CS8620 | Dictionary<string, string> -> IEnumerable<KeyValuePair<string, string?>>? |
| `AllowedHostsIntegrationTests.cs` | CS8620 | Dictionary<string, string> -> IEnumerable<KeyValuePair<string, string?>>? |
| `AllowedHostsIntegrationTests.cs` | CS8620 | Dictionary<string, string> -> IEnumerable<KeyValuePair<string, string?>>? |
| `EmailTests.cs` | CS8620 | Dictionary<string, string> -> IEnumerable<KeyValuePair<string, string?>>? |
| `EmailTests.cs` | CS8620 | Dictionary<string, string> -> IEnumerable<KeyValuePair<string, string?>>? |

## Silenced with #pragma

Warnings still reported at the end, silenced around the statement that raises them. Each is a place the conversion could not make null-safe: worth reading before shipping.

- `src\BaGet.Aliyun\AliyunApplicationExtensions.cs(29): CS8603`
- `src\BaGet.Aws\AwsApplicationExtensions.cs(25): CS8603`
- `src\BaGet.Azure\AzureApplicationExtensions.cs(47): CS8603`
- `src\BaGet.Azure\AzureApplicationExtensions.cs(54-55): CS8603`
- `src\BaGet.Azure\AzureApplicationExtensions.cs(62-63): CS8603`
- `src\BaGet.Azure\AzureApplicationExtensions.cs(114): CS8603`
- `src\BaGet.Azure\AzureApplicationExtensions.cs(160): CS8603`
- `src\BaGet.Azure\AzureApplicationExtensions.cs(167): CS8603`
- `src\BaGet.Azure\Search\AzureSearchService.cs(66): CS8602`
- `src\BaGet.Azure\Search\AzureSearchService.cs(83-85): CS8604`
- `src\BaGet.Azure\Search\AzureSearchService.cs(105-110): CS8629`
- `src\BaGet.Azure\Search\AzureSearchService.cs(139-144): CS8629`
- `src\BaGet.Azure\Search\AzureSearchService.cs(252-256): CS8629`
- `src\BaGet.Azure\Search\IndexActionBuilder.cs(68-73): CS8604`
- `src\BaGet.Azure\Search\IndexActionBuilder.cs(96-97): CS8604`
- `src\BaGet.Azure\Search\IndexActionBuilder.cs(79): CS8602`
- `src\BaGet.Azure\Search\IndexActionBuilder.cs(97): CS8602`
- `src\BaGet.Azure\Table\TableJson.cs(22): CS8604`
- `src\BaGet.Azure\Table\TableJson.cs(22): CS8603`
- `src\BaGet.Azure\Table\TableOperationBuilder.cs(19-51): CS8602`
- `src\BaGet.Azure\Table\TableOperationBuilder.cs(72): CS8602`
- `src\BaGet.Azure\Table\TableOperationBuilder.cs(83): CS8602`
- `src\BaGet.Azure\Table\TableOperationBuilder.cs(95): CS8602`
- `src\BaGet.Azure\Table\TableOperationBuilder.cs(105): CS8604`
- `src\BaGet.Azure\Table\TablePackageDatabase.cs(93): CS8602`
- `src\BaGet.Azure\Table\TablePackageDatabase.cs(109-112): CS8602`
- `src\BaGet.Azure\Table\TablePackageDatabase.cs(121): CS8602`
- `src\BaGet.Azure\Table\TablePackageDatabase.cs(157-159): CS8602`
- `src\BaGet.Catalog\CatalogDatabase.cs(108-109): CS8625`
- `src\BaGet.Catalog\CatalogDatabase.cs(108-109): CS8619`
- `src\BaGet.Catalog\CatalogDatabase.cs(28-31): CS8604`
- `src\BaGet.Catalog\CatalogDatabase.cs(45-48): CS8604`
- `src\BaGet.Catalog\CatalogDatabase.cs(55-60): CS8604`
- `src\BaGet.Catalog\CatalogDatabase.cs(72-74): CS8604`
- `src\BaGet.Catalog\CatalogRebuilder.cs(62-65): CS8604`
- `src\BaGet.Catalog\CatalogRebuilder.cs(79-82): CS8604`
- `src\BaGet.Catalog\CatalogRebuilder.cs(89-92): CS8604`
- `src\BaGet.Catalog\CatalogRebuilder.cs(105-106): CS8604`
- `src\BaGet.Catalog\CatalogRebuilder.cs(105-106): CS8619`
- `src\BaGet.Catalog\CatalogWriter.cs(243): CS8602`
- `src\BaGet.Catalog\CatalogWriter.cs(250): CS8602`
- `src\BaGet.Core\Configuration\Settings\SettingsForm.cs(63): CS8625`
- `src\BaGet.Core\Configuration\Settings\SettingsForm.cs(123): CS8619`
- `src\BaGet.Core\Configuration\Settings\SettingsOverridesStore.cs(102): CS8619`
- `src\BaGet.Core\Configuration\Settings\SettingsOverridesStore.cs(137): CS8604`
- `src\BaGet.Core\Configuration\Settings\SettingsService.cs(93): CS8601`
- `src\BaGet.Core\Configuration\Settings\SettingsService.cs(446): CS8602`
- `src\BaGet.Core\Configuration\Settings\XmlDocumentation.cs(44): CS8604`
- `src\BaGet.Core\Configuration\SiteOptions.cs(26-31): CS8620`
- `src\BaGet.Core\Entities\AbstractContext.cs(111-114): CS8620`
- `src\BaGet.Core\Entities\AbstractContext.cs(116-118): CS8620`
- `src\BaGet.Core\Entities\AbstractContext.cs(120-122): CS8620`
- `src\BaGet.Core\Entities\AbstractContext.cs(124-126): CS8620`
- `src\BaGet.Core\Entities\AbstractContext.cs(128-130): CS8620`
- `src\BaGet.Core\Entities\AbstractContext.cs(132-135): CS8620`
- `src\BaGet.Core\Entities\AbstractContext.cs(172-176): CS8620`
- `src\BaGet.Core\Entities\AbstractContext.cs(262-266): CS8620`
- `src\BaGet.Core\Entities\Converters\StringArrayToJsonConverter.cs(10-13): CS8603`
- `src\BaGet.Core\Entities\Package.cs(20-23): CS8604`
- `src\BaGet.Core\Extensions\DependencyInjectionExtensions.cs(130): CS8634`
- `src\BaGet.Core\Extensions\DependencyInjectionExtensions.cs(137): CS8634`
- `src\BaGet.Core\Extensions\DependencyInjectionExtensions.cs(130): CS8621`
- `src\BaGet.Core\Extensions\DependencyInjectionExtensions.cs(137): CS8621`
- `src\BaGet.Core\Extensions\DependencyInjectionExtensions.cs(156): CS8603`
- `src\BaGet.Core\Extensions\DependencyInjectionExtensions.Providers.cs(44): CS8602`
- `src\BaGet.Core\Extensions\DependencyInjectionExtensions.Providers.cs(55): CS8602`
- `src\BaGet.Core\Extensions\DependencyInjectionExtensions.Providers.cs(66): CS8602`
- `src\BaGet.Core\Extensions\DependencyInjectionExtensions.Providers.cs(82): CS8603`
- `src\BaGet.Core\Extensions\DependencyInjectionExtensions.Providers.cs(89): CS8603`
- `src\BaGet.Core\Extensions\DependencyInjectionExtensions.Providers.cs(96-97): CS8603`
- `src\BaGet.Core\Extensions\DependencyInjectionExtensions.Providers.cs(104-105): CS8603`
- `src\BaGet.Core\Extensions\PackageArchiveReaderExtensions.cs(137): CS8619`
- `src\BaGet.Core\Extensions\PackageArchiveReaderExtensions.cs(142): CS8619`
- `src\BaGet.Core\Indexing\FrameworkCompatibilityService.cs(29-35): CS8714`
- `src\BaGet.Core\Indexing\FrameworkCompatibilityService.cs(29-35): CS8621`
- `src\BaGet.Core\Indexing\FrameworkCompatibilityService.cs(49): CS8621`
- `src\BaGet.Core\Indexing\FrameworkCompatibilityService.cs(29-35): CS8619`
- `src\BaGet.Core\Indexing\FrameworkCompatibilityService.cs(49): CS8604`
- `src\BaGet.Core\Indexing\FrameworkCompatibilityService.cs(68): CS8604`
- `src\BaGet.Core\Metadata\RegistrationBuilder.cs(61-96): CS8604`
- `src\BaGet.Core\Metadata\RegistrationBuilder.cs(146-164): CS8604`
- `src\BaGet.Core\PackageDatabase.cs(39-42): CS8604`
- `src\BaGet.Core\PackageDatabase.cs(47-51): CS8604`
- `src\BaGet.Core\PackageDatabase.cs(56-63): CS8604`
- `src\BaGet.Core\PackageDatabase.cs(79-86): CS8604`
- `src\BaGet.Core\PackageDatabase.cs(113-121): CS8604`
- `src\BaGet.Core\PackageDatabase.cs(130): CS8604`
- `src\BaGet.Core\PackageDatabase.cs(138): CS8604`
- `src\BaGet.Core\PackageDatabase.cs(206): CS8604`
- `src\BaGet.Core\PackageDatabase.cs(216-220): CS8604`
- `src\BaGet.Core\PackageDatabase.cs(228-234): CS8604`
- `src\BaGet.Core\PackageDatabase.cs(243-246): CS8604`
- `src\BaGet.Core\Search\DatabaseSearchService.cs(73-76): CS8604`
- `src\BaGet.Core\Search\DatabaseSearchService.cs(78): CS8604`
- `src\BaGet.Core\Search\DatabaseSearchService.cs(80-84): CS8604`
- `src\BaGet.Core\Search\DatabaseSearchService.cs(102-105): CS8604`
- `src\BaGet.Core\Search\DatabaseSearchService.cs(140-146): CS8604`
- `src\BaGet.Core\Search\DatabaseSearchService.cs(156-158): CS8604`
- `src\BaGet.Core\Search\DatabaseSearchService.cs(167-169): CS8604`
- `src\BaGet.Core\Search\DatabaseSearchService.cs(177-190): CS8604`
- `src\BaGet.Core\Search\DatabaseSearchService.cs(204-207): CS8604`
- `src\BaGet.Core\Search\DatabaseSearchService.cs(210-213): CS8604`
- `src\BaGet.Core\Search\DatabaseSearchService.cs(216-218): CS8604`
- `src\BaGet.Core\Search\DatabaseSearchService.cs(232-235): CS8604`
- `src\BaGet.Core\Search\DatabaseSearchService.cs(269): CS8604`
- `src\BaGet.Core\Search\DatabaseSearchService.cs(274): CS8604`
- `src\BaGet.Core\Search\DatabaseSearchService.cs(102-105): CS8602`
- `src\BaGet.Core\Search\DatabaseSearchService.cs(108): CS8602`
- `src\BaGet.Core\Search\DatabaseSearchService.cs(156-158): CS8602`
- `src\BaGet.Core\Search\DatabaseSearchService.cs(247): CS8602`
- `src\BaGet.Core\Staging\StoragePackageStagingService.cs(293-294): CS8604`
- `src\BaGet.Core\Statistics\StatisticsService.cs(67-73): CS8604`
- `src\BaGet.Core\Statistics\StatisticsService.cs(115-116): CS8604`
- `src\BaGet.Core\Statistics\StatisticsService.cs(118-122): CS8604`
- `src\BaGet.Core\Statistics\StatisticsService.cs(124): CS8604`
- `src\BaGet.Core\Statistics\StatisticsService.cs(118-122): CS8602`
- `src\BaGet.Core\Storage\FileStorageService.cs(26): CS8604`
- `src\BaGet.Core\Storage\FileStorageService.cs(64): CS8604`
- `src\BaGet.Core\Storage\NullStorageService.cs(29): CS8625`
- `src\BaGet.Core\Storage\NullStorageService.cs(34): CS8625`
- `src\BaGet.Core\Storage\PackageStorageService.cs(217-221): CS8604`
- `src\BaGet.Core\Storage\PackageStorageService.cs(226-230): CS8604`
- `src\BaGet.Core\Storage\PackageStorageService.cs(235-239): CS8604`
- `src\BaGet.Core\Storage\PackageStorageService.cs(244-248): CS8604`
- `src\BaGet.Core\Storage\SymbolStorageService.cs(54): CS8604`
- `src\BaGet.Core\Upstream\Clients\DisabledUpstreamClient.cs(32): CS8625`
- `src\BaGet.Core\Upstream\Clients\DisabledUpstreamClient.cs(32): CS8619`
- `src\BaGet.Core\Upstream\Clients\V2UpstreamClient.cs(197-202): CS8603`
- `src\BaGet.Core\Upstream\Clients\V3UpstreamClient.cs(149-151): CS8604`
- `src\BaGet.Core\Upstream\Clients\V3UpstreamClient.cs(171-176): CS8603`
- `src\BaGet.Core\Upstream\DownloadsImporter.cs(32): CS8602`
- `src\BaGet.Core\Upstream\DownloadsImporter.cs(44-45): CS8604`
- `src\BaGet.Core\Upstream\DownloadsImporter.cs(59-64): CS8604`
- `src\BaGet.Core\Upstream\PackageDownloadsJsonSource.cs(53): CS8604`
- `src\BaGet.Core\Upstream\PackageDownloadsJsonSource.cs(71): CS8604`
- `src\BaGet.Core\Upstream\PackageDownloadsJsonSource.cs(72): CS8602`
- `src\BaGet.Gcp\GoogleCloudApplicationExtensions.cs(20): CS8603`
- `src\BaGet.Identity\AccountAuthenticationService.cs(52): CS8604`
- `src\BaGet.Identity\AccountDeletionService.cs(91): CS8602`
- `src\BaGet.Identity\AccountDeletionService.cs(93-97): CS8602`
- `src\BaGet.Identity\AccountDeletionService.cs(103): CS8602`
- `src\BaGet.Identity\AccountDeletionService.cs(112-114): CS8602`
- `src\BaGet.Identity\AccountDeletionService.cs(210-217): CS8602`
- `src\BaGet.Identity\AccountDeletionService.cs(225-227): CS8602`
- `src\BaGet.Identity\AccountDeletionService.cs(249): CS8602`
- `src\BaGet.Identity\AccountDeletionService.cs(267): CS8602`
- `src\BaGet.Identity\AccountDeletionService.cs(271): CS8602`
- `src\BaGet.Identity\AccountDeletionService.cs(287-288): CS8602`
- `src\BaGet.Identity\AccountDeletionService.cs(292): CS8602`
- `src\BaGet.Identity\AccountDeletionService.cs(328-332): CS8602`
- `src\BaGet.Identity\AccountDeletionService.cs(337-341): CS8602`
- `src\BaGet.Identity\AccountDeletionService.cs(357): CS8602`
- `src\BaGet.Identity\AccountDeletionService.cs(93-97): CS8604`
- `src\BaGet.Identity\AccountDeletionService.cs(186): CS8604`
- `src\BaGet.Identity\AccountDeletionService.cs(210-217): CS8604`
- `src\BaGet.Identity\AccountDeletionService.cs(231-237): CS8604`
- `src\BaGet.Identity\AccountDeletionService.cs(276-278): CS8604`
- `src\BaGet.Identity\AccountDeletionService.cs(304): CS8604`
- `src\BaGet.Identity\AccountDeletionService.cs(328-332): CS8604`
- `src\BaGet.Identity\AccountDeletionService.cs(132): CS8619`
- `src\BaGet.Identity\AccountDeletionService.cs(186): CS8619`
- `src\BaGet.Identity\AccountRecoveryService.cs(34): CS8604`
- `src\BaGet.Identity\ApiKeyService.cs(72-76): CS8604`
- `src\BaGet.Identity\ApiKeyService.cs(101-103): CS8604`
- `src\BaGet.Identity\ApiKeyService.cs(84): CS8602`
- `src\BaGet.Identity\AvatarService.cs(49-53): CS8602`
- `src\BaGet.Identity\AvatarService.cs(54): CS8714`
- `src\BaGet.Identity\AvatarService.cs(54): CS8621`
- `src\BaGet.Identity\AvatarService.cs(59): CS8604`
- `src\BaGet.Identity\ContactRateLimiter.cs(37): CS8620`
- `src\BaGet.Identity\ContactRateLimiter.cs(42): CS8620`
- `src\BaGet.Identity\ContactRateLimiter.cs(54): CS8620`
- `src\BaGet.Identity\PackageOwnershipService.cs(48-52): CS8604`
- `src\BaGet.Identity\PackageOwnershipService.cs(60-64): CS8604`
- `src\BaGet.Identity\PackageOwnershipService.cs(71-73): CS8604`
- `src\BaGet.Identity\PackageOwnershipService.cs(76-77): CS8604`
- `src\BaGet.Identity\PackageOwnershipService.cs(85-87): CS8604`
- `src\BaGet.Identity\PackageOwnershipService.cs(89-93): CS8604`
- `src\BaGet.Identity\PackageOwnershipService.cs(133-135): CS8604`
- `src\BaGet.Identity\PackageOwnershipService.cs(153-155): CS8604`
- `src\BaGet.Identity\PackageOwnershipService.cs(174-179): CS8604`
- `src\BaGet.Identity\PackageOwnershipService.cs(208-211): CS8604`
- `src\BaGet.Identity\PackageOwnershipService.cs(247-249): CS8604`
- `src\BaGet.Identity\PackageOwnershipService.cs(48-52): CS8602`
- `src\BaGet.Identity\PackageOwnershipService.cs(60-64): CS8602`
- `src\BaGet.Identity\PackageOwnershipService.cs(76-77): CS8602`
- `src\BaGet.Identity\PackageOwnershipService.cs(89-93): CS8602`
- `src\BaGet.Identity\PackageOwnershipService.cs(102): CS8602`
- `src\BaGet.Identity\PackageOwnershipService.cs(110-111): CS8602`
- `src\BaGet.Identity\PackageOwnershipService.cs(133-135): CS8602`
- `src\BaGet.Identity\PackageOwnershipService.cs(153-155): CS8602`
- `src\BaGet.Identity\PackageOwnershipService.cs(169): CS8602`
- `src\BaGet.Identity\PackageOwnershipService.cs(174-179): CS8602`
- `src\BaGet.Identity\PackageOwnershipService.cs(208-211): CS8602`
- `src\BaGet.Identity\PackageOwnershipService.cs(224): CS8602`
- `src\BaGet.Identity\PackageOwnershipService.cs(247-249): CS8602`
- `src\BaGet.Identity\PackageOwnershipService.cs(66): CS8619`
- `src\BaGet.Identity\PackageOwnershipService.cs(95): CS8619`
- `src\BaGet.Identity\PackageOwnershipService.cs(174-179): CS8619`
- `src\BaGet.Identity\Pages\Account\AccountPageModel.cs(101): CS8604`
- `src\BaGet.Identity\Pages\Account\AccountPageModel.cs(148-151): CS8604`
- `src\BaGet.Identity\Pages\Account\ForgotPassword.cshtml.cs(38): CS8604`
- `src\BaGet.Identity\Pages\Account\Login.cshtml.cs(51): CS8604`
- `src\BaGet.Identity\Pages\Account\LoginWith2fa.cshtml.cs(39): CS8604`
- `src\BaGet.Identity\Pages\Account\LoginWithRecoveryCode.cshtml.cs(35): CS8604`
- `src\BaGet.Identity\Pages\Account\Organizations.cshtml.cs(85): CS8602`
- `src\BaGet.Identity\Pages\Account\Organizations.cshtml.cs(91): CS8604`
- `src\BaGet.Identity\Pages\Account\Organizations.cshtml.cs(92): CS8620`
- `src\BaGet.Identity\Pages\Account\Packages.cshtml.cs(62): CS8604`
- `src\BaGet.Identity\Pages\Account\Packages.cshtml.cs(66-77): CS8604`
- `src\BaGet.Identity\Pages\Account\Packages.cshtml.cs(95-97): CS8604`
- `src\BaGet.Identity\Pages\Account\Packages.cshtml.cs(99-108): CS8604`
- `src\BaGet.Identity\Pages\Account\Packages.cshtml.cs(66-77): CS8602`
- `src\BaGet.Identity\Pages\Account\Packages.cshtml.cs(90): CS8602`
- `src\BaGet.Identity\Pages\Account\Packages.cshtml.cs(86-88): CS8714`
- `src\BaGet.Identity\Pages\Account\Packages.cshtml.cs(86-88): CS8621`
- `src\BaGet.Identity\Pages\Account\Profile.cshtml.cs(12): CS8604`
- `src\BaGet.Identity\Pages\Account\Reauthenticate.cshtml.cs(115): CS8620`
- `src\BaGet.Identity\Pages\Account\Register.cshtml.cs(75): CS8604`
- `src\BaGet.Identity\Pages\Account\Register.cshtml.cs(94): CS8604`
- `src\BaGet.Identity\Pages\Account\ResetPassword.cshtml.cs(46): CS8604`
- `src\BaGet.Identity\Pages\Account\ResetPassword.cshtml.cs(54): CS8604`
- `src\BaGet.Identity\Pages\Account\TwoFactor.cshtml.cs(76): CS8604`
- `src\BaGet.Identity\Pages\Account\TwoFactor.cshtml.cs(173-175): CS8604`
- `src\BaGet.Identity\Pages\Account\TwoFactor.cshtml.cs(164): CS8602`
- `src\BaGet.Identity\Pages\Admin\DeleteUser.cshtml.cs(122): CS8619`
- `src\BaGet.Identity\Pages\Admin\DeleteUser.cshtml.cs(125): CS8619`
- `src\BaGet.Identity\Pages\Admin\DeleteUser.cshtml.cs(131): CS8619`
- `src\BaGet.Identity\Pages\Admin\DeleteUser.cshtml.cs(134): CS8619`
- `src\BaGet.Identity\Pages\Admin\Packages.cshtml.cs(38-42): CS8604`
- `src\BaGet.Identity\Pages\Admin\Packages.cshtml.cs(38-42): CS8602`
- `src\BaGet.Identity\Pages\Admin\UserPackages.cshtml.cs(55): CS8602`
- `src\BaGet.Identity\Pages\Admin\UserPackages.cshtml.cs(62-66): CS8602`
- `src\BaGet.Identity\Pages\Admin\UserPackages.cshtml.cs(62-66): CS8604`
- `src\BaGet.Identity\Pages\Admin\UserPackages.cshtml.cs(67-72): CS8714`
- `src\BaGet.Identity\Pages\Admin\UserPackages.cshtml.cs(67-72): CS8621`
- `src\BaGet.Identity\Pages\Admin\Users.cshtml.cs(84): CS8602`
- `src\BaGet.Identity\Pages\Admin\Users.cshtml.cs(248): CS8602`
- `src\BaGet.Identity\Pages\Admin\Users.cshtml.cs(265-267): CS8620`
- `src\BaGet.Identity\Pages\Packages\ContactOwners.cshtml.cs(98): CS8602`
- `src\BaGet.Identity\Pages\Packages\ContactOwners.cshtml.cs(105): CS8602`
- `src\BaGet.Identity\Pages\Packages\ContactOwners.cshtml.cs(125-131): CS8602`
- `src\BaGet.Identity\Pages\Packages\ContactOwners.cshtml.cs(165): CS8602`
- `src\BaGet.Identity\Pages\Packages\ContactOwners.cshtml.cs(167-171): CS8602`
- `src\BaGet.Identity\Pages\Packages\ContactOwners.cshtml.cs(173-177): CS8602`
- `src\BaGet.Identity\Pages\Packages\ContactOwners.cshtml.cs(186-190): CS8602`
- `src\BaGet.Identity\Pages\Packages\ContactOwners.cshtml.cs(173-177): CS8604`
- `src\BaGet.Identity\Pages\Packages\Manage.cshtml.cs(104): CS8602`
- `src\BaGet.Identity\Pages\Packages\Manage.cshtml.cs(222): CS8602`
- `src\BaGet.Identity\Pages\Packages\Manage.cshtml.cs(250-253): CS8602`
- `src\BaGet.Identity\Pages\Packages\Manage.cshtml.cs(256-260): CS8602`
- `src\BaGet.Identity\Pages\Packages\Manage.cshtml.cs(308-309): CS8602`
- `src\BaGet.Identity\Pages\Packages\Manage.cshtml.cs(368-373): CS8602`
- `src\BaGet.Identity\Pages\Packages\Manage.cshtml.cs(309): CS8604`
- `src\BaGet.Identity\Pages\Packages\Manage.cshtml.cs(369-373): CS8604`
- `src\BaGet.Identity\Pages\Profiles\Profile.cshtml.cs(38): CS8604`
- `src\BaGet.Identity\Pages\Profiles\Profile.cshtml.cs(48-52): CS8604`
- `src\BaGet.Identity\Pages\Profiles\Profile.cshtml.cs(58): CS8604`
- `src\BaGet.Identity\Pages\Profiles\Profile.cshtml.cs(72-76): CS8604`
- `src\BaGet.Identity\Pages\Profiles\Profile.cshtml.cs(84-90): CS8604`
- `src\BaGet.Identity\Pages\Profiles\Profile.cshtml.cs(47-52): CS8602`
- `src\BaGet.Identity\Pages\Profiles\Profile.cshtml.cs(58): CS8602`
- `src\BaGet.Identity\Pages\Profiles\Profile.cshtml.cs(72-76): CS8602`
- `src\BaGet.Identity\Pages\Profiles\Profile.cshtml.cs(83-90): CS8602`
- `src\BaGet.Identity\Pages\Profiles\Profile.cshtml.cs(81): CS8620`
- `src\BaGet.Identity\SmtpEmailSender.cs(57): CS8604`
- `src\BaGet.Identity\SmtpEmailSender.cs(83): CS8604`
- `src\BaGet.Identity\SmtpEmailSender.cs(106): CS8604`
- `src\BaGet.Identity\SmtpEmailSender.cs(109): CS8604`
- `src\BaGet.Identity\SmtpEmailSender.cs(64): CS8601`
- `src\BaGet.Protocol\Catalog\CatalogClient.cs(23): CS8602`
- `src\BaGet.Protocol\Catalog\CatalogClient.cs(30): CS8602`
- `src\BaGet.Protocol\Catalog\CatalogClient.cs(37): CS8602`
- `src\BaGet.Protocol\Catalog\CatalogClient.cs(44): CS8602`
- `src\BaGet.Protocol\Converters\StringOrStringArrayJsonConverter.cs(18): CS8604`
- `src\BaGet.Protocol\Converters\StringOrStringArrayJsonConverter.cs(37): CS8619`
- `src\BaGet.Protocol\EndpointFailover.cs(74): CS8604`
- `src\BaGet.Protocol\EndpointFailover.cs(77-79): CS8604`
- `src\BaGet.Protocol\Extensions\CatalogModelExtensions.cs(30-33): CS8604`
- `src\BaGet.Protocol\Extensions\CatalogModelExtensions.cs(77-80): CS8604`
- `src\BaGet.Protocol\Extensions\CatalogModelExtensions.cs(102): CS8604`
- `src\BaGet.Protocol\Extensions\CatalogModelExtensions.cs(132): CS8604`
- `src\BaGet.Protocol\Extensions\HttpClientExtensions.cs(35): CS8603`
- `src\BaGet.Protocol\Extensions\HttpClientExtensions.cs(61): CS8603`
- `src\BaGet.Protocol\Extensions\HttpClientExtensions.cs(68): CS8603`
- `src\BaGet.Protocol\Extensions\PackageContentModelExtensions.cs(20-23): CS8604`
- `src\BaGet.Protocol\Extensions\PackageMetadataModelExtensions.cs(19): CS8604`
- `src\BaGet.Protocol\Extensions\PackageMetadataModelExtensions.cs(46): CS8604`
- `src\BaGet.Protocol\Extensions\PackageMetadataModelExtensions.cs(56): CS8604`
- `src\BaGet.Protocol\Extensions\SearchModelExtensions.cs(18): CS8604`
- `src\BaGet.Protocol\Extensions\SearchModelExtensions.cs(28): CS8604`
- `src\BaGet.Protocol\NuGetClient.cs(79): CS8602`
- `src\BaGet.Protocol\NuGetClient.cs(96): CS8602`
- `src\BaGet.Protocol\NuGetClient.cs(120): CS8602`
- `src\BaGet.Protocol\NuGetClient.cs(145): CS8602`
- `src\BaGet.Protocol\NuGetClient.cs(196): CS8602`
- `src\BaGet.Protocol\NuGetClient.cs(218): CS8602`
- `src\BaGet.Protocol\NuGetClient.cs(225): CS8602`
- `src\BaGet.Protocol\NuGetClient.cs(233-235): CS8602`
- `src\BaGet.Protocol\NuGetClient.cs(264): CS8602`
- `src\BaGet.Protocol\NuGetClient.cs(271): CS8602`
- `src\BaGet.Protocol\NuGetClient.cs(286-288): CS8602`
- `src\BaGet.Protocol\NuGetClient.cs(322): CS8602`
- `src\BaGet.Protocol\NuGetClient.cs(343-349): CS8602`
- `src\BaGet.Protocol\NuGetClient.cs(368-371): CS8602`
- `src\BaGet.Protocol\NuGetClient.cs(394-400): CS8602`
- `src\BaGet.Protocol\NuGetClient.cs(417): CS8602`
- `src\BaGet.Protocol\NuGetClient.cs(438-444): CS8602`
- `src\BaGet.Protocol\NuGetClient.cs(463-466): CS8602`
- `src\BaGet.Protocol\NuGetClient.cs(489-495): CS8602`
- `src\BaGet.Protocol\NuGetClient.cs(81): CS8604`
- `src\BaGet.Protocol\NuGetClient.cs(243): CS8620`
- `src\BaGet.Protocol\NuGetClient.cs(419): CS8619`
- `src\BaGet.Protocol\NuGetClient.cs(446): CS8619`
- `src\BaGet.Protocol\NuGetClient.cs(468): CS8619`
- `src\BaGet.Protocol\NuGetClient.cs(497): CS8619`
- `src\BaGet.Protocol\NuGetClientFactory.cs(185): CS8602`
- `src\BaGet.Protocol\PackageContent\PackageContentClient.cs(28): CS8602`
- `src\BaGet.Protocol\PackageContent\PackageContentClient.cs(38): CS8602`
- `src\BaGet.Protocol\PackageContent\PackageContentClient.cs(47): CS8602`
- `src\BaGet.Protocol\PackageMetadata\PackageMetadataClient.cs(25): CS8602`
- `src\BaGet.Protocol\PackageMetadata\PackageMetadataClient.cs(34): CS8602`
- `src\BaGet.Protocol\PackageMetadata\PackageMetadataClient.cs(43): CS8602`
- `src\BaGet.Protocol\Search\RawSearchClient.cs(56-57): CS8601`
- `src\BaGet.Validation\PackageValidationRunner.cs(55-59): CS8604`
- `src\BaGet.Validation\PackageValidationRunner.cs(64-66): CS8604`
- `src\BaGet.Validation\PackageValidationRunner.cs(128): CS8604`
- `src\BaGet.Validation\PackageValidationRunner.cs(55-59): CS8619`
- `src\BaGet.Validation\RepositorySignaturesController.cs(114-118): CS8604`
- `src\BaGet.Validation\RepositorySignaturesController.cs(114-118): CS8602`
- `src\BaGet.Validation\RepositorySigner.cs(106-110): CS8604`
- `src\BaGet.Validation\ValidatingPackageSubmissionService.cs(90): CS8604`
- `src\BaGet.Validation\ValidatingPackageSubmissionService.cs(90-91): CS8602`
- `src\BaGet.Validation\ValidationWorker.cs(66): CS8602`
- `src\BaGet.Web.V2\V2FeedController.cs(220): CS8602`
- `src\BaGet.Web.V2\V2FeedController.cs(232-234): CS8603`
- `src\BaGet.Web.V2\V2FeedWriter.cs(169-184): CS8604`
- `src\BaGet.Web\BaGetUrlGenerator.cs(33-36): CS8604`
- `src\BaGet.Web\BaGetUrlGenerator.cs(51-54): CS8604`
- `src\BaGet.Web\BaGetUrlGenerator.cs(59-62): CS8604`
- `src\BaGet.Web\BaGetUrlGenerator.cs(67-70): CS8604`
- `src\BaGet.Web\BaGetUrlGenerator.cs(75-78): CS8604`
- `src\BaGet.Web\BaGetUrlGenerator.cs(83-86): CS8604`
- `src\BaGet.Web\BaGetUrlGenerator.cs(97-104): CS8604`
- `src\BaGet.Web\BaGetUrlGenerator.cs(109-112): CS8604`
- `src\BaGet.Web\BaGetUrlGenerator.cs(120-128): CS8604`
- `src\BaGet.Web\BaGetUrlGenerator.cs(136-144): CS8604`
- `src\BaGet.Web\BaGetUrlGenerator.cs(152-159): CS8604`
- `src\BaGet.Web\BaGetUrlGenerator.cs(164-167): CS8604`
- `src\BaGet.Web\BaGetUrlGenerator.cs(200): CS8604`
- `src\BaGet.Web\BaGetUrlGenerator.cs(83-86): CS8602`
- `src\BaGet.Web\BaGetUrlGenerator.cs(97-104): CS8602`
- `src\BaGet.Web\BaGetUrlGenerator.cs(117): CS8602`
- `src\BaGet.Web\BaGetUrlGenerator.cs(149): CS8602`
- `src\BaGet.Web\BaGetUrlGenerator.cs(220): CS8602`
- `src\BaGet.Web\Controllers\PackageMetadataAdminController.cs(62-68): CS8604`
- `src\BaGet.Web\Controllers\VulnerabilityController.cs(73-94): CS8602`
- `src\BaGet.Web\Extensions\HttpRequestExtensions.cs(33): CS8602`
- `src\BaGet.Web\InstallSnippets.cs(39): CS8602`
- `src\BaGet.Web\NavLinkTagHelper.cs(80): CS8602`
- `src\BaGet.Web\PackageFrameworks.cs(47-53): CS8602`
- `src\BaGet.Web\Pages\Package.cshtml.cs(98): CS8604`
- `src\BaGet.Web\Pages\Package.cshtml.cs(131-132): CS8604`
- `src\BaGet.Web\Pages\Package.cshtml.cs(98-99): CS8602`
- `src\BaGet.Web\Pages\Package.cshtml.cs(127-128): CS8602`
- `src\BaGet.Web\Pages\ReportAbuse.cshtml.cs(79-87): CS8602`
- `src\BaGet\Program.cs(119): CS8604`
- `src\BaGet\Program.cs(248): CS8604`
- `src\BaGet\Program.cs(308-312): CS8634`
- `src\BaGet\Program.cs(369): CS8602`
- `src\BaGet\Program.cs(430): CS8602`
- `tests\BaGet.Core.Tests\Extensions\PackageArchiveReaderExtensionsTests.cs(102): CS8604`
- `tests\BaGet.Core.Tests\Extensions\PackageArchiveReaderExtensionsTests.cs(270): CS8604`
- `tests\BaGet.Core.Tests\Extensions\PackageArchiveReaderExtensionsTests.cs(283): CS8604`
- `tests\BaGet.Core.Tests\Extensions\PackageArchiveReaderExtensionsTests.cs(297): CS8604`
- `tests\BaGet.Core.Tests\Extensions\PackageArchiveReaderExtensionsTests.cs(325): CS8604`
- `tests\BaGet.Core.Tests\Extensions\PackageArchiveReaderExtensionsTests.cs(338): CS8604`
- `tests\BaGet.Core.Tests\Extensions\PackageArchiveReaderExtensionsTests.cs(344): CS8604`
- `tests\BaGet.Core.Tests\Extensions\PackageArchiveReaderExtensionsTests.cs(357): CS8604`
- `tests\BaGet.Core.Tests\Metadata\ModelTests.cs(250-257): CS8602`
- `tests\BaGet.Core.Tests\Search\SearchResponseBuilderTests.cs(62): CS8604`
- `tests\BaGet.Core.Tests\Search\SearchResponseBuilderTests.cs(75): CS8604`
- `tests\BaGet.Core.Tests\Search\SearchResponseBuilderTests.cs(88): CS8604`
- `tests\BaGet.Core.Tests\Search\SearchResponseBuilderTests.cs(102-104): CS8604`
- `tests\BaGet.Core.Tests\Search\SearchResponseBuilderTests.cs(117): CS8604`
- `tests\BaGet.Core.Tests\Search\SearchResponseBuilderTests.cs(130): CS8604`
- `tests\BaGet.Core.Tests\Search\SearchResponseBuilderTests.cs(143): CS8604`
- `tests\BaGet.Core.Tests\Search\SearchResponseBuilderTests.cs(154): CS8604`
- `tests\BaGet.Core.Tests\Search\SearchResponseBuilderTests.cs(174): CS8604`
- `tests\BaGet.Core.Tests\Search\SearchResponseBuilderTests.cs(191): CS8604`
- `tests\BaGet.Core.Tests\Search\SearchResponseBuilderTests.cs(204): CS8604`
- `tests\BaGet.Core.Tests\Search\SearchResponseBuilderTests.cs(213): CS8604`
- `tests\BaGet.Core.Tests\Search\SearchResponseBuilderTests.cs(227): CS8604`
- `tests\BaGet.Core.Tests\ServiceIndex\BaGetServiceIndexTests.cs(34): CS8619`
- `tests\BaGet.Core.Tests\ServiceIndex\BaGetServiceIndexTests.cs(67): CS8602`
- `tests\BaGet.Core.Tests\ServiceIndex\BaGetServiceIndexTests.cs(78): CS8602`
- `tests\BaGet.Core.Tests\ServiceIndex\BaGetServiceIndexTests.cs(84-85): CS8602`
- `tests\BaGet.Core.Tests\ServiceIndex\BaGetServiceIndexTests.cs(67): CS8604`
- `tests\BaGet.Core.Tests\ServiceIndex\BaGetServiceIndexTests.cs(78): CS8604`
- `tests\BaGet.Core.Tests\ServiceIndex\BaGetServiceIndexTests.cs(84-85): CS8604`
- `tests\BaGet.Core.Tests\ServiceIndex\BaGetServiceIndexTests.cs(117): CS8604`
- `tests\BaGet.Core.Tests\Services\FileStorageServiceTests.cs(289): CS8603`
- `tests\BaGet.Core.Tests\Services\FileStorageServiceTests.cs(110): CS8604`
- `tests\BaGet.Core.Tests\Services\FileStorageServiceTests.cs(130): CS8604`
- `tests\BaGet.Core.Tests\Services\FileStorageServiceTests.cs(290): CS8604`
- `tests\BaGet.Core.Tests\Services\FileStorageServiceTests.cs(56): CS8602`
- `tests\BaGet.Core.Tests\Services\FileStorageServiceTests.cs(78): CS8602`
- `tests\BaGet.Core.Tests\Services\FileStorageServiceTests.cs(147): CS8602`
- `tests\BaGet.Core.Tests\Services\FileStorageServiceTests.cs(184): CS8602`
- `tests\BaGet.Core.Tests\Services\FileStorageServiceTests.cs(223): CS8602`
- `tests\BaGet.Core.Tests\Services\PackageDatabaseTests.cs(129-131): CS8604`
- `tests\BaGet.Core.Tests\Services\PackageDatabaseTests.cs(203-204): CS8604`
- `tests\BaGet.Core.Tests\Services\PackageDatabaseTests.cs(341-348): CS8604`
- `tests\BaGet.Core.Tests\Services\PackageDatabaseTests.cs(456-458): CS8604`
- `tests\BaGet.Core.Tests\Services\PackageIndexingServiceTests.cs(356): CS8604`
- `tests\BaGet.Core.Tests\Services\PackageIndexingServiceTests.cs(386): CS8604`
- `tests\BaGet.Core.Tests\Services\PackageStorageServiceTests.cs(509): CS8604`
- `tests\BaGet.Core.Tests\Services\PackageStorageServiceTests.cs(561): CS8604`
- `tests\BaGet.Core.Tests\Services\StoragePackageStagingServiceTests.cs(303): CS8604`
- `tests\BaGet.Core.Tests\Settings\SettingsOverridesStoreTests.cs(63): CS8604`
- `tests\BaGet.Core.Tests\Settings\SettingsOverridesStoreTests.cs(75): CS8604`
- `tests\BaGet.Core.Tests\Settings\SettingsOverridesStoreTests.cs(112-114): CS8604`
- `tests\BaGet.Core.Tests\Settings\SettingsOverridesStoreTests.cs(126): CS8604`
- `tests\BaGet.Core.Tests\Settings\SettingsOverridesStoreTests.cs(134): CS8604`
- `tests\BaGet.Core.Tests\Settings\SettingsOverridesStoreTests.cs(148): CS8604`
- `tests\BaGet.Core.Tests\Settings\SettingsOverridesStoreTests.cs(72): CS8620`
- `tests\BaGet.Core.Tests\Settings\SettingsOverridesStoreTests.cs(81): CS8620`
- `tests\BaGet.Core.Tests\Settings\SettingsOverridesStoreTests.cs(160-162): CS8620`
- `tests\BaGet.Core.Tests\Settings\SettingsOverridesStoreTests.cs(112): CS8602`
- `tests\BaGet.Core.Tests\Settings\SettingsServiceTests.cs(51): CS8620`
- `tests\BaGet.Core.Tests\Settings\SettingsServiceTests.cs(57): CS8620`
- `tests\BaGet.Core.Tests\Settings\SettingsServiceTests.cs(59): CS8620`
- `tests\BaGet.Core.Tests\Settings\SettingsServiceTests.cs(157): CS8620`
- `tests\BaGet.Core.Tests\Settings\SettingsServiceTests.cs(50): CS8604`
- `tests\BaGet.Core.Tests\Settings\SettingsServiceTests.cs(85-86): CS8604`
- `tests\BaGet.Core.Tests\Settings\SettingsServiceTests.cs(225): CS8604`
- `tests\BaGet.Core.Tests\Settings\SettingsServiceTests.cs(85-86): CS8601`
- `tests\BaGet.Core.Tests\Settings\SettingsServiceTests.cs(50): CS8602`
- `tests\BaGet.Core.Tests\Support\RecordingLogger.cs(21): CS8633`
- `tests\BaGet.Protocol.Tests\Converters\PackageDependencyRangeJsonConverterTests.cs(71): CS8625`
- `tests\BaGet.Protocol.Tests\Converters\StringOrStringArrayJsonConverterTests.cs(20): CS8604`
- `tests\BaGet.Protocol.Tests\Converters\StringOrStringArrayJsonConverterTests.cs(35): CS8604`
- `tests\BaGet.Protocol.Tests\Converters\StringOrStringArrayJsonConverterTests.cs(49): CS8604`
- `tests\BaGet.Protocol.Tests\Converters\StringOrStringArrayJsonConverterTests.cs(62): CS8602`
- `tests\BaGet.Protocol.Tests\Converters\StringOrStringArrayJsonConverterTests.cs(116): CS8625`
- `tests\BaGet.Protocol.Tests\NuGetClientFactoryTests.cs(64): CS8604`
- `tests\BaGet.Protocol.Tests\NuGetClientFactoryTests.cs(75): CS8604`
- `tests\BaGet.Protocol.Tests\NuGetClientFactoryTests.cs(87): CS8604`
- `tests\BaGet.Protocol.Tests\NuGetClientFactoryTests.cs(162): CS8604`
- `tests\BaGet.Protocol.Tests\NuGetClientFactoryTests.cs(174): CS8604`
- `tests\BaGet.Protocol.Tests\NuGetClientFactoryTests.cs(98): CS8602`
- `tests\BaGet.Protocol.Tests\NuGetClientFactoryTests.cs(100-105): CS8602`
- `tests\BaGet.Protocol.Tests\NuGetClientFactoryTests.cs(116): CS8602`
- `tests\BaGet.Protocol.Tests\NuGetClientFactoryTests.cs(149): CS8602`
- `tests\BaGet.Protocol.Tests\NuGetClientFactoryTests.cs(100-105): CS8603`
- `tests\BaGet.Protocol.Tests\NuGetClientTests.cs(155): CS8602`
- `tests\BaGet.Protocol.Tests\NuGetClientTests.cs(165): CS8602`
- `tests\BaGet.Protocol.Tests\NuGetClientTests.cs(199): CS8602`
- `tests\BaGet.Protocol.Tests\NuGetClientTests.cs(189): CS8604`
- `tests\BaGet.Protocol.Tests\NuGetClientTests.cs(193): CS8604`
- `tests\BaGet.Protocol.Tests\NuGetClientTests.cs(201): CS8604`
- `tests\BaGet.Protocol.Tests\RawAutocompleteClientTests.cs(25): CS8604`
- `tests\BaGet.Protocol.Tests\RawAutocompleteClientTests.cs(81): CS8604`
- `tests\BaGet.Protocol.Tests\RawCatalogClientTests.cs(26): CS8602`
- `tests\BaGet.Protocol.Tests\RawCatalogClientTests.cs(36): CS8602`
- `tests\BaGet.Protocol.Tests\RawCatalogClientTests.cs(48): CS8602`
- `tests\BaGet.Protocol.Tests\RawCatalogClientTests.cs(61): CS8602`
- `tests\BaGet.Protocol.Tests\RawPackageContentClientTests.cs(26): CS8602`
- `tests\BaGet.Protocol.Tests\RawPackageMetadataClientTests.cs(25): CS8602`
- `tests\BaGet.Protocol.Tests\RawPackageMetadataClientTests.cs(28): CS8602`
- `tests\BaGet.Protocol.Tests\RawPackageMetadataClientTests.cs(34): CS8602`
- `tests\BaGet.Protocol.Tests\RawPackageMetadataClientTests.cs(46): CS8602`
- `tests\BaGet.Protocol.Tests\RawPackageMetadataClientTests.cs(73): CS8602`
- `tests\BaGet.Protocol.Tests\RawPackageMetadataClientTests.cs(87): CS8602`
- `tests\BaGet.Protocol.Tests\RawPackageMetadataClientTests.cs(109): CS8602`
- `tests\BaGet.Protocol.Tests\RawPackageMetadataClientTests.cs(93): CS8604`
- `tests\BaGet.Protocol.Tests\RawPackageMetadataClientTests.cs(109): CS8604`
- `tests\BaGet.Protocol.Tests\RawSearchClientTests.cs(26): CS8604`
- `tests\BaGet.Protocol.Tests\RawSearchClientTests.cs(28): CS8604`
- `tests\BaGet.Protocol.Tests\RawSearchClientTests.cs(31): CS8604`
- `tests\BaGet.Protocol.Tests\RawServiceIndexClientTests.cs(21-22): CS8602`
- `tests\BaGet.Protocol.Tests\SearchFailoverTests.cs(76): CS8604`
- `tests\BaGet.Protocol.Tests\SearchFailoverTests.cs(196): CS8604`
- `tests\BaGet.Protocol.Tests\SearchFailoverTests.cs(265): CS8604`
- `tests\BaGet.Protocol.Tests\Support\CountingHttpMessageHandler.cs(29): CS8619`
- `tests\BaGet.Protocol.Tests\Support\FakeHttpMessageHandler.cs(133): CS8602`
- `tests\BaGet.Protocol.Tests\Support\RecordingHttpMessageHandler.cs(34): CS8619`
- `tests\BaGet.Protocol.Tests\Support\TestDataHttpMessageHandler.cs(49-50): CS8602`
- `tests\BaGet.Tests\AccountDeletionAtomicityTests.cs(58): CS8604`
- `tests\BaGet.Tests\AccountDeletionAtomicityTests.cs(70): CS8604`
- `tests\BaGet.Tests\AccountReauthenticationTests.cs(83): CS8602`
- `tests\BaGet.Tests\AccountReauthenticationTests.cs(117): CS8602`
- `tests\BaGet.Tests\AccountReauthenticationTests.cs(169): CS8602`
- `tests\BaGet.Tests\AccountReauthenticationTests.cs(178): CS8602`
- `tests\BaGet.Tests\AccountReauthenticationTests.cs(310): CS8602`
- `tests\BaGet.Tests\AccountReauthenticationTests.cs(98): CS8604`
- `tests\BaGet.Tests\AccountReviewFixesTests.cs(83): CS8602`
- `tests\BaGet.Tests\AccountReviewFixesTests.cs(100): CS8602`
- `tests\BaGet.Tests\AccountReviewFixesTests.cs(156): CS8602`
- `tests\BaGet.Tests\AccountReviewFixesTests.cs(183): CS8602`
- `tests\BaGet.Tests\AccountReviewFixesTests.cs(158-162): CS8604`
- `tests\BaGet.Tests\AccountSettingsTests.cs(76-78): CS8602`
- `tests\BaGet.Tests\AccountSettingsTests.cs(135): CS8602`
- `tests\BaGet.Tests\AccountSettingsTests.cs(230): CS8602`
- `tests\BaGet.Tests\AccountSettingsTests.cs(248): CS8602`
- `tests\BaGet.Tests\AccountSettingsTests.cs(254): CS8602`
- `tests\BaGet.Tests\AccountSettingsTests.cs(263): CS8602`
- `tests\BaGet.Tests\AccountSettingsTests.cs(267): CS8602`
- `tests\BaGet.Tests\AccountSettingsTests.cs(292): CS8602`
- `tests\BaGet.Tests\AccountSettingsTests.cs(319): CS8602`
- `tests\BaGet.Tests\AccountSettingsTests.cs(321): CS8602`
- `tests\BaGet.Tests\AccountSettingsTests.cs(424): CS8602`
- `tests\BaGet.Tests\AccountSettingsTests.cs(438): CS8602`
- `tests\BaGet.Tests\AccountSettingsTests.cs(450): CS8602`
- `tests\BaGet.Tests\AccountSettingsTests.cs(478): CS8602`
- `tests\BaGet.Tests\AccountSettingsTests.cs(481): CS8602`
- `tests\BaGet.Tests\AccountSettingsTests.cs(499): CS8602`
- `tests\BaGet.Tests\AccountSettingsTests.cs(520): CS8602`
- `tests\BaGet.Tests\AccountSettingsTests.cs(590): CS8602`
- `tests\BaGet.Tests\AccountSettingsTests.cs(592): CS8602`
- `tests\BaGet.Tests\AccountSettingsTests.cs(604): CS8602`
- `tests\BaGet.Tests\AccountSettingsTests.cs(626-627): CS8602`
- `tests\BaGet.Tests\AccountSettingsTests.cs(375-381): CS8604`
- `tests\BaGet.Tests\AccountSettingsTests.cs(506): CS8604`
- `tests\BaGet.Tests\AccountSettingsTests.cs(606): CS8604`
- `tests\BaGet.Tests\AccountsIntegrationTests.cs(194-195): CS8714`
- `tests\BaGet.Tests\AccountsIntegrationTests.cs(283): CS8714`
- `tests\BaGet.Tests\AccountsIntegrationTests.cs(194-195): CS8621`
- `tests\BaGet.Tests\AccountsIntegrationTests.cs(242): CS8602`
- `tests\BaGet.Tests\AccountsWithoutGlobalKeyTests.cs(109): CS8619`
- `tests\BaGet.Tests\AdminDeleteUserTests.cs(187-188): CS8602`
- `tests\BaGet.Tests\AdminDeleteUserTests.cs(234): CS8602`
- `tests\BaGet.Tests\AdminDeleteUserTests.cs(205): CS8604`
- `tests\BaGet.Tests\AdminDeleteUserTests.cs(219): CS8604`
- `tests\BaGet.Tests\AdministratorTests.cs(68-69): CS8602`
- `tests\BaGet.Tests\AdministratorTests.cs(122): CS8602`
- `tests\BaGet.Tests\AdminPackageTests.cs(440): CS8633`
- `tests\BaGet.Tests\AdminPackageTests.cs(44-45): CS8602`
- `tests\BaGet.Tests\AdminPackageTests.cs(97): CS8602`
- `tests\BaGet.Tests\AdminUserActionsTests.cs(94): CS8604`
- `tests\BaGet.Tests\AdminUserActionsTests.cs(102): CS8602`
- `tests\BaGet.Tests\AdminUserActionsTests.cs(117): CS8602`
- `tests\BaGet.Tests\AllowedHostsIntegrationTests.cs(43): CS8602`
- `tests\BaGet.Tests\AllowedHostsIntegrationTests.cs(73): CS8602`
- `tests\BaGet.Tests\AllowedHostsIntegrationTests.cs(45-47): CS8604`
- `tests\BaGet.Tests\AllowedHostsIntegrationTests.cs(75-77): CS8604`
- `tests\BaGet.Tests\AllowedHostsIntegrationTests.cs(142-147): CS8620`
- `tests\BaGet.Tests\AlpineExpressionTests.cs(52-66): CS8602`
- `tests\BaGet.Tests\AvatarTests.cs(70-85): CS8602`
- `tests\BaGet.Tests\AvatarTests.cs(93): CS8602`
- `tests\BaGet.Tests\BaGetClientIntegrationTests.cs(43-44): CS8602`
- `tests\BaGet.Tests\BaGetClientIntegrationTests.cs(90): CS8602`
- `tests\BaGet.Tests\BaGetClientIntegrationTests.cs(100): CS8602`
- `tests\BaGet.Tests\BaGetClientIntegrationTests.cs(46-53): CS8604`
- `tests\BaGet.Tests\BaGetClientIntegrationTests.cs(63-65): CS8604`
- `tests\BaGet.Tests\BaGetClientIntegrationTests.cs(82): CS8604`
- `tests\BaGet.Tests\BaGetClientIntegrationTests.cs(92): CS8604`
- `tests\BaGet.Tests\BaGetClientIntegrationTests.cs(102): CS8604`
- `tests\BaGet.Tests\BaGetClientIntegrationTests.cs(113): CS8604`
- `tests\BaGet.Tests\BaGetClientIntegrationTests.cs(125): CS8604`
- `tests\BaGet.Tests\BrowserUploadSecurityTests.cs(156-157): CS8625`
- `tests\BaGet.Tests\BrowserUploadSecurityTests.cs(156-157): CS8619`
- `tests\BaGet.Tests\BrowserUploadSecurityTests.cs(193-194): CS8602`
- `tests\BaGet.Tests\BrowserUploadTests.cs(316): CS8604`
- `tests\BaGet.Tests\BrowserUploadTests.cs(439-441): CS8619`
- `tests\BaGet.Tests\CatalogIntegrationTests.cs(48-50): CS8602`
- `tests\BaGet.Tests\CatalogIntegrationTests.cs(127): CS8602`
- `tests\BaGet.Tests\CatalogIntegrationTests.cs(196): CS8602`
- `tests\BaGet.Tests\CatalogIntegrationTests.cs(48-50): CS8604`
- `tests\BaGet.Tests\ContactOwnersTests.cs(85-100): CS8602`
- `tests\BaGet.Tests\ContactOwnersTests.cs(110): CS8602`
- `tests\BaGet.Tests\ContactOwnersTests.cs(114): CS8602`
- `tests\BaGet.Tests\ContactOwnersTests.cs(205): CS8602`
- `tests\BaGet.Tests\ContactOwnersTests.cs(392): CS8602`
- `tests\BaGet.Tests\DatabaseSearchServiceTests.cs(181): CS8602`
- `tests\BaGet.Tests\DatabaseSearchServiceTests.cs(194-195): CS8602`
- `tests\BaGet.Tests\DatabaseSearchServiceTests.cs(248): CS8602`
- `tests\BaGet.Tests\DatabaseSearchServiceTests.cs(283): CS8602`
- `tests\BaGet.Tests\DatabaseSearchServiceTests.cs(296): CS8602`
- `tests\BaGet.Tests\DatabaseSearchServiceTests.cs(337): CS8602`
- `tests\BaGet.Tests\DatabaseSearchServiceTests.cs(404): CS8602`
- `tests\BaGet.Tests\DatabaseSearchServiceTests.cs(464-480): CS8602`
- `tests\BaGet.Tests\DatabaseSearchServiceTests.cs(74): CS8604`
- `tests\BaGet.Tests\DatabaseSearchServiceTests.cs(87): CS8604`
- `tests\BaGet.Tests\DatabaseSearchServiceTests.cs(104): CS8604`
- `tests\BaGet.Tests\DatabaseSearchServiceTests.cs(110): CS8604`
- `tests\BaGet.Tests\DatabaseSearchServiceTests.cs(128): CS8604`
- `tests\BaGet.Tests\DatabaseSearchServiceTests.cs(157): CS8604`
- `tests\BaGet.Tests\DatabaseSearchServiceTests.cs(168): CS8604`
- `tests\BaGet.Tests\DatabaseSearchServiceTests.cs(196): CS8604`
- `tests\BaGet.Tests\DatabaseSearchServiceTests.cs(211): CS8604`
- `tests\BaGet.Tests\DatabaseSearchServiceTests.cs(224): CS8604`
- `tests\BaGet.Tests\DatabaseSearchServiceTests.cs(233): CS8604`
- `tests\BaGet.Tests\DatabaseSearchServiceTests.cs(309): CS8604`
- `tests\BaGet.Tests\DatabaseSearchServiceTests.cs(322): CS8604`
- `tests\BaGet.Tests\DatabaseSearchServiceTests.cs(417): CS8604`
- `tests\BaGet.Tests\DatabaseSearchServiceTests.cs(128): CS8620`
- `tests\BaGet.Tests\EmailTests.cs(233): CS8633`
- `tests\BaGet.Tests\EmailTests.cs(154): CS8604`
- `tests\BaGet.Tests\EmailTests.cs(170): CS8604`
- `tests\BaGet.Tests\EmailTests.cs(215): CS8602`
- `tests\BaGet.Tests\EmailTests.cs(216): CS8620`
- `tests\BaGet.Tests\HeaderLinksTests.cs(181): CS8633`
- `tests\BaGet.Tests\InstallSnippetsTests.cs(9-10): CS8604`
- `tests\BaGet.Tests\LookAndFeelTests.cs(133): CS8602`
- `tests\BaGet.Tests\NuGetClientIntegrationTests.cs(54): CS8602`
- `tests\BaGet.Tests\NuGetClientIntegrationTests.cs(74-80): CS8602`
- `tests\BaGet.Tests\NuGetClientIntegrationTests.cs(103-109): CS8602`
- `tests\BaGet.Tests\NuGetClientIntegrationTests.cs(120-124): CS8602`
- `tests\BaGet.Tests\NuGetClientIntegrationTests.cs(135-139): CS8602`
- `tests\BaGet.Tests\NuGetClientIntegrationTests.cs(150-154): CS8602`
- `tests\BaGet.Tests\NuGetClientIntegrationTests.cs(165-169): CS8602`
- `tests\BaGet.Tests\NuGetClientIntegrationTests.cs(184-189): CS8602`
- `tests\BaGet.Tests\NuGetClientIntegrationTests.cs(206-212): CS8602`
- `tests\BaGet.Tests\NuGetClientIntegrationTests.cs(226-232): CS8602`
- `tests\BaGet.Tests\NuGetClientIntegrationTests.cs(247-253): CS8602`
- `tests\BaGet.Tests\NuGetV2ClientIntegrationTests.cs(63-64): CS8602`
- `tests\BaGet.Tests\NuGetV2ClientIntegrationTests.cs(73-74): CS8602`
- `tests\BaGet.Tests\NuGetV2ClientIntegrationTests.cs(85-86): CS8602`
- `tests\BaGet.Tests\NuGetV2ClientIntegrationTests.cs(100-106): CS8602`
- `tests\BaGet.Tests\NuGetV2ClientIntegrationTests.cs(118-124): CS8602`
- `tests\BaGet.Tests\OwnershipAuthorizationServiceTests.cs(81): CS8604`
- `tests\BaGet.Tests\PackageFrameworksTests.cs(17-21): CS8602`
- `tests\BaGet.Tests\PackageFrameworksTests.cs(38): CS8602`
- `tests\BaGet.Tests\PackageFrameworksTests.cs(42-45): CS8602`
- `tests\BaGet.Tests\PackageFrameworksTests.cs(48-49): CS8602`
- `tests\BaGet.Tests\PackageFrameworksTests.cs(58): CS8602`
- `tests\BaGet.Tests\PackageFrameworksTests.cs(81): CS8602`
- `tests\BaGet.Tests\PackageManagementPageTests.cs(65-66): CS8602`
- `tests\BaGet.Tests\PackageManagementPageTests.cs(170): CS8602`
- `tests\BaGet.Tests\PackageManagementPageTests.cs(227-228): CS8602`
- `tests\BaGet.Tests\PackageManagementPageTests.cs(373): CS8602`
- `tests\BaGet.Tests\PackageManagementPageTests.cs(392): CS8602`
- `tests\BaGet.Tests\PackageManagementPageTests.cs(409): CS8602`
- `tests\BaGet.Tests\PackageMetadataAdminTests.cs(209): CS8602`
- `tests\BaGet.Tests\PackagePageTabsTests.cs(63): CS8602`
- `tests\BaGet.Tests\PackagePageTabsTests.cs(261): CS8602`
- `tests\BaGet.Tests\PathBaseIntegrationTests.cs(43): CS8602`
- `tests\BaGet.Tests\PathBaseIntegrationTests.cs(59): CS8602`
- `tests\BaGet.Tests\PathBaseIntegrationTests.cs(94): CS8602`
- `tests\BaGet.Tests\PathBaseIntegrationTests.cs(113): CS8602`
- `tests\BaGet.Tests\PathBaseIntegrationTests.cs(45): CS8604`
- `tests\BaGet.Tests\PathBaseIntegrationTests.cs(61): CS8604`
- `tests\BaGet.Tests\PathBaseIntegrationTests.cs(96): CS8604`
- `tests\BaGet.Tests\PathBaseIntegrationTests.cs(115): CS8604`
- `tests\BaGet.Tests\ReadmeMarkdownTests.cs(19): CS8602`
- `tests\BaGet.Tests\ReadmeMarkdownTests.cs(35): CS8602`
- `tests\BaGet.Tests\ReadmeMarkdownTests.cs(44): CS8602`
- `tests\BaGet.Tests\ReadmeMarkdownTests.cs(75-76): CS8602`
- `tests\BaGet.Tests\ReadmeMarkdownTests.cs(88): CS8602`
- `tests\BaGet.Tests\ReadmeMarkdownTests.cs(96): CS8602`
- `tests\BaGet.Tests\SearchSortWebTests.cs(47-62): CS8602`
- `tests\BaGet.Tests\SearchSortWebTests.cs(72-87): CS8602`
- `tests\BaGet.Tests\SessionPrincipalTests.cs(62): CS8602`
- `tests\BaGet.Tests\SettingsCatalogTests.cs(111): CS8602`
- `tests\BaGet.Tests\SettingsCatalogTests.cs(162): CS8602`
- `tests\BaGet.Tests\SettingsCatalogTests.cs(168): CS8602`
- `tests\BaGet.Tests\SettingsCatalogTests.cs(201): CS8602`
- `tests\BaGet.Tests\SettingsCatalogTests.cs(234-236): CS8602`
- `tests\BaGet.Tests\SettingsCatalogTests.cs(230): CS8604`
- `tests\BaGet.Tests\SettingsCatalogTests.cs(251): CS8604`
- `tests\BaGet.Tests\SettingsCatalogTests.cs(255-256): CS8604`
- `tests\BaGet.Tests\SmtpEmailSenderTests.cs(26): CS8602`
- `tests\BaGet.Tests\SmtpEmailSenderTests.cs(71): CS8602`
- `tests\BaGet.Tests\Support\AdminTestSupport.cs(57-58): CS8602`
- `tests\BaGet.Tests\Support\AdminTestSupport.cs(75-76): CS8602`
- `tests\BaGet.Tests\Support\AdminTestSupport.cs(92): CS8602`
- `tests\BaGet.Tests\Support\AdminTestSupport.cs(99): CS8602`
- `tests\BaGet.Tests\Support\AdminTestSupport.cs(108): CS8602`
- `tests\BaGet.Tests\Support\Browser.cs(67): CS8602`
- `tests\BaGet.Tests\Support\Browser.cs(93): CS8604`
- `tests\BaGet.Tests\Support\ExternalCookieFilter.cs(30-36): CS8604`
- `tests\BaGet.Tests\Support\ExternalCookieFilter.cs(38): CS8604`
- `tests\BaGet.Tests\Support\TestableHttpSource.cs(16): CS8619`
- `tests\BaGet.Tests\Support\TestableHttpSource.cs(31-37): CS8620`
- `tests\BaGet.Tests\Support\XunitLogger.cs(20): CS8633`
- `tests\BaGet.Tests\V2FeedIntegrationTests.cs(35-36): CS8602`
- `tests\BaGet.Tests\V2FeedIntegrationTests.cs(67): CS8602`
- `tests\BaGet.Tests\V2FeedIntegrationTests.cs(69-72): CS8602`
- `tests\BaGet.Tests\V2FeedIntegrationTests.cs(121): CS8602`
- `tests\BaGet.Tests\V2FeedIntegrationTests.cs(152): CS8602`
- `tests\BaGet.Tests\V2FeedIntegrationTests.cs(176): CS8602`
- `tests\BaGet.Tests\V2FeedIntegrationTests.cs(195): CS8602`
- `tests\BaGet.Tests\ValidationIntegrationTests.cs(113): CS8602`
- `tests\BaGet.Tests\ValidationIntegrationTests.cs(257): CS8604`
- `tests\BaGet.Tests\WebUiIntegrationTests.cs(124): CS8602`
- `tests\BaGet.Tests\WebUiIntegrationTests.cs(152): CS8602`
- `tests\BaGet.Tests\WebUiIntegrationTests.cs(191): CS8602`
- `tests\BaGet.Tests\WebUiIntegrationTests.cs(261-262): CS8602`
- `tests\BaGet.Tests\WebUiIntegrationTests.cs(82): CS8604`
- `tests\BaGet.Tests\WebUiIntegrationTests.cs(228): CS8604`
- `tests\BaGet.Web.Tests\Pages\IndexModelFacts.cs(34): CS8602`
- `tests\BaGet.Web.Tests\Pages\IndexModelFacts.cs(54): CS8602`
- `tests\BaGet.Web.Tests\Pages\PackageModelFacts.cs(51): CS8602`
- `tests\BaGet.Web.Tests\Pages\PackageModelFacts.cs(69): CS8602`
- `tests\BaGet.Web.Tests\Pages\PackageModelFacts.cs(89): CS8602`
- `tests\BaGet.Web.Tests\Pages\PackageModelFacts.cs(92-93): CS8602`
- `tests\BaGet.Web.Tests\Pages\PackageModelFacts.cs(95): CS8602`
- `tests\BaGet.Web.Tests\Pages\PackageModelFacts.cs(97): CS8602`
- `tests\BaGet.Web.Tests\Pages\PackageModelFacts.cs(116): CS8602`
- `tests\BaGet.Web.Tests\Pages\PackageModelFacts.cs(119-120): CS8602`
- `tests\BaGet.Web.Tests\Pages\PackageModelFacts.cs(122): CS8602`
- `tests\BaGet.Web.Tests\Pages\PackageModelFacts.cs(141): CS8602`
- `tests\BaGet.Web.Tests\Pages\PackageModelFacts.cs(144-145): CS8602`
- `tests\BaGet.Web.Tests\Pages\PackageModelFacts.cs(147): CS8602`
- `tests\BaGet.Web.Tests\Pages\PackageModelFacts.cs(196): CS8602`
- `tests\BaGet.Web.Tests\Pages\PackageModelFacts.cs(234): CS8602`
- `tests\BaGet.Web.Tests\Pages\PackageModelFacts.cs(238): CS8602`
- `tests\BaGet.Web.Tests\Pages\PackageModelFacts.cs(241-242): CS8602`
- `tests\BaGet.Web.Tests\Pages\PackageModelFacts.cs(244-245): CS8602`
- `tests\BaGet.Web.Tests\Pages\PackageModelFacts.cs(247-248): CS8602`
- `tests\BaGet.Web.Tests\Pages\PackageModelFacts.cs(331-333): CS8602`
- `tests\BaGet.Web.Tests\Pages\PackageModelFacts.cs(239): CS8604`
- `tests\BaGet.Web.Tests\Pages\PackageModelFacts.cs(277): CS8604`

## Warnings

- Msbuild failed when processing the file 'D:\CommonLibrary\BaGet.Converted\src\BaGet.Core\BaGet.Core.csproj' with message: PackageReference System.Reflection.Metadata will not be pruned. This package is automatically available and does not need to be referenced explicitly. Remove the PackageReference item.
- BaGet.Core.Tests was converted as production code, but it references a test framework. Its test packages are not named in its own project file, so they most likely come from a Directory.Packages.props or an imported .props. If it holds tests, mark it with <IsTestProject>true</IsTestProject> and run again.
- BaGet.Web.Tests was converted as production code, but it references a test framework. Its test packages are not named in its own project file, so they most likely come from a Directory.Packages.props or an imported .props. If it holds tests, mark it with <IsTestProject>true</IsTestProject> and run again.
- BaGet.Azure.Tests was converted as production code, but it references a test framework. Its test packages are not named in its own project file, so they most likely come from a Directory.Packages.props or an imported .props. If it holds tests, mark it with <IsTestProject>true</IsTestProject> and run again.
- BaGet.Tests was converted as production code, but it references a test framework. Its test packages are not named in its own project file, so they most likely come from a Directory.Packages.props or an imported .props. If it holds tests, mark it with <IsTestProject>true</IsTestProject> and run again.
- BaGet.Protocol.Tests was converted as production code, but it references a test framework. Its test packages are not named in its own project file, so they most likely come from a Directory.Packages.props or an imported .props. If it holds tests, mark it with <IsTestProject>true</IsTestProject> and run again.
- the annotation fixpoint hit its limit of 20 iteration(s) without converging; what it had done is kept and the remaining diagnostics are listed in the report.

