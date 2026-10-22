using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.Pages.Account;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Versioning;

namespace BaGet.Identity.Pages.Packages;

/// <summary>
/// The owners' page for a package ID: its owners, its versions' listing and
/// deprecation, vulnerabilities and indexed symbols (PDBs), and the abuse reports against it. An
/// administrator may open it for any ID, ownerless ones included, and alone may set the ID's verified (reserved)
/// flag and delete permanently whatever <see cref="BaGetOptions.PackageDeletionBehavior"/> says, after confirming
/// their password. Everything works in the request's repository.
/// </summary>
public class ManageModel : AccountPageModel
{
    /// <summary>
    /// What every write handler here answers on a read-only server. The API refuses each
    /// of these operations with a 401, so the page must refuse all of them too rather
    /// than only the one it was last asked about.
    /// </summary>
    internal const string ReadOnlyMessage =
        "This server is in read-only mode, so nothing can be changed.";

    protected override bool RequiresSignIn => true;

    [BindProperty(SupportsGet = true)]
    public string? Id { get; set; }

    public IReadOnlyList<PackageOwner> Owners { get; private set; } = Array.Empty<PackageOwner>();

    public IReadOnlyList<Package> Versions { get; private set; } = Array.Empty<Package>();

    public IReadOnlyList<AbuseReport> Reports { get; private set; } = Array.Empty<AbuseReport>();

    public string? Message { get; private set; }

    /// <summary>Whether validation has submissions for the ID (the page links to their status).</summary>
    public bool HasValidation { get; private set; }

    /// <summary>Whether the ID is verified (reserved): an owner row carries the flag.</summary>
    public bool IsVerified => Owners.Any(o => o.IsVerified);

    /// <summary>
    /// The PDBs indexed from each version's symbol package, by normalized version (case-insensitive). Empty
    /// when <see cref="SymbolsSupported"/> is false; PDBs indexed before they were recorded are not listed.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<SymbolReference>> Symbols { get; private set; }
        = new Dictionary<string, IReadOnlyList<SymbolReference>>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Whether the database records symbols per package version (false for Azure Table).</summary>
    public bool SymbolsSupported => HttpContext.RequestServices.GetRequiredService<ISymbolRegistry>().IsSupported;

    /// <summary>The severity names a vulnerability can have, by their protocol number.</summary>
    public IReadOnlyList<string> SeverityNames => PackageVulnerabilityRules.SeverityNames;

    /// <summary>Whether the signed in user is an administrator.</summary>
    public bool IsAdministrator { get; private set; }

    /// <summary>
    /// Whether the user manages this ID only as an administrator: they are not one of its
    /// owners, directly or through an organization.
    /// </summary>
    public bool ActingAsAdministrator { get; private set; }

    /// <summary>Whether the signed in administrator has a password to confirm with.</summary>
    public bool HasPassword { get; private set; } = true;

    /// <summary>For an account without a password: it signed in again with its provider a moment ago.</summary>
    public bool Reauthenticated { get; private set; }

    /// <summary>Whether an external provider is available to sign in again with.</summary>
    public bool CanReauthenticate => Accounts.OpenIdConnect.IsConfigured;

    private IPackageDatabase Packages => HttpContext.RequestServices.GetRequiredService<IPackageDatabase>();

    private PackageChangeNotifier Notifier => HttpContext.RequestServices.GetRequiredService<PackageChangeNotifier>();

    private IContext Context => HttpContext.RequestServices.GetRequiredService<IContext>();

    private IPackageDeletionService Deletions => HttpContext.RequestServices.GetRequiredService<IPackageDeletionService>();

    private BaGetOptions Options => HttpContext.RequestServices.GetRequiredService<IOptionsSnapshot<BaGetOptions>>().Value;

    /// <summary>
    /// What a deletion does on this server. The two behaviours are different operations,
    /// so the page says which one the button performs rather than calling both "delete".
    /// </summary>
    public PackageDeletionBehavior DeletionBehavior => Options.PackageDeletionBehavior;

    public bool IsReadOnlyMode => Options.IsReadOnlyMode;

    public bool DeletionIsPermanent => DeletionBehavior == PackageDeletionBehavior.HardDelete;

    public string DeleteLabel => DeletionIsPermanent
        ? "Delete this version permanently"
        : "Unlist this version";

    public string DeleteCaption => DeletionIsPermanent
        ? "Removes the database rows and the .nupkg, .nuspec, readme and icon from storage. "
          + "Consumers can no longer restore it. Indexed PDBs keep being served, and an "
          + "external search index keeps the version until it is reindexed."
        : "Hidden from search and from the version list; anyone who knows the exact version "
          + "can still restore it.";

    public override async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        OnPageHandlerExecuting(context);
        if (context.Result != null) return;

        // Only an owner, directly or through an organization, or an administrator, may
        // manage the package.
        #pragma warning disable CS8602
        var isOwner = await Ownership.IsOwnerAsync(UserManager.GetUserId(User), User.Identity.Name, Id, HttpContext.RequestAborted);
        #pragma warning restore CS8602
        IsAdministrator = await HttpContext.RequestServices.GetRequiredService<AdministratorService>()
            .IsAdministratorAsync(User, HttpContext.RequestAborted);
        // An owner also needs the Manage permission in this repository (a seeded target gives every signed-in user
        // that, so nothing changes until an administrator narrows it); an administrator holds every action.
        if (isOwner && !IsAdministrator
            && !await ManagePermission.HeldAsync(HttpContext.RequestServices, User, Id, HttpContext.RequestAborted))
        {
            isOwner = false;
        }

        if (!isOwner && !IsAdministrator)
        {
            context.Result = StatusCode(403);
            return;
        }

        ActingAsAdministrator = IsAdministrator && !isOwner;
        // Every handler on this page but the GET writes to the database, and most of
        // them notify the catalog as well. Refused here, once, rather than in each
        // handler: unlisting, relisting, deprecating and changing owners are the same
        // operations the push and metadata endpoints short-circuit in read-only mode,
        // and a page that refused only one of them would contradict itself.
        if (IsReadOnlyMode && HttpMethods.IsPost(HttpContext.Request.Method))
        {
            Message = ReadOnlyMessage;
            await LoadAsync(HttpContext.RequestAborted);

            context.Result = Page();
            return;
        }

        if (ActingAsAdministrator && HttpMethods.IsPost(HttpContext.Request.Method))
        {
            // The audit trail: an administrator changing what is not theirs.
            HttpContext.RequestServices.GetRequiredService<ILogger<ManageModel>>().LogWarning(
                "Administrator {Admin} ran {Handler} on {PackageId}",
                User.Identity.Name,
                context.HandlerMethod?.Name,
                Id);
        }

        await next();
    }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadAsync(cancellationToken);
        return Versions.Count == 0 && Owners.Count == 0 ? NotFound() : Page();
    }

    public async Task<IActionResult> OnPostAddOwnerAsync(string ownerName, CancellationToken cancellationToken)
    {
        var result = string.IsNullOrWhiteSpace(ownerName)
            ? OwnerChangeResult.UnknownAccount
            : await Ownership.AddOwnerAsync(Id, ownerName.Trim(), cancellationToken);

        return await DoneAsync(result == OwnerChangeResult.Success ? $"{ownerName} is now an owner." : Describe(result), cancellationToken);
    }

    public async Task<IActionResult> OnPostRemoveOwnerAsync(string ownerName, CancellationToken cancellationToken)
    {
        var result = await Ownership.RemoveOwnerAsync(Id, ownerName, cancellationToken);

        return await DoneAsync(result == OwnerChangeResult.Success ? $"{ownerName} is no longer an owner." : Describe(result), cancellationToken);
    }

    public async Task<IActionResult> OnPostUnlistAsync(string version, CancellationToken cancellationToken)
        => await ChangeVersionAsync(version, v => Packages.UnlistPackageAsync(Id, v, cancellationToken), "unlisted", cancellationToken);

    public async Task<IActionResult> OnPostRelistAsync(string version, CancellationToken cancellationToken)
        => await ChangeVersionAsync(version, v => Packages.RelistPackageAsync(Id, v, cancellationToken), "relisted", cancellationToken);

    /// <summary>
    /// Deletion as the server is configured to perform it: <see cref="PackageDeletionBehavior.Unlist"/>
    /// hides the version, <see cref="PackageDeletionBehavior.HardDelete"/> removes its rows
    /// and its files. The page labels the button from the behaviour, so it never promises a
    /// deletion that only unlists.
    /// </summary>
    public async Task<IActionResult> OnPostDeleteAsync(string version, CancellationToken cancellationToken)
    {
        // Read-only mode is refused for every handler at once, in
        // OnPageHandlerExecutionAsync, as the push endpoint's DELETE refuses before
        // anything is touched.
        if (!NuGetVersion.TryParse(version, out var parsed)) return NotFound();

        // False means the database held no such version - not that nothing happened:
        // a hard delete removes the files either way.
        var found = await Deletions.TryDeletePackageAsync(Id, parsed, cancellationToken);
        var normalized = parsed.ToNormalizedString();

        // PackageDeletionService notifies the catalog itself, so nothing is notified here.
        if (DeletionIsPermanent)
        {
            return await DoneAsync(
                found
                    ? $"{Id} {normalized} is deleted. Its rows and its files are gone, and consumers "
                      + "can no longer restore it. Any indexed PDBs are still served, and an external "
                      + "search index keeps the version until it is reindexed."
                    : $"{Id} {normalized} was not in the database. Any files left in storage for it were removed.",
                cancellationToken);
        }

        return await DoneAsync(
            found
                ? $"{Id} {normalized} is unlisted. Deleting is configured to unlist on this server, so "
                  + "anyone who knows the exact version can still restore it."
                : $"There is no {Id} {normalized} to unlist.",
            cancellationToken);
    }

    /// <summary>
    /// Removes one version's rows and files whatever the server's deletion behaviour is.
    /// Administrators only, and only after they confirm their password: it cannot be undone.
    /// </summary>
    public async Task<IActionResult> OnPostHardDeleteAsync(string version, string? password, CancellationToken cancellationToken)
    {
        if (!IsAdministrator) return StatusCode(403);
        if (!NuGetVersion.TryParse(version, out var parsed)) return NotFound();

        var refusal = await RefuseUnconfirmedAsync(password, cancellationToken);
        if (refusal != null) return refusal;

        var found = await Deletions.TryHardDeletePackageAsync(Id, parsed, cancellationToken);
        var normalized = parsed.ToNormalizedString();

        #pragma warning disable CS8602
        Log().LogWarning("Administrator {Admin} permanently deleted {PackageId} {PackageVersion}", User.Identity.Name, Id, normalized);
        #pragma warning restore CS8602

        return await DoneAsync(
            found
                ? $"{Id} {normalized} is deleted permanently. Its rows and its files are gone, and consumers "
                  + "can no longer restore it. Any indexed PDBs are still served, and an external "
                  + "search index keeps the version until it is reindexed."
                : $"{Id} {normalized} was not in the database. Any files left in storage for it were removed.",
            cancellationToken);
    }

    /// <summary>
    /// Removes every version of the ID, then its owners, so the ID is free to claim again.
    /// Administrators only, and only after they confirm their password.
    /// </summary>
    public async Task<IActionResult> OnPostHardDeleteAllAsync(string? password, CancellationToken cancellationToken)
    {
        if (!IsAdministrator) return StatusCode(403);

        var refusal = await RefuseUnconfirmedAsync(password, cancellationToken);
        if (refusal != null) return refusal;

        var versions = await Packages.FindAsync(Id, includeUnlisted: true, cancellationToken);
        foreach (var package in versions)
        {
            await Deletions.TryHardDeletePackageAsync(Id, package.Version, cancellationToken);
        }

        #pragma warning disable CS8602
        var lowerId = Id.ToLowerInvariant();
        Context.PackageOwners.RemoveRange(await Context.PackageOwners
            .Where(o => o.PackageId.ToLower() == lowerId)
            .ToListAsync(cancellationToken));
        #pragma warning restore CS8602
        await Context.SaveChangesAsync(cancellationToken);

        #pragma warning disable CS8602
        Log().LogWarning(
            "Administrator {Admin} permanently deleted every version of {PackageId} ({Count}) and removed its owners",
            User.Identity.Name,
            Id,
            versions.Count);
        #pragma warning restore CS8602

        return await DoneAsync(
            $"Every version of {Id} ({versions.Count}) is deleted permanently, and the ID has no owners.",
            cancellationToken);
    }

    /// <summary>Null when the signed in user confirmed with their password, otherwise the page to show.</summary>
    private async Task<IActionResult?> RefuseUnconfirmedAsync(string? password, CancellationToken cancellationToken)
    {
        var current = await UserManager.GetUserAsync(User);
        if (current == null) return Challenge();

        return await IsConfirmedAsync(current, password)
            ? null
            : await DoneAsync("The confirmation is incorrect, so nothing was deleted.", cancellationToken);
    }

    private ILogger<ManageModel> Log() => HttpContext.RequestServices.GetRequiredService<ILogger<ManageModel>>();

    public async Task<IActionResult> OnPostDeprecateAsync(
        string version,
        List<string>? reasons,
        string message,
        string alternatePackageId,
        string? alternateVersionRange,
        CancellationToken cancellationToken)
    {
        reasons = reasons?.Where(r => PackageDeprecation.KnownReasons.Contains(r, StringComparer.Ordinal)).Distinct().ToList();
        if (reasons == null || reasons.Count == 0)
        {
            return await DoneAsync("Choose at least one deprecation reason.", cancellationToken);
        }

        // The same rules as the metadata API (PackageMetadataAdminController.Validate).
        var alternateId = string.IsNullOrWhiteSpace(alternatePackageId) ? null : alternatePackageId.Trim();
        var alternateRange = string.IsNullOrWhiteSpace(alternateVersionRange) ? null : alternateVersionRange.Trim();
        if (alternateId == null && alternateRange != null)
        {
            return await DoneAsync("An alternate version range needs an alternate package ID.", cancellationToken);
        }

        if (alternateRange != null && alternateRange != "*" && !VersionRange.TryParse(alternateRange, out _))
        {
            return await DoneAsync($"'{alternateRange}' is not a version range.", cancellationToken);
        }

        var deprecation = new PackageDeprecation
        {
            Reasons = reasons.ToArray(),
            Message = string.IsNullOrWhiteSpace(message) ? null : message.Trim(),
            AlternatePackageId = alternateId,
            AlternateVersionRange = alternateRange,
        };

        return await ChangeVersionAsync(version, v => Packages.SetDeprecationAsync(Id, v, deprecation, cancellationToken), "deprecated", cancellationToken);
    }

    public async Task<IActionResult> OnPostUndeprecateAsync(string version, CancellationToken cancellationToken)
        => await ChangeVersionAsync(version, v => Packages.RemoveDeprecationAsync(Id, v, cancellationToken), "no longer deprecated", cancellationToken);

    /// <summary>
    /// Adds one vulnerability to a version, through the rules the metadata API applies
    /// (<see cref="PackageVulnerabilityRules"/>).
    /// </summary>
    public async Task<IActionResult> OnPostAddVulnerabilityAsync(
        string version,
        string? advisoryUrl,
        int severity,
        string? versionRange,
        CancellationToken cancellationToken)
    {
        var added = new PackageVulnerability
        {
            AdvisoryUrl = advisoryUrl?.Trim(),
            Severity = severity,
            VersionRange = string.IsNullOrWhiteSpace(versionRange) ? null : versionRange.Trim(),
        };

        return await ChangeVulnerabilitiesAsync(version, current => current.Append(added).ToList(), "updated (vulnerability added)", cancellationToken);
    }

    /// <summary>Removes one vulnerability, identified by its key, from a version.</summary>
    public async Task<IActionResult> OnPostRemoveVulnerabilityAsync(string version, int key, CancellationToken cancellationToken)
        => await ChangeVulnerabilitiesAsync(version, current => current.Where(v => v.Key != key).ToList(), "updated (vulnerability removed)", cancellationToken);

    private async Task<IActionResult> ChangeVulnerabilitiesAsync(
        string version,
        Func<IReadOnlyList<PackageVulnerability>, IReadOnlyList<PackageVulnerability>> change,
        string done,
        CancellationToken cancellationToken)
    {
        if (!NuGetVersion.TryParse(version, out var parsed)) return NotFound();

        var package = await Packages.FindOrNullAsync(Id, parsed, includeUnlisted: true, cancellationToken);
        if (package == null) return NotFound();

        // Fresh copies: the stored rows are replaced as a whole, so the list must not reuse tracked entities.
        var updated = change(package.Vulnerabilities ?? new List<PackageVulnerability>())
            .Select(v => new PackageVulnerability
            {
                Key = v.Key,
                AdvisoryUrl = v.AdvisoryUrl,
                Severity = v.Severity,
                VersionRange = v.VersionRange,
            })
            .ToList();

        var error = PackageVulnerabilityRules.Validate(updated);
        if (error != null) return await DoneAsync(error, cancellationToken);

        foreach (var vulnerability in updated) vulnerability.Key = 0;

        return await ChangeVersionAsync(
            version,
            v => Packages.SetVulnerabilitiesAsync(Id!, v, updated, cancellationToken),
            done,
            cancellationToken);
    }

    /// <summary>
    /// Marks or unmarks the ID as verified (reserved for its owners). Administrators only: owners cannot
    /// grant the flag to themselves.
    /// </summary>
    public async Task<IActionResult> OnPostSetVerifiedAsync(bool verified, CancellationToken cancellationToken)
    {
        if (!IsAdministrator) return StatusCode(403);

        var changed = await HttpContext.RequestServices.GetRequiredService<PackageOwnershipService>()
            .SetVerifiedAsync(Id!, verified, cancellationToken);
        if (!changed) return NotFound();

        Log().LogWarning("Administrator {Admin} {Action} {PackageId}", User.Identity?.Name, verified ? "verified" : "unverified", Id);

        return await DoneAsync(verified ? $"{Id} is verified." : $"{Id} is no longer verified.", cancellationToken);
    }

    /// <summary>
    /// Deletes the PDBs recorded for a version. They stop being served; PDBs indexed before they were
    /// recorded cannot be deleted from here.
    /// </summary>
    public async Task<IActionResult> OnPostDeleteSymbolsAsync(string version, CancellationToken cancellationToken)
    {
        if (!NuGetVersion.TryParse(version, out var parsed)) return NotFound();

        var removed = await HttpContext.RequestServices.GetRequiredService<ISymbolRegistry>()
            .DeleteAsync(Id!, parsed, cancellationToken);

        return await DoneAsync(
            removed == 0
                ? $"{Id} {parsed.ToNormalizedString()} has no recorded symbols to delete."
                : $"Deleted {removed} symbol file(s) of {Id} {parsed.ToNormalizedString()}.",
            cancellationToken);
    }

    public async Task<IActionResult> OnPostResolveReportAsync(int key, CancellationToken cancellationToken)
    {
        #pragma warning disable CS8602
        var lowerId = Id.ToLowerInvariant();
        #pragma warning disable CS8604
        var report = await Context.AbuseReports.FirstOrDefaultAsync(r => r.Key == key && r.PackageId.ToLower() == lowerId, cancellationToken);
        #pragma warning restore CS8602, CS8604
        if (report != null && report.Resolved == null)
        {
            report.Resolved = HttpContext.RequestServices.GetRequiredService<SystemTime>().UtcNow;
            await Context.SaveChangesAsync(cancellationToken);
        }

        return await DoneAsync("The report is resolved.", cancellationToken);
    }

    private async Task<IActionResult> ChangeVersionAsync(
        string version,
        Func<NuGetVersion, Task<bool>> change,
        string done,
        CancellationToken cancellationToken)
    {
        if (!NuGetVersion.TryParse(version, out var parsed)) return NotFound();

        bool changed;
        try
        {
            changed = await change(parsed);
        }
        catch (NotSupportedException e)
        {
            return await DoneAsync(e.Message, cancellationToken);
        }

        if (!changed) return NotFound();

        await Notifier.PackageChangedAsync(Id, parsed, cancellationToken);

        return await DoneAsync($"{Id} {parsed.ToNormalizedString()} is {done}.", cancellationToken);
    }

    private async Task<IActionResult> DoneAsync(string message, CancellationToken cancellationToken)
    {
        Message = message;
        await LoadAsync(cancellationToken);

        return Page();
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        if (IsAdministrator)
        {
            var current = await UserManager.GetUserAsync(User);
            HasPassword = current == null || await UserManager.HasPasswordAsync(current);
            Reauthenticated = !HasPassword && await HasReauthenticatedRecentlyAsync();
        }

        Owners = await Ownership.GetOwnersAsync(Id, cancellationToken);
        Versions = (await Packages.FindAsync(Id, includeUnlisted: true, cancellationToken))
            .OrderByDescending(p => p.Version)
            .ToList();

        var registry = HttpContext.RequestServices.GetRequiredService<ISymbolRegistry>();
        if (registry.IsSupported)
        {
            var symbols = new Dictionary<string, IReadOnlyList<SymbolReference>>(StringComparer.OrdinalIgnoreCase);
            foreach (var package in Versions)
            {
                var listed = await registry.ListAsync(Id!, package.Version, cancellationToken);
                if (listed.Count > 0) symbols[package.Version.ToNormalizedString()] = listed;
            }

            Symbols = symbols;
        }

        if (!(Context is NullContext))
        {
            #pragma warning disable CS8602
            var lowerId = Id.ToLowerInvariant();
            #pragma warning disable CS8604
            Reports = await Context.AbuseReports
                .AsNoTracking()
                .Where(r => r.PackageId.ToLower() == lowerId && r.Resolved == null)
                .OrderByDescending(r => r.Key)
                .ToListAsync(cancellationToken);
            HasValidation = await Context.PackageValidations
                .AsNoTracking()
                .AnyAsync(v => v.PackageId.ToLower() == lowerId, cancellationToken);
            #pragma warning restore CS8602, CS8604
        }
    }

    private static string Describe(OwnerChangeResult result) => result switch
    {
        OwnerChangeResult.UnknownAccount => "There is no user or organization with that name.",
        OwnerChangeResult.AlreadyOwner => "That account is already an owner.",
        OwnerChangeResult.NotOwner => "That account is not an owner.",
        OwnerChangeResult.LastOwner => "A package must keep at least one owner.",
        _ => result.ToString(),
    };
}