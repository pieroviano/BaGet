using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BaGet.Identity.Workflows;

/// <summary>
/// Edits every registered setting. Values are written to the overrides file; see <see cref="SettingsService"/>. One form per
/// section, so one section's errors never block another. Used by <c>AdminSettingsController</c>.
/// </summary>
internal sealed class AdminSettingsWorkflow : AdminWorkflow
{
    public AdminSettingsWorkflow(HttpContext httpContext, IUrlHelper url) : base(httpContext, url)
    {
    }

    /// <summary>The state the screen renders.</summary>
    public AdminSettingsViewModel Model { get; } = new();

    private SettingsService Settings => Services.GetRequiredService<SettingsService>();

    private ITelemetryDiagnostics? Telemetry => Services.GetService<ITelemetryDiagnostics>();

    private DatabaseCopyService Copier => Services.GetRequiredService<DatabaseCopyService>();

    /// <summary>Shows the settings. <paramref name="section"/> is the id of the section to show (no JavaScript needed).</summary>
    public async Task<IActionResult?> GetAsync(string? section = null)
    {
        if (!Settings.IsAvailable) return new NotFoundResult();

        await LoadAsync();
        Select(section);

        return null;
    }

    /// <summary>
    /// The progress of the database copy as JSON, polled by the page. Administrators only (the admin check of the base class that
    /// runs first); holds no connection string.
    /// </summary>
    public IActionResult GetCopyStatus()
    {
        if (!Settings.IsAvailable) return new NotFoundResult();

        var status = Copier.Status;
        return new JsonResult(new
        {
            phase = status.Phase.ToString(),
            active = status.IsActive,
            finished = status.IsFinished,
            succeeded = status.Phase == DatabaseCopyPhase.Succeeded,
            restartRequired = status.RestartRequired,
            table = status.Table,
            tableRowsCopied = status.TableRowsCopied,
            tableRowsTotal = status.TableRowsTotal,
            tablesDone = status.TablesDone,
            tablesTotal = status.TablesTotal,
            rowsCopied = status.RowsCopied,
            rowsTotal = status.RowsTotal,
            message = status.Message,
            errors = status.Errors,
        });
    }

    /// <summary>
    /// Saves the settings of one section after the administrator confirms. Refused with HTTP 503 while the
    /// <see cref="MaintenanceGate"/> is closed. A Database section that changes to another relational database with "Copy all
    /// data" ticked is queued as a copy (<see cref="DatabaseCopyService"/>) instead of saved: the settings are saved by the copy
    /// once it is verified.
    /// </summary>
    public async Task<IActionResult?> PostAsync(string? section, string? password)
    {
        if (!Settings.IsAvailable) return new NotFoundResult();

        // Saving writes a file, not the database, so the SaveChanges gate would not stop it; but a save
        // while the database is read-only for maintenance (a copy, or the wait for a restart) could
        // change the very settings the job depends on. The job itself saves through SettingsService.
        ThrowIfClosed();

        section ??= string.Empty;
        var user = await UserManager.GetUserAsync(User);
        if (user == null) return new ChallengeResult();

        if (!await IsConfirmedAsync(user, password))
        {
            return await FailAsync(section, new[] { "The confirmation is incorrect, so nothing was changed." });
        }

        var form = Http.Request.Form;
        var states = Settings.GetState()
            .Where(s => string.Equals(s.Descriptor.Section, section, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var (changes, parseErrors) = SettingsForm.Parse(form, states);

        if (parseErrors.Count == 0
            && string.Equals(section, "Database", StringComparison.OrdinalIgnoreCase)
            && form["copyData"].Any(v => string.Equals(v, "true", StringComparison.OrdinalIgnoreCase))
            && PlanCopy(changes, user.UserName) is { } copy)
        {
            if (!Copier.TryQueue(copy, out var refusal)) return await FailAsync(section, new[] { refusal! });

            Model.Message = "Copying the data to the new database. The settings are saved when the copy is verified.";
            return RedirectToSection(section);
        }

        var result = parseErrors.Count == 0
            ? await Settings.SaveAsync(changes, user.UserName, Http.RequestAborted)
            : SettingsSaveResult.Failed(parseErrors);
        if (!result.Succeeded) return await FailAsync(section, result.Errors);

        Model.Message = changes.Count == 0 ? "Nothing changed." : $"Saved {changes.Count} setting(s).";
        return RedirectToSection(section);
    }

    /// <summary>
    /// Sets the lowest level logged for one source (a namespace prefix); applies live. Confirmed with the password like a save,
    /// refused with HTTP 503 while the <see cref="MaintenanceGate"/> is closed.
    /// </summary>
    public Task<IActionResult?> SetLogLevelAsync(string? source, string? level, string? password)
        => ChangeLogLevelAsync(source?.Trim(), level ?? string.Empty, password, "set");

    /// <summary>Removes a per-source level saved on this page (one a configuration file sets is only given another level).</summary>
    public Task<IActionResult?> RemoveLogLevelAsync(string? source, string? password)
        => ChangeLogLevelAsync(source?.Trim(), null, password, "removed");

    private async Task<IActionResult?> ChangeLogLevelAsync(string? source, string? level, string? password, string verb)
    {
        if (!Settings.IsAvailable) return new NotFoundResult();

        ThrowIfClosed();

        var user = await UserManager.GetUserAsync(User);
        if (user == null) return new ChallengeResult();

        if (!await IsConfirmedAsync(user, password))
        {
            return await FailAsync("Serilog", new[] { "The confirmation is incorrect, so nothing was changed." });
        }

        var result = await Settings.SetLogLevelOverrideAsync(source, level, user.UserName, Http.RequestAborted);
        if (!result.Succeeded) return await FailAsync("Serilog", result.Errors);

        Model.Message = level == null ? $"The level of {source} was {verb}." : $"The level of {source} was {verb} to {LogLevelOverrides.NormalizeLevel(level)}.";
        return RedirectToSection("Serilog");
    }

    /// <summary>
    /// Sends one test span to the Telemetry section's <i>posted, unsaved</i> endpoint, protocol and headers and shows the
    /// outcome; nothing is saved. Confirmed like a save (a test sends the headers, which may hold a credential, to an address the
    /// administrator chose). A blank headers field keeps the saved headers unless "Clear" is ticked. Not refused while the
    /// <see cref="MaintenanceGate"/> is closed: it writes nothing.
    /// </summary>
    public async Task<IActionResult?> TestTelemetryAsync(string? password)
    {
        if (!Settings.IsAvailable || Telemetry == null) return new NotFoundResult();

        var user = await UserManager.GetUserAsync(User);
        if (user == null) return new ChallengeResult();

        if (!await IsConfirmedAsync(user, password))
        {
            return await FailAsync("Telemetry", new[] { "The confirmation is incorrect, so nothing was tested." });
        }

        var form = Http.Request.Form;
        string? Posted(string key) => form.TryGetValue(SettingsForm.ValuePrefix + key, out var v) ? v.ToString().Trim() : null;

        var headers = form.ContainsKey(SettingsForm.ClearPrefix + "Telemetry:Otlp:Headers")
            ? string.Empty
            : Posted("Telemetry:Otlp:Headers") is { Length: > 0 } typed ? typed : null;

        Model.TelemetryTest = await Telemetry.TestAsync(
            Posted("Telemetry:Otlp:Endpoint"),
            Posted("Telemetry:Otlp:Protocol"),
            headers,
            Http.RequestAborted);

        await LoadAsync();
        Select(null, "Telemetry", locked: true);

        return null;
    }

    private void Select(string? requested, string? failedSection = null, bool locked = false)
    {
        string? Find(string? value) => value == null ? null : Model.Ids.FirstOrDefault(i => string.Equals(i, value, StringComparison.OrdinalIgnoreCase));

        var fromKey = failedSection != null && Model.TryGetId(failedSection, out var failed)
            ? failed
            : failedSection != null ? Find(failedSection) : null;

        Model.SelectionLocked = locked && fromKey != null;
        Model.SelectedId = fromKey ?? Find(requested) ?? (Model.Sections.Count > 0 ? Model.SectionId(Model.Sections[0].Key) : string.Empty);
    }

    /// <summary>Back to the screen showing <paramref name="section"/>: <c>?section=id</c> (the server renders it) and <c>#id</c> (the browser keeps it).</summary>
    private static IActionResult RedirectToSection(string section)
    {
        var id = AdminSettingsViewModel.BuildId(section);

        return RedirectToScreen("/Admin/Settings", new { section = id }, id);
    }

    /// <summary>
    /// The copy a Database section asks for, or null when it is an ordinary save: neither the type nor the
    /// connection string is set to a new value, either database is not relational (AzureTable), or it is the
    /// database already running.
    /// </summary>
    private DatabaseCopyRequest? PlanCopy(IReadOnlyDictionary<SettingDescriptor, object> changes, string? actor)
    {
        string? Changed(string key, out bool reset)
        {
            reset = false;
            foreach (var change in changes)
            {
                if (!string.Equals(change.Key.Key, key, StringComparison.OrdinalIgnoreCase)) continue;

                reset = change.Value == null;
                return change.Value as string;
            }

            return null;
        }

        var type = Changed("Database:Type", out var typeReset);
        var connection = Changed("Database:ConnectionString", out var connectionReset);
        if (typeReset || connectionReset) return null;
        if (type == null && connection == null) return null;

        var configuration = Services.GetRequiredService<IConfiguration>();
        type ??= configuration["Database:Type"];
        connection ??= configuration["Database:ConnectionString"];
        var mySqlVersion = Changed("Database:MySqlServerVersion", out _) ?? configuration["Database:MySqlServerVersion"];

        if (string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(connection)) return null;
        if (!Copier.IsRelational(Copier.CurrentRelationalType) || !Copier.IsRelational(type)) return null;
        if (Copier.IsCurrentDatabase(type, connection)) return null;

        return new DatabaseCopyRequest(type, connection, mySqlVersion, changes, actor);
    }

    private async Task<IActionResult?> FailAsync(string section, IReadOnlyList<string> errors)
    {
        Model.Errors = errors;
        Model.ErrorSection = section;
        await LoadAsync();
        Select(null, section, locked: true);

        return null;
    }

    private async Task LoadAsync()
    {
        var states = Settings.GetState();
        Model.LoadError = Settings.OverridesError;
        Model.OverridesPath = Settings.OverridesPath;

        Model.Sections = states
            .GroupBy(s => s.Descriptor.Section ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key.Length == 0 ? 0 : 1)
            .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var ids = new Dictionary<string, string>(StringComparer.Ordinal);
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in Model.Sections)
        {
            var id = AdminSettingsViewModel.BuildId(group.Key);
            var candidate = id;
            for (var n = 2; !used.Add(candidate); n++) candidate = id + "-" + n;
            ids[group.Key] = candidate;
        }
        Model.SetIds(ids);
        Model.LogLevels = Settings.GetLogLevelOverrides();
        Model.TelemetrySignals = Telemetry?.GetStatus();
        Model.PendingRestart = states.Where(s => s.PendingRestart).Select(s => s.Descriptor.Key).ToList();
        Model.CanCopyDatabase = Copier.CurrentRelationalType != null;
        Model.CopyTargetTypes = Copier.RelationalTypes;
        Model.CopyStatus = Copier.Status;

        await LoadConfirmationAsync(Model);
    }
}
