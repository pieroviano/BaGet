using System;
using System.Collections.Generic;
using BaGet.Core;

namespace BaGet.Identity.ViewModels;

/// <summary>What the user detail screen (<c>/admin/users/{name}</c>) shows: the account, its keys, organizations and access.</summary>
public sealed class UserDetailViewModel : ConfirmationModel, IFlashModel
{
    private IReadOnlyDictionary<int, IReadOnlyList<string>> _keyRepositories = new Dictionary<int, IReadOnlyList<string>>();

    /// <summary>A package ID to evaluate the effective permissions for; empty means each repository as a whole.</summary>
    public string? Package { get; set; }

    /// <summary>The user name (route).</summary>
    public string? Name { get; set; }

    /// <summary>Whether the configured database has an access model (else the access sections are hidden).</summary>
    public bool ModelAvailable { get; internal set; }

    /// <summary>The names of the groups the account belongs to (the implicit built-ins excluded).</summary>
    public IReadOnlyList<string> Groups { get; internal set; } = Array.Empty<string>();

    /// <summary>The roles the account holds in projects, directly or through a group.</summary>
    public IReadOnlyList<UserProjectRole> ProjectRoles { get; internal set; } = Array.Empty<UserProjectRole>();

    /// <summary>What the account may do in each NuGet repository, and why.</summary>
    public IReadOnlyList<RepositoryAccessRow> Access { get; internal set; } = Array.Empty<RepositoryAccessRow>();

    /// <summary>The account.</summary>
    public BaGetUser? Account { get; internal set; }

    /// <summary>The account's API keys; the hash is never shown.</summary>
    public IReadOnlyList<ApiKey> Keys { get; internal set; } = Array.Empty<ApiKey>();

    /// <summary>The organizations the account belongs to.</summary>
    public IReadOnlyList<Organization> Organizations { get; internal set; } = Array.Empty<Organization>();

    /// <summary>How many package IDs the account owns, in every repository.</summary>
    public int OwnedPackageIds { get; internal set; }

    /// <inheritdoc />
    public string? Message { get; set; }

    /// <inheritdoc />
    public string? Error { get; set; }

    internal void SetKeyRepositories(IReadOnlyDictionary<int, IReadOnlyList<string>> keyRepositories)
        => _keyRepositories = keyRepositories;

    /// <summary>Where the key may be used: the repositories it is limited to, or "all repositories".</summary>
    public string DescribeRepositories(ApiKey key)
        => ApiKeyService.DescribeRepositories(_keyRepositories.GetValueOrDefault(key.Key));
}
