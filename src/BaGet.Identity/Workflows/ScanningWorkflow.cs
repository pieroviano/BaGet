using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.Mvc;
using BaGet.Identity.Pages.Admin;
using BaGet.Identity.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BaGet.Identity.Workflows;

/// <summary>
/// The vulnerability scanning screens: the overview of every repository and project, the dashboard of one repository (its findings,
/// its policy, <em>Scan now</em>) and the policy of a project. Saving a policy asks for the administrator's password; starting a
/// scan does not. Shared by <c>AdminScanningController</c> and the obsolete <c>ScanningModel</c>, <c>ScanRepositoryModel</c> and
/// <c>ScanProjectModel</c>.
/// </summary>
internal sealed class ScanningWorkflow : PolicyWorkflow
{
    public ScanningWorkflow(HttpContext httpContext, IUrlHelper url) : base(httpContext, url)
    {
    }

    /// <summary>The overview.</summary>
    public ScanningViewModel Overview { get; } = new();

    /// <summary>The dashboard of one repository; the caller sets <c>Name</c> (and the paging) first.</summary>
    public ScanRepositoryViewModel Repository { get; } = new();

    /// <summary>The policy of one project; the caller sets <c>Name</c> first.</summary>
    public ScanProjectViewModel Project { get; } = new();

    private IAccessAdministration AccessAdmin => Services.GetRequiredService<IAccessAdministration>();

    private IScanPolicyService Policies => Services.GetRequiredService<IScanPolicyService>();

    // ------------------------------------------------------------------ overview

    /// <summary>Shows the overview.</summary>
    public async Task GetOverviewAsync(CancellationToken cancellationToken)
    {
        var options = Services.GetRequiredService<IOptionsSnapshot<ScanningOptions>>().Value;
        Overview.Options = options;
        Overview.TrivyConfigured = Services.GetRequiredService<ITrivyRunner>().IsConfigured(options);

        var scopes = Services.GetRequiredService<IServiceScopeFactory>();
        var rows = new List<ScanningRepositoryRow>();
        foreach (var repository in (await Services.GetRequiredService<IRepositoryService>().ListAsync(cancellationToken)).Where(r => r.Kind != RepositoryKind.Virtual))
        {
            using var scope = scopes.CreateRepositoryScope(repository);
            var summary = await scope.ServiceProvider.GetRequiredService<IScanService>().SummarizeAsync(cancellationToken);
            rows.Add(new ScanningRepositoryRow(repository, summary, await Policies.GetEffectiveAsync(repository.Key, cancellationToken)));
        }

        Overview.Repositories = rows;

        var projects = new List<ScanningProjectRow>();
        foreach (var project in await AccessAdmin.ListProjectsAsync(cancellationToken))
        {
            projects.Add(new ScanningProjectRow(project, await Policies.GetAsync(ScanPolicyScope.Project, project.Key, cancellationToken) != null));
        }

        Overview.Projects = projects;
    }

    // ------------------------------------------------------------------ one repository

    /// <summary>Shows the dashboard.</summary>
    public async Task<IActionResult?> GetRepositoryAsync(CancellationToken cancellationToken)
    {
        if (!await LoadRepositoryAsync(cancellationToken)) return NotFound();

        return await ShowAsync(Repository);
    }

    /// <summary>Saves the repository policy; empty values remove it.</summary>
    public async Task<IActionResult> SaveRepositoryPolicyAsync(ScanPolicyFields fields, string? password, CancellationToken cancellationToken)
    {
        var repository = await FindRepositoryAsync(cancellationToken);
        if (repository == null) return NotFound();

        return await ActAsync(Repository, password, confirm: true, async () =>
        {
            var values = fields.ToValues();
            await Policies.SaveAsync(ScanPolicyScope.Repository, repository.Key, values, Actor, cancellationToken);

            return values.IsEmpty ? $"The scanning policy of {repository.Name} was removed." : $"The scanning policy of {repository.Name} was saved.";
        }, BackToRepository);
    }

    /// <summary>Starts a scan of every artifact of the repository in the background.</summary>
    public async Task<IActionResult> ScanAsync(CancellationToken cancellationToken)
    {
        var repository = await FindRepositoryAsync(cancellationToken);
        if (repository == null) return NotFound();

        var jobs = Services.GetRequiredService<AdminJobService>();
        var operation = jobs.Operations.FirstOrDefault(o => o.Name == "vulnerability-scan");
        if (operation == null || !operation.IsAvailable(Services) || !operation.AppliesTo(repository))
        {
            Repository.Error = "Scanning is not available: it is turned off (Scanning:Enabled) or the repository cannot be scanned.";

            return BackToRepository();
        }

        if (!jobs.TryStart(operation.Name, repository, Actor, out var refusal)) Repository.Error = refusal;
        else Repository.Message = $"A scan of {repository.Name} started. Its status is shown below and on the Operations page.";

        return BackToRepository();
    }

    private IActionResult BackToRepository() => RedirectToScreen("/Admin/ScanRepository", new { name = Repository.Name });

    private async Task<RepositoryInfo?> FindRepositoryAsync(CancellationToken cancellationToken)
    {
        var repository = await Services.GetRequiredService<IRepositoryService>().FindAsync(Repository.Name, cancellationToken);

        return repository == null || repository.Kind == RepositoryKind.Virtual ? null : repository;
    }

    private async Task<bool> LoadRepositoryAsync(CancellationToken cancellationToken)
    {
        var model = Repository;
        model.PackageUrls = PackageUrl;
        model.Repository = await FindRepositoryAsync(cancellationToken);
        if (model.Repository == null) return false;

        model.Own = await Policies.GetAsync(ScanPolicyScope.Repository, model.Repository.Key, cancellationToken);
        model.Effective = await Policies.GetEffectiveAsync(model.Repository.Key, cancellationToken);
        model.Job = Services.GetRequiredService<AdminJobService>().Get("vulnerability-scan", model.Repository.Name);

        using var scope = Services.GetRequiredService<IServiceScopeFactory>().CreateRepositoryScope(model.Repository);
        var scans = scope.ServiceProvider.GetRequiredService<IScanService>();
        model.Summary = await scans.SummarizeAsync(cancellationToken);
        var page = Math.Max(1, model.PageNumber);
        var size = ScanRepositoryViewModel.PageSize;
        var results = await scans.ListAsync(Math.Clamp(model.MinSeverity, 0, PackageVulnerabilityRules.MaxSeverity), (page - 1) * size, size + 1, cancellationToken);
        model.HasMore = results.Count > size;
        model.Findings = results.Take(size).ToList();

        return true;
    }

    private string? PackageUrl(ScanResult result)
    {
        var info = Repository.Repository;
        var repo = info == null || info.IsDefault ? null : info.Name;
        var name = result.Name ?? string.Empty;
        var version = result.Version ?? string.Empty;

        switch (info?.Format)
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

    // ------------------------------------------------------------------ one project

    /// <summary>Shows the policy of the project.</summary>
    public async Task<IActionResult?> GetProjectAsync(CancellationToken cancellationToken)
    {
        Project.Project = await AccessAdmin.GetProjectAsync(Project.Name ?? string.Empty, cancellationToken);
        if (Project.Project == null) return NotFound();

        Project.Own = await Policies.GetAsync(ScanPolicyScope.Project, Project.Project.Project.Key, cancellationToken);

        return await ShowAsync(Project);
    }

    /// <summary>Saves the project policy; empty values remove it.</summary>
    public async Task<IActionResult> SaveProjectPolicyAsync(ScanPolicyFields fields, string? password, CancellationToken cancellationToken)
    {
        var project = await AccessAdmin.GetProjectAsync(Project.Name ?? string.Empty, cancellationToken);
        if (project == null) return NotFound();

        return await ActAsync(Project, password, confirm: true, async () =>
        {
            var values = fields.ToValues();
            await Policies.SaveAsync(ScanPolicyScope.Project, project.Project.Key, values, Actor, cancellationToken);

            return values.IsEmpty ? $"The scanning policy of {project.Project.Name} was removed." : $"The scanning policy of {project.Project.Name} was saved.";
        }, () => RedirectToScreen("/Admin/ScanProject", new { name = Project.Name }));
    }
}
