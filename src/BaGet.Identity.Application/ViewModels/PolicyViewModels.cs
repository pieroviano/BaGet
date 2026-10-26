using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using BaGet.Core;
using BaGet.Identity.Pages.Admin;

namespace BaGet.Identity.ViewModels;

/// <summary>
/// Shared shape of the policy screens (retention, scanning, replication): the password confirmation partial's inputs and the
/// outcome of the last action, which <c>AdminControllerBase.Flash</c> carries across a redirect.
/// </summary>
public abstract class PolicyViewModel : ConfirmationModel, IFlashModel
{
    /// <inheritdoc />
    public string? Message { get; set; }

    /// <inheritdoc />
    public string? Error { get; set; }
}

/// <summary>What the retention policies screen (<c>/admin/retention</c>) shows: every policy and the form that creates one.</summary>
public sealed class RetentionViewModel : PolicyViewModel
{
    /// <summary>The new policy.</summary>
    public RetentionPolicyForm Form { get; set; } = new();

    /// <summary>Every policy.</summary>
    public IReadOnlyList<RetentionPolicyView> Policies { get; internal set; } = Array.Empty<RetentionPolicyView>();

    /// <summary>The repositories a policy can name.</summary>
    public IReadOnlyList<RepositoryInfo> Repositories { get; internal set; } = Array.Empty<RepositoryInfo>();

    /// <summary>The names of the projects a policy can name.</summary>
    public IReadOnlyList<string> ProjectNames { get; internal set; } = Array.Empty<string>();
}

/// <summary>What one retention policy's screen (<c>/admin/retention/{key}</c>) shows.</summary>
public sealed class RetentionPolicyViewModel : PolicyViewModel
{
    /// <summary>The policy's key (route).</summary>
    public int Key { get; set; }

    /// <summary>The rules, as edited.</summary>
    public RetentionPolicyForm Form { get; set; } = new();

    /// <summary>The policy with the names of what it applies to.</summary>
    public RetentionPolicyView? View { get; internal set; }

    /// <summary>The last run since the server started (a dry run's report included), or null.</summary>
    public RetentionJobStatus? Status { get; internal set; }

    /// <summary>Whether a run is in progress (the page refreshes itself).</summary>
    public bool Running => Status?.State == RetentionJobState.Running;

    /// <summary>The repositories a policy can name.</summary>
    public IReadOnlyList<RepositoryInfo> Repositories { get; internal set; } = Array.Empty<RepositoryInfo>();

    /// <summary>The names of the projects a policy can name.</summary>
    public IReadOnlyList<string> ProjectNames { get; internal set; } = Array.Empty<string>();
}

/// <summary>What the legal holds screen (<c>/admin/retention/holds</c>) shows.</summary>
public sealed class RetentionHoldsViewModel : PolicyViewModel
{
    /// <summary>Only the holds of this repository (query).</summary>
    public string? Repository { get; set; }

    /// <summary>The repository of the new hold.</summary>
    public string? HoldRepository { get; set; }

    /// <summary>The artifact name of the new hold.</summary>
    public string? HoldName { get; set; }

    /// <summary>The version of the new hold; empty for every version.</summary>
    public string? HoldVersion { get; set; }

    /// <summary>Why (a case number, a ticket).</summary>
    public string? Reason { get; set; }

    /// <summary>The holds.</summary>
    public IReadOnlyList<RetentionHoldView> Holds { get; internal set; } = Array.Empty<RetentionHoldView>();

    /// <summary>The repositories a hold can name.</summary>
    public IReadOnlyList<RepositoryInfo> Repositories { get; internal set; } = Array.Empty<RepositoryInfo>();
}

/// <summary>The scanning options the overview shows.</summary>
/// <param name="Enabled">Whether artifacts are scanned.</param>
/// <param name="ScanOnDeploy">Whether a stored version is queued for a scan at once.</param>
/// <param name="OsvUrl">The OSV API root.</param>
/// <param name="OsvOfflinePath">The offline copy of the OSV data, or null.</param>
/// <param name="RescanInterval">The age after which an artifact is scanned again (zero or negative: never).</param>
public sealed record ScanningSettingsView(bool Enabled, bool ScanOnDeploy, string OsvUrl, string? OsvOfflinePath, TimeSpan RescanInterval);

/// <summary>A screen as a client links to it: the page name (as <c>asp-page</c> writes it) and its route values; the view turns it into an address.</summary>
/// <param name="Screen">The legacy page name of the screen (<c>/Package</c>).</param>
/// <param name="RouteValues">Route values; a null value (<c>repo</c> of the default repository) is kept.</param>
public sealed record ScreenLink(string Screen, IReadOnlyDictionary<string, object?> RouteValues);

/// <summary>One repository of the scanning overview.</summary>
/// <param name="Repository">The repository.</param>
/// <param name="Summary">Its scan counts.</param>
/// <param name="Policy">The policy in force.</param>
public sealed record ScanningRepositoryRow(RepositoryInfo Repository, ScanSummary Summary, EffectiveScanPolicy Policy);

/// <summary>One project of the scanning overview.</summary>
/// <param name="Project">The project.</param>
/// <param name="HasPolicy">Whether it has a scanning policy of its own.</param>
public sealed record ScanningProjectRow(ProjectSummary Project, bool HasPolicy);

/// <summary>What the vulnerability scanning overview (<c>/admin/scanning</c>) shows.</summary>
public sealed class ScanningViewModel
{
    /// <summary>
    /// The scanning options in force, for the view. Not serialised: the options class holds the scanner's registry credential; the JSON carries
    /// <see cref="Settings"/>, the values the screen shows.
    /// </summary>
    [JsonIgnore]
    public ScanningOptions Options { get; internal set; } = new();

    /// <summary>The scanning options the screen shows (no credential).</summary>
    public ScanningSettingsView Settings { get; internal set; } = new(false, true, string.Empty, null, TimeSpan.Zero);

    /// <summary>Whether Trivy is configured (container images are scanned).</summary>
    public bool TrivyConfigured { get; internal set; }

    /// <summary>The repositories that can hold artifacts (virtual ones hold none).</summary>
    public IReadOnlyList<ScanningRepositoryRow> Repositories { get; internal set; } = Array.Empty<ScanningRepositoryRow>();

    /// <summary>The projects.</summary>
    public IReadOnlyList<ScanningProjectRow> Projects { get; internal set; } = Array.Empty<ScanningProjectRow>();
}

/// <summary>What the scanning dashboard of one repository (<c>/admin/scanning/{name}</c>) shows.</summary>
public sealed class ScanRepositoryViewModel : PolicyViewModel
{
    /// <summary>Versions listed per page.</summary>
    public const int PageSize = 50;

    /// <summary>The repository's name (route).</summary>
    public string? Name { get; set; }

    /// <summary>The lowest severity listed (0 low ... 3 critical).</summary>
    public int MinSeverity { get; set; }

    /// <summary>The page of the list, from 1.</summary>
    public int PageNumber { get; set; } = 1;

    /// <summary>The repository.</summary>
    public RepositoryInfo? Repository { get; internal set; }

    /// <summary>The counts.</summary>
    public ScanSummary? Summary { get; internal set; }

    /// <summary>The policy in force and where it comes from.</summary>
    public EffectiveScanPolicy Effective { get; internal set; } = EffectiveScanPolicy.None;

    /// <summary>The policy stored for this repository itself, or null.</summary>
    public ScanPolicy? Own { get; internal set; }

    /// <summary>The versions with findings on this page.</summary>
    public IReadOnlyList<ScanResult> Findings { get; internal set; } = Array.Empty<ScanResult>();

    /// <summary>Whether there is another page.</summary>
    public bool HasMore { get; internal set; }

    /// <summary>The last run of the repository scan, or null.</summary>
    public AdminJobStatus? Job { get; internal set; }

    /// <summary>The fields of the policy editor (for the view; the JSON carries <see cref="Own"/>).</summary>
    [JsonIgnore]
    public ScanPolicyFormView PolicyForm => new(ScanPolicyFields.From(Own), Confirmation("scan-policy-password"), "scan-policy");

    /// <summary>The package screen of each finding's version (by <see cref="ScanResult.Key"/>), when its format has one.</summary>
    public IReadOnlyDictionary<int, ScreenLink> PackageLinks { get; internal set; } = new Dictionary<int, ScreenLink>();

    /// <summary>Turns a <see cref="ScreenLink"/> into an address; set by the view controller (the use case knows no routes).</summary>
    [JsonIgnore]
    public Func<ScreenLink, string?>? LinkResolver { get; set; }

    /// <summary>The page of a version in its format (the repository's package page), for the findings list.</summary>
    public string? PackageUrl(ScanResult result)
        => PackageLinks.TryGetValue(result.Key, out var link) ? LinkResolver?.Invoke(link) : null;
}

/// <summary>What the scanning policy of a project (<c>/admin/scanning/projects/{name}</c>) shows.</summary>
public sealed class ScanProjectViewModel : PolicyViewModel
{
    /// <summary>The project's name (route).</summary>
    public string? Name { get; set; }

    /// <summary>The project.</summary>
    public ProjectDetails? Project { get; internal set; }

    /// <summary>The policy stored for the project, or null.</summary>
    public ScanPolicy? Own { get; internal set; }

    /// <summary>The fields of the policy editor (for the view; the JSON carries <see cref="Own"/>).</summary>
    [JsonIgnore]
    public ScanPolicyFormView PolicyForm => new(ScanPolicyFields.From(Own), Confirmation("scan-project-password"), "scan-project");
}

/// <summary>What the replications screen (<c>/admin/replication</c>) shows: every replication and the form that creates one.</summary>
public sealed class ReplicationViewModel : PolicyViewModel
{
    /// <summary>The new replication.</summary>
    public ReplicationForm Form { get; set; } = new();

    /// <summary>Every replication.</summary>
    public IReadOnlyList<ReplicationConfigView> Replications { get; internal set; } = Array.Empty<ReplicationConfigView>();

    /// <summary>The local repositories a replication can name.</summary>
    public IReadOnlyList<RepositoryInfo> Repositories { get; internal set; } = Array.Empty<RepositoryInfo>();
}

/// <summary>What one replication's screen (<c>/admin/replication/{key}</c>) shows.</summary>
public sealed class ReplicationConfigViewModel : PolicyViewModel
{
    /// <summary>The configuration's key (route).</summary>
    public int Key { get; set; }

    /// <summary>The settings, as edited.</summary>
    public ReplicationForm Form { get; set; } = new();

    /// <summary>The configuration with the names of what it uses.</summary>
    public ReplicationConfigView? View { get; internal set; }

    /// <summary>The log, newest first.</summary>
    public IReadOnlyList<ReplicationRun> Runs { get; internal set; } = Array.Empty<ReplicationRun>();

    /// <summary>What this process knows of the last job (a refusal that wrote no log entry shows here).</summary>
    public ReplicationJobStatus? Job { get; internal set; }

    /// <summary>The result of the last connection test on this request, or null.</summary>
    public ReplicationTestResult? Test { get; internal set; }

    /// <summary>How many items the local repository holds, or null when it could not be listed.</summary>
    public int? LocalItems { get; internal set; }

    /// <summary>Whether a run is going here (the page refreshes itself).</summary>
    public bool Running => Job?.Running == true;

    /// <summary>The local repositories a replication can name.</summary>
    public IReadOnlyList<RepositoryInfo> Repositories { get; internal set; } = Array.Empty<RepositoryInfo>();

    /// <summary>The newest run that ended without finishing everything, which <b>Resume</b> continues; null when there is none.</summary>
    public ReplicationRun? Resumable
    {
        get
        {
            var last = Runs.FirstOrDefault();

            return last != null && last.Status is ReplicationRunStatus.Failed or ReplicationRunStatus.Interrupted ? last : null;
        }
    }
}
