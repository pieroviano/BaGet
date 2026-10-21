using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Identity.Pages.Admin;

/// <summary>
/// The access audit log: one entry per change to groups, memberships, permission targets, grants, projects, the
/// repositories' project and anonymous-read switch, newest first, filtered by who made the change, what it was done to
/// and the dates (UTC, <c>yyyy-MM-dd</c>, both inclusive). Read only; administrators only.
/// </summary>
public class AuditModel : AccessPageModel
{
    /// <summary>How many entries the page lists at most.</summary>
    public const int PageSize = 200;

    /// <summary>The user name that made the change.</summary>
    [BindProperty(SupportsGet = true, Name = "actor")]
    public string? ActorFilter { get; set; }

    /// <summary>Part of what the change was done to (a group, target, project or repository name).</summary>
    [BindProperty(SupportsGet = true)]
    public string? Subject { get; set; }

    /// <summary>First day, <c>yyyy-MM-dd</c>.</summary>
    [BindProperty(SupportsGet = true)]
    public string? From { get; set; }

    /// <summary>Last day, <c>yyyy-MM-dd</c>.</summary>
    [BindProperty(SupportsGet = true)]
    public string? To { get; set; }

    /// <summary>The matching entries.</summary>
    public IReadOnlyList<AccessAuditEntry> Entries { get; private set; } = Array.Empty<AccessAuditEntry>();

    /// <summary>Why the filter was not applied, if a date could not be read.</summary>
    public string? FilterError { get; private set; }

    /// <summary>Shows the entries.</summary>
    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var from = ParseDay(From, "From", 0);
        var to = ParseDay(To, "To", 1);

        Entries = await AccessAdmin.AuditAsync(new AuditFilter(ActorFilter, Subject, from, to), PageSize, cancellationToken);
    }

    private DateTime? ParseDay(string? text, string field, int addDays)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        if (DateTime.TryParseExact(text.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var day))
        {
            return day.AddDays(addDays);
        }

        FilterError = $"{field} is not a date (use yyyy-MM-dd).";
        return null;
    }
}
