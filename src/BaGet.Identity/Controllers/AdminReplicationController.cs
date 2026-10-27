using System.Threading;
using System.Threading.Tasks;
using BaGet.Identity.Application.Models;
using BaGet.Identity.Controllers.Base;
using BaGet.Identity.Controllers.UI;
using BaGet.Identity.Mvc;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Identity.Controllers;

/// <summary>
/// The replication screens: the replications (<c>/admin/replication</c>) and one replication (<c>/admin/replication/{key}</c>).
/// They replace the Razor Pages of the same names (same URLs, handlers and <c>asp-page</c> names); the work is done by the JSON twin
/// (<see cref="ReplicationUiApiController"/>) and this controller only renders its answers. Administrators only (404 for anyone else), every POST
/// is refused with 503 while the read-only gate is closed. The peer API key is write only: it is never filled back into a form.
/// </summary>
public sealed class AdminReplicationController : ReplicationControllerBase
{
    // ---------------------------------------------------------------- replications

    /// <summary>Lists the replications.</summary>
    [Route("admin/replication"), HttpGet, HttpHead, PageHandler, LegacyPage("/Admin/Replication")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
        => ShowReplications(await Api<ReplicationUiApiController, ReplicationViewModel>(api => api.GetReplications(cancellationToken)));

    /// <summary>Creates a replication and opens it.</summary>
    [Route("admin/replication"), HttpPost, PageHandler, LegacyPage("/Admin/Replication")]
    public async Task<IActionResult> Create([Bind(Prefix = "Form")] ReplicationForm? form, string? password, CancellationToken cancellationToken)
        => ShowReplications(await Api<ReplicationUiApiController, ReplicationViewModel>(api => api.Create(new ReplicationBody(form, password), cancellationToken)));

    // ---------------------------------------------------------------- one replication

    /// <summary>Shows the replication.</summary>
    [Route("admin/replication/{key:int}"), HttpGet, HttpHead, PageHandler, LegacyPage("/Admin/ReplicationConfig")]
    public async Task<IActionResult> Config(int key, CancellationToken cancellationToken)
        => ShowConfig(await Api<ReplicationUiApiController, ReplicationConfigViewModel>(api => api.GetConfig(key, cancellationToken)));

    /// <summary>Saves the settings.</summary>
    [Route("admin/replication/{key:int}"), HttpPost, PageHandler("Save"), LegacyPage("/Admin/ReplicationConfig")]
    public async Task<IActionResult> Save(int key, [Bind(Prefix = "Form")] ReplicationForm? form, string? password, CancellationToken cancellationToken)
        => ShowConfig(await Api<ReplicationUiApiController, ReplicationConfigViewModel>(api => api.Save(key, new ReplicationBody(form, password), cancellationToken)));

    /// <summary>Tests the connection with the typed settings; saves nothing.</summary>
    [Route("admin/replication/{key:int}"), HttpPost, PageHandler("Test"), LegacyPage("/Admin/ReplicationConfig")]
    public async Task<IActionResult> Test(int key, [Bind(Prefix = "Form")] ReplicationForm? form, string? password, CancellationToken cancellationToken)
        => ShowConfig(await Api<ReplicationUiApiController, ReplicationConfigViewModel>(api => api.Test(key, new ReplicationBody(form, password), cancellationToken)));

    /// <summary>Starts a run.</summary>
    [Route("admin/replication/{key:int}"), HttpPost, PageHandler("Run"), LegacyPage("/Admin/ReplicationConfig")]
    public async Task<IActionResult> Run(int key, string? password, CancellationToken cancellationToken)
        => ShowConfig(await Api<ReplicationUiApiController, ReplicationConfigViewModel>(api => api.Run(key, new PasswordForm(password), cancellationToken)));

    /// <summary>Starts a run that continues the last interrupted or failed one.</summary>
    [Route("admin/replication/{key:int}"), HttpPost, PageHandler("Resume"), LegacyPage("/Admin/ReplicationConfig")]
    public async Task<IActionResult> Resume(int key, string? password, CancellationToken cancellationToken)
        => ShowConfig(await Api<ReplicationUiApiController, ReplicationConfigViewModel>(api => api.Resume(key, new PasswordForm(password), cancellationToken)));

    /// <summary>Asks the running replication to stop; it ends as interrupted and can be resumed.</summary>
    [Route("admin/replication/{key:int}"), HttpPost, PageHandler("Stop"), LegacyPage("/Admin/ReplicationConfig")]
    public async Task<IActionResult> Stop(int key, CancellationToken cancellationToken)
        => ShowConfig(await Api<ReplicationUiApiController, ReplicationConfigViewModel>(api => api.Stop(key, cancellationToken)));

    /// <summary>Deletes the configuration with its log.</summary>
    [Route("admin/replication/{key:int}"), HttpPost, PageHandler("Delete"), LegacyPage("/Admin/ReplicationConfig")]
    public async Task<IActionResult> Delete(int key, string? password, CancellationToken cancellationToken)
        => ShowConfig(await Api<ReplicationUiApiController, ReplicationConfigViewModel>(api => api.Delete(key, new PasswordForm(password), cancellationToken)));

    /// <summary>A form posted to the screen's own URL, with no handler: the screen is rendered (the page had no default handler).</summary>
    [Route("admin/replication/{key:int}"), HttpPost, PageHandler, ImplicitPageHandler, LegacyPage("/Admin/ReplicationConfig")]
    public async Task<IActionResult> ConfigPost(int key, CancellationToken cancellationToken)
        => ShowConfig(await Api<ReplicationUiApiController, ReplicationConfigViewModel>(api => api.GetConfig(key, cancellationToken)));

    // ---------------------------------------------------------------- helpers

    private IActionResult ShowReplications(UiApiResponse<ReplicationViewModel> response) => RenderFlashed(response, "Index");

    private IActionResult ShowConfig(UiApiResponse<ReplicationConfigViewModel> response) => RenderFlashed(response, "Config");
}
