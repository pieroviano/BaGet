using System.Threading;
using System.Threading.Tasks;
using BaGet.Identity.Application;
using BaGet.Identity.Application.Models;
using BaGet.Identity.Controllers.Base;
using BaGet.Identity.Controllers.UI;
using BaGet.Identity.Mvc;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Identity.Controllers;

/// <summary>
/// The access audit log (<c>/admin/audit</c>): one entry per change to groups, memberships, permission targets, grants, projects, the
/// repositories' project and anonymous-read switch, newest first, filtered by who made the change, what it was done to and the dates.
/// Read only; server administrators only. It replaces the Razor Page <c>/Admin/Audit</c> (same URL and <c>asp-page</c> name); the work is done by
/// the JSON twin (<see cref="AdminAuditUiApiController"/>) and this controller only renders its answer.
/// </summary>
public sealed class AdminAuditController : AccessControllerBase
{
    /// <summary>Lists the entries that match the filter (<c>actor</c>, <c>subject</c>, <c>from</c>, <c>to</c>).</summary>
    [Route("admin/audit"), HttpGet, HttpHead, PageHandler, LegacyPage("/Admin/Audit")]
    public async Task<IActionResult> Index(string? actor, string? subject, string? from, string? to, CancellationToken cancellationToken)
        => Render(await Api<AdminAuditUiApiController, AuditViewModel>(api => api.Get(actor, subject, from, to, cancellationToken)), "Index");
}
