using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.Pages.Account;
using BaGet.Identity.Workflows;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Identity.Pages.Packages;

/// <summary>
/// The page model of the validation status page when it was a Razor Page (<c>/packages/{id}/validation</c>): for each version of the
/// ID, its newest submission and the verdict and message of every step.
/// </summary>
/// <remarks>
/// Kept as a shim so that code compiled against it still loads and runs (no breaking change of the public API); there is no page
/// behind it any more: <c>ValidationStatusPageController</c> serves the screen, and both run <see cref="ValidationStatusWorkflow"/>.
/// </remarks>
[Obsolete("The validation status screen is served by ValidationStatusPageController; this page model has no page any more and will be removed in the next major version.")]
public class ValidationStatusModel : AccountPageModel
{
    /// <summary>One version's newest submission.</summary>
    public sealed record Submission(string Version, string? SubmissionId, string Status, IReadOnlyList<PackageValidation> Steps);

    private ValidationStatusWorkflow? _workflow;

    private ValidationStatusWorkflow Workflow => _workflow ??= new ValidationStatusWorkflow(HttpContext, null);

    /// <inheritdoc/>
    protected override bool RequiresSignIn => true;

    /// <summary>The package ID from the address.</summary>
    [BindProperty(SupportsGet = true)]
    public string? Id
    {
        get => _workflow?.Id;
        set => Workflow.Id = value;
    }

    /// <summary>The newest submission of each version, newest version first.</summary>
    public IReadOnlyList<Submission> Submissions
        => _workflow?.Model.Submissions.Select(s => new Submission(s.Version, s.SubmissionId, s.Status, s.Steps)).ToList()
           ?? (IReadOnlyList<Submission>)Array.Empty<Submission>();

    /// <summary>Shows the submissions of the ID, 403 without the permission, 404 when validation never saw it.</summary>
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var result = await Workflow.GetAsync(cancellationToken);

        return PackageWorkflowBase.IsShow(result) ? Page() : result;
    }
}
