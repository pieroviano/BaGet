using System;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.Pages.Account;
using BaGet.Identity.Workflows;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Identity.Pages.Packages;

/// <summary>
/// The page model of the <em>Scan now</em> page when it was a Razor Page (<c>/scan-artifact</c>): checks one version of an artifact
/// against the vulnerability source again and shows what was found.
/// </summary>
/// <remarks>
/// Kept as a shim so that code compiled against it still loads and runs (no breaking change of the public API); there is no page
/// behind it any more: <c>ScanArtifactController</c> serves the screen, and both run <see cref="ScanWorkflow"/>.
/// </remarks>
[Obsolete("The scan screen is served by ScanArtifactController; this page model has no page any more and will be removed in the next major version.")]
public class ScanModel : AccountPageModel
{
    private ScanWorkflow? _workflow;

    private ScanWorkflow Workflow => _workflow ??= new ScanWorkflow(HttpContext, Url, null, null, null);

    /// <inheritdoc/>
    protected override bool RequiresSignIn => true;

    /// <summary>The artifact name (NuGet id, npm name, Maven <c>group:artifact</c>, Docker image).</summary>
    [BindProperty(SupportsGet = true)]
    public string? Name
    {
        get => _workflow?.Name;
        set => Workflow.Name = value;
    }

    /// <summary>The version (Docker: the manifest digest).</summary>
    [BindProperty(SupportsGet = true)]
    public string? Version
    {
        get => _workflow?.Version;
        set => Workflow.Version = value;
    }

    /// <summary>Where the link back goes: the package page the user came from (a local address only).</summary>
    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl
    {
        get => _workflow?.ReturnUrl;
        set => Workflow.ReturnUrl = value;
    }

    /// <summary>What happened, for the page to show.</summary>
    public string? Message => _workflow?.Model.Message;

    /// <summary>Whether <see cref="Message"/> reports a refusal.</summary>
    public bool Failed => _workflow?.Model.Failed ?? false;

    /// <summary>The result just stored, with its findings, or null.</summary>
    public ScanResult? Result => _workflow?.Model.Result;

    /// <summary>The scanner status of the repository, for the explanation when it cannot run.</summary>
    public ScanEngineStatus? Engine => _workflow?.Model.Engine;

    /// <summary>The address the link back leads to (the return address when it is local, else the home page).</summary>
    public string BackUrl => Workflow.BackUrl;

    /// <summary>Shows the form (and the stored result) of the version.</summary>
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
        => Map(await Workflow.GetAsync(cancellationToken));

    /// <summary>Scans the version now (or queues it, for an image) and shows the outcome.</summary>
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
        => Map(await Workflow.PostAsync(cancellationToken));

    private IActionResult Map(IActionResult result) => PackageWorkflowBase.IsShow(result) ? Page() : result;
}
