namespace BaGet.Identity.Application.Models;

// The bodies of the administration screens' writes. A page posts them as form fields (the view controllers bind the same names and build these),
// the JSON API takes them as the request body; the use cases never see how they arrived.

/// <summary>A write that only asks the administrator's password again (approve an account, delete an organization).</summary>
/// <param name="Password">The administrator's password.</param>
public sealed record PasswordForm(string? Password);

/// <summary>Deleting an account.</summary>
/// <param name="SoleOwned">"keep" (default) or "delete": what to do with the package IDs only this user owns.</param>
/// <param name="Password">The administrator's password.</param>
public sealed record UserDeletionForm(string? SoleOwned, string? Password);

/// <summary>Revoking one of an account's API keys.</summary>
/// <param name="Key">The key's number (<see cref="UserKeyRow.Key"/>).</param>
/// <param name="Password">The administrator's password.</param>
public sealed record UserKeyForm(int Key, string? Password);

/// <summary>Adding an account to a group or removing it.</summary>
/// <param name="Group">The group's name.</param>
/// <param name="Password">The administrator's password.</param>
public sealed record UserGroupForm(string? Group, string? Password);

/// <summary>Renaming an organization.</summary>
/// <param name="NewName">The new name.</param>
/// <param name="Password">The administrator's password.</param>
public sealed record OrganizationRenameForm(string? NewName, string? Password);

/// <summary>Adding a member to an organization, changing their administrator flag or removing them.</summary>
/// <param name="UserName">The member's user name.</param>
/// <param name="IsAdmin">Whether the member administers the organization (ignored when removing).</param>
/// <param name="Password">The administrator's password.</param>
public sealed record OrganizationMemberForm(string? UserName, bool IsAdmin, string? Password);

/// <summary>Setting or removing the lowest level logged for one source.</summary>
/// <param name="Source">The source (a namespace prefix).</param>
/// <param name="Level">The level (ignored when removing).</param>
/// <param name="Password">The administrator's password.</param>
public sealed record LogLevelForm(string? Source, string? Level, string? Password);

/// <summary>Starting a maintenance operation.</summary>
/// <param name="Operation">The operation's name (<c>catalog-rebuild</c>).</param>
/// <param name="Repository">The repository it runs on.</param>
/// <param name="Password">The administrator's password (asked for operations that delete data).</param>
public sealed record OperationForm(string? Operation, string? Repository, string? Password);

/// <summary>A decision on a stuck submission of the validation queue.</summary>
/// <param name="SubmissionId">The submission.</param>
/// <param name="Password">The administrator's password (asked to release or reject).</param>
public sealed record SubmissionForm(string? SubmissionId, string? Password);

/// <summary>A decision on an abuse report.</summary>
/// <param name="Key">The report's number.</param>
/// <param name="All">Whether the list to come back to shows the resolved reports too.</param>
public sealed record AbuseForm(int Key, bool All);
