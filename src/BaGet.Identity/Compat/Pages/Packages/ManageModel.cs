using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.Pages.Account;
using BaGet.Identity.Workflows;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace BaGet.Identity.Pages.Packages;

/// <summary>
/// The page model of the package management page when it was a Razor Page (<c>/packages/{id}/manage</c>): the owners of a package
/// ID, its versions' listing and deprecation, vulnerabilities, symbols and abuse reports.
/// </summary>
/// <remarks>
/// Kept as a shim so that code compiled against it still loads and runs (no breaking change of the public API); there is no page
/// behind it any more: <c>PackageManageController</c> serves the screen, and both run <see cref="ManageWorkflow"/>.
/// </remarks>
[Obsolete("The package management screen is served by PackageManageController; this page model has no page any more and will be removed in the next major version.")]
public class ManageModel : AccountPageModel
{
    /// <summary>What every write handler answers on a read-only server.</summary>
    internal const string ReadOnlyMessage = ManageWorkflow.ReadOnlyMessage;

    private ManageWorkflow? _workflow;

    private ManageWorkflow Workflow => _workflow ??= new ManageWorkflow(HttpContext, null);

    /// <inheritdoc/>
    protected override bool RequiresSignIn => true;

    /// <summary>The package ID from the address.</summary>
    [BindProperty(SupportsGet = true)]
    public string? Id
    {
        get => _workflow?.Id;
        set => Workflow.Id = value;
    }

    /// <summary>The ID's owners.</summary>
    public IReadOnlyList<PackageOwner> Owners => _workflow?.Model.Owners ?? Array.Empty<PackageOwner>();

    /// <summary>The ID's versions, newest first.</summary>
    public IReadOnlyList<Package> Versions => _workflow?.Model.Versions ?? Array.Empty<Package>();

    /// <summary>The open abuse reports against the ID.</summary>
    public IReadOnlyList<AbuseReport> Reports => _workflow?.Model.Reports ?? Array.Empty<AbuseReport>();

    /// <summary>The outcome of the last action.</summary>
    public string? Message => _workflow?.Model.Message;

    /// <summary>Whether validation has submissions for the ID.</summary>
    public bool HasValidation => _workflow?.Model.HasValidation ?? false;

    /// <summary>Whether the ID is verified (reserved): an owner row carries the flag.</summary>
    public bool IsVerified => Owners.Any(o => o.IsVerified);

    /// <summary>The PDBs indexed from each version's symbol package, by normalized version.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<SymbolReference>> Symbols
        => _workflow?.Model.Symbols ?? new Dictionary<string, IReadOnlyList<SymbolReference>>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Whether the database records symbols per package version (false for Azure Table).</summary>
    public bool SymbolsSupported => Workflow.SymbolsSupported;

    /// <summary>The severity names a vulnerability can have, by their protocol number.</summary>
    public IReadOnlyList<string> SeverityNames => PackageVulnerabilityRules.SeverityNames;

    /// <summary>Whether the signed in user is an administrator.</summary>
    public bool IsAdministrator => _workflow?.Model.IsAdministrator ?? false;

    /// <summary>Whether the user manages this ID only as an administrator.</summary>
    public bool ActingAsAdministrator => _workflow?.Model.ActingAsAdministrator ?? false;

    /// <summary>Whether the signed in administrator has a password to confirm with.</summary>
    public bool HasPassword => _workflow?.Model.HasPassword ?? true;

    /// <summary>For an account without a password: it signed in again with its provider a moment ago.</summary>
    public bool Reauthenticated => _workflow?.Model.Reauthenticated ?? false;

    /// <summary>Whether an external provider is available to sign in again with.</summary>
    public bool CanReauthenticate => Accounts.OpenIdConnect.IsConfigured;

    /// <summary>What a deletion does on this server.</summary>
    public PackageDeletionBehavior DeletionBehavior => Workflow.DeletionBehavior;

    /// <summary>Whether the server refuses writes.</summary>
    public bool IsReadOnlyMode => Workflow.ServerIsReadOnly;

    /// <summary>Whether a deletion removes the rows and files.</summary>
    public bool DeletionIsPermanent => DeletionBehavior == PackageDeletionBehavior.HardDelete;

    /// <summary>The label of the delete button.</summary>
    public string DeleteLabel => DeletionIsPermanent
        ? "Delete this version permanently"
        : "Unlist this version";

    /// <summary>What the delete button does, in words.</summary>
    public string DeleteCaption => DeletionIsPermanent
        ? "Removes the database rows and the .nupkg, .nuspec, readme and icon from storage. "
          + "Consumers can no longer restore it. Indexed PDBs keep being served, and an "
          + "external search index keeps the version until it is reindexed."
        : "Hidden from search and from the version list; anyone who knows the exact version "
          + "can still restore it.";

    /// <inheritdoc/>
    public override async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        OnPageHandlerExecuting(context);
        if (context.Result != null) return;

        var refusal = await Workflow.EnterAsync(context.HandlerMethod?.Name);
        if (refusal != null)
        {
            context.Result = Map(refusal);
            return;
        }

        await next();
    }

    /// <summary>Loads the screen; 404 when the ID has neither versions nor owners.</summary>
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
        => Map(await Workflow.GetAsync(cancellationToken));

    /// <summary>Adds an owner.</summary>
    public async Task<IActionResult> OnPostAddOwnerAsync(string ownerName, CancellationToken cancellationToken)
        => Map(await Workflow.AddOwnerAsync(ownerName, cancellationToken));

    /// <summary>Removes an owner.</summary>
    public async Task<IActionResult> OnPostRemoveOwnerAsync(string ownerName, CancellationToken cancellationToken)
        => Map(await Workflow.RemoveOwnerAsync(ownerName, cancellationToken));

    /// <summary>Unlists a version.</summary>
    public async Task<IActionResult> OnPostUnlistAsync(string version, CancellationToken cancellationToken)
        => Map(await Workflow.UnlistAsync(version, cancellationToken));

    /// <summary>Relists a version.</summary>
    public async Task<IActionResult> OnPostRelistAsync(string version, CancellationToken cancellationToken)
        => Map(await Workflow.RelistAsync(version, cancellationToken));

    /// <summary>Deletes a version as the server is configured to (unlist or permanent).</summary>
    public async Task<IActionResult> OnPostDeleteAsync(string version, CancellationToken cancellationToken)
        => Map(await Workflow.DeleteAsync(version, cancellationToken));

    /// <summary>Permanently deletes a version (administrators, after the password).</summary>
    public async Task<IActionResult> OnPostHardDeleteAsync(string version, string? password, CancellationToken cancellationToken)
        => Map(await Workflow.HardDeleteAsync(version, password, cancellationToken));

    /// <summary>Permanently deletes every version and the owners (administrators, after the password).</summary>
    public async Task<IActionResult> OnPostHardDeleteAllAsync(string? password, CancellationToken cancellationToken)
        => Map(await Workflow.HardDeleteAllAsync(password, cancellationToken));

    /// <summary>Deprecates a version.</summary>
    public async Task<IActionResult> OnPostDeprecateAsync(
        string version,
        List<string>? reasons,
        string message,
        string alternatePackageId,
        string? alternateVersionRange,
        CancellationToken cancellationToken)
        => Map(await Workflow.DeprecateAsync(version, reasons, message, alternatePackageId, alternateVersionRange, cancellationToken));

    /// <summary>Removes a version's deprecation.</summary>
    public async Task<IActionResult> OnPostUndeprecateAsync(string version, CancellationToken cancellationToken)
        => Map(await Workflow.UndeprecateAsync(version, cancellationToken));

    /// <summary>Adds a vulnerability to a version.</summary>
    public async Task<IActionResult> OnPostAddVulnerabilityAsync(
        string version,
        string? advisoryUrl,
        int severity,
        string? versionRange,
        CancellationToken cancellationToken)
        => Map(await Workflow.AddVulnerabilityAsync(version, advisoryUrl, severity, versionRange, cancellationToken));

    /// <summary>Removes a vulnerability from a version.</summary>
    public async Task<IActionResult> OnPostRemoveVulnerabilityAsync(string version, int key, CancellationToken cancellationToken)
        => Map(await Workflow.RemoveVulnerabilityAsync(version, key, cancellationToken));

    /// <summary>Marks or unmarks the ID as verified (administrators).</summary>
    public async Task<IActionResult> OnPostSetVerifiedAsync(bool verified, CancellationToken cancellationToken)
        => Map(await Workflow.SetVerifiedAsync(verified, cancellationToken));

    /// <summary>Deletes the PDBs recorded for a version.</summary>
    public async Task<IActionResult> OnPostDeleteSymbolsAsync(string version, CancellationToken cancellationToken)
        => Map(await Workflow.DeleteSymbolsAsync(version, cancellationToken));

    /// <summary>Resolves an abuse report.</summary>
    public async Task<IActionResult> OnPostResolveReportAsync(int key, CancellationToken cancellationToken)
        => Map(await Workflow.ResolveReportAsync(key, cancellationToken));

    private IActionResult Map(IActionResult result) => PackageWorkflowBase.IsShow(result) ? Page() : result;
}
