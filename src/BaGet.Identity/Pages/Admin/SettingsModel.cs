using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.Workflows;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Identity.Pages.Admin;

/// <summary>
/// The page model of the server settings page when it was a Razor Page: edits every registered setting; values are written to the
/// overrides file (see <see cref="SettingsService"/>), one form per section.
/// </summary>
/// <remarks>
/// Kept as a shim (no breaking change of the public API); <c>AdminSettingsController</c> serves <c>/admin/settings</c>. Both call
/// <c>SettingsWorkflow</c>.
/// </remarks>
[Obsolete("The settings screen is served by AdminSettingsController; this page model has no page any more and will be removed in the next major version.")]
public class SettingsModel : AdminPageModel
{
    private SettingsWorkflow? _workflow;

    private SettingsWorkflow Workflow => _workflow ??= new SettingsWorkflow(HttpContext, Url);

    /// <summary>The settings grouped by section; the top-level keys come first, under the empty key.</summary>
    public IReadOnlyList<IGrouping<string, SettingState>> Sections => Workflow.Model.Sections;

    /// <summary>Whether the signed in administrator has a password to confirm with.</summary>
    public bool HasPassword => Workflow.Model.HasPassword;

    /// <summary>For an account without a password: it signed in again with its provider a moment ago.</summary>
    public bool Reauthenticated => Workflow.Model.Reauthenticated;

    /// <summary>Whether an external provider is available to sign in again with.</summary>
    public bool CanReauthenticate => Accounts.OpenIdConnect.IsConfigured;

    /// <summary>The file the settings are saved to.</summary>
    public string? OverridesPath => Workflow.Model.OverridesPath;

    /// <summary>The keys changed since startup that only apply after a restart.</summary>
    public IReadOnlyList<string> PendingRestart => Workflow.Model.PendingRestart;

    /// <summary>Outcome of the last save.</summary>
    [TempData]
    public string? Message { get => Workflow.Model.Message; set => Workflow.Model.Message = value; }

    /// <summary>Why the last save was refused.</summary>
    public IReadOnlyList<string> Errors => Workflow.Model.Errors;

    /// <summary>The section the <see cref="Errors"/> belong to.</summary>
    public string? ErrorSection => Workflow.Model.ErrorSection;

    /// <summary>The per-source log levels (<c>Serilog:MinimumLevel:Override</c>) shown under the Serilog section.</summary>
    public IReadOnlyList<LogLevelOverride> LogLevels => Workflow.Model.LogLevels;

    /// <summary>The level names offered for a source.</summary>
    public IReadOnlyList<string> LogLevelNames => LogLevelOverrides.Levels;

    /// <summary>Why the overrides file could not be read, or null.</summary>
    public string? LoadError => Workflow.Model.LoadError;

    /// <summary>The state of each telemetry signal for the Telemetry section, or null when the host exports no telemetry.</summary>
    public IReadOnlyList<TelemetrySignalReport>? TelemetrySignals => Workflow.Model.TelemetrySignals;

    /// <summary>True when this host exports telemetry (registers <see cref="ITelemetryDiagnostics"/>).</summary>
    public bool HostExportsTelemetry => Workflow.Model.HostExportsTelemetry;

    /// <summary>The outcome of the last "Test connection", or null. Shown in the Telemetry section; never saved.</summary>
    public TelemetryTestOutcome? TelemetryTest => Workflow.Model.TelemetryTest;

    /// <summary>Whether the running database is relational, so a change of database can copy the data.</summary>
    public bool CanCopyDatabase => Workflow.Model.CanCopyDatabase;

    /// <summary>The <c>Database:Type</c> values a copy can target; the option is hidden for any other type.</summary>
    public IReadOnlyList<string> CopyTargetTypes => Workflow.Model.CopyTargetTypes;

    /// <summary>The state of the database copy, if one was requested since startup.</summary>
    public DatabaseCopyStatus CopyStatus => Workflow.Model.CopyStatus;

    /// <summary>The DOM id (slug) of the section shown; every other section is rendered <c>hidden</c>.</summary>
    public string SelectedId => Workflow.Model.SelectedId;

    /// <summary>True when the server picked the section for a reason the URL fragment must not override.</summary>
    public bool SelectionLocked => Workflow.Model.SelectionLocked;

    /// <summary>The stable DOM id of a section: <c>general</c> for the empty key, else the key made safe for an id.</summary>
    public string SectionId(string key) => Workflow.Model.SectionId(key);

    /// <summary>Shows the settings. <paramref name="section"/> is the id of the section to show (no JavaScript needed).</summary>
    public async Task<IActionResult> OnGetAsync(string? section = null)
        => await Workflow.GetAsync(section) ?? Page();

    /// <summary>The progress of the database copy as JSON, polled by the page.</summary>
    public IActionResult OnGetCopyStatus() => Workflow.GetCopyStatus();

    /// <summary>Saves the settings of one section after the administrator confirms.</summary>
    public async Task<IActionResult> OnPostAsync(string section, string? password)
        => await Workflow.PostAsync(section, password) ?? Page();

    /// <summary>Sets the lowest level logged for one source (a namespace prefix); applies live.</summary>
    public async Task<IActionResult> OnPostSetLogLevelAsync(string? source, string? level, string? password)
        => await Workflow.SetLogLevelAsync(source, level, password) ?? Page();

    /// <summary>Removes a per-source level saved on this page.</summary>
    public async Task<IActionResult> OnPostRemoveLogLevelAsync(string? source, string? password)
        => await Workflow.RemoveLogLevelAsync(source, password) ?? Page();

    /// <summary>Sends one test span to the posted, unsaved telemetry endpoint and shows the outcome; nothing is saved.</summary>
    public async Task<IActionResult> OnPostTestTelemetryAsync(string? password)
        => await Workflow.TestTelemetryAsync(password) ?? Page();
}
