using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.Workflows;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Identity.Pages.Admin;

/// <summary>
/// The page model of the access audit page when it was a Razor Page: one entry per change to groups, memberships, permission
/// targets, grants, projects, the repositories project and anonymous-read switch, newest first, filtered by who made the change,
/// what it was done to and the dates (UTC, <c>yyyy-MM-dd</c>, both inclusive). Read only; administrators only.
/// </summary>
/// <remarks>
/// Kept as a shim (no breaking change of the public API); <c>AdminAuditController</c> serves <c>/admin/audit</c>. Both call
/// <c>AuditWorkflow</c>.
/// </remarks>
[Obsolete("The access audit screen is served by AdminAuditController; this page model has no page any more and will be removed in the next major version.")]
public class AuditModel : AccessPageModel
{
    /// <summary>How many entries the page lists at most.</summary>
    public const int PageSize = AuditWorkflow.PageSize;

    private AuditWorkflow? _workflow;

    private AuditWorkflow Workflow => _workflow ??= new AuditWorkflow(HttpContext, Url);

    /// <summary>The user name that made the change.</summary>
    [BindProperty(SupportsGet = true, Name = "actor")]
    public string? ActorFilter { get => Workflow.Model.ActorFilter; set => Workflow.Model.ActorFilter = value; }

    /// <summary>Part of what the change was done to (a group, target, project or repository name).</summary>
    [BindProperty(SupportsGet = true)]
    public string? Subject { get => Workflow.Model.Subject; set => Workflow.Model.Subject = value; }

    /// <summary>First day, <c>yyyy-MM-dd</c>.</summary>
    [BindProperty(SupportsGet = true)]
    public string? From { get => Workflow.Model.From; set => Workflow.Model.From = value; }

    /// <summary>Last day, <c>yyyy-MM-dd</c>.</summary>
    [BindProperty(SupportsGet = true)]
    public string? To { get => Workflow.Model.To; set => Workflow.Model.To = value; }

    /// <summary>The matching entries.</summary>
    public IReadOnlyList<AccessAuditEntry> Entries => Workflow.Model.Entries;

    /// <summary>Why the filter was not applied, if a date could not be read.</summary>
    public string? FilterError => Workflow.Model.FilterError;

    /// <summary>Shows the entries.</summary>
    public async Task OnGetAsync(CancellationToken cancellationToken) => await Workflow.GetAsync(cancellationToken);
}
