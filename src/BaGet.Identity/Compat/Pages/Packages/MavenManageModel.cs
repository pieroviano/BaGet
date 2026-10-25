using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.Pages.Account;
using BaGet.Identity.Workflows;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace BaGet.Identity.Pages.Packages;

/// <summary>
/// The page model of the Maven artifact management page when it was a Razor Page (<c>/maven-manage/{group}/{artifact}</c>): owners,
/// versions, the files of the chosen version, deletion and snapshot clean-up, as the signed-in user.
/// </summary>
/// <remarks>
/// Kept as a shim so that code compiled against it still loads and runs (no breaking change of the public API); there is no page
/// behind it any more: <c>MavenManageController</c> serves the screen, and both run <see cref="MavenManageWorkflow"/>.
/// </remarks>
[Obsolete("The Maven artifact management screen is served by MavenManageController; this page model has no page any more and will be removed in the next major version.")]
public class MavenManageModel : AccountPageModel
{
    private MavenManageWorkflow? _workflow;

    private MavenManageWorkflow Workflow => _workflow ??= new MavenManageWorkflow(HttpContext, null, null, null);

    /// <inheritdoc/>
    protected override bool RequiresSignIn => true;

    /// <summary>The group id from the address.</summary>
    [BindProperty(SupportsGet = true)]
    public string? Group
    {
        get => _workflow?.Group;
        set => Workflow.Group = value;
    }

    /// <summary>The artifact id from the address.</summary>
    [BindProperty(SupportsGet = true)]
    public string? Artifact
    {
        get => _workflow?.Artifact;
        set => Workflow.Artifact = value;
    }

    /// <summary>The version whose files are listed (<c>?version=</c>), or null.</summary>
    [BindProperty(SupportsGet = true, Name = "version")]
    public string? Version
    {
        get => _workflow?.Version;
        set => Workflow.Version = value;
    }

    /// <summary>The artifact, or null before it is loaded (or after its last version went).</summary>
    public MavenArtifactInfo? Info => _workflow?.Model.Info;

    /// <summary>The chosen version with its files, or null.</summary>
    public MavenVersionDetail? Selected => _workflow?.Model.Selected;

    /// <summary>The artifact's owners.</summary>
    public IReadOnlyList<PackageOwner> Owners => _workflow?.Model.Owners ?? Array.Empty<PackageOwner>();

    /// <summary>The outcome of the last action.</summary>
    public string? Message => _workflow?.Model.Message;

    /// <summary>Whether <see cref="Message"/> reports a refusal.</summary>
    public bool Failed => _workflow?.Model.Failed ?? false;

    /// <summary>Whether the signed-in user is an administrator.</summary>
    public bool IsAdministrator => _workflow?.Model.IsAdministrator ?? false;

    /// <summary>Whether the user manages the artifact only as an administrator (they are not an owner).</summary>
    public bool ActingAsAdministrator => _workflow?.Model.ActingAsAdministrator ?? false;

    /// <summary>Whether the user has a password to confirm with.</summary>
    public bool HasPassword => _workflow?.Model.HasPassword ?? true;

    /// <summary>For an account without a password: it signed in again with its provider a moment ago.</summary>
    public bool Reauthenticated => _workflow?.Model.Reauthenticated ?? false;

    /// <summary>Whether an external provider is available to sign in again with.</summary>
    public bool CanReauthenticate => Accounts.OpenIdConnect.IsConfigured;

    /// <summary><c>group:artifact</c>.</summary>
    public string Name => Workflow.Name;

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

    /// <summary>Loads the artifact; 404 when it does not exist.</summary>
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
        => Map(await Workflow.GetAsync(cancellationToken));

    /// <summary>Adds an owner.</summary>
    public async Task<IActionResult> OnPostAddOwnerAsync(string ownerName, CancellationToken cancellationToken)
        => Map(await Workflow.AddOwnerAsync(ownerName, cancellationToken));

    /// <summary>Removes an owner.</summary>
    public async Task<IActionResult> OnPostRemoveOwnerAsync(string ownerName, CancellationToken cancellationToken)
        => Map(await Workflow.RemoveOwnerAsync(ownerName, cancellationToken));

    /// <summary>Deletes one file of the chosen version (and its checksums with it), after the password is confirmed.</summary>
    public async Task<IActionResult> OnPostDeleteFileAsync(string? fileName, string? password, CancellationToken cancellationToken)
        => Map(await Workflow.DeleteFileAsync(fileName, password, cancellationToken));

    /// <summary>Deletes one version with its files, after the password is confirmed.</summary>
    public async Task<IActionResult> OnPostDeleteVersionAsync(string? version, string? password, CancellationToken cancellationToken)
        => Map(await Workflow.DeleteVersionAsync(version, password, cancellationToken));

    /// <summary>Deletes every version of the artifact, after the password is confirmed.</summary>
    public async Task<IActionResult> OnPostDeleteArtifactAsync(string? password, CancellationToken cancellationToken)
        => Map(await Workflow.DeleteArtifactAsync(password, cancellationToken));

    /// <summary>Deletes the builds of a snapshot version older than the newest <paramref name="keep"/>.</summary>
    public async Task<IActionResult> OnPostCleanupAsync(string? version, string? keep, string? password, CancellationToken cancellationToken)
        => Map(await Workflow.CleanupAsync(version, keep, password, cancellationToken));

    private IActionResult Map(IActionResult result) => PackageWorkflowBase.IsShow(result) ? Page() : result;
}
