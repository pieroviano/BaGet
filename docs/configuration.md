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
| `IsReadOnlyMode` | `false` | refuses pushes (packages and symbols), deletes, relists, deprecation/vulnerability changes, browser uploads, the manage page's POSTs and an administrator's permanent deletion of a deleted user's packages |
| `RunMigrationsAtStartup` | `true` | apply pending database migrations when BaGet Extension starts |
| `SettingsOverridesPath` | empty (= `appsettings.Overrides.json`) | the file the [Settings page](#settings-page) saves to; relative paths start at the content root (or `BAGET_CONFIG_ROOT`) |
| `Urls` | none | the ASP.NET Core listening-address key; ineffective while `Kestrel:Endpoints` is set, see [Listening address](#listening-address) |

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
| `Enabled` | `false` | index packages from the upstream |
| `PackageSource` | none | the upstream service index URL; required while `Enabled` (startup fails otherwise) |
| `Legacy` | `false` | read the upstream through the NuGet V2 protocol |
| `PackageDownloadTimeoutSeconds` | `600` | timeout of every HTTP request to the upstream (and of `import downloads`) |

While `Enabled`, a [remote repository](remote-repositories.md#the-mirror) named `mirror` is created from these keys at startup and the
default repository fetches through its settings: set credentials, cache periods or offline mode for the mirror on
`/admin/repositories/mirror`. The URL, protocol and timeout stay owned by these keys.

## Remote repositories: refuse private upstreams

An upstream URL (and *Test connection*) makes this server send requests to whatever address the administrator typed, so
an administrator who may create remote repositories can reach what the server can reach (internal services, the cloud
metadata address `169.254.169.254`). Internal feeds are a normal use, hence the default is to allow them.

| Key | Default | Meaning |
| --- | --- | --- |
| `Remote:BlockPrivateAddresses` | `false` | refuse upstreams that are `localhost`, loopback, private (RFC 1918, `fc00::/7`), link-local, carrier-grade NAT or otherwise non-public. Live (no restart) |

Enforced when a repository is created or saved (the host name or literal address in the URL), when *Test connection* runs
(also after DNS resolution) and on every connection a V3 upstream client opens (a name that later resolves to a private
address, or a redirect to one, is refused; with an HTTP proxy configured the proxy's address is what is checked). A V2
upstream is contacted by NuGet's own HTTP stack: only the URL's host is checked. `Mirror:PackageSource` is operator
configuration: if it is refused the `mirror` repository is not created (a warning is logged) and fetches fail.

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
symbols being served from `api/download/symbols/...` (delete them first from the Manage page's *Symbols* list),
and it does not update an external search
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
it never creates a package ID and confers no ownership. The *Upload* navigation entries are hidden
where an upload cannot work: read-only mode, a closed maintenance gate, or accounts off with no `ApiKey`.

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

| Key | Default | Meaning |
| --- | --- | --- |
| `Enabled` | `false` in code, `true` in the shipped file | turns accounts on |
| `AllowRegistration` | `true` | whether `/account/register` creates accounts |
| `InitialAdministrators`, `CreateDefaultAdministrator` | none, `true` | see [Administrators](#administrators) |
| `OpenIdConnect:*` | none | see [Sign in with OpenID Connect](#sign-in-with-openid-connect) |

Users then sign in at `/account/login`, create an account at `/account/register` (unless
`AllowRegistration` is `false`), and create API keys at `/account/apikeys`. `/account/packages`
lists the IDs they own, directly or through an organization, and links to each one's
`/packages/{id}/manage`. A key is shown once, and has:

| Property | Meaning |
| --- | --- |
| Scopes | `package:push` (new IDs and new versions), `package:pushversion` (new versions of owned IDs only), `package:unlist` (unlist, relist, delete, deprecation and vulnerabilities) |
| Package glob pattern | the IDs the key applies to, `*` as a wildcard, for example `Contoso.*` |
| Repositories | optional: the repositories the key may be used in (tick them on `/account/apikeys`; none ticked = every repository, as every key was before). See [Repository-scoped keys](access-control.md#repository-scoped-api-keys) |
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
| `Accounts:CreateDefaultAdministrator` | Default `true`. On a server with **no users at all**, startup creates the administrator `Admin` with password `Admin1234!` (email counted as confirmed, so it can sign in while SMTP is configured). Every start warns until the password is changed. Set `false` to bootstrap only through `InitialAdministrators` or `account promote`. |
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
  | Manage, *Verified* | Marks or clears the ID as verified (reserved); owners see the state but cannot change it. |
  | `/admin/users/{name}/packages` | The IDs a user owns, with owners and a link to Manage. |
  | `/admin/packages` | IDs with no owner (predating accounts, or left without one) in every repository, 50 per page; a Repository column appears once one is outside `default`, and each link goes to that repository's page. |
| Manage, *Vulnerabilities* | Owners and administrators add or remove a version's advisories (http/https URL, severity low to critical, optional version range); the same rules as the `PUT .../vulnerabilities` API. |
| Manage, *Symbols* | Lists the PDBs each version's symbol package contributed and deletes them (database rows and stored content; content another version still lists is kept). Only symbols indexed after this list was introduced are recorded: older ones keep being served but cannot be listed or deleted per package. Not available with Azure Table. |
| Package page, *Manage package* | Shown only to an owner (directly or through an organization) or an administrator. |
* The check reads the database on every request, so a demotion applies at once.

**User powers** (`/admin/users`; each action asks for the administrator's password, and none applies to your own row, which stays on `/account/settings`):

| Action | Behaviour |
| --- | --- |
| Name (`/admin/users/{name}`) | The user's detail page, built from sections: account, organizations, API keys (see below). |
| Packages | Opens `/admin/users/{name}/packages`. |
| Create a user | The *Create a user* form: name, optional email, and a typed password, a generated one (blank field; shown **once**, like a reset) or *Send an invitation* (needs SMTP and an address): the account gets an unknown random password and is mailed a reset link to choose its own. A name used by a user or organization is refused. The address counts as confirmed. Logged at `Warning`. |
| Reset password, *Generate* | Replaces the password with a generated one, shown **once** on the response (no redirect, `Cache-Control: no-store`, never stored or logged). Two-factor is switched off, a lockout lifted, an unconfirmed email address marked confirmed, the user's sessions end. |
| Reset password, *Email link* | Mails the user a reset link. Only offered while SMTP is configured and the user has an address; no rate limit. |
| Delete (`/admin/users/{name}/delete`) | Removes the account, its API keys and its ownerships. The last administrator cannot be deleted. |

* **API keys of any user** (`/admin/users/{name}`): lists description, scopes, package pattern, repositories and dates (never the key or its hash) and revokes one. Needs the administrator's password; logged.
* **Organizations** (`/admin/organizations`, `/admin/organizations/{name}`; every action asks for the password, 503 while the server is read-only for maintenance):

  | Action | Behaviour |
  | --- | --- |
  | List | Every organization with member, administrator and owned package-ID counts (all repositories). |
  | Rename | Updates the organization and every `PackageOwner` row naming it, **in every repository**, in one save. Refused if the name is a user's or another organization's (a change of case is fine), blank, over 256 characters or has `/ \ ? # %`. |
  | Delete | Removes the organization and its memberships. Refused while it owns a package ID in any repository (remove it as an owner first). |
  | Members | Add by user name (optionally administrator), promote/demote, remove. An organization always keeps at least one administrator. |

* **Account deletion by an administrator** is never blocked by ownership. A package ID the user alone owned is either
  *left without owners* (the default: only administrators and the global API key manage it, like IDs that predate accounts)
  or *deleted permanently with all its versions*, in **every** repository the user was sole owner in (the confirmation lists each as `ID (repository)`). The user is removed first; a failure while deleting packages is reported
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

| Key | Default |
| --- | --- |
| `Host`, `From` | none; both required to switch email on |
| `Port` | `587` |
| `EnableSsl` | `true` |
| `UserName`, `Password` | none |

`EnableSsl` uses implicit TLS on port 465 and STARTTLS otherwise; `false` sends in the clear. A send failure is logged and
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

`Site:UseGravatar` defaults to `true`.

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

The validation pipeline is **off** by default (`Validation:Enabled` and `RequireSignedPackages` default to `false`); with it off a push is indexed at once. Turn it on with:

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

Administrators see and decide on stuck submissions of **every repository** at `/admin/validation`
(menu: *Validation queue*):

| Action | Effect |
|---|---|
| Retry | Resets the steps to pending and queues the submission again (needs the staged file) |
| Release | Indexes the quarantined package although validation failed. Needs the administrator's password; logged as a warning and recorded with `IAdminAudit`; the failed verdicts stay on record |
| Reject | Deletes the staged file and the submission's rows. Needs the password; audited |

Release and Reject apply to quarantined submissions only; all three are refused with `503` while the server is read-only for maintenance.

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
advertises the certificate through the `RepositorySignatures/5.0.0` resource. Signing is on while
`RepositorySigningCertificatePath` is set; if `RepositorySigningServiceIndexUrl` is not an absolute `https` URL,
the package fails the final indexing step and is quarantined.

## Catalog

BaGet Extension keeps a [NuGet catalog](https://learn.microsoft.com/nuget/api/catalog-resource), an
append-only log of package changes that mirrors can follow. It needs no configuration. Packages
pushed before the catalog existed are added with:

```
dotnet BaGet.dll catalog rebuild
```

Each repository has its own catalog; `catalog rebuild --repository <name>` rebuilds a repository other than `default`.
Administrators can also start it, and `import downloads`, per repository at `/admin/operations` (menu: *Operations*):
a background job, one run at a time per operation and repository, with its last status (counts or error) kept in
memory until the next start. Starting is refused with `503` while the server is read-only for maintenance.

The catalog is not available with Azure Table Storage.

## Legal pages

`Site:TermsOfUsePath` and `Site:PrivacyPolicyPath` point at Markdown files (absolute, or relative to the
content root). They are served at `/terms` and `/privacy`, and the footer links to each only while its
path is set; BaGet Extension ships no legal text. A page whose path is unset or whose file is missing answers 404.
The path must end in `.md` and contain no `..` segment (the pages are public, so no other file may be served); any
other value is refused when saved on the Settings page and at startup.

```json
{ "Site": { "TermsOfUsePath": "legal/terms.md", "PrivacyPolicyPath": "legal/privacy.md" } }
```

## Private feeds

A repository can require authentication to read: switch off *Anonymous read* on `/admin/repositories/{name}` (or remove
the *Anyone reads every repository* permission target) and anonymous callers get `401` with a Basic challenge, which
NuGet clients answer with an API key as password. See [Access control](access-control.md) for the model, the upgrade
defaults (nothing changes until you edit them) and the client setup. Needs [accounts](#accounts-package-owners-and-api-keys)
for per-user keys, and a relational database.

**Upgrading.** A repository whose *Anonymous read* was saved as off before access control existed becomes private on upgrade (the switch did nothing before). The setting is kept, and a startup Warning names every such repository with no Read grant for anyone but administrators, until you switch it on or grant Read.

## Storage

`Storage:Type` picks where package and symbol content lives (case-insensitive):

Shipped default `FileSystem`; the code has none, and a missing or unknown `Type` fails startup. Cloud
providers require their identifying keys (bucket or container, region, credentials) and fail startup without them.

| `Type` | Keys under `Storage` | Guide |
| --- | --- | --- |
| `FileSystem` (default) | `Path`; empty means the current working directory | [local](installation/local.md) |
| `AzureBlobStorage` | `ConnectionString`, or `AccountName` and `AccessKey`; `Container`; `DownloadUriExpiry` (default 10 minutes) | [Azure](installation/azure.md) |
| `AwsS3` | `Region`, `Bucket`, `Prefix`; `AccessKey` and `SecretKey` (or `UseInstanceProfile`, `AssumeRoleArn`) | [AWS](installation/aws.md) |
| `GoogleCloud` | `BucketName` | [Google Cloud](installation/gcp.md) |
| `AliyunOss` | `AccessKey`, `AccessKeySecret`, `Endpoint`, `Bucket`, `Prefix` | [Alibaba Cloud](installation/aliyun.md) |
| `Null` | none; stores nothing | |

## Search

`Search:Type` picks the search index (shipped default `Database`; a missing or unknown value fails startup):

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
- **ConnectionString**: The connection string for your database engine. Examples:

    | `Type` | Example |
    | --- | --- |
    | `Sqlite` | `Data Source=baget.db` |
    | `SqlServer` | `Server=localhost;Database=BaGet;Trusted_Connection=True;TrustServerCertificate=True` |
    | `PostgreSql` | `Host=localhost;Port=5432;Database=baget;Username=baget;Password=<password>` |
    | `MySql` | `Server=localhost;Port=3306;Database=baget;User=baget;Password=<password>` |
    | `AzureTable` | `DefaultEndpointsProtocol=https;AccountName=<account>;AccountKey=<key>;EndpointSuffix=core.windows.net` |

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

## Logging

The host logs with [Serilog](https://serilog.net), configured only from the `Serilog` section (levels, sinks,
retention); libraries log through `Microsoft.Extensions.Logging` and need no change. The old `Logging` section is
no longer read.

| Where | What |
| --- | --- |
| Console | `[HH:mm:ss LVL] SourceContext: message`, Information and up |
| `logs/baget-YYYYMMDD.log` | same events, daily files, 14 kept, rolls at 50 MB (`baget-YYYYMMDD_001.log`) |
| Windows Application event log | Windows only: Warning and up, source `.NET Runtime` (as before, no administrator right needed) |

- **Reload.** `Serilog:MinimumLevel:Default` and `Override` change **live** when a configuration file reloads
  (`appsettings*.json`, `appsettings.Overrides.json`; environment variables and the command line are read once).
  Sinks, their arguments, `Enabled` and the `EventLog` keys need a **restart**.
- A relative file `path` resolves against the content root (or `BAGET_CONFIG_ROOT`), not the working directory, for
  every `File` sink under `WriteTo` (found by `Name`, keyed by name or by array index). An absolute path is used as is.
  In Docker point it into the volume: `Serilog__WriteTo__File__Args__path=/var/baget/logs/baget-.log`.
  A file sink that cannot write (an unwritable folder under IIS) is not silent: Serilog's self-log goes to stderr.
- **Levels.** Without any configured level the host applies `Default` Information, `Microsoft` Warning,
  `Microsoft.Hosting.Lifetime` Information and `Microsoft.AspNetCore.Hosting.Diagnostics` Warning; the `Serilog` section
  then replaces them one by one (a host with its own `appsettings.json` stays safe). `Override` lowers or raises a
  namespace (`Microsoft.EntityFrameworkCore`, `System`: `Warning`). Quieter, like the previous default:
  `Serilog__MinimumLevel__Default=Warning`. `appsettings.Development.json` uses `Debug`.
- `Microsoft.AspNetCore.Hosting.Diagnostics` at Information logs every full request URL, query string (`?apikey=`)
  included. It stays at Warning unless you lower it explicitly: that is your choice, and then keys reach the log.
- Sinks are named under `WriteTo` (`Console`, `File`), so one can be tuned with an environment variable such as
  `Serilog__WriteTo__File__Args__retainedFileCountLimit=30`. Other sinks need their package referenced by the host.
- **Disable a sink** on any platform with `Enabled=false`: `Serilog__WriteTo__File__Enabled=false` (or
  `Serilog:WriteTo:0:Enabled` for an array entry). An empty `Name` also works but an empty environment variable is
  deleted on Windows (and probably in IIS `web.config`), so use `Enabled`.
- **Windows event log.** The Windows event log gets Warning and up, as `WebApplication.CreateBuilder` did before Serilog.
  `Serilog:EventLog:Enabled=false` turns it off; `Serilog:EventLog:Source` (default `.NET Runtime`, which exists on every
  Windows) must already be registered if changed.
- Request logging: one line per request, `HTTP GET /v3/index.json from <client> responded 200 in 12.3 ms`, after
  forwarded headers so the client is the real one. Level: Warning for 5xx or an exception, Debug for static files,
  Information otherwise. The line has the path only: no query string, headers or body, so API keys never reach the log.
  Quiet only that line: `Serilog__MinimumLevel__Override__Serilog.AspNetCore.RequestLoggingMiddleware=Warning`.
- The Settings page does not list `Serilog` (it is not an options type); edit the file or the environment.

## Telemetry

Off by default. The server emits traces and metrics with `System.Diagnostics` (`ActivitySource` / `Meter` named `BaGet`);
the host exports them over OTLP (OpenTelemetry) only while `Telemetry:Enabled` is true and an endpoint is set. Logs go
out through a Serilog sink. Keys apply **without a restart**, except `ServiceName` and `Metrics:ExportInterval`
(read at startup):

| Key | Default | Meaning |
|---|---|---|
| `Telemetry:Enabled` | `false` | Master switch. Off: spans are not recorded, nothing is queued, no connection is made |
| `Telemetry:ServiceName` | `baget` | `service.name` of every signal, read once at startup (restart) |
| `Telemetry:Otlp:Endpoint` | empty | Traces collector, absolute http(s) URL, for example `http://jaeger:4317`; empty = no traces |
| `Telemetry:Otlp:Protocol` | `Grpc` | `Grpc` (port 4317) or `HttpProtobuf` (4318; `/v1/traces`, `/v1/metrics`, `/v1/logs` is appended) |
| `Telemetry:Otlp:Headers` | empty | `key=value,...` request headers, for an API key (sensitive: masked on the Settings page, never logged or shown) |
| `Telemetry:Otlp:Timeout` | `00:00:10` | Per-export timeout |
| `Telemetry:Traces:Enabled` / `SamplingRatio` / `Database` | `true` / `1.0` (0 to 1) / `true` | Trace export, share of new traces kept (a trace sampled upstream is kept), database command spans |
| `Telemetry:Metrics:Endpoint` / `ExportInterval` | empty / `00:01:00` | Metrics collector (empty = not exported) and period (restart) |
| `Telemetry:Logs:Endpoint` / `MinimumLevel` | empty / `Information` | Logs collector (empty = not exported) and lowest Serilog level |

An invalid endpoint (not absolute http or https), protocol, level, a sampling ratio outside 0 to 1 or a non-positive
duration stops the server at startup.

**Jaeger** accepts traces only (its OTLP receiver answers `Unimplemented` to metrics and logs), so metrics and logs have
their own endpoints, for example an OpenTelemetry Collector. A typical setup:

```
Telemetry__Enabled=true
Telemetry__Otlp__Endpoint=http://jaeger:4317
```

A push then shows in Jaeger (UI on 16686) as one trace: the ASP.NET Core request, `package.index`, and the database commands.
See the [Docker example](installation/docker.md#tracing-with-jaeger).

**Traced:** incoming requests (static files excluded), outgoing HTTP calls (mirror, upstream), database commands
(`Telemetry:Traces:Database`; the SQL text, also under the newer database semantic conventions, is removed from the spans before export), the spans `package.index`, `package.validate`, `mirror.fetch`,
`database.copy` (one `database.copy.table` child per table). **Metrics:** `baget.packages.pushed`, `baget.downloads`,
`baget.validation.results`, plus ASP.NET Core, HTTP client and .NET runtime metrics.

**Never exported:** query-string values (the instrumentation records `apikey=Redacted`), request headers (such as
`X-NuGet-ApiKey`), SQL text, connection strings; the OTLP `Headers` value. Spans and metrics carry no user names or email addresses. Logs
reach the collector exactly as Serilog would write them, so they **do** include whatever the application logs, such as the
audit lines naming the acting administrator and the affected user (settings changes, administrator promotion, password
resets, account deletion); point `Logs:Endpoint` only at a collector you trust with that. The
`Microsoft.AspNetCore.Hosting.Diagnostics` Warning floor still keeps request URLs out; each log event carries `TraceId` / `SpanId` of the request it was written in.

**Never blocks, never throws into a request.** Spans wait in a bounded queue (2048) and are dropped when the collector is
down; export runs on a background thread with the `Otlp:Timeout`. Failures are kept in `TelemetryStatus` (per signal: last
attempt, last success, failing since, scrubbed last error without headers or URL paths). `TelemetryConnectionTester`
sends one test span to the settings given, saving nothing.

**On the Settings page** (a host that does not export telemetry, such as an embedded one, shows a note that these settings have no effect there; they stay editable for a host sharing the file) the Telemetry section starts with a status table, one row per signal: *Off* (telemetry or the
signal is disabled), *Not configured* (no endpoint), *Configured, nothing exported yet*, *Last export ok at ...* or
*Failing since ...* with the scrubbed error (and the spans dropped when the queue was full). **Test connection** sends one
test span (10 s timeout) to the **endpoint, protocol and headers currently typed in the form**, saved or not, and shows
*Connection ok* or the scrubbed error; nothing is saved and telemetry is not switched on. It asks for your password like a
save, is for administrators only, and a blank *Headers* field tests with the saved headers (tick *Clear* to test with none).

Changing the endpoint, protocol, headers or timeout rebuilds the exporter in place; the old one finishes its running export
first and no queued span is lost. Any configuration reload fires this check, but an exporter whose settings did not change
is kept.

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

- Open **Server settings** in the account menu (needs `Accounts:Enabled`; administrators only, anyone else gets a 404). The left list picks the
  section shown (the first, *General*, on load); the others are hidden. The server renders the chosen section: the one that failed to save, else
  *Telemetry* after *Test connection*, else `?section=<id>`, else the first. In the browser a click switches without a reload and sets the URL
  fragment (`#<id>`, kept on reload and back); links work without JavaScript. A section id is its key (`Database`), `general` for the top-level
  settings. The page has one form per
  section, each with its own **Save** button (a save returns to `?section=<id>#<id>`); saving asks for your password (an account without one signs in
  again with its provider instead).
- Each setting shows its key, its description, and badges: **Restart required**, **Saved override** (tick *Reset to
  default* to remove it) and **Overridden by environment variable / command line** (the saved value then has no effect).
- A secret field is blank: leave it empty to keep the value, type to replace it, tick *Clear* to empty it.
- Only changed values are saved. A value that fails to parse or validate is listed above its section and nothing in
  that section is saved. Settings of other kinds (maps) are shown read-only.
- `Database:Type`, `Storage:Type` and `Search:Type` are drop-downs of the allowed values (matched ignoring case; a
  current value that is empty or not in the list is kept as the selected entry, so saving never changes it). A value
  outside the list is refused when saved, and at startup.
- `Database:ConnectionString` shows an example for the selected `Database:Type` as a placeholder (only while no value
  is stored) and as an "Example for ..." line; both change when you pick another type. The example is never put in
  the field's value.
- A setting no file sets shows the built-in default (for example `Feeds:EnableV2` is ticked) and can be changed from it.
- If the overrides file cannot be written (a read-only content root) or is not valid JSON, the page shows the error,
  naming the path, instead of failing; point `SettingsOverridesPath` at a writable file.

## Read-only maintenance gate

While a maintenance job (such as a database copy) runs, BaGet Extension makes the database read-only with an
in-memory gate (`MaintenanceGate`). It is not a setting and is never saved: a restart opens it.

| While the gate is closed | Behaviour |
| --- | --- |
| Any other database write (push, delete, unlist, sign-up, failed sign-in, API keys, owners, abuse reports, Settings page save) | refused with **HTTP 503** and `Retry-After: 60` |
| Reads: feeds, search, downloads, pages | work; a download is served but not counted |
| Pushes (also with `Validation:Enabled`, which stages the file) and catalog entries | refused before any file reaches storage, so a retry after the gate opens is clean |
| Sign-in | a correct password completes: the failed-attempt counter reset (an incidental write) is skipped, so an administrator can sign in after a copy to see the status and restart instructions. A wrong password, sign-up and every other write stay refused (counting a failed attempt is a write) |
| Validation worker | logs a warning, keeps the submission pending and retries every 30 s |
| Pages | show a "Read-only" banner with the reason |

- After a job that needs a restart (a successful database copy) the gate is closed **until restart**: writes accepted
  before the restart would land in the old database and be lost.
- Every write goes through EF `SaveChanges`, which an interceptor checks; a context built with
  `MaintenanceGate.AllowWrites(optionsBuilder)` is exempt, so the job holding the gate can write to its own target.

## Moving to another database

On the Settings page, changing `Database:Type` or `Database:ConnectionString` between two **relational** databases
(SQLite, SQL Server, PostgreSQL, MySQL, in any direction, also the same engine on another server) shows
**Copy all data to the new database** (ticked). With it ticked, Save does not save: it queues a copy, and the settings
are saved only after the copy is verified. Unticked, or with AzureTable on either side (no option shown), Save behaves
as before.

| Step | Detail |
| --- | --- |
| 1. Read-only | The [gate](#read-only-maintenance-gate) closes when the copy is accepted (pushes etc. get 503). |
| 2. Source | Checked first, before the target is created, migrated or changed. Refused if it has pending migrations or holds what the target cannot, judged per target engine and column: strings longer than the model's maximum length (SQL Server, PostgreSQL, MySQL; SQLite does not enforce lengths), values of a unique index or key (primary or alternate) differing only by case where the target column compares case-insensitively (SQL Server and MySQL by default; any `COLLATE NOCASE` / `citext` / `*_ci` column; not SQLite or PostgreSQL by default), local-time dates (PostgreSQL). All problems are listed at once, before anything is copied. |
| 3. Target | Must be reachable (a new SQLite file or a database the login may create is created) and, after its schema is migrated, **every table empty**. A current target that holds rows is refused and nothing is changed; one that needed migrating and then proves not empty keeps its updated schema, and the refusal says so. |
| 4. Copy | Tables in foreign-key order (Identity's included), 1000 rows per batch, original keys kept; SQL Server `IDENTITY_INSERT` and PostgreSQL sequences are handled, so keys generated later continue after the highest copied one. |
| 5. Verify | Row counts and an order-independent checksum of the keys (engines sort text differently) must match per table. |
| 6. Save | `Database:Type`, `Database:ConnectionString` (and `MySqlServerVersion` if changed) are saved. The gate **stays closed until you restart** BaGet: writes accepted before the restart would land in the old database. |

- On any failure before step 6 (also a shutdown while the settings are being written, before the file is replaced) the rows copied are deleted from the target (never from the source), the gate reopens, no setting is
  saved and the page lists the errors; fix the cause and retry. A crash mid-copy leaves a partly filled target that must
  be emptied before a retry (a non-empty target is refused).
- A failure **after** step 6 began (saving the settings) never deletes the target: the verified copy is kept, the gate stays closed until restart and the page reports the error; check the `Database` settings before restarting.
- The source is never modified or deleted: to roll back, restore the old `Database` settings.
- Package files are not copied: they live in `Storage`, which is unchanged.
- One copy at a time; while it runs, every Settings save is refused with 503. Progress shows on the Settings page
  (also as JSON at `/admin/settings?handler=CopyStatus`, administrators only).
- Connection strings are never logged or shown in errors.
