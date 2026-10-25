using System.Threading.Tasks;
using BaGet.Identity.Mvc;
using BaGet.Identity.Workflows;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Identity.Controllers;

/// <summary>
/// The server settings screen (<c>/admin/settings</c>): edits every registered setting, one form per section, the per-source log
/// levels, the database copy progress (<c>?handler=CopyStatus</c>) and the telemetry connection test. It replaces the Razor Page
/// <c>/Admin/Settings</c> (same URL, handlers and <c>asp-page</c> name); the logic is in <see cref="AdminSettingsWorkflow"/>. Every
/// change asks the administrator's password again and is refused with 503 while the read-only gate is closed.
/// </summary>
public sealed class AdminSettingsController : AdminControllerBase
{
    /// <summary>Shows the settings. <paramref name="section"/> is the id of the section to show.</summary>
    [Route("admin/settings"), HttpGet, HttpHead, PageHandler, LegacyPage("/Admin/Settings")]
    public async Task<IActionResult> Index(string? section = null)
    {
        var workflow = new AdminSettingsWorkflow(HttpContext, Url);

        return Show(workflow, await workflow.GetAsync(section));
    }

    /// <summary>The progress of the database copy as JSON, polled by the page.</summary>
    [Route("admin/settings"), HttpGet, PageHandler("CopyStatus"), LegacyPage("/Admin/Settings")]
    public IActionResult CopyStatus() => new AdminSettingsWorkflow(HttpContext, Url).GetCopyStatus();

    /// <summary>Saves the settings of one section after the administrator confirms.</summary>
    [Route("admin/settings"), HttpPost, PageHandler, LegacyPage("/Admin/Settings")]
    public async Task<IActionResult> Save(string? section, string? password)
    {
        var workflow = new AdminSettingsWorkflow(HttpContext, Url);

        return Show(workflow, await workflow.PostAsync(section, password));
    }

    /// <summary>Sets the lowest level logged for one source.</summary>
    [Route("admin/settings"), HttpPost, PageHandler("SetLogLevel"), LegacyPage("/Admin/Settings")]
    public async Task<IActionResult> SetLogLevel(string? source, string? level, string? password)
    {
        var workflow = new AdminSettingsWorkflow(HttpContext, Url);

        return Show(workflow, await workflow.SetLogLevelAsync(source, level, password));
    }

    /// <summary>Removes a per-source level saved on this page.</summary>
    [Route("admin/settings"), HttpPost, PageHandler("RemoveLogLevel"), LegacyPage("/Admin/Settings")]
    public async Task<IActionResult> RemoveLogLevel(string? source, string? password)
    {
        var workflow = new AdminSettingsWorkflow(HttpContext, Url);

        return Show(workflow, await workflow.RemoveLogLevelAsync(source, password));
    }

    /// <summary>Sends one test span to the posted, unsaved telemetry endpoint and shows the outcome.</summary>
    [Route("admin/settings"), HttpPost, PageHandler("TestTelemetry"), LegacyPage("/Admin/Settings")]
    public async Task<IActionResult> TestTelemetry(string? password)
    {
        var workflow = new AdminSettingsWorkflow(HttpContext, Url);

        return Show(workflow, await workflow.TestTelemetryAsync(password));
    }

    private IActionResult Show(AdminSettingsWorkflow workflow, IActionResult? result)
    {
        Flash(workflow.Model);

        return result ?? View("Index", workflow.Model);
    }
}
