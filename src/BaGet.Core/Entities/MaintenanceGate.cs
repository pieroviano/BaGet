using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace BaGet.Core;

/// <summary>
/// An in-memory, process-wide read-only switch for the database. While it is closed,
/// <see cref="MaintenanceSaveChangesInterceptor"/> refuses every <c>SaveChanges</c> with a
/// <see cref="MaintenanceException"/>; reads are unaffected.
/// <para>
/// A context built with <see cref="AllowWrites(DbContextOptionsBuilder)"/> is exempt, so the job that
/// holds the gate closed (for example a database copy) can still write to its own target.
/// </para>
/// <para>
/// <see cref="CloseUntilRestart"/> closes the gate for good: <see cref="Open"/> then does nothing,
/// because the state is not persisted and a restart is the only thing that resets it.
/// </para>
/// </summary>
public sealed class MaintenanceGate
{
    private readonly object _lock = new object();
    private string? _reason;
    private bool _untilRestart;

    /// <summary>Whether writes are currently refused.</summary>
    public bool IsClosed { get { lock (_lock) return _reason != null; } }

    /// <summary>True when the gate was closed with <see cref="CloseUntilRestart"/> and cannot be reopened.</summary>
    public bool IsClosedUntilRestart { get { lock (_lock) return _untilRestart; } }

    /// <summary>Why the gate is closed, or null while it is open.</summary>
    public string? Reason { get { lock (_lock) return _reason; } }

    /// <summary>Refuse writes until <see cref="Open"/> is called.</summary>
    public void Close(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("A reason is required.", nameof(reason));

        lock (_lock)
        {
            // A permanent closure keeps its own reason.
            if (!_untilRestart) _reason = reason;
        }
    }

    /// <summary>Refuse writes until the process restarts; <see cref="Open"/> no longer reopens.</summary>
    public void CloseUntilRestart(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("A reason is required.", nameof(reason));

        lock (_lock)
        {
            _reason = reason;
            _untilRestart = true;
        }
    }

    /// <summary>Allow writes again. Returns false, leaving the gate closed, after <see cref="CloseUntilRestart"/>.</summary>
    public bool Open()
    {
        lock (_lock)
        {
            if (_untilRestart) return false;
            _reason = null;
            return true;
        }
    }

    /// <summary>Throw <see cref="MaintenanceException"/> while the gate is closed.</summary>
    public void ThrowIfClosed()
    {
        var reason = Reason;
        if (reason != null) throw new MaintenanceException(reason);
    }

    /// <summary>
    /// Mark a context's options as exempt from the gate. Use it only for a context that the job holding
    /// the gate closed writes to itself, such as the target of a database copy.
    /// </summary>
    public static DbContextOptionsBuilder AllowWrites(DbContextOptionsBuilder optionsBuilder)
    {
        if (optionsBuilder == null) throw new ArgumentNullException(nameof(optionsBuilder));

        var extension = optionsBuilder.Options.FindExtension<AllowWritesOptionsExtension>()
            ?? new AllowWritesOptionsExtension();
        ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(extension);
        return optionsBuilder;
    }

    /// <summary>Typed form of <see cref="AllowWrites(DbContextOptionsBuilder)"/>.</summary>
    public static DbContextOptionsBuilder<TContext> AllowWrites<TContext>(DbContextOptionsBuilder<TContext> optionsBuilder)
        where TContext : DbContext
    {
        AllowWrites((DbContextOptionsBuilder)optionsBuilder);
        return optionsBuilder;
    }

    /// <summary>Whether a context was built with <see cref="AllowWrites(DbContextOptionsBuilder)"/>.</summary>
    internal static bool AreWritesAllowed(DbContext? context)
    {
        return context?.GetService<IDbContextOptions>().FindExtension<AllowWritesOptionsExtension>() != null;
    }

    private sealed class AllowWritesOptionsExtension : IDbContextOptionsExtension
    {
        private DbContextOptionsExtensionInfo? _info;

        public DbContextOptionsExtensionInfo Info => _info ??= new ExtensionInfo(this);

        public void ApplyServices(IServiceCollection services)
        {
        }

        public void Validate(IDbContextOptions options)
        {
        }

        private sealed class ExtensionInfo : DbContextOptionsExtensionInfo
        {
            public ExtensionInfo(IDbContextOptionsExtension extension) : base(extension) { }

            public override bool IsDatabaseProvider => false;

            public override string LogFragment => "AllowWrites ";

            public override int GetServiceProviderHashCode() => 0;

            public override bool ShouldUseSameServiceProvider(DbContextOptionsExtensionInfo other) => other is ExtensionInfo;

            public override void PopulateDebugInfo(IDictionary<string, string> debugInfo)
            {
                debugInfo["BaGet:AllowWrites"] = "true";
            }
        }
    }
}
