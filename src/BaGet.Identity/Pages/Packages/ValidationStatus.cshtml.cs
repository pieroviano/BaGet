using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.Pages.Account;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BaGet.Identity.Pages.Packages;

/// <summary>
/// Where a pushed package stands in validation: for each version of the ID, its newest submission and the verdict and message of
/// every step. The page counterpart of <c>GET v3/package/{id}/{version}/validation</c>, gated the same way: a signed-in user
/// who may push or manage the ID (an administrator holds both). Nothing is written.
/// </summary>
public class ValidationStatusModel : AccountPageModel
{
    /// <summary>One version's newest submission.</summary>
    public sealed record Submission(string Version, string? SubmissionId, string Status, IReadOnlyList<PackageValidation> Steps);

    protected override bool RequiresSignIn => true;

    [BindProperty(SupportsGet = true)]
    public string? Id { get; set; }

    /// <summary>The newest submission of each version, newest version first.</summary>
    public IReadOnlyList<Submission> Submissions { get; private set; } = Array.Empty<Submission>();

    /// <summary>Shows the submissions of the ID, 403 without the permission, 404 when validation never saw it.</summary>
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var services = HttpContext.RequestServices;
        var principal = await services.GetRequiredService<ISessionPrincipalFactory>().CreateAsync(User);
        if (principal == null) return Challenge();

        var authorization = services.GetRequiredService<IPackageAuthorizationService>();
        if (!await authorization.AuthorizeAsync(principal, PackageAction.Push, Id, cancellationToken)
            && !await authorization.AuthorizeAsync(principal, PackageAction.Manage, Id, cancellationToken))
        {
            return StatusCode(403);
        }

        var context = services.GetRequiredService<IContext>();
        if (context is NullContext) return NotFound();

        var lowerId = (Id ?? string.Empty).ToLowerInvariant();
        #pragma warning disable CS8602, CS8604
        var steps = await context.PackageValidations
            .AsNoTracking()
            .Where(v => v.PackageId.ToLower() == lowerId)
            .OrderByDescending(v => v.Key)
            .ToListAsync(cancellationToken);
        #pragma warning restore CS8602, CS8604
        if (steps.Count == 0) return NotFound();

        // The newest submission of a version is the one that matters, as in the API.
        Submissions = steps
            .GroupBy(s => s.PackageVersion, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var latest = g.First().SubmissionId;
                var submission = g.Where(s => s.SubmissionId == latest).OrderBy(s => s.Key).ToList();
                var index = submission.FirstOrDefault(s => s.Type == PackageValidation.IndexType);

                return new Submission(
                    g.Key ?? string.Empty,
                    latest,
                    index?.State switch
                    {
                        PackageValidationState.Succeeded => "Available",
                        PackageValidationState.Failed => "Quarantined",
                        _ => "Validating",
                    },
                    submission);
            })
            .OrderByDescending(s => NuGet.Versioning.NuGetVersion.TryParse(s.Version, out var v) ? v : null)
            .ToList();

        return Page();
    }
}
