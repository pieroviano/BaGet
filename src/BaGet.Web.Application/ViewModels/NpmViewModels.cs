using System;
using System.Collections.Generic;
using BaGet.Core;
using BaGet.Web.Application;
using Microsoft.AspNetCore.Html;

namespace BaGet.Web;

/// <summary>What the npm front page (<c>/npm-packages</c>) shows: this page of search results and the registry address.</summary>
public sealed class NpmPackagesViewModel
{
    /// <summary>The search text.</summary>
    public string? Query { get; init; }

    /// <summary>The page, from 1.</summary>
    public int PageIndex { get; init; } = 1;

    /// <summary>This page of results.</summary>
    public NpmSearchPage Result { get; init; } = new(0, Array.Empty<NpmSearchHit>());

    /// <summary>The address npm is configured with for this repository.</summary>
    public string RegistryUrl { get; init; } = string.Empty;

    /// <summary>Whether another page follows.</summary>
    public bool HasNext => PageIndex * NpmBrowseUseCase.ResultsPerPage < Result.Total;
}

/// <summary>What the page of one npm package (<c>/npm-packages/{name}</c>) shows.</summary>
public sealed class NpmPackageViewModel
{
    /// <summary>The package name from the address.</summary>
    public string? Id { get; init; }

    /// <summary>The version asked for with <c>?version=</c>; the latest when absent or unknown.</summary>
    public string? RequestedVersion { get; init; }

    /// <summary>The HTTP status the page is answered with: 200, or 404 when the package does not exist (the page then explains).</summary>
    public int StatusCode { get; init; } = 200;

    /// <summary>The package, or null when there is none of that name.</summary>
    public NpmPackageInfo? Package { get; init; }

    /// <summary>The version the page shows.</summary>
    public NpmVersionInfo? Selected { get; init; }

    /// <summary>The selected version's readme as safe HTML, or null.</summary>
    public HtmlString? Readme { get; init; }

    /// <summary>The dist-tags that point at the selected version.</summary>
    public IReadOnlyList<string> SelectedTags { get; init; } = Array.Empty<string>();

    /// <summary>The address npm is configured with for this repository.</summary>
    public string RegistryUrl { get; init; } = string.Empty;

    /// <summary>Where the selected version's tarball is.</summary>
    public string? TarballUrl => Selected == null || Package == null
        ? null
        : RegistryUrl + Package.Name + "/-/" + NpmNames.TarballFileName(Package.Name, Selected.Version);

    public IReadOnlyList<InstallSnippet> InstallTabs { get; init; } = Array.Empty<InstallSnippet>();

    /// <summary>The owners, when accounts are on; empty otherwise.</summary>
    public IReadOnlyList<string> Owners { get; init; } = Array.Empty<string>();

    /// <summary>Whether the signed-in user can open the manage page (an owner or an administrator).</summary>
    public bool CanManage { get; init; }
}

/// <summary>What the npm upload page (<c>/npm-upload</c>) shows, before and after a publish.</summary>
public sealed class NpmUploadViewModel
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

    /// <summary>The address npm is configured with for this repository.</summary>
    public string RegistryUrl { get; init; } = string.Empty;

    public IReadOnlyList<string> PublishLines { get; init; } = Array.Empty<string>();

    /// <summary>What happened, for the page to show.</summary>
    public string? Message { get; init; }

    /// <summary>Whether <see cref="Message"/> reports a failure.</summary>
    public bool Failed { get; init; }

    /// <summary>The package just published, so the page can link to it.</summary>
    public string? PublishedName { get; init; }

    /// <summary>The HTTP status the page is answered with (200, or the refusal's).</summary>
    public int StatusCode { get; init; } = 200;
}
