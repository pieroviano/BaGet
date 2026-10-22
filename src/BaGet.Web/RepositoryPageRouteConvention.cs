using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace BaGet.Web;

/// <summary>
/// Makes a Razor Page reachable at <c>/r/{repo}/…</c> as well as at its own route, so every page can show a
/// repository other than <c>default</c>. Each selector of the page (its template and aliases such as
/// <c>/packages</c>) gets a twin prefixed with <see cref="Routes.RepositoryPathPrefix"/>.
/// <para>
/// Link generation: the twin has a lower <c>Order</c> than the original. Without a <c>repo</c> value (explicit or
/// ambient from the current <c>/r/{repo}/</c> request) the twin cannot be filled in, so <c>Url.Page</c>,
/// <c>asp-page</c> and <c>RedirectToPage</c> produce the plain URL, exactly as before; with one they produce the
/// repository URL, so links stay inside the repository being browsed. Passing <c>repo = null</c> explicitly
/// leaves it (the header switcher's "default" entry). This is why the twin is a selector of the same page and not
/// a second <c>AddPageRoute</c> page route: that makes generation ambiguous (empty <c>href</c>s).
/// </para>
/// </summary>
public class RepositoryPageRouteConvention : IPageRouteModelConvention
{
    /// <summary>
    /// The pages that act on the request's repository (a page that does not is account, settings or
    /// administration of the whole server and keeps one route). Page names as MVC knows them.
    /// </summary>
    public static readonly IReadOnlyCollection<string> RepositoryPages = new[]
    {
        "/Index",
        "/Package",
        "/Statistics",
        "/ReportAbuse",
        "/Upload",
        "/Packages/Manage",
        "/Packages/ContactOwners",
        "/Packages/ValidationStatus",
        "/Profiles/Profile",
        "/Admin/Packages",
        "/Admin/UserPackages",
        "/NpmPackages",
        "/NpmPackage",
        "/NpmUpload",
        "/Packages/NpmManage",
        "/MavenArtifacts",
        "/MavenArtifact",
        "/MavenUpload",
        "/Packages/MavenManage",
        "/DockerImages",
        "/DockerImage",
        "/Packages/DockerManage",
    };

    /// <summary>
    /// The format whose repositories a repository page belongs to; a page not listed is NuGet's. The pages of a format
    /// answer 404 in a repository of another one (<see cref="RepositoryPageFilter"/>), and the NuGet landing page of an
    /// npm repository sends the visitor to the format's own.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, BaGet.Core.RepositoryFormat> PageFormats = new Dictionary<string, BaGet.Core.RepositoryFormat>(StringComparer.OrdinalIgnoreCase)
    {
        { "/NpmPackages", BaGet.Core.RepositoryFormat.Npm },
        { "/NpmPackage", BaGet.Core.RepositoryFormat.Npm },
        { "/NpmUpload", BaGet.Core.RepositoryFormat.Npm },
        { "/Packages/NpmManage", BaGet.Core.RepositoryFormat.Npm },
        { "/MavenArtifacts", BaGet.Core.RepositoryFormat.Maven },
        { "/MavenArtifact", BaGet.Core.RepositoryFormat.Maven },
        { "/MavenUpload", BaGet.Core.RepositoryFormat.Maven },
        { "/Packages/MavenManage", BaGet.Core.RepositoryFormat.Maven },
        { "/DockerImages", BaGet.Core.RepositoryFormat.Docker },
        { "/DockerImage", BaGet.Core.RepositoryFormat.Docker },
        { "/Packages/DockerManage", BaGet.Core.RepositoryFormat.Docker },
    };

    /// <summary>The format a repository page serves (NuGet unless <see cref="PageFormats"/> says otherwise).</summary>
    public static BaGet.Core.RepositoryFormat FormatOf(string viewEnginePath)
        => PageFormats.TryGetValue(viewEnginePath, out var format) ? format : BaGet.Core.RepositoryFormat.NuGet;

    /// <summary>Order of the repository twin of a selector; below the default 0 so it wins when it can be filled in.</summary>
    public const int TwinOrder = -1;

    private readonly ISet<string> _pages;

    public RepositoryPageRouteConvention() : this(RepositoryPages)
    {
    }

    public RepositoryPageRouteConvention(IEnumerable<string> pages)
    {
        _pages = new HashSet<string>(pages ?? throw new ArgumentNullException(nameof(pages)), StringComparer.OrdinalIgnoreCase);
    }

    public void Apply(PageRouteModel model)
    {
        if (!_pages.Contains(model.ViewEnginePath)) return;

        // Snapshot: the twins are added to the same list. Twins are never twinned again (idempotent).
        foreach (var selector in model.Selectors.ToList())
        {
            var template = selector.AttributeRouteModel?.Template ?? string.Empty;
            if (template.StartsWith(Routes.RepositoryPathPrefix, StringComparison.Ordinal)) continue;

            var twin = Routes.RepositoryPathPrefix + template.TrimStart('/');
            model.Selectors.Add(new SelectorModel
            {
                AttributeRouteModel = new AttributeRouteModel
                {
                    Template = twin.TrimEnd('/'),
                    Order = TwinOrder,
                    Name = selector.AttributeRouteModel?.Name == null ? null : Routes.RepositoryRouteName(selector.AttributeRouteModel.Name),
                    SuppressLinkGeneration = selector.AttributeRouteModel?.SuppressLinkGeneration ?? false,
                    SuppressPathMatching = selector.AttributeRouteModel?.SuppressPathMatching ?? false,
                },
            });
        }
    }
}
