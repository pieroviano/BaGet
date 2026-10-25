using System;
using System.Collections.Generic;
using BaGet.Core;

namespace BaGet.Web;

/// <summary>What the Maven front page (<c>/maven-artifacts</c>) shows: this page of search results and the client configuration.</summary>
public sealed class MavenArtifactsViewModel
{
    /// <summary>The search text.</summary>
    public string? Query { get; init; }

    /// <summary>The page, from 1.</summary>
    public int PageIndex { get; init; } = 1;

    /// <summary>This page of results.</summary>
    public MavenSearchPage Result { get; init; } = new(0, Array.Empty<MavenSearchHit>());

    /// <summary>The repository's policy, redeploy and snapshot retention.</summary>
    public MavenRepositorySettings Settings { get; init; } = new();

    /// <summary>The address Maven is configured with for this repository.</summary>
    public string RepositoryUrl { get; init; } = string.Empty;

    /// <summary>The kind of the repository: a remote one lists what it cached, a virtual one what its members hold.</summary>
    public RepositoryKind Kind { get; init; }

    /// <summary>The repository's name, which is the <c>id</c> in <c>settings.xml</c> and the POM.</summary>
    public string RepositoryName { get; init; } = string.Empty;

    /// <summary>Whether another page follows.</summary>
    public bool HasNext => PageIndex * MavenArtifactsWorkflow.ResultsPerPage < Result.Total;

    internal IReadOnlyList<InstallSnippet> ConfigurationTabs { get; init; } = Array.Empty<InstallSnippet>();
}

/// <summary>What the page of one Maven artifact (<c>/maven-artifacts/{group}/{artifact}/{version?}</c>) shows.</summary>
public sealed class MavenArtifactViewModel
{
    /// <summary>The group id from the address.</summary>
    public string? Group { get; init; }

    /// <summary>The artifact id from the address.</summary>
    public string? Artifact { get; init; }

    /// <summary>The version from the address; the latest when absent.</summary>
    public string? Version { get; init; }

    /// <summary>The HTTP status the page is answered with: 200, or 404 when the artifact or version does not exist (the page then explains).</summary>
    public int StatusCode { get; init; } = 200;

    /// <summary>The artifact, or null when there is none of those coordinates.</summary>
    public MavenArtifactInfo? Info { get; init; }

    /// <summary>The version the page shows, with its files and POM.</summary>
    public MavenVersionDetail? Selected { get; init; }

    /// <summary>The address Maven is configured with for this repository.</summary>
    public string RepositoryUrl { get; init; } = string.Empty;

    /// <summary>The repository's name (the <c>id</c> in <c>settings.xml</c>).</summary>
    public string RepositoryName { get; init; } = string.Empty;

    internal IReadOnlyList<InstallSnippet> InstallTabs { get; init; } = Array.Empty<InstallSnippet>();

    /// <summary>The owners, when accounts are on; empty otherwise.</summary>
    public IReadOnlyList<string> Owners { get; init; } = Array.Empty<string>();

    /// <summary>Whether the signed-in user can open the manage page (an owner or an administrator).</summary>
    public bool CanManage { get; init; }

    /// <summary>The address of a file of the selected version.</summary>
    public string FileUrl(string fileName)
        => RepositoryUrl + MavenNames.ClientPath(Info!.GroupId, Info.ArtifactId, Selected!.Summary.Version, fileName);
}

/// <summary>What the Maven upload page (<c>/maven-upload</c>) shows, before and after a deployment.</summary>
public sealed class MavenUploadViewModel
{
    /// <summary>Whether accounts are on (the uploader then signs in).</summary>
    public bool AccountsEnabled { get; init; }

    /// <summary>Whether the server is read-only.</summary>
    public bool IsReadOnlyMode { get; init; }

    /// <summary>Whether the visitor is signed in.</summary>
    public bool SignedIn { get; init; }

    /// <summary>Without accounts there is no user to own an upload, so the uploader proves itself with the API key.</summary>
    public bool NeedsApiKey => !AccountsEnabled;

    /// <summary>Accounts are off and no API key is set: nothing tells one visitor from another, so the page is closed.</summary>
    public bool NothingCanAuthenticate { get; init; }

    /// <summary>Whether to render the form: not read-only, someone could be authenticated, and (with accounts) signed in.</summary>
    public bool CanUpload => !IsReadOnlyMode && !NothingCanAuthenticate && (!AccountsEnabled || SignedIn);

    /// <summary>The address Maven is configured with for this repository.</summary>
    public string RepositoryUrl { get; init; } = string.Empty;

    /// <summary>The repository's name (the <c>id</c> in <c>settings.xml</c>).</summary>
    public string RepositoryName { get; init; } = string.Empty;

    /// <summary>The repository's policy, shown beside the form.</summary>
    public MavenRepositorySettings Settings { get; init; } = new();

    /// <summary>What happened, for the page to show.</summary>
    public string? Message { get; init; }

    /// <summary>Whether <see cref="Message"/> reports a failure.</summary>
    public bool Failed { get; init; }

    /// <summary>The artifact just deployed (<c>group:artifact</c>), so the page can link to it.</summary>
    public string? DeployedName { get; init; }

    /// <summary>The version just deployed.</summary>
    public string? DeployedVersion { get; init; }

    /// <summary>The coordinates just posted, so a refused form keeps them.</summary>
    public MavenDeployment? Posted { get; init; }

    /// <summary>The HTTP status the page is answered with (200, or the refusal's).</summary>
    public int StatusCode { get; init; } = 200;
}
