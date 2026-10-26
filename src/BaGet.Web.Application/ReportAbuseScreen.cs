using System.ComponentModel.DataAnnotations;
using BaGet.Core;
using Microsoft.EntityFrameworkCore;

namespace BaGet.Web;

/// <summary>What the reporter fills in. The field names (Reason, Details, ReporterEmail) are the contract of the form and of the JSON body.</summary>
public sealed class ReportAbuseForm
{
    /// <summary>One of <see cref="AbuseReport.Reasons"/>.</summary>
    [Required]
    public string? Reason { get; set; }

    /// <summary>What the reporter saw.</summary>
    [Required]
    [StringLength(AbstractContext<DbContext>.DefaultMaxStringLength)]
    public string? Details { get; set; }

    /// <summary>The address of the reporter, optional.</summary>
    [EmailAddress]
    [StringLength(AbstractContext<DbContext>.MaxEmailLength)]
    [Display(Name = "Your email (optional)")]
    public string? ReporterEmail { get; set; }
}

/// <summary>The report-abuse screen: the package it is about, what was typed, and whether the report was recorded.</summary>
/// <param name="Id">The package ID.</param>
/// <param name="Version">The package version.</param>
/// <param name="CanReport">Reports are kept in the database, which Azure Table storage cannot do.</param>
/// <param name="Submitted">True once the report was recorded.</param>
/// <param name="Form">What was typed (empty on the first visit).</param>
public sealed record ReportAbuseScreen(string? Id, string? Version, bool CanReport, bool Submitted, ReportAbuseForm Form);
