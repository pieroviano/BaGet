# Configuration

You can modify BaGet's configurations by editing the `appsettings.json` file.

## Require an API key

You can require that users provide a password, called an API key, to publish packages.
To do so, you can insert the desired API key in the `ApiKey` field.

```json
{
    "ApiKey": "NUGET-SERVER-API-KEY",
    ...
}
```

Users will now have to provide the API key to push packages:

```c#
dotnet nuget push -s http://localhost:6060/v3/index.json -k NUGET-SERVER-API-KEY package.1.0.0.nupkg
```

With [accounts](#accounts-package-owners-and-api-keys) enabled, users can also push with keys of their own.

## Restrict the host names BaGet serves

The absolute URLs BaGet advertises in its service index, registrations and URI templates are
built from the incoming request's `Host` header. By default BaGet serves any host, because it
is commonly run behind a reverse proxy on a host name it cannot know. If the host names are
known, list them in `AllowedHosts`, separated by semicolons; a request with any other host is
then rejected with a `400` response:

```json
{
    ...

    "AllowedHosts": "nuget.example.com;nuget.example.com:8080",

    ...
}
```

| Entry | Matches |
| --- | --- |
| `*` | any host — host filtering is off (the default) |
| `example.com` | that host on any port |
| `example.com:8080` | that host on port 8080 only |
| `*.example.com` | any subdomain of `example.com` |

!!! info
    This is [ASP.NET Core's host filtering](https://learn.microsoft.com/aspnet/core/fundamentals/servers/kestrel/host-filtering)
    configuration key. If you embed BaGet in your own application, call `UseHostFiltering` in
    its pipeline.

## Enable read-through caching

Read-through caching lets you index packages from an upstream source. You can use read-through
caching to:

1. Speed up your builds if restores from [nuget.org](https://nuget.org) are slow
1. Enable package restores in offline scenarios

The following `Mirror` setting configures BaGet to index packages from [nuget.org](https://nuget.org):

```json
{
    ...

    "Mirror": {
        "Enabled":  true,
        "PackageSource": "https://api.nuget.org/v3/index.json"
    },

    ...
}
```

!!! info
    `PackageSource` is the value of the [NuGet service index](https://docs.microsoft.com/en-us/nuget/api/service-index).

## Enable package hard deletions

To prevent the ["left pad" problem](https://blog.npmjs.org/post/141577284765/kik-left-pad-and-npm),
BaGet's default configuration doesn't allow package deletions. Whenever BaGet receives a package deletion
request, it will instead "unlist" the package. An unlisted package is undiscoverable but can still be
downloaded if you know the package's id and version. You can override this behavior by setting the
`PackageDeletionBehavior`:

```json
{
    ...

    "PackageDeletionBehavior": "HardDelete",

    ...
}
```

## Enable package overwrites

Normally, BaGet will reject a package upload if the id and version are already taken. You can configure BaGet
to overwrite the already existing package by setting `AllowPackageOverwrites`:

```json
{
    ...

    "AllowPackageOverwrites": true,

    ...
}
```

## NuGet V2 feed

BaGet serves a NuGet V2 (OData) feed at `/api/v2` for clients that cannot use V3: `nuget list`,
older `Install-Package`, and PowerShellGet. It is on by default. To turn it off:

```json
{
    ...

    "Feeds": {
        "EnableV2": false
    },

    ...
}
```

!!! info
    The OData query options `$filter`, `$orderby`, `$select` and `$expand` are ignored; `$skip` and
    `$top` are honoured.

## Accounts, package owners and API keys

Accounts are **off** by default; with them off, only the global `ApiKey` authenticates, exactly as
before. Turn them on with:

```json
{
    ...

    "ApiKey": "NUGET-SERVER-API-KEY",

    "Accounts": {
        "Enabled": true,
        "AllowRegistration": true
    },

    ...
}
```

Users then sign in at `/account/login`, create an account at `/account/register` (unless
`AllowRegistration` is `false`), and create API keys at `/account/apikeys`. A key is shown once,
and has:

| Property | Meaning |
| --- | --- |
| Scopes | `package:push` (new IDs and new versions), `package:pushversion` (new versions of owned IDs only), `package:unlist` (unlist, relist, delete, deprecation and vulnerabilities) |
| Package glob pattern | the IDs the key applies to, `*` as a wildcard, for example `Contoso.*` |
| Expiry | after which the key stops working |

Who may change a package:

* The global `ApiKey` may change any package.
* Pushing a new package ID makes the pusher its owner.
* Only an owner, directly or through an [organization](#organizations), may push new versions of an ID or manage it.
* A package pushed before accounts were enabled has no owner, and only the global `ApiKey` may change it.
  Add owners from its manage page (`/packages/{id}/manage`) to hand it over.

!!! warning
    Set `ApiKey`. Without it the server is open: anyone may push anything, and per-user keys restrict nothing.

### Organizations

Signed-in users create organizations at `/account/organizations`. An organization can own packages
like a user; its members act for it, and its administrators add and remove members. User and
organization names share one namespace.

### Sign in with OpenID Connect

To let users sign in with an external provider such as Microsoft Entra ID:

```json
{
    ...

    "Accounts": {
        "Enabled": true,
        "OpenIdConnect": {
            "Authority": "https://login.microsoftonline.com/<tenant-id>/v2.0",
            "ClientId": "<client-id>",
            "ClientSecret": "<client-secret>",
            "DisplayName": "Microsoft"
        }
    },

    ...
}
```

Register `https://<your-host>/signin-oidc` as the redirect URI. The first sign-in creates an account,
whatever `AllowRegistration` says: the provider decides who may sign in.

### Verified packages

An owner can be marked as verified for a package ID, which search reports as `verified`. There is no
page for this yet; it is set through `PackageOwnershipService.SetVerifiedAsync`.

## Validate packages before serving them

The validation pipeline is **off** by default; with it off a push is indexed at once. Turn it on with:

```json
{
    ...

    "Validation": {
        "Enabled": true,
        "RequireSignedPackages": false
    },

    ...
}
```

A push is then answered `202 Accepted`, and the package is checked in the background before it
appears. Until then it cannot be found or downloaded. The checks are:

* every entry in the package has a safe path;
* a signed package's signature is intact and valid (untrusted roots are allowed);
* with `RequireSignedPackages`, the package is signed;
* any validator you register by implementing `IPackageValidator`, for example a malware scanner.

A package that fails is quarantined and never served. Its owners can see why with
`GET /v3/package/{id}/{version}/validation`, sending their API key in `X-NuGet-ApiKey`.
Pending validations resume when BaGet restarts.

### Repository signing

BaGet can repository-sign every package that passes validation:

```json
{
    ...

    "Validation": {
        "Enabled": true,
        "RepositorySigningCertificatePath": "/secrets/repository-signing.pfx",
        "RepositorySigningCertificatePassword": "...",
        "RepositorySigningServiceIndexUrl": "https://nuget.example.com/v3/index.json"
    },

    ...
}
```

The certificate needs the code signing usage. `RepositorySigningServiceIndexUrl` must be your public,
`https` service index URL: NuGet rejects repository signatures for any other scheme. BaGet then
advertises the certificate through the `RepositorySignatures/5.0.0` resource.

## Catalog

BaGet keeps a [NuGet catalog](https://learn.microsoft.com/nuget/api/catalog-resource), an
append-only log of package changes that mirrors can follow. It needs no configuration. Packages
pushed before the catalog existed are added with:

```
dotnet BaGet.dll catalog rebuild
```

The catalog is not available with Azure Table Storage.

## Private feeds

A private feed requires users to authenticate before accessing packages.

!!! warning
    Private feeds are not supported at this time: [accounts](#accounts-package-owners-and-api-keys)
    control who may change packages, not who may read them. See [this pull request](https://github.com/loic-sharma/BaGet/pull/69) for more information.

## Database configuration

BaGet supports multiple database engines for storing package information:


- MySQL: `MySql`
- SQLite: `Sqlite`
- SQL Server: `SqlServer`
- PostgreSQL: `PostgreSql`
- Azure Table Storage: `AzureTable`

!!! info
    Azure Table Storage only stores packages. Accounts, the catalog, the validation pipeline,
    statistics, abuse reports, deprecation and vulnerabilities need one of the relational databases.

Each database engine requires a connection string to configure the connection. Please refer to [ConnectionStrings.com](https://www.connectionstrings.com/) to learn how to create the proper connection string for each database engine.

You may configure the chosen database engine either using environment variables or by editing the `appsettings.json` file.

### Environment Variables

There are two environment variables related to database configuration. These are:

- **Database__Type**: The database engine to use, this should be one of the strings from the above list such as `PostgreSql` or `Sqlite`.
- **Database__ConnectionString**: The connection string for your database engine.

### `appsettings.json`

The database settings are located under the `Database` key in the `appsettings.json` configuration file:

```json
{
    ...

    "Database": {
        "Type": "Sqlite",
        "ConnectionString": "Data Source=baget.db"
    },

    ...
}
```

There are two settings related to the database configuration:

- **Type**: The database engine to use, this should be one of the strings from the above list such as `PostgreSql` or `Sqlite`.
- **ConnectionString**: The connection string for your database engine.

## IIS server options

IIS Server options can be configured under the `IISServerOptions` key. The available options are detailed at [docs.microsoft.com](https://docs.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.builder.iisserveroptions)
Note: If not specified, the MaxRequestBodySize in BaGet defaults to 250MB (262144000 bytes), rather than the ASP.NET Core default of 30MB

```json
{
    ...

    "IISServerOptions": {
        "MaxRequestBodySize": 262144000
    },

    ...
}
```
