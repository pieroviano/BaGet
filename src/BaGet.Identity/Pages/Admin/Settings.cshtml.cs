using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BaGet.Identity.Pages.Admin;

/// <summary>
/// Edits every registered setting. Values are written to the overrides file; see
/// <see cref="SettingsService"/>. One form per section, so one section's errors never block another.
/// </summary>
public class SettingsModel : AdminPageModel
{
    private SettingsService Settings => HttpContext.RequestServices.GetRequiredService<SettingsService>();

    /// <summary>The settings grouped by section; the top-level keys come first, under the empty key.</summary>
    public IReadOnlyList<IGrouping<string, SettingState>> Sections { get; private set; } = Array.Empty<IGrouping<string, SettingState>>();

    /// <summary>Whether the signed in administrator has a password to confirm with.</summary>
    public bool HasPassword { get; private set; } = true;

    /// <summary>For an account without a password: it signed in again with its provider a moment ago.</summary>
    public bool Reauthenticated { get; private set; }

    /// <summary>Whether an external provider is available to sign in again with.</summary>
    public bool CanReauthenticate => Accounts.OpenIdConnect.IsConfigured;

    /// <summary>The file the settings are saved to.</summary>
    public string? OverridesPath => Settings.OverridesPath;

    /// <summary>The keys changed since startup that only apply after a restart.</summary>
    public IReadOnlyList<string> PendingRestart { get; private set; } = Array.Empty<string>();

    /// <summary>Outcome of the last save.</summary>
    [TempData]
    public string? Message { get; set; }

    /// <summary>Why the last save was refused.</summary>
    public IReadOnlyList<string> Errors { get; private set; } = Array.Empty<string>();

    /// <summary>The section the <see cref="Errors"/> belong to.</summary>
    public string? ErrorSection { get; private set; }

    /// <summary>Why the overrides file could not be read, or null.</summary>
    public string? LoadError { get; private set; }

    private DatabaseCopyService Copier => HttpContext.RequestServices.GetRequiredService<DatabaseCopyService>();

    /// <summary>
    /// Whether the running database is relational, so a change of database can copy the data
    /// (the "Copy all data" option of the Database section).
    /// </summary>
    public bool CanCopyDatabase => Copier.CurrentRelationalType != null;

    /// <summary>The <c>Database:Type</c> values a copy can target; the option is hidden for any other type.</summary>
    public IReadOnlyList<string> CopyTargetTypes => Copier.RelationalTypes;

    /// <summary>The state of the database copy, if one was requested since startup.</summary>
    public DatabaseCopyStatus CopyStatus => Copier.Status;

    /// <summary>Shows the settings.</summary>
    public async Task<IActionResult> OnGetAsync()
    {
        if (!Settings.IsAvailable) return NotFound();

        await LoadAsync();
        return Page();
    }

    /// <summary>
    /// The progress of the database copy as JSON, polled by the page. Administrators only (see
    /// <see cref="AdminPageModel"/>); holds no connection string.
    /// </summary>
    public IActionResult OnGetCopyStatus()
    {
        if (!Settings.IsAvailable) return NotFound();

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
    /// <see cref="MaintenanceGate"/> is closed. A Database section that changes to another relational database
    /// with "Copy all data" ticked is queued as a copy (<see cref="DatabaseCopyService"/>) instead of saved:
    /// the settings are saved by the copy once it is verified.
    /// </summary>
    public async Task<IActionResult> OnPostAsync(string section, string? password)
    {
        if (!Settings.IsAvailable) return NotFound();

        // Saving writes a file, not the database, so the SaveChanges gate would not stop it; but a save
        // while the database is read-only for maintenance (a copy, or the wait for a restart) could
        // change the very settings the job depends on. The job itself saves through SettingsService.
        HttpContext.RequestServices.GetService<MaintenanceGate>()?.ThrowIfClosed();

        section ??= string.Empty;
        var user = await UserManager.GetUserAsync(User);
        if (user == null) return Challenge();

        if (!await IsConfirmedAsync(user, password))
        {
            return await FailAsync(section, new[] { "The confirmation is incorrect, so nothing was changed." });
        }

        var states = Settings.GetState()
            .Where(s => string.Equals(s.Descriptor.Section, section, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var (changes, parseErrors) = SettingsForm.Parse(Request.Form, states);

        if (parseErrors.Count == 0
            && string.Equals(section, "Database", StringComparison.OrdinalIgnoreCase)
            && Request.Form["copyData"].Any(v => string.Equals(v, "true", StringComparison.OrdinalIgnoreCase))
            && PlanCopy(changes, user.UserName) is { } copy)
        {
            if (!Copier.TryQueue(copy, out var refusal)) return await FailAsync(section, new[] { refusal! });

            Message = "Copying the data to the new database. The settings are saved when the copy is verified.";
            return RedirectToPage(null, null, null, section);
        }

        var result = parseErrors.Count == 0
            ? await Settings.SaveAsync(changes, user.UserName, HttpContext.RequestAborted)
            : SettingsSaveResult.Failed(parseErrors);
        if (!result.Succeeded) return await FailAsync(section, result.Errors);

        Message = changes.Count == 0 ? "Nothing changed." : $"Saved {changes.Count} setting(s).";
        return RedirectToPage(null, null, null, section);
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

        var configuration = HttpContext.RequestServices.GetRequiredService<IConfiguration>();
        type ??= configuration["Database:Type"];
        connection ??= configuration["Database:ConnectionString"];
        var mySqlVersion = Changed("Database:MySqlServerVersion", out _) ?? configuration["Database:MySqlServerVersion"];

        if (string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(connection)) return null;
        if (!Copier.IsRelational(Copier.CurrentRelationalType) || !Copier.IsRelational(type)) return null;
        if (Copier.IsCurrentDatabase(type, connection)) return null;

        return new DatabaseCopyRequest(type, connection, mySqlVersion, changes, actor);
    }

    private async Task<IActionResult> FailAsync(string section, IReadOnlyList<string> errors)
    {
        Errors = errors;
        ErrorSection = section;
        await LoadAsync();
        return Page();
    }

    private async Task LoadAsync()
    {
        var states = Settings.GetState();
        LoadError = Settings.OverridesError;

        Sections = states
            .GroupBy(s => s.Descriptor.Section ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key.Length == 0 ? 0 : 1)
            .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();
        PendingRestart = states.Where(s => s.PendingRestart).Select(s => s.Descriptor.Key).ToList();

        var current = await UserManager.GetUserAsync(User);
        HasPassword = current == null || await UserManager.HasPasswordAsync(current);
        Reauthenticated = !HasPassword && await HasReauthenticatedRecentlyAsync();
    }
}
