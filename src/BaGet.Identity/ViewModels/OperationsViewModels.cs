using System;
using System.Collections.Generic;
using System.Linq;
using BaGet.Core;

namespace BaGet.Identity.ViewModels;

/// <summary>One row of the operations screen: an operation on a repository.</summary>
/// <param name="Operation">The operation.</param>
/// <param name="Repository">The repository it runs on.</param>
/// <param name="Status">The status of its last run, or null.</param>
public sealed record OperationRow(IAdminOperation Operation, RepositoryInfo Repository, AdminJobStatus? Status);

/// <summary>What the operations screen (<c>/admin/operations</c>) shows: the maintenance operations and the status of each.</summary>
public sealed class OperationsViewModel : ConfirmationModel, IFlashModel
{
    /// <summary>The operations available on this server, for every repository.</summary>
    public IReadOnlyList<OperationRow> Rows { get; internal set; } = Array.Empty<OperationRow>();

    /// <summary>Whether any job is running (the page refreshes itself).</summary>
    public bool AnyRunning => Rows.Any(r => r.Status?.State == AdminJobState.Running);

    /// <inheritdoc />
    public string? Message { get; set; }

    /// <inheritdoc />
    public string? Error { get; set; }
}

/// <summary>What the validation queue screen (<c>/admin/validation</c>) shows: pending and quarantined submissions.</summary>
public sealed class ValidationViewModel : ConfirmationModel, IFlashModel
{
    /// <summary>Whether validation can be administered here.</summary>
    public bool IsSupported { get; internal set; }

    /// <summary>The pending and quarantined submissions.</summary>
    public IReadOnlyList<ValidationSubmissionSummary> Submissions { get; internal set; } = Array.Empty<ValidationSubmissionSummary>();

    /// <inheritdoc />
    public string? Message { get; set; }

    /// <inheritdoc />
    public string? Error { get; set; }
}

/// <summary>What the abuse reports screen (<c>/admin/abuse</c>) shows: the reports of every repository.</summary>
public sealed class AbuseViewModel : IFlashModel
{
    /// <summary>Show the resolved reports too.</summary>
    public bool All { get; set; }

    /// <summary>Whether reports are stored with the configured database.</summary>
    public bool IsSupported { get; internal set; }

    /// <summary>The reports, newest first.</summary>
    public IReadOnlyList<AbuseReportSummary> Items { get; internal set; } = Array.Empty<AbuseReportSummary>();

    /// <inheritdoc />
    public string? Message { get; set; }

    /// <inheritdoc />
    public string? Error { get; set; }
}

/// <summary>One format with what it offers, as the formats screen lists it.</summary>
/// <param name="Module">The format module.</param>
/// <param name="Repositories">How many repositories use it.</param>
/// <param name="Capabilities">The capabilities its services implement.</param>
public sealed record FormatRow(IFormatModule Module, int Repositories, IReadOnlyList<string> Capabilities);

/// <summary>What the formats screen (<c>/admin/formats</c>) lists: the package formats this server can host.</summary>
public sealed class FormatsViewModel
{
    /// <summary>The formats.</summary>
    public IReadOnlyList<FormatRow> Rows { get; internal set; } = Array.Empty<FormatRow>();
}

/// <summary>What the cluster screen (<c>/admin/cluster</c>) shows: nodes, leases, shared state and health.</summary>
public sealed class ClusterViewModel
{
    /// <summary>The status read for the screen.</summary>
    public ClusterStatus Status { get; internal set; } = null!;
}
