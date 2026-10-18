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

This setting also labels the button on a package's management page: it reads "Unlist this version"
by default and "Delete this version permanently" with `HardDelete`, so the page never offers a
deletion the server would only unlist. Two things a hard delete does *not* do: it leaves indexed
symbols being served from `api/download/symbols/...`, and it does not update an external search
index, so with `Search:Type=AzureSearch` the version can still appear in search results until it is
reindexed.

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

## Upload through the browser

`/upload` accepts a `.nupkg` or a `.snupkg` in two steps: the file is staged, what it contains is
shown, and nothing is published until the preview is confirmed. With
[accounts](#accounts-package-owners-and-api-keys) on, the signed-in session is the identity and
ownership decides what it may publish; with accounts off the page asks for the same `ApiKey` a push
needs. `IsReadOnlyMode` refuses both. A `.snupkg` is indexed for a version the server already has -
it never creates a package ID and confers no ownership.

| Operating note | |
| --- | --- |
| Temporary space | Each upload is spooled to `ASPNETCORE_TEMP` (else the system temporary directory) before it is staged, so a browser upload needs free space there equal to the package's size, on top of the staged copy. |
| Size ceiling | About 2 GB (`MultipartBodyLengthLimit` is `int.MaxValue`). Beyond it the request is rejected with a bare `400` before the page runs, so the page cannot explain it. Under IIS in-process, `IISServerOptions.MaxRequestBodySize` caps uploads at 250 MB and `requestLimits/maxAllowedContentLength` in `web.config` at 30 MB - the ceilings the push endpoint already has. |
| Unconfirmed uploads | Staged under `staging/` in the configured store, and removed when the preview is confirmed or discarded. Whatever nobody comes back to is swept after 24 hours - on `FileSystem` and `Null` storage; the cloud providers cannot list a prefix yet, so abandoned uploads there accumulate. |
| `Storage:Path` | Do not point it at the web root: static files would then serve the store, `staging/` and `validation/` included. That is a pre-existing misconfiguration rather than something uploads introduce. |

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

Accounts are **on** in the shipped `appsettings.json` (the code default, for hosts embedding BaGet, is
off). With them off, only the global `ApiKey` authenticates. Set `Accounts:Enabled` to `false` to get
that behaviour back; otherwise configure them with:

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
`AllowRegistration` is `false`), and create API keys at `/account/apikeys`. `/account/packages`
lists the IDs they own, directly or through an organization, and links to each one's
`/packages/{id}/manage`. A key is shown once, and has:

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

!!! warning "Upgrade note: keyless pushes are refused"
    Earlier releases shipped with accounts off, so a default install accepted `dotnet nuget push` with
    no key. With accounts on and `ApiKey` empty, a push without a key now gets **401**; BaGet logs a
    warning at startup. Fix CI by pushing with `-k <key>`, using a user key from `/account/apikeys` (or
    the global `ApiKey`), or restore the old behaviour with `"Accounts": { "Enabled": false }`.

!!! warning
    With accounts off, set `ApiKey`. Without it the server is open: anyone may push anything.

### Email (optional)

Email is off until `Email:Smtp` has a `Host` and a `From`. While it is off, nothing that needs email
is offered, and sign-in does not require a confirmed address. When it is on, sign-in requires one.

```json
{
    "Email": {
        "Smtp": {
            "Host": "smtp.example.com",
            "Port": 587,
            "UserName": "baget",
            "Password": "...",
            "EnableSsl": true,
            "From": "BaGet <baget@example.com>"
        }
    }
}
```

`EnableSsl` uses implicit TLS on port 465 and STARTTLS otherwise. A send failure is logged and
reported to the user as a failure.

Without email, an administrator recovers an account from the command line:

```
baget account reset-password <user>
```

It prints a generated password, switches off the user's two-factor authentication and lifts any
lockout. It needs `Accounts:Enabled` and works with or without SMTP.

Email also enables **Contact owners** on a package page (`/packages/{id}/contact-owners`, signed-in
users only). The message goes, one email each, to the owning users with a confirmed address and to the
administrators of owning organizations, with the sender in `Reply-To`; the sender never sees owner
addresses. One message per sender per package every 10 minutes, counted in memory per process (it
resets on restart and is not shared between instances). Without email the page answers 404 and the
link is not shown.

### Avatars

Owners, profiles and the account menu show a [Gravatar](https://gravatar.com) (`d=retro`), keyed by
the MD5 of the user's email, or of the name for organizations and users without an email. Every
avatar is a request from the visitor's browser to gravatar.com carrying that hash. A private feed can
turn them off, which shows initial-letter placeholders instead:

```json
{ "Site": { "UseGravatar": false } }
```

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

## Search sort

The package list offers *Most downloaded* and *Recently updated* beside relevance (web UI only; the V3
search API has no sort parameter and is unchanged). The `Database` search provider sorts on the
server. `AzureSearch` and Azure Table do not (the index has no sortable `published` field, and Table
storage cannot order), so the control is hidden there.

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
