# Copy the database when its connection changes on the Settings page

When an administrator changes `Database:Type` or `Database:ConnectionString` on `/admin/settings` to a
different, working relational database, BaGet Extension can migrate the target's schema, copy every row
into it, and only then save the new settings.

## Blocking — resolve before coding

| # | Check | Why it blocks | How |
|---|---|---|---|
| **B1** | Test servers for SQL Server, PostgreSQL, MySQL | Key preservation, sequences, dates and collations differ per engine; SQLite-only tests prove nothing about them. | Confirm Docker (or local servers) is available to run them. If not, those providers ship **untested** — decide before coding. |
| B2 | Every database write goes through EF `SaveChanges` | The read-only gate (D3) is an EF interceptor. | Grep found **no** `ExecuteSqlRaw`/`ExecuteUpdate`/`ExecuteDelete` outside migrations. Confirm Identity's stores and every `BackgroundService` (`ValidationWorker`, `StagingSweepWorker`, `BackgroundEmailService`) and the mirror's read-through indexing write only via `SaveChanges`. |
| B3 | Data the target cannot hold | SQLite stores longer strings than the model's `HasMaxLength`, and is case-sensitive where SQL Server/MySQL unique indexes are not, so two IDs differing only by case could collide. PostgreSQL needs UTC `DateTime`. | Write a pre-flight scan over the source: over-length values, case-insensitive duplicates on unique indexes, non-UTC dates. Report them all before anything is copied. |
| B4 | Source schema is current | A source with pending migrations (`RunMigrationsAtStartup=false`) has a different shape from a freshly migrated target. | `GetPendingMigrationsAsync()` on the source; refuse if any. |
| B5 | MySQL version detection | `ServerVersion.AutoDetect` opens a connection (`DatabaseOptions.cs:13-22`). | Acceptable for a target being tested; still honour `Database:MySqlServerVersion` when set. |

## Decisions

| # | Decision | Consequence |
|---|---|---|
| D1 | **Opt-in on save.** Changing `Database:Type` or `ConnectionString` shows "Copy all data to the new database" (checked). Save tests the connection first; the copy runs as a background job; settings are saved **only after** a verified copy. | Unchecked keeps today's behaviour. A failed copy changes no settings. |
| D2 | **Target must be empty**: after migration every mapped table has zero rows. | No merge, no overwrite. A non-empty target refuses the copy and nothing changes. |
| D3 | **Read-only while copying, and until the restart.** Every `SaveChanges` is refused (503 "maintenance") from the copy's start until the process restarts on the new database. | `Database:*` are `[RestartRequired]`. Writes accepted after the copy but before the restart would land in the old database and be lost, so the gate stays closed. Restores, search and the UI keep working. |
| D4 | **Relational only**: SQLite, SQL Server, PostgreSQL, MySQL, in any direction, including the same engine on another server. | AzureTable as source or target: checkbox hidden, save behaves as today. |
| D5 | **Source untouched.** It is never modified or deleted. | Rollback = restore the old settings. |
| D6 | **On failure, the target is emptied** in reverse dependency order. It was empty at the start, so this is safe. | Leaves a migrated, empty schema. |
| D7 | Package files are not copied: they live in `Storage`, which is unchanged. | Stated on the page. |

## Changes

### C1 — Gate (BaGet.Core)

| File | Change |
|---|---|
| `src/BaGet.Core/Entities/MaintenanceGate.cs` (new) | Singleton `MaintenanceGate { bool IsClosed; string? Reason; void Close(string reason); void Open(); }` |
| `src/BaGet.Core/Entities/MaintenanceSaveChangesInterceptor.cs` (new) | `SaveChangesInterceptor` that throws `MaintenanceException` in `SavingChanges`/`SavingChangesAsync` while the gate is closed, **except** on the copy's own target context (marked by an options extension) |
| `DependencyInjectionExtensions.Providers.cs:84` | `AddDbContext<TContext>((p, o) => { configureContext(p, o); o.AddInterceptors(p.GetRequiredService<MaintenanceSaveChangesInterceptor>()); })` |
| `src/BaGet.Web` middleware | Maps `MaintenanceException` to 503, with `Retry-After` and a text body. Pages show a banner while the gate is closed |

### C2 — Target contexts by type (each `BaGet.Database.*`)

`AddBaGetDbContextProvider<TContext>` (`DependencyInjectionExtensions.Providers.cs:75`) also registers an
`IDatabaseContextFactory` keyed by `databaseType`:

```csharp
public interface IDatabaseContextFactory
{
    string DatabaseType { get; }
    IContext Create(string connectionString, DatabaseOptions options); // not from DI options, so a second database can be opened
}
```

Each provider builds its options exactly as its `Add*Database` does, for example
`SqliteApplicationExtensions.cs:14-19`.

### C3 — Provider hooks for explicit keys (`AbstractContext` + overrides)

```csharp
// AbstractContext.cs, next to RunMigrationsAsync (line 62)
public virtual Task BeginKeyedInsertAsync(IEntityType table, CancellationToken ct) => Task.CompletedTask;
public virtual Task EndKeyedInsertAsync(IEntityType table, CancellationToken ct) => Task.CompletedTask;
```

| Provider | Override |
|---|---|
| SQL Server | `SET IDENTITY_INSERT [t] ON/OFF` on the same open connection. The copy holds the connection open for each table |
| PostgreSQL | After insert: `SELECT setval(pg_get_serial_sequence('"t"','"Key"'), MAX("Key"))` |
| MySQL, SQLite | None: explicit values advance `AUTO_INCREMENT` / `sqlite_sequence` (verified under B1) |

### C4 — Copy service (BaGet.Core)

`src/BaGet.Core/Entities/DatabaseCopyService.cs` (new), run by a `BackgroundService` with a single-slot queue:

1. `CanConnectAsync` on the target. If it fails, report the error and stop.
2. B4: refuse if the source has pending migrations. B3: run the pre-flight scan and report every problem.
3. `target.RunMigrationsAsync`. Then check that every mapped table is empty (D2).
4. `MaintenanceGate.Close("Copying the database")`.
5. Tables in **dependency order**, computed from `Model.GetEntityTypes()` foreign keys (topological sort), not a hand-kept list, so a future table is covered automatically. This covers the BaGet tables (`AbstractContext.cs:46-58`) and Identity's.
6. Per table: read the source `AsNoTracking`, ordered by primary key, in batches of 1,000. Copy the scalar properties into new instances, insert with `BeginKeyedInsertAsync` / `EndKeyedInsertAsync`, `SaveChanges`, then clear the target's change tracker.
7. Verify: per-table row counts equal; package count and a key checksum equal.
8. `SettingsService.SaveAsync({Database:Type, Database:ConnectionString[, MySqlServerVersion]})`. The gate stays closed (D3), and the status says "Restart BaGet Extension now".
9. On any failure: delete the copied rows (D6), `Gate.Open()`, report. Settings are not saved.

Progress (table, rows copied / total, phase) is held in memory. The Settings page polls
`OnGetCopyStatusAsync` (JSON) every 2 s.

### C5 — Settings page

| File | Change |
|---|---|
| `src/BaGet.Identity/Pages/Admin/Settings.cshtml.cs:60` `OnPostAsync` | For the `Database` section: if `Type` or `ConnectionString` differs from the effective value, both are relational, and `copyData=true`, then queue the copy instead of `SaveAsync` and redirect to the status view. Otherwise behave as today |
| `Settings.cshtml` (Database form) | Checkbox (D1), shown only when both types are relational. Status panel with phase, progress, errors and "Restart required" |
| Confirmation | Reuses the existing password or re-authentication check (`IsConfirmedAsync`) |

### C6 — Docs

`docs/configuration.md`: a "Moving to another database" section covering D1-D7, the read-only window and the
restart. `CLAUDE.md`: the gate interceptor and `IDatabaseContextFactory`.

## Verification

| # | Check | Expected |
|---|---|---|
| 1 | SQLite → SQLite (new file), in tests | Every table equal row by row; settings saved; gate closed until restart |
| 2 | Push during the copy | 503; nothing written to either database |
| 3 | Target not empty | Refused; no settings change; no rows touched |
| 4 | Target unreachable | Connection error shown; nothing changed |
| 5 | Failure midway (fault injected) | Target emptied; gate open; settings unchanged |
| 6 | Pre-flight: over-length value, case-duplicate ID, pending migration | Listed, copy refused |
| 7 | Every engine pair (B1) | SQLite ↔ SQL Server ↔ PostgreSQL ↔ MySQL: counts, keys and a later insert (sequence/identity continues after the max key) |
| 8 | After restart on the target | Push, sign-in, API keys, owners and catalog work; package downloads still served from storage |
| 9 | AzureTable selected | No checkbox; save as today |
| 10 | Full suite | Green |

## Risks

| Risk | Handling |
|---|---|
| Large databases: long read-only window | Batches plus progress; documented; refuse to start while another copy runs |
| Engine differences not covered by B3 | Per-engine verification (B1). Insert errors name table and key |
| Process crash mid-copy | Gate is in memory, so a restart opens it while the settings still point at the source. Target partly filled: it must be emptied before a retry (D2 refuses it) |
| Restart forgotten | Gate stays closed (D3) and a persistent banner explains why |
| Credentials | Connection strings are never logged; errors are shown without the connection string |

## Out of scope

- AzureTable (D4); merging into non-empty targets (D2); copying storage (D7); a CLI equivalent.
