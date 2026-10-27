using System;
using System.Collections.Generic;
using BaGet.Core;

namespace BaGet.Identity.Application.Models;

/// <summary>One version's newest validation submission and the verdict of every step.</summary>
public sealed record ValidationSubmission(string Version, string? SubmissionId, string Status, IReadOnlyList<PackageValidation> Steps);

/// <summary>What the validation status screen (<c>/packages/{id}/validation</c>) shows; filled by the screen's workflow.</summary>
public sealed class ValidationStatusViewModel
{
    /// <summary>The package ID from the address.</summary>
    public string? Id { get; internal set; }

    /// <summary>The newest submission of each version, newest version first.</summary>
    public IReadOnlyList<ValidationSubmission> Submissions { get; internal set; } = Array.Empty<ValidationSubmission>();
}
