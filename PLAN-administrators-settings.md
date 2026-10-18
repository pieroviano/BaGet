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
