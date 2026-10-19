using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BaGet.Core;

/// <summary>
/// Copies every row of the running relational database into another relational database, then saves the
/// new <c>Database</c> settings. One copy at a time; <see cref="DatabaseCopyWorker"/> runs the queued request.
/// </summary>
/// <remarks>
/// <list type="number">
/// <item>The <see cref="MaintenanceGate"/> closes when the request is accepted (<see cref="TryQueue"/>).</item>
/// <item>Target: reachable, its schema migrated, every mapped table empty (otherwise refused untouched).
/// Source: no pending migrations, and nothing in it the target cannot hold (<see cref="DatabaseCopyPreflight"/>).</item>
/// <item>Tables in foreign-key order, in batches of <see cref="BatchSize"/> rows ordered by primary key, with
/// the provider's explicit-key hooks (<see cref="IContext.BeginKeyedInsertAsync"/>).</item>
/// <item>Row counts and a checksum of the keys must match; only then the settings are saved and the gate stays
/// closed until the restart.</item>
/// <item>On any failure the rows copied are deleted from the target (never from the source), the gate reopens and no
/// setting is saved.</item>
/// </list>
/// Connection strings are never logged or put in the status; error text is scrubbed of them.
/// </remarks>
public class DatabaseCopyService
{
    /// <summary>Rows read, inserted and saved at a time.</summary>
    public const int BatchSize = 1000;

    /// <summary>The reason the gate shows while a copy runs.</summary>
    public const string GateReason = "Copying the database";

    private static readonly string[] SecretKeys = { "password", "pwd", "user id", "uid", "user", "username", "accountkey", "sharedaccesssignature" };

    private readonly IServiceScopeFactory _scopes;
    private readonly IReadOnlyList<IDatabaseContextFactory> _factories;
    private readonly MaintenanceGate _gate;
    private readonly SettingsService _settings;
    private readonly IOptionsMonitor<DatabaseOptions> _current;
    private readonly ILogger<DatabaseCopyService> _logger;
    private readonly Channel<DatabaseCopyRequest> _queue = Channel.CreateBounded<DatabaseCopyRequest>(1);
    private readonly object _lock = new object();
    private DatabaseCopyStatus _status = DatabaseCopyStatus.Idle;

    public DatabaseCopyService(
        IServiceScopeFactory scopes,
        IEnumerable<IDatabaseContextFactory> factories,
        MaintenanceGate gate,
        SettingsService settings,
        IOptionsMonitor<DatabaseOptions> current,
        ILogger<DatabaseCopyService> logger)
    {
        _scopes = scopes ?? throw new ArgumentNullException(nameof(scopes));
        _factories = (factories ?? throw new ArgumentNullException(nameof(factories))).ToList();
        _gate = gate ?? throw new ArgumentNullException(nameof(gate));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _current = current ?? throw new ArgumentNullException(nameof(current));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>The latest progress.</summary>
    public DatabaseCopyStatus Status { get { lock (_lock) return _status; } }

    /// <summary>
    /// Test seam: runs after each batch is saved, with the table and the rows of it copied so far. An
    /// exception it throws fails the copy exactly as a failing insert would.
    /// </summary>
    internal Func<string, long, CancellationToken, Task>? AfterBatch { get; set; }

    /// <summary>Whether a database of <paramref name="type"/> can be a copy's source or target (it is relational).</summary>
    public bool IsRelational(string? type) => FactoryFor(type) != null;

    /// <summary>The running database's type, if it is relational (a copy can start from it); else null.</summary>
    public string? CurrentRelationalType => FactoryFor(_current.CurrentValue.Type)?.DatabaseType;

    /// <summary>The <c>Database:Type</c> values a copy can use, for the Settings page.</summary>
    public IReadOnlyList<string> RelationalTypes => _factories.Select(f => f.DatabaseType).ToList();

    /// <summary>
    /// Whether <paramref name="type"/> and <paramref name="connectionString"/> name the database the application is running on.
    /// </summary>
    public bool IsCurrentDatabase(string? type, string? connectionString)
    {
        var current = _current.CurrentValue;
        return string.Equals(current.Type, type, StringComparison.OrdinalIgnoreCase)
               && string.Equals(current.ConnectionString, connectionString, StringComparison.Ordinal);
    }

    /// <summary>
    /// Accepts a copy: closes the gate and queues it for <see cref="DatabaseCopyWorker"/>.
    /// </summary>
    /// <param name="request">The copy.</param>
    /// <param name="refusal">Why it was refused, when it was.</param>
    /// <returns>False when another copy is active or the database is already read-only.</returns>
    public bool TryQueue(DatabaseCopyRequest request, out string? refusal)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        lock (_lock)
        {
            if (_status.IsActive)
            {
                refusal = "Another database copy is already running.";
                return false;
            }

            if (_gate.IsClosed)
            {
                refusal = $"The database is read-only: {_gate.Reason}.";
                return false;
            }

            if (!_queue.Writer.TryWrite(request))
            {
                refusal = "Another database copy is already queued.";
                return false;
            }

            _gate.Close(GateReason);
            _status = DatabaseCopyStatus.Idle with { Phase = DatabaseCopyPhase.Queued, Message = "Waiting to start." };
        }

        refusal = null;
        return true;
    }

    /// <summary>The queued requests, for the worker.</summary>
    internal IAsyncEnumerable<DatabaseCopyRequest> DequeueAllAsync(CancellationToken cancellationToken)
        => _queue.Reader.ReadAllAsync(cancellationToken);

    /// <summary>
    /// Runs one copy to the end and reports it through <see cref="Status"/>; never throws. The gate must be
    /// closed (<see cref="TryQueue"/>).
    /// </summary>
    public async Task RunAsync(DatabaseCopyRequest request, CancellationToken cancellationToken)
    {
        IContext? target = null;
        IReadOnlyList<DatabaseTable> tables = Array.Empty<DatabaseTable>();
        var touchedTarget = false;
        IReadOnlyList<string> errors;

        try
        {
            Update(s => s with { Phase = DatabaseCopyPhase.Checking, Message = "Connecting to the target." });

            using var scope = _scopes.CreateScope();
            var source = AsDbContext(scope.ServiceProvider.GetRequiredService<IContext>());
            var sourceFactory = FactoryFor(_current.CurrentValue.Type)
                ?? throw new CopyRefusedException("The running database is not relational, so it cannot be copied.");
            var factory = FactoryFor(request.TargetType)
                ?? throw new CopyRefusedException($"\"{request.TargetType}\" is not a relational database type, so it cannot be a copy's target.");
            if (IsCurrentDatabase(request.TargetType, request.TargetConnectionString))
            {
                throw new CopyRefusedException("The target is the database BaGet is running on.");
            }

            target = factory.Create(
                request.TargetConnectionString,
                new DatabaseOptions { MySqlServerVersion = request.MySqlServerVersion },
                builder => MaintenanceGate.AllowWrites(builder));
            var targetDb = AsDbContext(target);

            // A database that does not exist yet (a new SQLite file, a SQL Server database the login may create) cannot be
            // connected to, but creating it is what BaGet's own migrations do; an unreachable one fails there.
            if (!await targetDb.Database.CanConnectAsync(cancellationToken).ConfigureAwait(false))
            {
                try
                {
                    await target.RunMigrationsAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    throw new CopyRefusedException(
                        "Cannot connect to the target database; it must be reachable with the given connection string (" + Describe(e, request) + ").");
                }
            }

            var pendingSource = (await source.Database.GetPendingMigrationsAsync(cancellationToken).ConfigureAwait(false)).ToList();
            if (pendingSource.Count > 0)
            {
                throw new CopyRefusedException(
                    $"The running database has {pendingSource.Count} pending migration(s) ({string.Join(", ", pendingSource)}); apply them (restart with RunMigrationsAtStartup) before copying.");
            }

            tables = DatabaseTable.InDependencyOrder(targetDb.Model);

            // Everything the target cannot hold is reported at once, before the target is changed.
            var preflight = new DatabaseCopyPreflight(request.TargetType);
            foreach (var table in tables)
            {
                Update(s => s with { Table = table.Name, Message = $"Checking {table.Name}." });
                await ForEachBatchAsync(source, table, rows =>
                {
                    preflight.Inspect(table, rows.Select(r => r.Entity));
                    return Task.CompletedTask;
                }, cancellationToken).ConfigureAwait(false);
                preflight.EndTable();
            }

            if (preflight.Problems.Count > 0)
            {
                throw new CopyRefusedException(preflight.Problems.ToArray());
            }

            // A target that is already current and holds rows is refused before anything, even its schema, changes.
            var pendingTarget = await targetDb.Database.GetPendingMigrationsAsync(cancellationToken).ConfigureAwait(false);
            if (!pendingTarget.Any())
            {
                await EnsureEmptyAsync(targetDb, tables, cancellationToken).ConfigureAwait(false);
            }

            Update(s => s with { Phase = DatabaseCopyPhase.Migrating, Table = null, Message = "Preparing the target's schema." });
            await target.RunMigrationsAsync(cancellationToken).ConfigureAwait(false);
            await EnsureEmptyAsync(targetDb, tables, cancellationToken).ConfigureAwait(false);

            var totals = new Dictionary<DatabaseTable, long>();
            foreach (var table in tables) totals[table] = await table.CountAsync(source, cancellationToken).ConfigureAwait(false);

            Update(s => s with
            {
                Phase = DatabaseCopyPhase.Copying,
                TablesTotal = tables.Count,
                RowsTotal = totals.Values.Sum(),
                Message = "Copying rows.",
            });

            touchedTarget = true;
            var checksums = new Dictionary<DatabaseTable, string>();
            var done = 0;
            foreach (var table in tables)
            {
                checksums[table] = await CopyTableAsync(source, target, targetDb, table, totals[table], cancellationToken).ConfigureAwait(false);
                done++;
                var doneNow = done;
                Update(s => s with { TablesDone = doneNow });
            }

            Update(s => s with { Phase = DatabaseCopyPhase.Verifying, Table = null, Message = "Comparing the copy with the source." });
            await VerifyAsync(targetDb, tables, totals, checksums, cancellationToken).ConfigureAwait(false);

            Update(s => s with { Phase = DatabaseCopyPhase.SavingSettings, Message = "Saving the database settings." });
            var saved = await _settings.SaveAsync(request.SettingChanges, request.Actor, cancellationToken).ConfigureAwait(false);
            if (!saved.Succeeded)
            {
                throw new CopyRefusedException(saved.Errors.Select(e => "The settings were not saved: " + e).ToArray());
            }

            _gate.CloseUntilRestart("The database was copied; restart BaGet to use the new database");
            Update(s => s with
            {
                Phase = DatabaseCopyPhase.Succeeded,
                Table = null,
                Message = "The data was copied and the settings saved. Restart BaGet to use the new database.",
            });
            _logger.LogWarning(
                "{Actor} copied the {SourceType} database to a {TargetType} database ({Rows} rows in {Tables} tables)",
                request.Actor, sourceFactory.DatabaseType, request.TargetType, totals.Values.Sum(), tables.Count);

            await AsDbContext(target).DisposeAsync().ConfigureAwait(false);
            return;
        }
        catch (CopyRefusedException e)
        {
            errors = e.Problems;
        }
        catch (Exception e)
        {
            errors = new[] { Describe(e, request) };
        }

        await FailAsync(request, target, tables, touchedTarget, errors).ConfigureAwait(false);
    }

    private async Task FailAsync(
        DatabaseCopyRequest request,
        IContext? target,
        IReadOnlyList<DatabaseTable> tables,
        bool touchedTarget,
        IReadOnlyList<string> errors)
    {
        var all = errors.ToList();
        if (target != null)
        {
            if (touchedTarget)
            {
                Update(s => s with { Phase = DatabaseCopyPhase.RollingBack, Table = null, Message = "Removing the copied rows from the target." });
                try
                {
                    // Only ever the target, which was empty when the copy began.
                    var db = AsDbContext(target);
                    db.ChangeTracker.Clear();
                    foreach (var table in tables.Reverse())
                    {
                        await table.DeleteAllAsync(db, CancellationToken.None).ConfigureAwait(false);
                    }
                }
                catch (Exception e)
                {
                    all.Add("The rows copied so far could not be removed from the target; empty it before trying again. " + Describe(e, request));
                }
            }

            try
            {
                await AsDbContext(target).DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Nothing left to clean up.
            }
        }

        _gate.Open();
        _logger.LogError("Database copy to {TargetType} failed: {Errors}", request.TargetType, string.Join(" | ", all));
        Update(s => s with
        {
            Phase = DatabaseCopyPhase.Failed,
            Table = null,
            Errors = all,
            Message = "The copy failed. The running database is unchanged and no setting was saved.",
        });
    }

    private async Task<string> CopyTableAsync(
        DbContext source,
        IContext target,
        DbContext targetDb,
        DatabaseTable table,
        long total,
        CancellationToken cancellationToken)
    {
        var dateProperties = table.EntityType.GetProperties()
            .Where(p => p.ClrType == typeof(DateTime) || p.ClrType == typeof(DateTime?))
            .ToList();
        var computed = table.EntityType.GetProperties()
            .Where(p => !p.IsKey() && p.ValueGenerated == ValueGenerated.OnAddOrUpdate)
            .ToList();

        long copied = 0;
        using var checksum = new DatabaseTable.Checksum();

        Update(s => s with { Table = table.Name, TableRowsCopied = 0, TableRowsTotal = total, Message = $"Copying {table.Name}." });
        var rowsBefore = Status.RowsCopied;

        await target.BeginKeyedInsertAsync(table.EntityType, cancellationToken).ConfigureAwait(false);
        try
        {
            await ForEachBatchAsync(source, table, async rows =>
            {
                foreach (var row in rows)
                {
                    checksum.Add(table.KeyText(row.Entity));

                    var entry = targetDb.Add(row.Entity);
                    foreach (var shadow in row.Shadow) entry.Property(shadow.Key).CurrentValue = shadow.Value;

                    // SQLite, SQL Server and MySQL return dates of unspecified kind; BaGet stores UTC, and PostgreSQL accepts
                    // nothing else. EF compares DateTimes without their kind, so assigning the same instant with another kind
                    // is ignored as "no change": move off it first. The entry reads the mapped field, so a property whose
                    // getter already forces UTC (Package.Published) is fixed too.
                    foreach (var property in dateProperties)
                    {
                        var member = entry.Property(property.Name);
                        if (member.CurrentValue is DateTime { Kind: DateTimeKind.Unspecified } date)
                        {
                            member.CurrentValue = DateTime.MinValue;
                            member.CurrentValue = DateTime.SpecifyKind(date, DateTimeKind.Utc);
                        }
                    }

                    // Values the database generates itself (row versions) are not inserted.
                    foreach (var property in computed)
                    {
                        entry.Property(property.Name).CurrentValue = property.ClrType.IsValueType && Nullable.GetUnderlyingType(property.ClrType) == null
                            ? Activator.CreateInstance(property.ClrType)
                            : null;
                    }
                }

                try
                {
                    await targetDb.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (DbUpdateException e)
                {
                    throw new CopyRefusedException(
                        $"Inserting into {table.Name} failed (rows {table.KeyText(rows[0].Entity)} to {table.KeyText(rows[rows.Count - 1].Entity)}): {e.GetBaseException().Message}");
                }

                targetDb.ChangeTracker.Clear();
                copied += rows.Count;
                var copiedNow = copied;
                Update(s => s with { TableRowsCopied = copiedNow, RowsCopied = rowsBefore + copiedNow });

                if (AfterBatch != null) await AfterBatch(table.Name, copiedNow, cancellationToken).ConfigureAwait(false);
            }, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            try
            {
                await target.EndKeyedInsertAsync(table.EntityType, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // The original failure is the one to report.
            }

            throw;
        }

        await target.EndKeyedInsertAsync(table.EntityType, cancellationToken).ConfigureAwait(false);
        return checksum.Finish();
    }

    private static async Task VerifyAsync(
        DbContext target,
        IReadOnlyList<DatabaseTable> tables,
        IReadOnlyDictionary<DatabaseTable, long> totals,
        IReadOnlyDictionary<DatabaseTable, string> checksums,
        CancellationToken cancellationToken)
    {
        var problems = new List<string>();
        foreach (var table in tables)
        {
            var count = await table.CountAsync(target, cancellationToken).ConfigureAwait(false);
            if (count != totals[table])
            {
                problems.Add($"{table.Name}: the source has {totals[table]} rows, the target {count}.");
                continue;
            }

            using var checksum = new DatabaseTable.Checksum();
            await ForEachBatchAsync(target, table, rows =>
            {
                foreach (var row in rows) checksum.Add(table.KeyText(row.Entity));
                return Task.CompletedTask;
            }, cancellationToken).ConfigureAwait(false);

            if (checksum.Finish() != checksums[table])
            {
                problems.Add($"{table.Name}: the keys of the target differ from the source's.");
            }
        }

        if (problems.Count > 0) throw new CopyRefusedException(problems.ToArray());
    }

    private static async Task EnsureEmptyAsync(DbContext target, IReadOnlyList<DatabaseTable> tables, CancellationToken cancellationToken)
    {
        var problems = new List<string>();
        foreach (var table in tables)
        {
            var count = await table.CountAsync(target, cancellationToken).ConfigureAwait(false);
            if (count > 0) problems.Add($"The target is not empty: {table.Name} has {count} row(s). Nothing was changed.");
        }

        if (problems.Count > 0) throw new CopyRefusedException(problems.ToArray());
    }

    private static async Task ForEachBatchAsync(
        DbContext context,
        DatabaseTable table,
        Func<List<CopyRow>, Task> action,
        CancellationToken cancellationToken)
    {
        var skip = 0;
        while (true)
        {
            var rows = await table.ReadBatchAsync(context, skip, BatchSize, cancellationToken).ConfigureAwait(false);
            if (rows.Count == 0) return;

            await action(rows).ConfigureAwait(false);
            skip += rows.Count;
            if (rows.Count < BatchSize) return;
        }
    }

    private void Update(Func<DatabaseCopyStatus, DatabaseCopyStatus> change)
    {
        lock (_lock) _status = change(_status);
    }

    private IDatabaseContextFactory? FactoryFor(string? type)
        => string.IsNullOrEmpty(type)
            ? null
            : _factories.FirstOrDefault(f => string.Equals(f.DatabaseType, type, StringComparison.OrdinalIgnoreCase));

    private static DbContext AsDbContext(IContext context)
        => context as DbContext ?? throw new InvalidOperationException("The database context is not an Entity Framework context.");

    /// <summary>An error as text safe to show: no connection string, no credential from it.</summary>
    internal static string Describe(Exception exception, DatabaseCopyRequest request)
        => Scrub(exception.GetBaseException().Message, request.TargetConnectionString);

    /// <summary>Removes the connection string, and the credentials inside it, from <paramref name="message"/>.</summary>
    internal static string Scrub(string message, string connectionString)
    {
        if (string.IsNullOrEmpty(connectionString)) return message;

        message = message.Replace(connectionString, "[connection string]", StringComparison.Ordinal);
        foreach (var part in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = part.IndexOf('=');
            if (separator <= 0) continue;

            var key = part.Substring(0, separator).Trim().ToLowerInvariant();
            var value = part.Substring(separator + 1).Trim().Trim('\'', '"');
            if (value.Length > 0 && SecretKeys.Contains(key))
            {
                message = message.Replace(value, "[hidden]", StringComparison.Ordinal);
            }
        }

        return message;
    }

    /// <summary>A failure that is the user's to fix: shown as is (it holds no connection string).</summary>
    private sealed class CopyRefusedException : Exception
    {
        public CopyRefusedException(string problem) : this(new[] { problem }) { }

        public CopyRefusedException(string[] problems) : base(string.Join(" ", problems)) => Problems = problems;

        public string[] Problems { get; }
    }
}
