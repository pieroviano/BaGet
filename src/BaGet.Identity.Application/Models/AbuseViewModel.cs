using System;
using System.Collections.Generic;
using System.Linq;
using BaGet.Core;

namespace BaGet.Identity.Application.Models;

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
