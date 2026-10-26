using System;
using System.Collections.Generic;
using BaGet.Core;
using BaGet.Identity.Pages.Admin;

namespace BaGet.Identity.ViewModels;

/// <summary>What the organizations screen (<c>/admin/organizations</c>) lists: every organization with its members and packages.</summary>
public sealed class AdminOrganizationsViewModel
{
    /// <summary>The organizations, by name.</summary>
    public IReadOnlyList<OrganizationSummary> Organizations { get; internal set; } = Array.Empty<OrganizationSummary>();
}

/// <summary>What the organization screen (<c>/admin/organizations/{name}</c>) shows: the organization, its members and the last action.</summary>
public sealed class OrganizationDetailViewModel : ConfirmationModel, IFlashModel
{
    /// <summary>The organization's name (route).</summary>
    public string? Name { get; set; }

    /// <summary>The organization.</summary>
    public Organization? Organization { get; internal set; }

    /// <summary>Its members, by name.</summary>
    public IReadOnlyList<OrganizationMemberRow> Members { get; internal set; } = Array.Empty<OrganizationMemberRow>();

    /// <summary>How many package IDs it owns, in every repository.</summary>
    public int OwnedPackageIds { get; internal set; }

    /// <inheritdoc />
    public string? Message { get; set; }

    /// <inheritdoc />
    public string? Error { get; set; }
}
