using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Mvc;
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

    /// <summary>Shows the settings.</summary>
    public async Task<IActionResult> OnGetAsync()
    {
        if (!Settings.IsAvailable) return NotFound();

        await LoadAsync();
        return Page();
    }

    /// <summary>
    /// Saves the settings of one section after the administrator confirms. Refused with HTTP 503 while the
    /// <see cref="MaintenanceGate"/> is closed.
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

        var result = parseErrors.Count == 0
            ? await Settings.SaveAsync(changes, user.UserName, HttpContext.RequestAborted)
            : SettingsSaveResult.Failed(parseErrors);
        if (!result.Succeeded) return await FailAsync(section, result.Errors);

        Message = changes.Count == 0 ? "Nothing changed." : $"Saved {changes.Count} setting(s).";
        return RedirectToPage(null, null, null, section);
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
