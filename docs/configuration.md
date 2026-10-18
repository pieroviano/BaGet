# Configuration

You can modify BaGet Extension's configurations by editing the `appsettings.json` file.

## Top-level keys

| Key | Default | Meaning |
| --- | --- | --- |
| `ApiKey` | empty | global key to push and change packages; see [Require an API key](#require-an-api-key) |
| `PathBase` | `.` | path the site is served under (`.` is the root) |
| `AllowedHosts` | `*` | see [Restrict the host names BaGet Extension serves](#restrict-the-host-names-baget-extension-serves) |
| `PackageDeletionBehavior` | `Unlist` | `Unlist` or `HardDelete` |
| `AllowPackageOverwrites` | `false` | accept a push of an existing id and version |
| `IsReadOnlyMode` | `false` | refuses pushes, deletes, relists, deprecation/vulnerability changes, browser uploads, the manage page's POSTs and an administrator's permanent deletion of a deleted user's packages |
| `RunMigrationsAtStartup` | `true` | apply pending database migrations when BaGet Extension starts |
| `SettingsOverridesPath` | `appsettings.Overrides.json` | the file the [Settings page](#settings-page) saves to; relative paths start at the content root (or `BAGET_CONFIG_ROOT`) |

## Listening address

The shipped `appsettings.json` sets `Kestrel:Endpoints:Http:Url` to `http://localhost:6060`. That
setting outranks `--urls` and `ASPNETCORE_URLS`, which therefore have no effect. Override it with the
environment variable `Kestrel__Endpoints__Http__Url`, or edit the file.

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

## Restrict the host names BaGet Extension serves

The absolute URLs BaGet Extension advertises in its service index, registrations and URI templates are
built from the incoming request's `Host` header. By default BaGet Extension serves any host, because it
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
    configuration key. If you embed BaGet Extension in your own application, call `UseHostFiltering` in
    its pipeline.

## Enable read-through caching

Read-through caching lets you index packages from an upstream source. You can use read-through
caching to:

1. Speed up your builds if restores from [nuget.org](https://nuget.org) are slow
1. Enable package restores in offline scenarios

The following `Mirror` setting configures BaGet Extension to index packages from [nuget.org](https://nuget.org):

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

| Key | Default | Meaning |
| --- | --- | --- |
| `Legacy` | `false` | read the upstream through the NuGet V2 protocol |
| `PackageDownloadTimeoutSeconds` | `600` | timeout of every HTTP request to the upstream (and of `import downloads`) |

## Enable package hard deletions

To prevent the ["left pad" problem](https://blog.npmjs.org/post/141577284765/kik-left-pad-and-npm),
BaGet Extension's default configuration doesn't allow package deletions. Whenever BaGet Extension receives a package deletion
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

Administrators (see [Administrators](#administrators)) can always hard-delete from a package's management
page, after confirming their password, even with the default `Unlist`. The NuGet `DELETE` API keeps
following this setting.

## Enable package overwrites

Normally, BaGet Extension will reject a package upload if the id and version are already taken. You can configure BaGet Extension
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

BaGet Extension serves a NuGet V2 (OData) feed at `/api/v2` for clients that cannot use V3: `nuget list`,
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

Accounts are **on** in the shipped `appsettings.json` (the code default, for hosts embedding BaGet Extension, is
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
* A package pushed before accounts were enabled has no owner. Only the global `ApiKey` and administrators (see [Administrators](#administrators)) may change it, and pushing to it never makes anyone its owner.
  Add owners from its manage page (`/packages/{id}/manage`) to hand it over.

!!! warning "Upgrade note: keyless pushes are refused"
    Earlier releases shipped with accounts off, so a default install accepted `dotnet nuget push` with
    no key. With accounts on and `ApiKey` empty, a push without a key now gets **401**; BaGet Extension logs a
    warning at startup. Fix CI by pushing with `-k <key>`, using a user key from `/account/apikeys` (or
    the global `ApiKey`), or restore the old behaviour with `"Accounts": { "Enabled": false }`.

!!! warning
    With accounts off, set `ApiKey`. Without it the server is open: anyone may push anything.

### Administrators

An account can be an **administrator**: a flag kept in the database, on the account. An administrator
reaches `/admin/users` (the "Users" entry of the account menu), where they promote and demote other
accounts, each time confirming with their password (or by signing in again with the provider, for an
account without one). Everyone else, and every request while accounts are off, gets 404 there.

```json
"Accounts": {
    "InitialAdministrators": "alice;bob"
}
```

| Setting or command | Meaning |
| --- | --- |
| `Accounts:InitialAdministrators` | User names, separated by `;`, promoted at start **only while no administrator exists** (it bootstraps an empty server). Only accounts that already exist are promoted; once any administrator exists the list is ignored, so registering a listed name later grants nothing and a demotion is not reversed. |
| `baget account promote <user>` | Makes the user an administrator. Needs `Accounts:Enabled`; exit code 1 for an unknown user. |
| `baget account demote <user>` | Removes the flag. Exit code 1 for an unknown user or the last administrator. |

* The **last administrator cannot be demoted, and cannot delete their account**: promote another user first.
  If every administrator is lost anyway, use `baget account promote` or `InitialAdministrators` (the latter works again once no administrator is left).
* **Package powers.** An administrator skips *ownership* only, never a key's scopes or glob (a push-only key an
  administrator made for CI stays push-only; use a separate non-admin account for CI keys):

  | Where | Power |
  | --- | --- |
  | API | Push a version of, or unlist, any existing ID (needs `PushVersion`/`Push`, or `Unlist`). A new ID still needs `Push` and makes the pusher its owner. |
  | `/packages/{id}/manage` | Opens for any ID, ownerless ones included, with a "managing as an administrator" banner. Writes by a non-owner administrator are logged (`Administrator {Admin} ran {Handler} on {PackageId}`). |
  | Manage, *Delete permanently* | Hard-deletes one version, or every version and the owners (*Danger zone*), whatever `PackageDeletionBehavior` says. Needs the administrator's password (or a fresh provider sign-in); logged. Read-only mode refuses it. |
  | `/admin/users/{name}/packages` | The IDs a user owns, with owners and a link to Manage. |
  | `/admin/packages` | IDs with no owner (predating accounts, or left without one), 50 per page. |
* The check reads the database on every request, so a demotion applies at once.

**User powers** (`/admin/users`; each action asks for the administrator's password, and none applies to your own row, which stays on `/account/settings`):

| Action | Behaviour |
| --- | --- |
| Packages | Opens `/admin/users/{name}/packages`. |
| Reset password, *Generate* | Replaces the password with a generated one, shown **once** on the response (no redirect, `Cache-Control: no-store`, never stored or logged). Two-factor is switched off, a lockout lifted, an unconfirmed email address marked confirmed, the user's sessions end. |
| Reset password, *Email link* | Mails the user a reset link. Only offered while SMTP is configured and the user has an address; no rate limit. |
| Delete (`/admin/users/{name}/delete`) | Removes the account, its API keys and its ownerships. The last administrator cannot be deleted. |

* **Account deletion by an administrator** is never blocked by ownership. A package ID the user alone owned is either
  *left without owners* (the default: only administrators and the global API key manage it, like IDs that predate accounts)
  or *deleted permanently with all its versions*. The user is removed first; a failure while deleting packages is reported
  and never rolls the deletion back.
* **Organizations**: if the user is the last organization administrator and others remain, the longest-standing member
  becomes administrator. If the user is the only member and the organization owns packages, the organization is kept without members;
  one that owns nothing is deleted.
* **Audit log**: resets, reset links and deletions are logged at `Warning` with the administrator and the user, never a password.

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
            "From": "BaGet Extension <baget@example.com>"
        }
    }
}
```

`EnableSsl` uses implicit TLS on port 465 and STARTTLS otherwise. A send failure is logged and
reported to the user as a failure.

**Links in emails need `Site:PublicUrl`.** Confirmation and password-reset links are never built from
the request's `Host` header, which anyone can forge (a forged `Host` on "forgot password" would
otherwise put the attacker's address in the victim's mail).

| Configuration | Links point at |
|---|---|
| `Site:PublicUrl` set (absolute http/https, a path is allowed: `https://example.com/feed`) | `Site:PublicUrl` |
| No `PublicUrl` (whatever `AllowedHosts` says) | **email is switched off** and a startup warning says so |

`AllowedHosts` does not help: even a restricted host list leaves the scheme to `X-Forwarded-Proto`,
which a client can spoof to downgrade a link to `http`.

```json
{ "Site": { "PublicUrl": "https://packages.example.com" } }
```

An invalid `PublicUrl` fails startup. While email is switched off for this reason, nothing that needs
it is offered and sign-in does not require a confirmed address, exactly as with no SMTP.

Other rules with email on:

* Changing the email address, setting a first password, deleting the account and turning two-factor
  off or resetting it ask for the current password. An account without a password (external sign-in
  only) must instead sign in again with its provider (the provider is asked to prompt for credentials
  again) within the last 5 minutes (the provider must return an `auth_time` claim inside that window, otherwise the sign-in is refused); without a configured provider it cannot do these, and an
  administrator uses `baget account reset-password`.
* "Forgot password" answers identically, in the same time, for known and unknown accounts: the mail is
  sent from a background queue. One reset mail per account per 10 minutes is the real protection; 60 mails per client
  address per 10 minutes is a backstop. There is no site-wide cap, which would let one caller block everyone's
  reset. State is in memory, per process, and bounded by the number of accounts; excess requests are answered the same and mail nothing. Requests for names that do not exist use up
  nothing. Known limit: the client address comes from `X-Forwarded-For`, which BaGet Extension trusts from any
  sender, so a caller can spoof it, and behind a proxy that sets no such header all callers share one
  address. Hence the per-client backstop is high and the per-account limit is what holds.
* OpenID Connect accounts are confirmed only when the provider sends `email_verified=true`; otherwise a
  confirmation link is mailed and the user signs in again once it is followed. A provider that sends no
  email address cannot create accounts while email is on.
* Contact owners uses the sender's address only if it is confirmed.
* Browser sessions are checked against the database on every request, so a deleted user's other
  sessions, or sessions of a user whose password was reset, stop working immediately.

Without email, an administrator recovers an account from the command line:

```
baget account reset-password <user>
```

It prints a generated password, switches off the user's two-factor authentication and lifts any
lockout, and marks the user's email address confirmed (the administrator at the console is trusted).
It needs `Accounts:Enabled` and works with or without SMTP.

**Turning SMTP on with existing users.** Sign-in then requires a confirmed address. A user with an
address who knows their password can use "Forgot password" (following the link confirms it). A user
with no address cannot sign in: the administrator runs
`baget account reset-password <user> --email <address>`, which sets the address, marks it confirmed
and prints a new password.

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

`Authority` and `ClientId` are required: without both, the provider is not offered. `DisplayName` (default `OpenID Connect`) labels the sign-in button. Register `https://<your-host>/signin-oidc` as the redirect URI. The first sign-in creates an account,
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
Pending validations resume when BaGet Extension restarts.

### Repository signing

BaGet Extension can repository-sign every package that passes validation:

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
`https` service index URL: NuGet rejects repository signatures for any other scheme. BaGet Extension then
advertises the certificate through the `RepositorySignatures/5.0.0` resource.

## Catalog

BaGet Extension keeps a [NuGet catalog](https://learn.microsoft.com/nuget/api/catalog-resource), an
append-only log of package changes that mirrors can follow. It needs no configuration. Packages
pushed before the catalog existed are added with:

```
dotnet BaGet.dll catalog rebuild
```

The catalog is not available with Azure Table Storage.

## Legal pages

`Site:TermsOfUsePath` and `Site:PrivacyPolicyPath` point at Markdown files (absolute, or relative to the
content root). They are served at `/terms` and `/privacy`, and the footer links to each only while its
path is set; BaGet Extension ships no legal text. A page whose path is unset or whose file is missing answers 404.
The path must end in `.md` and contain no `..` segment (the pages are public, so no other file may be served); a
other value is refused when saved on the Settings page and at startup.

```json
{ "Site": { "TermsOfUsePath": "legal/terms.md", "PrivacyPolicyPath": "legal/privacy.md" } }
```

## Private feeds

A private feed requires users to authenticate before accessing packages.

!!! warning
    Private feeds are not supported at this time: [accounts](#accounts-package-owners-and-api-keys)
    control who may change packages, not who may read them. See [this pull request](https://github.com/loic-sharma/BaGet/pull/69) for more information.

## Storage

`Storage:Type` picks where package and symbol content lives (case-insensitive):

| `Type` | Keys under `Storage` | Guide |
| --- | --- | --- |
| `FileSystem` (default) | `Path`; empty means the current working directory | [local](installation/local.md) |
| `AzureBlobStorage` | `ConnectionString`, or `AccountName` and `AccessKey`; `Container`; `DownloadUriExpiry` (default 10 minutes) | [Azure](installation/azure.md) |
| `AwsS3` | `Region`, `Bucket`, `Prefix`; `AccessKey` and `SecretKey` (or `UseInstanceProfile`, `AssumeRoleArn`) | [AWS](installation/aws.md) |
| `GoogleCloud` | `BucketName` | [Google Cloud](installation/gcp.md) |
| `AliyunOss` | `AccessKey`, `AccessKeySecret`, `Endpoint`, `Bucket`, `Prefix` | [Alibaba Cloud](installation/aliyun.md) |
| `Null` | none; stores nothing | |

## Search

`Search:Type` picks the search index:

| `Type` | Notes |
| --- | --- |
| `Database` (default) | searches the package database |
| `AzureSearch` | `AccountName` and `ApiKey` under `Search` |
| `Null` | no search results |

## Database configuration

BaGet Extension supports multiple database engines for storing package information:


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

There are three settings related to the database configuration:

- **Type**: The database engine to use, this should be one of the strings from the above list such as `PostgreSql` or `Sqlite`.
- **ConnectionString**: The connection string for your database engine.
- **MySqlServerVersion**: MySQL only, for example `8.0.36`. When empty, BaGet Extension detects the version by connecting to the server; set it to avoid that connection.

## IIS server options

IIS Server options can be configured under the `IISServerOptions` key. The available options are detailed at [docs.microsoft.com](https://docs.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.builder.iisserveroptions)
Note: If not specified, the MaxRequestBodySize in BaGet Extension defaults to 250MB (262144000 bytes), rather than the ASP.NET Core default of 30MB

```json
{
    ...

    "IISServerOptions": {
        "MaxRequestBodySize": 262144000
    },

    ...
}
```

## Settings page

Settings changed on the server's Settings page are saved to a JSON file, so `appsettings.json` and its
comments stay untouched. The file is `appsettings.Overrides.json` in the content root (or in
`BAGET_CONFIG_ROOT`); set `SettingsOverridesPath` (also as the environment variable
`SettingsOverridesPath`) to put it elsewhere.

| Source, lowest to highest precedence | Notes |
| --- | --- |
| `appsettings.json`, `appsettings.{Environment}.json` | shipped defaults |
| **the overrides file** | saved by the Settings page, reloaded when it changes |
| environment variables, command line | an operator's explicit setting still wins; the page marks a saved value that one of these overrides, because the saved value then has no effect |

- A setting read only at startup (`Accounts`, `Email`, `Database`, `Storage`, `Search`, `Mirror`,
  `Site:PublicUrl`, `PathBase`, `AllowedHosts`, `Urls`, `RunMigrationsAtStartup`, `IISServerOptions`) is marked
  **restart required**; every other setting applies to the next request. The page lists the changes waiting for a restart.
- A secret (API keys, connection strings, passwords, client secrets) is write-only: the page shows it masked,
  a blank field keeps the current value, and "Clear" saves an empty one. It is stored in plain text in the
  overrides file, as it would be in `appsettings.json`.
- A change is validated before it is written, using the same rules as at startup. A failure that exists without the
  change (an unused cloud provider's incomplete options) does not block a save.
- The file is written atomically, so the folder must be writable. In a container, point `SettingsOverridesPath`
  into a mounted volume, see [Docker](installation/docker.md). To recover from a bad value, edit or delete the file.
- The page logs which keys changed and who changed them, never the values.

### Using the Settings page

- Open **Server settings** in the account menu (administrators only; anyone else gets a 404). The page has one form per
  section, each with its own **Save** button; saving asks for your password (an account without one signs in
  again with its provider instead).
- Each setting shows its key, its description, and badges: **Restart required**, **Saved override** (tick *Reset to
  default* to remove it) and **Overridden by environment variable / command line** (the saved value then has no effect).
- A secret field is blank: leave it empty to keep the value, type to replace it, tick *Clear* to empty it.
- Only changed values are saved. A value that fails to parse or validate is listed above its section and nothing in
  that section is saved. Settings of other kinds (maps) are shown read-only.
- A setting no file sets shows the built-in default (for example `Feeds:EnableV2` is ticked) and can be changed from it.
- If the overrides file cannot be written (a read-only content root) or is not valid JSON, the page shows the error,
  naming the path, instead of failing; point `SettingsOverridesPath` at a writable file.
