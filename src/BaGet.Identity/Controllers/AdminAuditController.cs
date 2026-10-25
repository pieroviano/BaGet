using System.Threading;
using System.Threading.Tasks;
using BaGet.Identity.Mvc;
using BaGet.Identity.Workflows;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Identity.Controllers;

/// <summary>
/// The access audit log (<c>/admin/audit</c>): one entry per change to groups, memberships, permission targets, grants, projects, the
/// repositories' project and anonymous-read switch, newest first, filtered by who made the change, what it was done to and the dates.
/// Read only; server administrators only. It replaces the Razor Page <c>/Admin/Audit</c> (same URL and <c>asp-page</c> name); the
/// logic is in <see cref="AuditWorkflow"/>.
/// </summary>
public sealed class AdminAuditController : AccessControllerBase
{
    /// <summary>Lists the entries that match the filter (<c>actor</c>, <c>subject</c>, <c>from</c>, <c>to</c>).</summary>
    [Route("admin/audit"), HttpGet, HttpHead, PageHandler, LegacyPage("/Admin/Audit")]
    public async Task<IActionResult> Index(string? actor, string? subject, string? from, string? to, CancellationToken cancellationToken)
    {
        var workflow = new AuditWorkflow(HttpContext, Url) { IsServerAdministrator = IsServerAdministrator, CurrentUserId = CurrentUserId };
        workflow.Model.ActorFilter = actor;
        workflow.Model.Subject = subject;
        workflow.Model.From = from;
        workflow.Model.To = to;
        await workflow.GetAsync(cancellationToken);

        return View("Index", workflow.Model);
    }
}
