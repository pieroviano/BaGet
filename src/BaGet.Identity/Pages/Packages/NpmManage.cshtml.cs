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
/// The page model of the npm package management page when it was a Razor Page (<c>/npm-manage/{*id}</c>): owners, dist-tags,
/// per-version deprecation and unpublishing, as the signed-in user.
/// </summary>
/// <remarks>
/// Kept as a shim so that code compiled against it still loads and runs (no breaking change of the public API); there is no page
/// behind it any more: <c>NpmManageController</c> serves the screen, and both run <see cref="NpmManageWorkflow"/>.
/// </remarks>
[Obsolete("The npm package management screen is served by NpmManageController; this page model has no page any more and will be removed in the next major version.")]
public class NpmManageModel : AccountPageModel
{
    private NpmManageWorkflow? _workflow;

    private NpmManageWorkflow Workflow => _workflow ??= new NpmManageWorkflow(HttpContext, null);

    /// <inheritdoc/>
    protected override bool RequiresSignIn => true;

    /// <summary>The package name from the address.</summary>
    [BindProperty(SupportsGet = true)]
    public string? Id
    {
        get => _workflow?.Id;
        set => Workflow.Id = value;
    }

    /// <summary>The package, or null before it is loaded.</summary>
    public NpmPackageInfo? Package => _workflow?.Model.Package;

    /// <summary>The package's owners.</summary>
    public IReadOnlyList<PackageOwner> Owners => _workflow?.Model.Owners ?? Array.Empty<PackageOwner>();

    /// <summary>The outcome of the last action.</summary>
    public string? Message => _workflow?.Model.Message;

    /// <summary>Whether <see cref="Message"/> reports a refusal.</summary>
    public bool Failed => _workflow?.Model.Failed ?? false;

    /// <summary>Whether the signed-in user is an administrator.</summary>
    public bool IsAdministrator => _workflow?.Model.IsAdministrator ?? false;

    /// <summary>Whether the user manages the package only as an administrator (they are not an owner).</summary>
    public bool ActingAsAdministrator => _workflow?.Model.ActingAsAdministrator ?? false;

    /// <summary>Whether the user has a password to confirm with.</summary>
    public bool HasPassword => _workflow?.Model.HasPassword ?? true;

    /// <summary>For an account without a password: it signed in again with its provider a moment ago.</summary>
    public bool Reauthenticated => _workflow?.Model.Reauthenticated ?? false;

    /// <summary>Whether an external provider is available to sign in again with.</summary>
    public bool CanReauthenticate => Accounts.OpenIdConnect.IsConfigured;

    /// <summary>The address npm is configured with for this repository.</summary>
    public string RegistryUrl => Workflow.RegistryUrl;

    /// <summary>How long after publishing a version its owner may unpublish it with Deploy alone.</summary>
    public TimeSpan UnpublishWindow => Workflow.UnpublishWindow;

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

    /// <summary>Loads the package; 404 when it does not exist.</summary>
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
        => Map(await Workflow.GetAsync(cancellationToken));

    /// <summary>Adds an owner.</summary>
    public async Task<IActionResult> OnPostAddOwnerAsync(string ownerName, CancellationToken cancellationToken)
        => Map(await Workflow.AddOwnerAsync(ownerName, cancellationToken));

    /// <summary>Removes an owner.</summary>
    public async Task<IActionResult> OnPostRemoveOwnerAsync(string ownerName, CancellationToken cancellationToken)
        => Map(await Workflow.RemoveOwnerAsync(ownerName, cancellationToken));

    /// <summary>Points a dist-tag at a version.</summary>
    public async Task<IActionResult> OnPostSetTagAsync(string tag, string version, CancellationToken cancellationToken)
        => Map(await Workflow.SetTagAsync(tag, version, cancellationToken));

    /// <summary>Removes a dist-tag.</summary>
    public async Task<IActionResult> OnPostRemoveTagAsync(string tag, CancellationToken cancellationToken)
        => Map(await Workflow.RemoveTagAsync(tag, cancellationToken));

    /// <summary>Deprecates a version.</summary>
    public async Task<IActionResult> OnPostDeprecateAsync(string version, string? message, CancellationToken cancellationToken)
        => Map(await Workflow.DeprecateAsync(version, message, cancellationToken));

    /// <summary>Removes a version's deprecation.</summary>
    public async Task<IActionResult> OnPostUndeprecateAsync(string version, CancellationToken cancellationToken)
        => Map(await Workflow.UndeprecateAsync(version, cancellationToken));

    /// <summary>Unpublishes one version and deletes its tarball, after the password is confirmed.</summary>
    public async Task<IActionResult> OnPostUnpublishAsync(string version, string? password, CancellationToken cancellationToken)
        => Map(await Workflow.UnpublishAsync(version, password, cancellationToken));

    /// <summary>Unpublishes every version of the package, after the password is confirmed.</summary>
    public async Task<IActionResult> OnPostUnpublishAllAsync(string? password, CancellationToken cancellationToken)
        => Map(await Workflow.UnpublishAllAsync(password, cancellationToken));

    private IActionResult Map(IActionResult result) => PackageWorkflowBase.IsShow(result) ? Page() : result;
}
