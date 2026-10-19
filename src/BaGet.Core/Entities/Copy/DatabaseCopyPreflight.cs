using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore.Metadata;

namespace BaGet.Core;

/// <summary>
/// Finds, in the rows of the source, what the target could not hold, so every problem is reported
/// before anything is copied. Feed it each table's rows (any number of batches), then call
/// <see cref="EndTable"/>.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>A string longer than the model's maximum length: SQLite stores it, the other engines refuse it.</item>
/// <item>Two values of a unique index that differ only by case: SQL Server and MySQL compare case-insensitively.
/// Not checked for PostgreSQL, whose comparison is case-sensitive.</item>
/// <item>A <see cref="DateTimeKind.Local"/> date: its offset is unknown, and PostgreSQL accepts UTC only. (Unspecified
/// ones, what SQLite and MySQL return, are stored as UTC, which is what BaGet writes.)</item>
/// </list>
/// </remarks>
internal sealed class DatabaseCopyPreflight
{
    /// <summary>The most problems listed per table and kind; the rest is counted.</summary>
    public const int MaxListedPerKind = 10;

    private readonly bool _checkCaseCollisions;
    private readonly bool _checkLocalDates;
    private readonly List<string> _problems = new List<string>();
    private readonly Dictionary<string, int> _counts = new Dictionary<string, int>();
    private DatabaseTable? _table;
    private Dictionary<IIndex, Dictionary<string, string>> _seen = new Dictionary<IIndex, Dictionary<string, string>>();

    public DatabaseCopyPreflight(string targetType)
    {
        var postgres = string.Equals(targetType, "PostgreSql", StringComparison.OrdinalIgnoreCase);
        _checkCaseCollisions = !postgres;
        _checkLocalDates = postgres;
    }

    /// <summary>The problems found, one line each.</summary>
    public IReadOnlyList<string> Problems => _problems;

    public void Inspect(DatabaseTable table, IEnumerable<object> rows)
    {
        if (!ReferenceEquals(_table, table))
        {
            _table = table;
            _seen = new Dictionary<IIndex, Dictionary<string, string>>();
        }

        var properties = table.EntityType.GetProperties().ToList();
        var indexes = _checkCaseCollisions
            ? table.EntityType.GetIndexes().Where(i => i.IsUnique && i.Properties.Any(p => p.ClrType == typeof(string))).ToList()
            : new List<IIndex>();

        foreach (var row in rows)
        {
            foreach (var property in properties)
            {
                var value = DatabaseTable.ValueOf(property, row);
                if (value == null) continue;

                var max = property.GetMaxLength();
                if (max != null)
                {
                    var stored = property.GetValueConverter() is { } converter ? converter.ConvertToProvider(value) : value;
                    if (stored is string text && text.Length > max)
                    {
                        Report(table, "length", $"{table.Name}.{property.Name} of row {table.KeyText(row)} has {text.Length} characters; the target allows {max}.");
                    }
                }

                if (_checkLocalDates && value is DateTime date && date.Kind == DateTimeKind.Local)
                {
                    Report(table, "date", $"{table.Name}.{property.Name} of row {table.KeyText(row)} is a local time, which PostgreSQL cannot store; only UTC is supported.");
                }
            }

            foreach (var index in indexes)
            {
                var composite = string.Join("|", index.Properties.Select(p =>
                {
                    var value = DatabaseTable.ValueOf(p, row);
                    return value is string s ? s.ToUpperInvariant() : DatabaseTable.Format(value);
                }));

                if (!_seen.TryGetValue(index, out var keys)) _seen[index] = keys = new Dictionary<string, string>(StringComparer.Ordinal);

                var rowKey = table.KeyText(row);
                if (keys.TryGetValue(composite, out var other))
                {
                    var columns = string.Join(", ", index.Properties.Select(p => p.Name));
                    Report(table, "case:" + columns, $"{table.Name} rows {other} and {rowKey} differ only by case in unique index ({columns}); the target compares case-insensitively.");
                }
                else
                {
                    keys[composite] = rowKey;
                }
            }
        }
    }

    /// <summary>Ends the table: releases what was kept to find collisions and adds the "and N more" lines.</summary>
    public void EndTable()
    {
        foreach (var pair in _counts)
        {
            if (pair.Value > MaxListedPerKind)
            {
                _problems.Add($"... and {pair.Value - MaxListedPerKind} more of the same kind ({pair.Key}).");
            }
        }

        _counts.Clear();
        _seen = new Dictionary<IIndex, Dictionary<string, string>>();
        _table = null;
    }

    private void Report(DatabaseTable table, string kind, string message)
    {
        var key = table.Name + ":" + kind;
        _counts.TryGetValue(key, out var count);
        _counts[key] = count + 1;
        if (count < MaxListedPerKind) _problems.Add(message);
    }
}
