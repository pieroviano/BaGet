using System;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.Pages.Account;
using BaGet.Identity.Workflows;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace BaGet.Identity.Pages.Packages;

/// <summary>
/// The page model of the Docker image management page when it was a Razor Page (<c>/docker-manage/{**image}</c>): delete a tag, a
/// manifest, the unused layers or the whole image, as the signed-in user.
/// </summary>
/// <remarks>
/// Kept as a shim so that code compiled against it still loads and runs (no breaking change of the public API); there is no page
/// behind it any more: <c>DockerManageController</c> serves the screen, and both run <see cref="DockerManageWorkflow"/>.
/// </remarks>
[Obsolete("The Docker image management screen is served by DockerManageController; this page model has no page any more and will be removed in the next major version.")]
public class DockerManageModel : AccountPageModel
{
    private DockerManageWorkflow? _workflow;

    private DockerManageWorkflow Workflow => _workflow ??= new DockerManageWorkflow(HttpContext, null);

    /// <inheritdoc/>
    protected override bool RequiresSignIn => true;

    /// <summary>The image name from the address (it may contain slashes).</summary>
    [BindProperty(SupportsGet = true)]
    public string? Image
    {
        get => _workflow?.Image;
        set => Workflow.Image = value;
    }

    /// <summary>The image, or null before it is loaded (or after it was deleted).</summary>
    public OciImageInfo? Info => _workflow?.Model.Info;

    /// <summary>The outcome of the last action.</summary>
    public string? Message => _workflow?.Model.Message;

    /// <summary>Whether <see cref="Message"/> reports a refusal.</summary>
    public bool Failed => _workflow?.Model.Failed ?? false;

    /// <summary>Whether the user has a password to confirm with.</summary>
    public bool HasPassword => _workflow?.Model.HasPassword ?? true;

    /// <summary>For an account without a password: it signed in again with its provider a moment ago.</summary>
    public bool Reauthenticated => _workflow?.Model.Reauthenticated ?? false;

    /// <summary>Whether an external provider is available to sign in again with.</summary>
    public bool CanReauthenticate => Accounts.OpenIdConnect.IsConfigured;

    /// <inheritdoc/>
    public override async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        OnPageHandlerExecuting(context);
        if (context.Result != null) return;

        var refusal = await Workflow.EnterAsync();
        if (refusal != null)
        {
            context.Result = Map(refusal);
            return;
        }

        await next();
    }

    /// <summary>Loads the image; 404 when it does not exist.</summary>
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
        => Map(await Workflow.GetAsync(cancellationToken));

    /// <summary>Deletes a tag (the manifest it points at stays), after the password is confirmed.</summary>
    public async Task<IActionResult> OnPostDeleteTagAsync(string? tag, string? password, CancellationToken cancellationToken)
        => Map(await Workflow.DeleteTagAsync(tag, password, cancellationToken));

    /// <summary>Deletes a manifest with the tags that point at it, after the password is confirmed.</summary>
    public async Task<IActionResult> OnPostDeleteManifestAsync(string? digest, string? password, CancellationToken cancellationToken)
        => Map(await Workflow.DeleteManifestAsync(digest, password, cancellationToken));

    /// <summary>Removes the layers no manifest uses (older than the grace period), after the password is confirmed.</summary>
    public async Task<IActionResult> OnPostPruneAsync(string? password, CancellationToken cancellationToken)
        => Map(await Workflow.PruneAsync(password, cancellationToken));

    /// <summary>Deletes the image with every manifest, tag and layer only it holds, after the password is confirmed.</summary>
    public async Task<IActionResult> OnPostDeleteImageAsync(string? password, CancellationToken cancellationToken)
        => Map(await Workflow.DeleteImageAsync(password, cancellationToken));

    private IActionResult Map(IActionResult result) => PackageWorkflowBase.IsShow(result) ? Page() : result;
}
