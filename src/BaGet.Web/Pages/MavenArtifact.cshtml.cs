using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BaGet.Web;

/// <summary>
/// One Maven artifact: its versions, and for the chosen version its files (with sizes and checksums), its POM and the
/// dependency to copy. Served at <c>/r/{repo}/maven-artifacts/{group}/{artifact}/{version?}</c>; <see cref="RepositoryPageFilter"/>
/// requires Read on the repository and on <c>group:artifact</c>.
/// </summary>
public class MavenArtifactModel : PageModel
{
    private readonly IMavenRepository _maven;
    private readonly IUrlGenerator _urls;
    private readonly IRepositoryContext _repository;

    public MavenArtifactModel(IMavenRepository maven, IUrlGenerator urls, IRepositoryContext repository)
    {
        _maven = maven ?? throw new ArgumentNullException(nameof(maven));
        _urls = urls ?? throw new ArgumentNullException(nameof(urls));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <summary>The group id from the address.</summary>
    [BindProperty(SupportsGet = true)]
    public string? Group { get; set; }

    /// <summary>The artifact id from the address.</summary>
    [BindProperty(SupportsGet = true)]
    public string? Artifact { get; set; }

    /// <summary>The version from the address; the latest when absent.</summary>
    [BindProperty(SupportsGet = true)]
    public string? Version { get; set; }

    /// <summary>The artifact, or null when there is none of those coordinates.</summary>
    public MavenArtifactInfo? Info { get; private set; }

    /// <summary>The version the page shows, with its files and POM.</summary>
    public MavenVersionDetail? Selected { get; private set; }

    /// <summary>The address Maven is configured with for this repository.</summary>
    public string RepositoryUrl => _urls.GetMavenRepositoryUrl();

    /// <summary>The repository's name (the <c>id</c> in <c>settings.xml</c>).</summary>
    public string RepositoryName => _repository.Current.Name;

    internal IReadOnlyList<InstallSnippet> InstallTabs { get; private set; } = Array.Empty<InstallSnippet>();

    /// <summary>The owners, when accounts are on; empty otherwise.</summary>
    public IReadOnlyList<string> Owners { get; private set; } = Array.Empty<string>();

    /// <summary>Whether the signed-in user can open the manage page (an owner or an administrator).</summary>
    public bool CanManage { get; private set; }

    /// <summary>The address of a file of the selected version.</summary>
    public string FileUrl(string fileName)
        => RepositoryUrl + MavenNames.ClientPath(Info!.GroupId, Info.ArtifactId, Selected!.Summary.Version, fileName);

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!MavenNames.IsValidGroupId(Group) || !MavenNames.IsValidArtifactId(Artifact)) return NotFound();
        if (Version != null && !MavenNames.IsValidVersion(Version)) return NotFound();

        Info = await _maven.GetArtifactAsync(Group!, Artifact!, cancellationToken);
        if (Info == null)
        {
            // The page explains what is missing, with the status a link checker expects.
            Response.StatusCode = 404;
            return Page();
        }

        var chosen = Version == null ? Info.Latest : Info.Versions.FirstOrDefault(v => v.Version == Version);
        if (chosen == null)
        {
            Response.StatusCode = 404;
            return Page();
        }

        Selected = await _maven.GetVersionAsync(Info.GroupId, Info.ArtifactId, chosen.Version, cancellationToken);
        if (Selected != null)
        {
            InstallTabs = new[]
            {
                new InstallSnippet { Name = "Maven", Lines = MavenSnippets.Dependency(Info.GroupId, Info.ArtifactId, chosen.Version) },
                new InstallSnippet { Name = "Gradle", Lines = new[] { MavenSnippets.GradleDependency(Info.GroupId, Info.ArtifactId, chosen.Version) } },
                new InstallSnippet
                {
                    Name = "Check",
                    Prefix = ">",
                    Lines = new[] { MavenSnippets.GetCommand(Info.GroupId, Info.ArtifactId, chosen.Version, RepositoryName, RepositoryUrl) },
                },
            };
        }

        // Owners and the manage page belong to a repository that holds artifacts of its own.
        var services = HttpContext.RequestServices;
        if (services.GetService<IOptions<AccountsOptions>>()?.Value.Enabled == true)
        {
            var ownership = services.GetService<PackageOwnershipService>();
            if (ownership != null)
            {
                Owners = (await ownership.GetOwnersAsync(Info.Name, cancellationToken)).Select(o => o.OwnerName ?? string.Empty).ToList();
                CanManage = await NpmManageAccess.CanOpenAsync(services, User, Info.Name, cancellationToken);
            }
        }

        return Page();
    }
}
