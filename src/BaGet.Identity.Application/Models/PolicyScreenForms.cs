namespace BaGet.Identity.Application.Models;

// The bodies of the retention, scanning and replication screens' writes. A page posts them as form fields (the view controllers bind the same
// names and build these), the JSON API takes them as the request body; the use cases never see how they arrived.

/// <summary>Creating or saving a retention policy, or any write of it that asks the password.</summary>
/// <param name="Form">The rules (<c>Form.*</c> on the page).</param>
/// <param name="Password">The administrator's password.</param>
public sealed record RetentionPolicyBody(RetentionPolicyForm? Form, string? Password);

/// <summary>Placing a legal hold.</summary>
/// <param name="HoldRepository">The repository of the hold.</param>
/// <param name="HoldName">The artifact name.</param>
/// <param name="HoldVersion">The version; empty for every version.</param>
/// <param name="Reason">Why (a case number, a ticket).</param>
public sealed record RetentionHoldForm(string? HoldRepository, string? HoldName, string? HoldVersion, string? Reason);

/// <summary>Lifting a legal hold.</summary>
/// <param name="Key">The hold's number.</param>
/// <param name="Password">The administrator's password.</param>
public sealed record RetentionHoldRemoval(int Key, string? Password);

/// <summary>Saving the scanning policy of a repository or project (empty values remove it).</summary>
/// <param name="Warn">Warn from this severity (0 to 3, or empty).</param>
/// <param name="BlockDownload">Refuse downloads from this severity.</param>
/// <param name="BlockDeploy">Refuse deploys from this severity.</param>
/// <param name="BlockUnscanned">Refuse downloads of versions that were not scanned.</param>
/// <param name="Password">The administrator's password.</param>
public sealed record ScanPolicyBody(string? Warn, string? BlockDownload, string? BlockDeploy, bool BlockUnscanned, string? Password)
{
    /// <summary>The fields the policy editor edits.</summary>
    public ScanPolicyFields ToFields() => new() { Warn = Warn, BlockDownload = BlockDownload, BlockDeploy = BlockDeploy, BlockUnscanned = BlockUnscanned };
}

/// <summary>Creating, saving or testing a replication, or any write of it that asks the password.</summary>
/// <param name="Form">The settings (<c>Form.*</c> on the page). The peer's API key is write only: no answer carries it.</param>
/// <param name="Password">The administrator's password.</param>
public sealed record ReplicationBody(ReplicationForm? Form, string? Password);
