using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace BaGet.Identity.Pages.Admin;

/// <summary>
/// The vulnerability dashboard of one repository: how many artifacts are scanned, clean, unscanned or have findings by severity,
/// the versions with findings (worst first), the policy that applies and its editor, and <em>Scan now</em> for the whole
/// repository. The policy asks the administrator's password again (it decides who may download and deploy what); starting a
/// scan does not (it deletes nothing) and runs as the <c>vulnerability-scan</c> operation, so its status is the one the Operations
/// page shows. Administrators only; a virtual repository has no artifacts of its own and answers 404.
/// </summary>
public class ScanRepositoryModel : AccessPageModel
{
    /// <summary>Versions listed per page.</summary>
    public const int PageSize = 50;

    /// <summary>The repository's name (route).</summary>
    [BindProperty(SupportsGet = true)]
    public string? Name { get; set; }

    /// <summary>The lowest severity listed (0 low … 3 critical).</summary>
    [BindProperty(Name = "min", SupportsGet = true)]
    public int MinSeverity { get; set; }

    /// <summary>The page of the list, from 1.</summary>
    [BindProperty(Name = "p", SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    /// <summary>The repository.</summary>
    public RepositoryInfo? Repository { get; private set; }

    /// <summary>The counts.</summary>
    public ScanSummary? Summary { get; private set; }

    /// <summary>The policy in force and where it comes from.</summary>
    public EffectiveScanPolicy Effective { get; private set; } = EffectiveScanPolicy.None;

    /// <summary>The policy stored for this repository itself, or null.</summary>
    public ScanPolicy? Own { get; private set; }

    /// <summary>The versions with findings on this page.</summary>
    public IReadOnlyList<ScanResult> Findings { get; private set; } = Array.Empty<ScanResult>();

    /// <summary>Whether there is another page.</summary>
    public bool HasMore { get; private set; }

    /// <summary>The last run of the repository scan, or null.</summary>
    public AdminJobStatus? Job { get; private set; }

    /// <summary>The fields of the policy editor.</summary>
    public ScanPolicyFormView PolicyForm => new(ScanPolicyFields.From(Own), Confirmation("scan-policy-password"), "scan-policy");

    /// <summary>The page of a version in its format (the repository's package page), for the findings list.</summary>
    public string? PackageUrl(ScanResult result)
    {
        var repo = Repository == null || Repository.IsDefault ? null : Repository.Name;
        var name = result.Name ?? string.Empty;
        var version = result.Version ?? string.Empty;

        switch (Repository?.Format)
        {
            case RepositoryFormat.NuGet:
                return Url.Page("/Package", new { id = name, version, repo });
            case RepositoryFormat.Npm:
                return Url.Page("/NpmPackage", new { id = name, version, repo });
            case RepositoryFormat.Maven:
                return MavenNames.TryParseArtifactName(name, out var group, out var artifact)
                    ? Url.Page("/MavenArtifact", new { group, artifact, version, repo })
                    : null;
            case RepositoryFormat.Docker:
                return Url.Page("/DockerImage", new { image = name, digest = version, repo });
            default:
                return null;
        }
    }

    /// <summary>Shows the dashboard.</summary>
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!await LoadAsync(cancellationToken)) return NotFound();

        return Page();
    }

    /// <summary>Saves the repository's policy; empty values remove it.</summary>
    public async Task<IActionResult> OnPostPolicyAsync(string? warn, string? blockDownload, string? blockDeploy, bool blockUnscanned, string? password, CancellationToken cancellationToken)
    {
        var repository = await FindAsync(cancellationToken);
        if (repository == null) return NotFound();

        return await ActAsync(password, confirm: true, async () =>
        {
            var values = new ScanPolicyFields { Warn = warn, BlockDownload = blockDownload, BlockDeploy = blockDeploy, BlockUnscanned = blockUnscanned }.ToValues();
            await HttpContext.RequestServices.GetRequiredService<IScanPolicyService>()
                .SaveAsync(ScanPolicyScope.Repository, repository.Key, values, Actor, cancellationToken);

            return values.IsEmpty ? $"The scanning policy of {repository.Name} was removed." : $"The scanning policy of {repository.Name} was saved.";
        }, Back);
    }

    /// <summary>Starts a scan of every artifact of the repository in the background.</summary>
    public async Task<IActionResult> OnPostScanAsync(CancellationToken cancellationToken)
    {
        var repository = await FindAsync(cancellationToken);
        if (repository == null) return NotFound();

        var jobs = HttpContext.RequestServices.GetRequiredService<AdminJobService>();
        var operation = jobs.Operations.FirstOrDefault(o => o.Name == "vulnerability-scan");
        if (operation == null || !operation.IsAvailable(HttpContext.RequestServices) || !operation.AppliesTo(repository))
        {
            Error = "Scanning is not available: it is turned off (Scanning:Enabled) or the repository cannot be scanned.";

            return Back();
        }

        if (!jobs.TryStart(operation.Name, repository, Actor, out var refusal)) Error = refusal;
        else Message = $"A scan of {repository.Name} started. Its status is shown below and on the Operations page.";

        return Back();
    }

    private IActionResult Back() => RedirectToPage("/Admin/ScanRepository", new { name = Name });

    private async Task<RepositoryInfo?> FindAsync(CancellationToken cancellationToken)
    {
        var repository = await HttpContext.RequestServices.GetRequiredService<IRepositoryService>().FindAsync(Name, cancellationToken);

        return repository == null || repository.Kind == RepositoryKind.Virtual ? null : repository;
    }

    private async Task<bool> LoadAsync(CancellationToken cancellationToken)
    {
        Repository = await FindAsync(cancellationToken);
        if (Repository == null) return false;

        var services = HttpContext.RequestServices;
        Own = await services.GetRequiredService<IScanPolicyService>().GetAsync(ScanPolicyScope.Repository, Repository.Key, cancellationToken);
        Effective = await services.GetRequiredService<IScanPolicyService>().GetEffectiveAsync(Repository.Key, cancellationToken);
        Job = services.GetRequiredService<AdminJobService>().Get("vulnerability-scan", Repository.Name);

        using var scope = services.GetRequiredService<IServiceScopeFactory>().CreateRepositoryScope(Repository);
        var scans = scope.ServiceProvider.GetRequiredService<IScanService>();
        Summary = await scans.SummarizeAsync(cancellationToken);
        var page = Math.Max(1, PageNumber);
        var results = await scans.ListAsync(Math.Clamp(MinSeverity, 0, PackageVulnerabilityRules.MaxSeverity), (page - 1) * PageSize, PageSize + 1, cancellationToken);
        HasMore = results.Count > PageSize;
        Findings = results.Take(PageSize).ToList();

        return true;
    }
}
