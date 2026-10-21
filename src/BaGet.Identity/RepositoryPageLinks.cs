using System;
using BaGet.Core;

namespace BaGet.Identity;

/// <summary>
/// App-relative (<c>~/</c>) addresses of a package's pages in a given repository. The one place that decides
/// where a package page of a repository lives, so that pages that list packages of several repositories (the
/// account blockers, the administrator lists) link to the right one and a change of page routing is a change here.
/// </summary>
/// <remarks>
/// The default repository's pages are at the root; any other repository's are under <c>r/{name}/</c>, like its
/// protocol routes. Pass the result through <c>Url.Content</c> to honour the path base.
/// </remarks>
public static class RepositoryPageLinks
{
    /// <summary>The package page of <paramref name="id"/>.</summary>
    /// <param name="repositoryName">The repository; null or the default's name for the root pages.</param>
    /// <param name="id">The package ID.</param>
    public static string Package(string? repositoryName, string? id)
        => Prefix(repositoryName) + "packages/" + Uri.EscapeDataString(id ?? string.Empty);

    /// <summary>The management page of <paramref name="id"/>.</summary>
    /// <param name="repositoryName">The repository; null or the default's name for the root pages.</param>
    /// <param name="id">The package ID.</param>
    public static string Manage(string? repositoryName, string? id)
        => Package(repositoryName, id) + "/manage";

    private static string Prefix(string? repositoryName)
        => string.IsNullOrEmpty(repositoryName) || repositoryName == Repository.DefaultName
            ? "~/"
            : "~/r/" + Uri.EscapeDataString(repositoryName) + "/";
}
