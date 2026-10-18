# Plan — Administrator role and server Settings UI

## 0. Blocking verification (before coding)

| # | Check | How | If it fails |
|---|---|---|---|
| V1 | **Restart classification.** For every options type, decide whether a change applies live or needs a restart. Live = only read through `IOptionsSnapshot<T>`/`IOptionsMonitor<T>`. Restart = read at registration (`configuration.GetSection(...).Get<T>()`, e.g. `AccountsServiceCollectionExtensions.cs:22-26,49`), read once in `Program`/pipeline setup, or consumed through `IOptions<T>` (singleton, never reloads; 11 files). | `grep -rn "IOptions<\|GetSection(\|\.Get<" src` and the `IProvider<T>` chains (`DependencyInjectionExtensions.Providers.cs`). Expected restart: `Accounts`, `Email`, `Site:PublicUrl`, `Database`, `Storage`, `Search`, `PathBase`, `AllowedHosts`, `Urls`, `Kestrel`, `IISServerOptions`, `RunMigrationsAtStartup`. Verify `Feeds`, `Mirror`, `Validation`. | The result becomes the `[RestartRequired]` attributes (§3.1); anything unsure → mark restart. |
| V2 | **Source order in tests.** Where `WebApplicationFactory` puts `UseSetting` values relative to the overrides file. | Debug-print `((IConfigurationRoot)config).Providers` in one test. | Integration tests that save a setting must use keys the harness does not set (`BaGetApplication.cs:62-71`). |
| V3 | XML doc files of `BaGet.*` projects reach the output folder (descriptions come from them). | `ls src/BaGet/bin/Debug/net10.0/BaGet.Core.xml` after build; also `dotnet publish`. | Add `<CopyDocumentationFilesFromPackages>`/`AllowedReferenceRelatedFileExtensions` with `.xml` in `src/BaGet/BaGet.csproj`. |

## 1. Decisions (user)

| Decision | Consequence |
|---|---|
| Saved settings go to **`appsettings.Overrides.json`**, loaded after `appsettings*.json` and **before** environment variables and the command line, with `reloadOnChange` | Live settings take effect at once. Restart-only settings are marked in the UI. An environment variable or CLI argument still wins, and the UI says so. `appsettings.json` and its comments stay untouched. The config root must be writable, otherwise saving shows an error. |
| First admin comes from **`Accounts:InitialAdministrators`** (promoted at startup) **plus `account promote <user>`** on the CLI | Only accounts that already exist get promoted (a name can't be claimed by registering it later). Taking a name out of the list doesn't demote anyone. |
| **Every documented setting can be edited; secrets are write-only** | Secrets show as `••••`. A blank field keeps the current value; "Clear" sets it to empty. |
| **Promote and demote are allowed; the last admin can't be demoted** (also can't delete their account) | The flag only unlocks `/admin/settings` and `/admin/users`. It gives no rights over packages (`SessionPrincipalFactory.cs:39-44` stays `isSuperuser: false`). |
| Not in scope | A page for package verification (`docs/configuration.md:360`). |

## 2. Admin role

### 2.1 Entity — `src/BaGet.Core/Entities/Accounts.cs:10-13`
```csharp
public class BaGetUser : IdentityUser
{
    public DateTime Created { get; set; }

    /// <summary>
    /// May change the server's settings and promote or demote administrators. It grants
    /// nothing over packages: ownership rules apply to an administrator as to anyone.
    /// </summary>
    public bool IsAdministrator { get; set; }
}
```
Migration **`AddAdministrators`** in each of `BaGet.Database.{Sqlite,SqlServer,PostgreSql,MySql}` (`dotnet ef migrations add AddAdministrators --context <X>Context` in the project folder). This adds a non-null bool column with default `false` to `AspNetUsers`. All four must report no pending model changes.

### 2.2 `AccountsOptions` — `src/BaGet.Core/Configuration/AccountsOptions.cs:24`
```csharp
/// <summary>
/// User names promoted to administrator at startup, separated by semicolons. Only
/// existing accounts are promoted, so a listed name cannot be claimed by registering it;
/// removing a name does not demote. See also the "account promote" command.
/// </summary>
[RestartRequired]
public string InitialAdministrators { get; set; }
```

### 2.3 `src/BaGet.Identity/AdministratorService.cs` (new; registered `TryAddScoped` in `AccountsServiceCollectionExtensions.cs:35`)
```csharp
public enum AdministratorChange { Changed, Unchanged, UserNotFound, LastAdministrator }

/// <summary>
/// Reads and changes <see cref="BaGetUser.IsAdministrator"/>. Read from the database on
/// every check, so a demotion takes effect on the next request.
/// </summary>
public class AdministratorService
{
    private readonly UserManager<BaGetUser> _users;
    private readonly IContext _context;
    private readonly ILogger<AdministratorService> _logger;

    // ctor with null checks, as elsewhere

    public async Task<bool> IsAdministratorAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        var id = _users.GetUserId(principal);
        return !string.IsNullOrEmpty(id)
            && await _context.Users.AnyAsync(u => u.Id == id && u.IsAdministrator, cancellationToken);
    }

    public Task<AdministratorChange> PromoteAsync(string userName, string actor, CancellationToken ct) => SetAsync(userName, true, actor, ct);

    public Task<AdministratorChange> DemoteAsync(string userName, string actor, CancellationToken ct) => SetAsync(userName, false, actor, ct);

    public Task<bool> IsLastAdministratorAsync(BaGetUser user, CancellationToken ct)
        => user.IsAdministrator
            ? _context.Users.CountAsync(u => u.IsAdministrator, ct).ContinueWith(t => t.Result <= 1, ct)
            : Task.FromResult(false);

    public async Task PromoteInitialAdministratorsAsync(string names, CancellationToken ct)
    {
        foreach (var name in (names ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (await SetAsync(name, true, "configuration", ct) == AdministratorChange.UserNotFound)
                _logger.LogWarning("Initial administrator {User} does not exist and was not promoted", name);
        }
    }

    private async Task<AdministratorChange> SetAsync(string userName, bool value, string actor, CancellationToken ct)
    {
        var user = await _users.FindByNameAsync(userName);
        if (user == null) return AdministratorChange.UserNotFound;
        if (user.IsAdministrator == value) return AdministratorChange.Unchanged;
        if (!value && await IsLastAdministratorAsync(user, ct)) return AdministratorChange.LastAdministrator;

        user.IsAdministrator = value;
        var result = await _users.UpdateAsync(user); // concurrency stamp rejects a racing edit of the same user
        if (!result.Succeeded) throw new InvalidOperationException(string.Join(" ", result.Errors.Select(e => e.Description)));

        _logger.LogWarning("{Actor} {Action} {User} as administrator", actor, value ? "promoted" : "demoted", userName);
        return AdministratorChange.Changed;
    }
}
```
(Write `IsLastAdministratorAsync` with `async`/`await`. The `ContinueWith` above is shorthand.)

### 2.4 Startup and CLI — `src/BaGet/Program.cs`
- `:126-130`: run `PromoteInitialAdministratorsAsync(Accounts:InitialAdministrators)` inside a scope after `RunMigrationsAsync`. Skip it when `AdministratorService` isn't registered (accounts disabled).
- `:85-122`, next to `reset-password`: add `account promote <user>` and `account demote <user>`. Same scope/error pattern. Exit 1 for `UserNotFound`, `LastAdministrator`, or accounts disabled.

### 2.5 Last-admin guard on account deletion
`AccountDeletionService` / `Pages/Account/Settings.cshtml.cs` (the "why the account cannot be deleted yet" reason): add *"You are the last administrator. Promote another user first."* via `IsLastAdministratorAsync`.

### 2.6 `src/BaGet.Identity/Pages/Admin/AdminPageModel.cs` (new)
```csharp
/// <summary>
/// Base of the administrator pages: signed in, accounts enabled (see <see cref="AccountPageModel"/>),
/// and an administrator. Anyone else gets 404, so the pages' existence is not disclosed.
/// </summary>
public abstract class AdminPageModel : AccountPageModel
{
    protected override bool RequiresSignIn => true;

    protected AdministratorService Administrators
        => HttpContext.RequestServices.GetRequiredService<AdministratorService>();

    public override async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        OnPageHandlerExecuting(context);
        if (context.Result != null) return;
        if (!await Administrators.IsAdministratorAsync(User, HttpContext.RequestAborted))
        {
            context.Result = NotFound();
            return;
        }
        OnPageHandlerExecuted(await next());
    }
}
```

### 2.7 Users page — `Pages/Admin/Users.cshtml(.cs)` (`/admin/users`)
- GET: `q` (user-name contains), `page` (50 per page, ordered by `UserName`). Columns: avatar, name, email, created, `Administrator` badge.
- `OnPostPromoteAsync(string userName, string password)` / `OnPostDemoteAsync(...)`: require `IsConfirmedAsync(currentUser, password)` (`AccountPageModel.cs:148`). Map `AdministratorChange` to `Message`/`Error`. PRG redirect.
- Accounts without a password get the existing `_Reauthenticate` partial, the same way `Account/Settings` does.

### 2.8 Menu — `src/BaGet.Web/Pages/Shared/_Layout.cshtml:76-83`
Resolve `AdministratorService` with `Context.RequestServices.GetService` (null while accounts are off), the same way the avatar is resolved at `:9-15`. If the user is admin, add a divider plus **Server settings** (`/Admin/Settings`) and **Users** (`/Admin/Users`).

## 3. Settings

### 3.1 Metadata — `src/BaGet.Core/Configuration/Settings/` (new)
```csharp
/// <summary>The value is a credential: shown masked, never logged.</summary>
[AttributeUsage(AttributeTargets.Property)] public sealed class SensitiveSettingAttribute : Attribute { }

/// <summary>Read once at startup; a change takes effect after a restart.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property)] public sealed class RestartRequiredAttribute : Attribute { }

/// <summary>An options type bound to a configuration section; recorded by AddBaGetOptions.</summary>
public sealed record SettingsSchemaRegistration(Type OptionsType, string Section);
```
- `[SensitiveSetting]` goes on: `BaGetOptions.ApiKey`, `DatabaseOptions.ConnectionString`, `SmtpOptions.Password`, `AccountsOpenIdConnectOptions.ClientSecret`, and every storage, search or table key, secret or connection string in Azure/AWS/GCP/Aliyun options.
- `[RestartRequired]` follows V1.
- `DependencyInjectionExtensions.cs:42-59` `AddBaGetOptions<T>` also does `services.AddSingleton(new SettingsSchemaRegistration(typeof(T), key))`. Every options type, including provider-specific ones (`AzureApplicationExtensions.cs:22,82,133`, AWS, GCP, Aliyun, `IISServerOptions` at `Program.cs:195`), then shows up without being listed by hand.

### 3.2 `SettingsCatalog` (singleton)
- Built from all `SettingsSchemaRegistration`s by reflection over public read/write properties:
  - Root registration (`key == null`, `BaGetOptions`): scalar properties only. Its nested objects are already covered by their own registrations.
  - Nested complex types (e.g. `Email:Smtp`, `Accounts:OpenIdConnect`): recurse with the key prefix.
  - Two types that share a key (e.g. `Storage:Path`): merge into one descriptor.
- `SettingKind`: `Boolean, Integer, Number, String, Enum, TimeSpan, StringList, ReadOnly`. Dictionaries and other unsupported types become `ReadOnly`; they're shown but can't be edited.
- `SettingDescriptor(string Key, string Section, string Group, SettingKind Kind, Type ClrType, string[] EnumValues, bool IsSensitive, bool RequiresRestart, string Description)`. `Group` = type name without `Options` (e.g. `AzureBlobStorage`).
- `Description` comes from `XmlDocumentation.GetSummary(PropertyInfo)`: reads `<assembly>.xml` beside the assembly (`P:Ns.Type.Prop`), whitespace collapsed, `<see cref>` replaced by its last segment, cached. Missing file → empty string.
- `DocsAnchor(section)` maps each section to its `docs/configuration.md` heading, with a fallback to the page top.

### 3.3 `SettingsOverridesStore` (singleton; registered in `Program.ConfigureServices`)
```csharp
/// <summary>
/// The JSON file the Settings page writes. Loaded after appsettings*.json and before
/// environment variables and the command line, so those still win.
/// </summary>
public class SettingsOverridesStore
{
    public const string PathKey = "SettingsOverridesPath";
    public const string DefaultFileName = "appsettings.Overrides.json";
    private readonly SemaphoreSlim _lock = new(1, 1);

    public SettingsOverridesStore(string filePath) => FilePath = filePath;
    public string FilePath { get; }

    /// <summary>Absolute path: the "SettingsOverridesPath" setting, else the default file, relative to <paramref name="basePath"/>.</summary>
    public static string ResolvePath(IConfiguration configuration, string basePath)
        => Path.GetFullPath(configuration[PathKey] is { Length: > 0 } p ? p : DefaultFileName, basePath);

    /// <summary>Flattened "A:B" keys present in the file.</summary>
    public IReadOnlyDictionary<string, string> Read();

    /// <summary>
    /// Applies <paramref name="changes"/> (null value removes the override, a list replaces the
    /// whole array) and writes atomically: temp file in the same folder, then File.Move(overwrite: true).
    /// Typed JSON from the descriptor (bool, number, array); empty parent objects are pruned.
    /// </summary>
    public Task WriteAsync(IReadOnlyDictionary<SettingDescriptor, object> changes, CancellationToken ct);
}
```
Implemented with `System.Text.Json.Nodes.JsonObject`, case-insensitive key match (configuration is case-insensitive).

### 3.4 Config source — `src/BaGet/Program.cs:159-167`
```csharp
private static void ConfigureConfiguration(ConfigurationManager configuration, IWebHostEnvironment environment)
{
    var root = Environment.GetEnvironmentVariable("BAGET_CONFIG_ROOT");
    if (!string.IsNullOrEmpty(root)) configuration.SetBasePath(root);

    // The Settings page's file: after appsettings*.json, before environment variables and
    // the command line, so an operator's explicit environment still wins.
    var path = SettingsOverridesStore.ResolvePath(configuration, string.IsNullOrEmpty(root) ? environment.ContentRootPath : root);
    Directory.CreateDirectory(Path.GetDirectoryName(path));
    var source = new JsonConfigurationSource { Path = Path.GetFileName(path), Optional = true, ReloadOnChange = true,
        FileProvider = new PhysicalFileProvider(Path.GetDirectoryName(path)) };
    var index = configuration.Sources.ToList().FindLastIndex(s => s is JsonConfigurationSource) + 1;
    configuration.Sources.Insert(index, source);
    OverridesPath = path; // static, consumed by ConfigureServices: services.AddSingleton(new SettingsOverridesStore(path))
}
```
- Pass `builder.Environment` at `:148`. Pass the path to `ConfigureServices` as a parameter, not a static.
- Test harness `tests/BaGet.Tests/Support/BaGetApplication.cs:62`: add `{ "SettingsOverridesPath", Path.Combine(tempPath, "appsettings.Overrides.json") }`.

### 3.5 `SettingsService` (singleton, `BaGet.Core`; resolved eagerly in `ConfigurePipeline` so the startup snapshot is taken at boot)
| Member | Behaviour |
|---|---|
| `IsAvailable` | `SettingsOverridesStore` is registered. |
| `GetState()` | Per descriptor: `EffectiveValue` (`config[key]`; for sensitive keys only `HasValue`), `IsOverridden` (key is in the store), `ShadowedBy` (first provider, scanning `IConfigurationRoot.Providers` in reverse, whose `TryGet` hits and which sits **after** the overrides provider: "environment variable" / "command line" / "host setting"), `PendingRestart` (restart key whose value differs from the startup snapshot). |
| `SaveAsync(changes, actor, ct)` | 1. Build a candidate overrides set. 2. Validate it on `new ConfigurationRoot(providers with the overrides provider swapped for a MemoryConfigurationProvider of the candidate)`: for each registration, bind a new instance and run the registered `IValidateOptions<T>`. Report only failures **not already present** with the current configuration, so an unused cloud provider's options can't block a save. 3. `store.WriteAsync`. 4. `((IConfigurationRoot)configuration).Reload()` so the change is visible right away. 5. Log `"{Actor} changed settings {Keys}"` with keys only, never values. Returns `SettingsSaveResult(bool Succeeded, IReadOnlyList<string> Errors)`. |

### 3.6 Settings page — `src/BaGet.Identity/Pages/Admin/Settings.cshtml.cs` (`/admin/settings`)
```csharp
/// <summary>
/// Edits every registered setting. Values are written to the overrides file; see
/// <see cref="SettingsService"/>. One form per section, so one section's errors never block another.
/// </summary>
public class SettingsModel : AdminPageModel
{
    private SettingsService Settings => HttpContext.RequestServices.GetRequiredService<SettingsService>();

    public IReadOnlyList<IGrouping<string, SettingState>> Sections { get; private set; }
    public bool HasPassword { get; private set; } = true;
    public bool Reauthenticated { get; private set; }
    public bool CanReauthenticate => Accounts.OpenIdConnect.IsConfigured;
    public string OverridesPath => Settings.OverridesPath;
    public IReadOnlyList<string> PendingRestart { get; private set; }
    [TempData] public string Message { get; set; }
    public IReadOnlyList<string> Errors { get; private set; } = Array.Empty<string>();
    public string ErrorSection { get; private set; }

    public async Task<IActionResult> OnGetAsync() { if (!Settings.IsAvailable) return NotFound(); await LoadAsync(); return Page(); }

    public async Task<IActionResult> OnPostAsync(string section, string password)
    {
        var user = await UserManager.GetUserAsync(User);
        if (!await IsConfirmedAsync(user, password)) { Errors = new[] { "Confirm with your password." }; ErrorSection = section; await LoadAsync(); return Page(); }

        var (changes, parseErrors) = SettingsForm.Parse(Request.Form, Settings.GetState().Where(s => s.Descriptor.Section == section));
        var result = parseErrors.Count == 0 ? await Settings.SaveAsync(changes, user.UserName, HttpContext.RequestAborted) : SettingsSaveResult.Failed(parseErrors);
        if (!result.Succeeded) { Errors = result.Errors; ErrorSection = section; await LoadAsync(); return Page(); }

        Message = changes.Count == 0 ? "Nothing changed." : $"Saved {changes.Count} setting(s).";
        return RedirectToPage(null, null, null, section); // fragment = section
    }
}
```
`SettingsForm.Parse` (internal static, unit-tested) reads these form fields:

| Field | Meaning |
|---|---|
| `v:<key>` | Value. Bool: checkbox `true` plus hidden `false` (true wins). Integer/Number/TimeSpan: invariant culture. Enum: must be one of `EnumValues`. StringList: textarea, one entry per line, blanks dropped. |
| `reset:<key>` | Removes the override (null change). |
| `clear:<key>` | Sensitive only: sets `""`. A blank `v:` for a sensitive key means unchanged. |

Only values that differ from the effective value are emitted. A parse failure → `"<key>: <reason>"`.

### 3.7 Settings view — `Settings.cshtml` (Bootstrap 3, as `_Layout`)
```cshtml
@page "/admin/settings"
@model BaGet.Identity.Pages.Admin.SettingsModel
@{ ViewData["Title"] = "Server settings"; }
<h1>Server settings</h1>
<p class="text-muted">Saved to <code>@Model.OverridesPath</code>. Environment variables and command-line arguments override saved values.</p>
@if (Model.Message != null) { <div class="alert alert-success">@Model.Message</div> }
@if (Model.PendingRestart.Count > 0) { <div class="alert alert-warning">Restart BaGet to apply: @string.Join(", ", Model.PendingRestart)</div> }
<div class="row">
  <nav class="col-md-3"><ul class="nav nav-pills nav-stacked settings-nav">
    @foreach (var s in Model.Sections) { <li><a href="#@s.Key">@(s.Key.Length == 0 ? "General" : s.Key)</a></li> }
  </ul></nav>
  <div class="col-md-9">
  @foreach (var s in Model.Sections)
  {
    <form method="post" id="@s.Key" class="panel panel-default">
      <div class="panel-heading"><strong>@(s.Key.Length == 0 ? "General" : s.Key)</strong>
        <a class="pull-right" href="@SettingsCatalog.DocsAnchor(s.Key)" target="_blank" rel="noopener">Docs</a></div>
      <div class="panel-body">
        @if (Model.ErrorSection == s.Key) { <div class="alert alert-danger"><ul>@foreach (var e in Model.Errors) { <li>@e</li> }</ul></div> }
        @foreach (var g in s.GroupBy(x => x.Descriptor.Group))
        {
          <fieldset><legend>@g.Key</legend>
          @foreach (var f in g) { <partial name="_SettingField" model="f" /> }
          </fieldset>
        }
        <input type="hidden" name="section" value="@s.Key" />
        @if (Model.HasPassword) { <div class="form-group"><label>Your password</label><input type="password" name="password" class="form-control" autocomplete="current-password" required /></div> }
        else { <partial name="../Account/_Reauthenticate" model="Model" /> }
        <button type="submit" class="btn btn-primary">Save @(s.Key.Length == 0 ? "General" : s.Key)</button>
      </div>
    </form>
  }
  </div>
</div>
```
`_SettingField.cshtml` (model `SettingState`):
- Label `<code>Key</code>` and help text `Description`.
- Badges: `Restart required` (`label-warning`); `Saved override` (`label-info`) plus a `reset:` checkbox "Reset to default"; `Overridden by {ShadowedBy}` (`label-danger`, with the note that the saved value has no effect).
- Input by kind: checkbox plus hidden `false`; `type=number` (`step=any` for Number); `<select>` for Enum; `<textarea>` for StringList; `type=password placeholder="•••• unchanged"` plus a `clear:` checkbox for sensitive keys; plain text otherwise; ReadOnly shows `<pre>` with "Edit the configuration file".
- `id`/`for` derived from the key with `:` replaced by `_`.

Small CSS in `src/BaGet.Web/wwwroot/css/site.css`: `.settings-nav { position: sticky; top: 70px }`.

## 4. Tests (`BaGet.Core.Tests`, `BaGet.Tests`; `InternalsVisibleTo` already present)
| Test | Asserts |
|---|---|
| `SettingsCatalogTests` | Every public settable property of every registered options type has a descriptor. Every property whose name matches `(?i)password|secret|apikey|connectionstring|accesskey|accountkey|credential` carries `[SensitiveSetting]`. `ApiKey` has a non-empty description. Kinds map correctly. |
| `SettingsOverridesStoreTests` | Nested write and merge. Remove prunes empty parents. A shorter list leaves no stale indexes. Typed JSON. Atomic replace (no temp left behind). |
| `SettingsFormTests` | Each kind parses; invalid int/enum is reported; sensitive blank = unchanged; clear; reset. |
| `SettingsServiceTests` | Shadowing by a memory provider added after overrides. Validation reports only new failures. `PendingRestart` after a restart key changes. |
| `AdministratorTests` (integration, accounts on) | Non-admin and anonymous get 404 on `/admin/settings` and `/admin/users`; accounts off → 404. Promote/demote with password; wrong password rejected; demoting the last admin refused; deleting the last admin's account refused. Admin saves `AllowPackageOverwrites=true` → file contains it and a push overwrite succeeds without a restart. Secret value absent from the HTML. Menu links only for admins. `PromoteInitialAdministratorsAsync` promotes existing users and ignores missing ones. |
| Migrations | `dotnet ef migrations list` per provider; the existing pending-model check passes. |

## 5. Documentation (same commits as the code)
| File | Change |
|---|---|
| `docs/configuration.md` | New `### Administrators` under Accounts (`InitialAdministrators`, `account promote/demote`, last-admin rule, no package rights). New `## Settings page` (overrides file, precedence, `SettingsOverridesPath`, restart badges, masked secrets, writable config root). Add the new key to "Top-level keys". |
| `src/BaGet/appsettings.json` | Commented `"InitialAdministrators": ""` under `Accounts`. |
| `docs/installation/docker.md` | Mount a volume for the overrides file (`SettingsOverridesPath`). |
| `CLAUDE.md` | Test counts; overrides file in the configuration notes. |
| XML docs | On every new type and member (shown in the UI). |

## 6. Execution (workflow, git worktrees, ≤5 agents)
| Phase | Worktree / branch | Agent | Content |
|---|---|---|---|
| 1a | `../BaGet-wt-admin` / `wt/admin` | Sonnet | §2.1–2.8 (except the menu's Settings link target, which can exist before the page), migrations, admin tests, docs. |
| 1b | `../BaGet-wt-settings` / `wt/settings` | Sonnet | V1, V3, §3.1–3.5, harness path, unit tests, docs. |
| 2 | Rebase `wt/admin` then `wt/settings` onto `main`, `merge --ff-only` | orchestrator | Resolve conflicts (`Program.cs`, `configuration.md`, `CLAUDE.md`, harness); full build and tests. |
| 3 | `../BaGet-wt-ui` / `wt/ui` | Sonnet | §3.6–3.7, CSS, settings integration tests, docs. Then rebase and ff-merge. |
| 4 | main | Opus review → Sonnet fix (≤2 rounds) | Full diff review; build and all tests green. |
| 5 | — | orchestrator | Remove worktrees and branches; delete this plan; commit. |

Each phase builds and tests with `-p:GeneratePackageOnBuild=false` and commits in its worktree.

## 7. Risks
| Risk | Mitigation |
|---|---|
| Two admins demote each other at the same moment → zero admins | Rare. Recover with `account promote` or `InitialAdministrators` at the next start. |
| Config root is read-only (containers) | The save shows the IO error; docs say to mount `SettingsOverridesPath`. |
| A restart-only setting is misclassified as live | V1. When in doubt, mark restart. |
| The UI can break the server (bad `Database:*`) | Validation before saving; restart-only changes apply at the next start; to recover, delete or edit the overrides file. |
| Secrets stored in plain text in the overrides file | Same as `appsettings.json`; documented. |

---

# Part B — Admins reset passwords, manage any package, delete accounts

Status: Part A (sections 0–7) is implemented and merged (`1515d26..924e426`); its worktrees and branches are removed.

## B0. Blocking verification
| # | Check | How |
|---|---|---|
| BV1 | `SessionPrincipalFactory.Create` (`SessionPrincipalFactory.cs:29`) has a single caller, `Upload.cshtml.cs:444`. It becomes async. | `grep -rn "SessionPrincipalFactory" src tests` |
| BV2 | `IPackageDeletionService` has one implementation (`PackageDeletionService.cs:10`), plus any test doubles. | `grep -rn "IPackageDeletionService" src tests` |
| BV3 | How `ForgotPassword.cshtml.cs` builds the reset-link template (`UserIdPlaceholder`/`CodePlaceholder`, `EmailLinkPolicy`), so it can be moved and not copied. | Read `ForgotPassword.cshtml.cs:1-100` |

## B1. Decisions
| Decision (user) | Consequence |
|---|---|
| Password reset: **generated password and an email link** | *Generate* reuses `AccountRecoveryService.ResetPasswordAsync` (`AccountRecoveryService.cs:32`): 2FA off, lockout lifted, the user's sessions end (security stamp). *Email link* only shows while `Email.IsConfigured` and the user has an address. |
| Deletion covers **packages and accounts** | Admin deletes any version, or a whole ID, and any other account. |
| **Web UI and API** | Admin sessions and admin users' API keys skip **ownership**. |
| **Admin can always hard-delete** | A *Delete permanently* button for admins in the web UI even when `PackageDeletionBehavior=Unlist`. The NuGet `DELETE` API keeps following the server setting (the protocol has no hard flag). |

| Decision (mine, flagged — change before implementing if wrong) | Why |
|---|---|
| Admin keys **still obey their scopes and glob**; only ownership is skipped. Not `IsSuperuser`. | A push-only CI key an admin made must stay push-only. A full superuser would ignore scopes (`BaGetPrincipal.cs:50-52`). |
| Admin pushing a new ID still becomes its owner (`OwnershipAuthorizationService.cs:71`) | Same as any user. |
| Admin deletion of a user who is the **sole owner** of IDs: radio buttons, default *leave without owners* (only admins and the global key can manage them, like IDs that predate accounts), or *delete permanently (all versions)*. | Owner deletions are blocked (`AccountDeletionService.cs:51-68`); an admin needs a way through. |
| Org blockers when an admin deletes: if the user is the last org admin and others remain, the longest-standing member (lowest `Key`) becomes org admin. If the user is the only member and the org owns packages, the org is kept without members. | Admins have no organization UI; deletion must not get stuck. |
| An admin can't delete or reset their **own** account from the admin pages | Self-service stays on `/account/settings` (password confirmation and the last-admin guard). |
| Not in scope: an admin toggle for *verified* (`docs/configuration.md` "Verified packages") and a CLI `account delete` | Not requested. |

## B2. Admin bypasses ownership (API and session)
- `src/BaGet.Core/Authentication/BaGetPrincipal.cs:24-35`: optional ctor parameter `bool isAdministrator = false` and a property `IsAdministrator`, documented as *"skips package ownership, never the key's scopes or glob"*.
- `AccountAuthenticationService.cs:55`: pass `isAdministrator: user.IsAdministrator`.
- `SessionPrincipalFactory.cs:29`: becomes `Task<BaGetPrincipal> CreateAsync(ClaimsPrincipal user)`, which loads the user (`FindByIdAsync`) and passes `user.IsAdministrator`. Update the caller `Upload.cshtml.cs:444` and the XML doc ("never the superuser" stays true).
- `OwnershipAuthorizationService.cs:45-46`, after `MatchesPackage`:
```csharp
// An administrator skips ownership, never the key's own limits: a push-only key an
// administrator made for CI stays push-only.
if (principal.IsAdministrator)
{
    return action switch
    {
        PackageAction.Push => await _packages.ExistsAsync(packageId, cancellationToken) || await _ownership.HasOwnersAsync(packageId, cancellationToken)
            ? principal.HasScope(ApiKeyScopes.Push) || principal.HasScope(ApiKeyScopes.PushVersion)
            : principal.HasScope(ApiKeyScopes.Push),
        PackageAction.Manage => principal.HasScope(ApiKeyScopes.Unlist),
        _ => false,
    };
}
```
  Update the class's `<list>` doc (`:9-21`) with the new rule.

## B3. Manage any package — `src/BaGet.Identity/Pages/Packages/Manage.cshtml(.cs)`
- `:80-85`: allow when owner **or** `AdministratorService.IsAdministratorAsync`. Set `public bool ActingAsAdministrator` (admin and not owner). The view shows `alert-warning` "You are managing this package as an administrator."
- Admin POSTs where the admin isn't an owner are logged once in `OnPageHandlerExecutionAsync`: `LogWarning("Administrator {Admin} ran {Handler} on {Id}", ...)`.
- Hard delete, for admins only, password-confirmed (`IsConfirmedAsync`, `AccountPageModel.cs:148`; `_Reauthenticate` partial for accounts without a password):
  - `OnPostHardDeleteAsync(string version, string password)`: one version.
  - `OnPostHardDeleteAllAsync(string password)`: every version of the ID, then removes its owner rows.
  - Buttons: per version, *Delete permanently*, shown when `IsAdministrator && !DeletionIsPermanent` (when the server already hard-deletes, the normal button does it). *Delete all versions permanently* in a red "Danger zone" panel at the bottom, always for admins.
- `src/BaGet.Core/Indexing/IPackageDeletionService.cs`: add
```csharp
/// <summary>
/// Remove the version's rows and files whatever <see cref="BaGetOptions.PackageDeletionBehavior"/> says.
/// For administrators; the protocol's DELETE keeps following the setting.
/// </summary>
/// <returns>False if the database held no such version (its files are removed either way).</returns>
Task<bool> TryHardDeletePackageAsync(string id, NuGetVersion version, CancellationToken cancellationToken);
```
  `PackageDeletionService.cs:74`: the existing private method becomes this public member, with no behaviour change.
- Read-only mode still refuses every POST (`:87-99`), admins included.

## B4. Users page actions — `src/BaGet.Identity/Pages/Admin/Users.cshtml(.cs)`
Per row (not for the current admin's own row): **Packages**, **Reset password ▾** (Generate / Email link), **Delete**.
| Handler | Behaviour |
|---|---|
| `OnPostResetPasswordAsync(userName, password)` | Confirm → `AccountRecoveryService.ResetPasswordAsync(userName, ct)`. Renders `Page()` (no PRG, so the password never goes into TempData or a cookie) with `GeneratedPassword` in a one-time `alert-info` and a copy button. Sets `Cache-Control: no-store`. Logs `"{Admin} reset the password of {User}"` without the value. |
| `OnPostSendResetLinkAsync(userName, password)` | Confirm. 404 unless `Email.IsConfigured`. Error if the user has no address. Enqueues through `PasswordResetLinkSender` (below). No rate limiter (admin action). Message: "Reset link sent to {masked email}". |

`src/BaGet.Identity/PasswordResetLinkSender.cs` (new, scoped): the template building and `SendAsync` moved out of `ForgotPassword.cshtml.cs:~40-100` (BV3). `ForgotPasswordModel` calls it, so there's one implementation, and its rate limiting stays in the page.

## B5. Delete another account — `Pages/Admin/DeleteUser.cshtml(.cs)` (`/admin/users/{name}/delete`)
- GET: shows what goes: API key count, direct ownerships, **sole-owned IDs** (from `GetBlockersAsync`), organizations and what happens to each (B1). Radio `soleOwned=keep|delete` (default `keep`). Password field.
- POST: refuses self (`name == current`) and the last admin; confirm → `AccountDeletionService.DeleteByAdministratorAsync(user, deleteSoleOwnedPackages, actor, ct)`; redirect to Users with a summary.
- `src/BaGet.Identity/AccountDeletionService.cs`: new method beside `DeleteAsync` (`:90`), sharing the staging code (extract `StageCleanupAsync`):
  1. Ignore `LastOwnerPackageIds` and organization blockers (`LastAdministrator` still blocks).
  2. Organizations: the user is the last org admin and others remain → set `IsAdmin = true` on the remaining member with the lowest `Key`. The user is the only member and the org owns packages → remove the membership, keep the `Organization` row.
  3. Delete the user (one `SaveChanges`, as today: `:115-130`).
  4. Only after that commits, if `deleteSoleOwnedPackages`: `TryHardDeletePackageAsync` for every version of each sole-owned ID (`IPackageDatabase.FindAsync(id, includeUnlisted: true)`). Collect failures and return them; never roll the user back.
  5. Log `"{Admin} deleted user {User}; sole-owned IDs {Ids} {kept|deleted}"`.
  Returns `AdminDeletionResult(IdentityResult Result, IReadOnlyList<string> KeptWithoutOwners, IReadOnlyList<string> DeletedPackageIds, IReadOnlyList<string> Failures)`.

## B6. A user's packages and ownerless packages
- `Pages/Admin/UserPackages.cshtml(.cs)` (`/admin/users/{name}/packages`): `PackageOwnershipService.GetOwnedPackageIdsAsync(user.Id, user.UserName, ct)` (`PackageOwnershipService.cs:83`) → table: ID, latest version, downloads, owners, link to `/packages/{id}/manage`.
- `Pages/Admin/Packages.cshtml(.cs)` (`/admin/packages`): IDs with no `PackageOwner` row (pre-accounts, or left by B5), paged 50 per page (`p`), each linking to Manage.
- `_Layout.cshtml` admin menu: add **Packages** (`/Admin/Packages`).

## B7. Tests
| Test | Asserts |
|---|---|
| `OwnershipAuthorizationServiceTests` (Core/Identity unit) | Admin: manage a non-owned ID with the `Unlist` scope; refused without the scope or outside the glob; push to an existing ID with `PushVersion`; a new ID needs `Push`. Non-admin unchanged. |
| `AdminPackageTests` (integration) | Admin API key unlists another user's package over `DELETE`; admin session opens Manage for a non-owned and an ownerless ID (non-admin 403); hard delete with `Unlist` behaviour removes rows and the `.nupkg`; wrong password refused; hard-delete-all removes owners; read-only refuses; the action is logged. |
| `AdminUserActionsTests` (integration) | Generate reset: new password works, old one fails, 2FA off, password not in TempData or cookies, `no-store`. Email link: sent through a captured sender when SMTP is configured, 404 otherwise. Own row has no actions; self-delete or self-reset POST refused. |
| `AdminDeleteUserTests` (integration) | Sole-owned `keep` → ID is ownerless and listed on `/admin/packages`; `delete` → versions gone; org last-admin handover; memberless org kept; last admin refused; non-admin 404. |
| `ForgotPassword*` existing tests | Still green after the `PasswordResetLinkSender` move. |

## B8. Documentation (same commits)
| File | Change |
|---|---|
| `docs/configuration.md` `### Administrators` | Powers: settings, users, password reset (both kinds), manage any package (UI and API; keys keep scopes and glob), hard delete in the UI, account deletion rules (sole-owned IDs, orgs), audit logging. |
| `docs/configuration.md` "Enable package hard deletions" | Admins can always hard-delete from the UI; the API follows the setting. |
| XML docs | `BaGetPrincipal.IsAdministrator`, `OwnershipAuthorizationService` rule list, `IPackageDeletionService`, new services and pages. |
| `CLAUDE.md` | Test counts. |

## B9. Execution (workflow, ≤5 agents)
| Phase | Where | Agent | Content |
|---|---|---|---|
| B-0 | `main` | Sonnet | BV1–BV3; B3's `IPackageDeletionService.TryHardDeletePackageAsync`; B2 principal flag. Commit. These are the shared seams both branches need. |
| B-1 (parallel) | `../BaGet-wt-packages` / `wt/packages` | Sonnet | B2 authorization rule, B3, B6, related tests and docs. |
| | `../BaGet-wt-users` / `wt/users` | Sonnet | B4, B5, related tests and docs; the per-row *Packages* link to B6's route. |
| B-2 | rebase both onto `main`, `merge --ff-only`, full tests | Sonnet | |
| B-3 | `main` | Opus review → Sonnet fix (≤2 rounds) | |
| B-4 | — | orchestrator | Remove worktrees and branches, delete this plan, commit. |

## B10. Risks
| Risk | Mitigation |
|---|---|
| A stolen admin API key reaches every package | Scopes and glob still apply; docs advise a separate non-admin account for CI keys. |
| Hard delete is irreversible | Admin only, password confirmation, a JS confirm, logged. |
| Partial failure while deleting the packages of a deleted user | The user deletion is committed first; failures are reported and the leftover IDs appear on `/admin/packages` for a retry. |
| A generated password is visible on screen | Shown once, `no-store`, never stored or logged; the user's sessions end. |
