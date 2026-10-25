using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;
using NuGet.Versioning;

namespace BaGet.Web;

/// <summary>What <see cref="ReportAbuseWorkflow.SubmitAsync"/> decided.</summary>
public enum ReportAbuseOutcome
{
    /// <summary>The page answers 404 (reports unsupported, bad ID or version).</summary>
    NotFound,

    /// <summary>The form has errors; show it again.</summary>
    Invalid,

    /// <summary>The report was recorded.</summary>
    Submitted,
}

/// <summary>
/// The logic of the report-abuse page: records a report against a package version, for its owners and the administrator. Works for any
/// ID and version (a client expands the report abuse template from whatever it is looking at). Shared by
/// <see cref="ReportAbuseController"/>.
/// </summary>
public sealed class ReportAbuseWorkflow
{
    private readonly IContext _context;
    private readonly SystemTime _time;

    /// <summary>Creates the workflow.</summary>
    public ReportAbuseWorkflow(IContext context, SystemTime time)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _time = time ?? throw new ArgumentNullException(nameof(time));
    }

    /// <summary>Reports are kept in the database, which Azure Table storage cannot do.</summary>
    public bool CanReport => !(_context is NullContext);

    /// <summary>Validates and records the report; errors are added to <paramref name="modelState"/>.</summary>
    public async Task<ReportAbuseOutcome> SubmitAsync(ReportAbuseViewModel model, ModelStateDictionary modelState, CancellationToken cancellationToken)
    {
        if (!CanReport) return ReportAbuseOutcome.NotFound;

        if (!AbuseReport.Reasons.Contains(model.Reason, StringComparer.Ordinal))
        {
            modelState.AddModelError(nameof(ReportAbuseViewModel.Reason), "Choose one of the reasons listed.");
        }

        if (model.Id == null || model.Id.Length > AbstractContext<DbContext>.MaxPackageIdLength
                             || !NuGetVersion.TryParse(model.Version, out var version))
        {
            return ReportAbuseOutcome.NotFound;
        }

        if (!modelState.IsValid) return ReportAbuseOutcome.Invalid;

        #pragma warning disable CS8602
        _context.AbuseReports.Add(new AbuseReport
        {
            PackageId = model.Id,
            PackageVersion = version.ToNormalizedString(),
            Reason = model.Reason,
            Details = model.Details,
            ReporterEmail = string.IsNullOrWhiteSpace(model.ReporterEmail) ? null : model.ReporterEmail.Trim(),
            Created = _time.UtcNow,
        });
        #pragma warning restore CS8602
        await _context.SaveChangesAsync(cancellationToken);

        return ReportAbuseOutcome.Submitted;
    }
}
