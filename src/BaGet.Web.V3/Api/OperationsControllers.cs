using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BaGet.Web.V3.Api;

/// <summary>A maintenance operation and its last runs.</summary>
public sealed record OperationDto(string Name, string Title, string Description, bool RequiresConfirmation, IReadOnlyList<string> Repositories, IReadOnlyList<AdminJobStatus> Runs);

/// <summary>
/// The Operations page's operations (catalog rebuild, downloads import, Docker garbage collection, vulnerability scan…): list them
/// with their last runs, start one on a repository, read a run's status. An operation that deletes data
/// (<see cref="IAdminOperation.RequiresConfirmation"/>) asks for the owner's password as the page does. Status is kept in memory per node.
/// </summary>
[Route(ApiCatalog.RootTemplate + "/operations")]
public class OperationsController : AdminApiController
{
    /// <summary>Lists the available operations.</summary>
    [HttpGet]
    [ApiOperation("List the operations this server offers, the repositories each applies to and their last runs", "/Admin/Operations", Response = typeof(OperationDto))]
    public async Task<IActionResult> List()
    {
        var jobs = Service<AdminJobService>();
        var repositories = await Service<IRepositoryService>().ListAsync(Cancellation);
        var result = new List<OperationDto>();
        foreach (var operation in jobs.Operations.Where(o => o.IsAvailable(HttpContext.RequestServices)))
        {
            var applies = repositories.Where(operation.AppliesTo).ToList();
            result.Add(new OperationDto(
                operation.Name,
                operation.Title,
                operation.Description,
                operation.RequiresConfirmation,
                applies.Select(r => r.Name).ToList(),
                applies.Select(r => jobs.Get(operation.Name, r.Name)).Where(s => s != null).Select(s => s!).ToList()));
        }

        return Respond(result);
    }

    /// <summary>The last run of an operation on a repository.</summary>
    [HttpGet("{operation}/repositories/{repository}")]
    [ApiOperation("Show the last run of an operation on a repository", "/Admin/Operations", Response = typeof(AdminJobStatus))]
    public IActionResult Status(string operation, string repository)
        => Service<AdminJobService>().Get(operation, repository) is { } status
            ? Respond(status)
            : ApiProblems.NotFound($"run of '{operation}' on '{repository}' since the server started");

    /// <summary>Starts an operation.</summary>
    [HttpPost("{operation}/repositories/{repository}")]
    [ApiOperation("Start an operation on a repository (one that deletes data asks for the password)", "/Admin/Operations", Confirm = true, Response = typeof(AdminJobStatus), Status = 202)]
    public async Task<IActionResult> Start(string operation, string repository)
    {
        var jobs = Service<AdminJobService>();
        var info = await Service<IRepositoryService>().FindAsync(repository, Cancellation);
        var definition = jobs.Operations.FirstOrDefault(o => o.Name == operation);
        if (info == null || definition == null || !definition.IsAvailable(HttpContext.RequestServices) || !definition.AppliesTo(info))
        {
            return ApiProblems.NotFound($"operation '{operation}' available for repository '{repository}'");
        }

        ThrowIfClosed();
        if (definition.RequiresConfirmation && await RequireConfirmationAsync() is { } refusal) return refusal;

        if (!jobs.TryStart(definition.Name, info, Actor, out var problem)) return ApiProblems.Conflict(problem ?? "The operation was not started.");

        return Respond(jobs.Get(definition.Name, info.Name), 202, $"{ApiCatalog.Root}/operations/{definition.Name}/repositories/{Uri.EscapeDataString(info.Name)}");
    }
}

/// <summary>A retention policy, as the API takes it. The cache limit is in bytes.</summary>
public sealed class RetentionPolicyRequest
{
    /// <summary>The policy's name.</summary>
    public string? Name { get; set; }

    /// <summary><c>repository</c> (the default) or <c>project</c>.</summary>
    public string? ScopeKind { get; set; }

    /// <summary>The repository or project it applies to.</summary>
    public string? ScopeName { get; set; }

    /// <summary>A disabled policy only reports (dry runs).</summary>
    public bool Enabled { get; set; }

    /// <summary>Package/image name globs; empty = all.</summary>
    public string? NamePatterns { get; set; }

    /// <summary>Keep the newest N versions.</summary>
    public int? KeepLast { get; set; }

    /// <summary>Delete versions older than N days.</summary>
    public int? OlderThanDays { get; set; }

    /// <summary>Delete versions nobody downloaded for N days.</summary>
    public int? UnusedDays { get; set; }

    /// <summary>Touch prerelease versions only.</summary>
    public bool PrereleaseOnly { get; set; }

    /// <summary>A remote repository's cache limit in bytes.</summary>
    public long? CacheLimitBytes { get; set; }

    /// <summary>Delete untagged Docker manifests after N days.</summary>
    public int? UntaggedAfterDays { get; set; }

    /// <summary>Run the Docker garbage collection.</summary>
    public bool CollectGarbage { get; set; }

    /// <summary>Run every N hours (the scheduler); none = only on request.</summary>
    public int? ScheduleHours { get; set; }

    internal RetentionPolicyInput ToInput(int key)
    {
        var project = string.Equals(ScopeKind, "project", StringComparison.OrdinalIgnoreCase);

        return new RetentionPolicyInput(
            key, Name, project ? null : ScopeName, project ? ScopeName : null, Enabled, NamePatterns,
            KeepLast, OlderThanDays, UnusedDays, PrereleaseOnly, CacheLimitBytes, UntaggedAfterDays, CollectGarbage, ScheduleHours);
    }
}

/// <summary>A policy with its scope's name and the last run.</summary>
public sealed record RetentionPolicyDto(RetentionPolicyView Policy, RetentionJobStatus? LastRun);

/// <summary>Places a legal hold.</summary>
public sealed class HoldRequest
{
    /// <summary>The repository.</summary>
    public string? Repository { get; set; }

    /// <summary>The package, artifact or image name.</summary>
    public string? Name { get; set; }

    /// <summary>One version; empty holds every version.</summary>
    public string? Version { get; set; }

    /// <summary>Why.</summary>
    public string? Reason { get; set; }
}

/// <summary>
/// Retention policies, their runs and legal holds: the Retention, RetentionPolicy and RetentionHolds pages' operations. Creating,
/// changing, running and deleting a policy, and lifting a hold, ask for the owner's password as the pages do; a dry run and placing a hold
/// do not.
/// </summary>
[Route(ApiCatalog.RootTemplate + "/retention")]
public class RetentionController : AdminApiController
{
    private IRetentionAdministration Retention => Service<IRetentionAdministration>();

    private RetentionJobs Jobs => Service<RetentionJobs>();

    /// <summary>Lists policies.</summary>
    [HttpGet("policies")]
    [ApiOperation("List retention policies with their last run", "/Admin/Retention", Response = typeof(RetentionPolicyDto), PagedList = true)]
    public async Task<IActionResult> List(int? page, int? pageSize)
        => RespondPage((await Retention.ListPoliciesAsync(Cancellation)).Select(p => new RetentionPolicyDto(p, Jobs.Get(p.Policy.Key))).ToList(), page, pageSize);

    /// <summary>One policy.</summary>
    [HttpGet("policies/{key:int}")]
    [ApiOperation("Show a retention policy with its last run and report", "/Admin/RetentionPolicy", Response = typeof(RetentionPolicyDto))]
    public async Task<IActionResult> Get(int key)
        => await Retention.GetPolicyAsync(key, Cancellation) is { } policy ? Respond(new RetentionPolicyDto(policy, Jobs.Get(key))) : ApiProblems.NotFound($"retention policy {key}");

    /// <summary>Creates a policy.</summary>
    [HttpPost("policies")]
    [ApiOperation("Create a retention policy", "/Admin/Retention", Confirm = true, Response = typeof(RetentionPolicyDto), Status = 201)]
    public async Task<IActionResult> Create([FromBody] RetentionPolicyRequest request)
    {
        ThrowIfClosed();
        if (await RequireConfirmationAsync() is { } refusal) return refusal;

        var policy = await Retention.SavePolicyAsync(request.ToInput(0), Actor, Cancellation);

        return Respond(new RetentionPolicyDto((await Retention.GetPolicyAsync(policy.Key, Cancellation))!, null), 201, $"{ApiCatalog.Root}/retention/policies/{policy.Key}");
    }

    /// <summary>Replaces a policy's rules.</summary>
    [HttpPut("policies/{key:int}")]
    [ApiOperation("Replace a retention policy's scope and rules", "/Admin/RetentionPolicy", Confirm = true, Response = typeof(RetentionPolicyDto))]
    public async Task<IActionResult> Update(int key, [FromBody] RetentionPolicyRequest request)
    {
        ThrowIfClosed();
        if (await Retention.GetPolicyAsync(key, Cancellation) == null) return ApiProblems.NotFound($"retention policy {key}");
        if (await RequireConfirmationAsync() is { } refusal) return refusal;

        await Retention.SavePolicyAsync(request.ToInput(key), Actor, Cancellation);

        return Respond(new RetentionPolicyDto((await Retention.GetPolicyAsync(key, Cancellation))!, Jobs.Get(key)));
    }

    /// <summary>Deletes a policy.</summary>
    [HttpDelete("policies/{key:int}")]
    [ApiOperation("Delete a retention policy (what it deleted stays deleted)", "/Admin/RetentionPolicy", Confirm = true, Status = 204)]
    public async Task<IActionResult> Delete(int key)
    {
        ThrowIfClosed();
        if (await Retention.GetPolicyAsync(key, Cancellation) == null) return ApiProblems.NotFound($"retention policy {key}");
        if (await RequireConfirmationAsync() is { } refusal) return refusal;

        await Retention.DeletePolicyAsync(key, Actor, Cancellation);

        return Done();
    }

    /// <summary>Starts a dry run.</summary>
    [HttpPost("policies/{key:int}/dry-run")]
    [ApiOperation("Start a dry run of a policy: the report of what a run would delete (nothing is deleted)", "/Admin/RetentionPolicy", Response = typeof(RetentionJobStatus), Status = 202)]
    public async Task<IActionResult> DryRun(int key)
    {
        if (await Retention.GetPolicyAsync(key, Cancellation) == null) return ApiProblems.NotFound($"retention policy {key}");
        if (!Jobs.TryStart(key, dryRun: true, Actor, out var refusal)) return ApiProblems.Conflict(refusal ?? "The dry run was not started.");

        return Respond(Jobs.Get(key), 202, $"{ApiCatalog.Root}/retention/policies/{key}/run");
    }

    /// <summary>Starts a run that deletes.</summary>
    [HttpPost("policies/{key:int}/run")]
    [ApiOperation("Start a run of a policy: the versions it selects are deleted", "/Admin/RetentionPolicy", Confirm = true, Response = typeof(RetentionJobStatus), Status = 202)]
    public async Task<IActionResult> Run(int key)
    {
        ThrowIfClosed();
        var view = await Retention.GetPolicyAsync(key, Cancellation);
        if (view == null) return ApiProblems.NotFound($"retention policy {key}");
        if (await RequireConfirmationAsync() is { } refusal) return refusal;
        if (!view.Policy.Enabled) return ApiProblems.Conflict("The policy is disabled: enable it first (a dry run works either way).");
        if (!Jobs.TryStart(key, dryRun: false, Actor, out var problem)) return ApiProblems.Conflict(problem ?? "The run was not started.");

        return Respond(Jobs.Get(key), 202, $"{ApiCatalog.Root}/retention/policies/{key}/run");
    }

    /// <summary>The last run of a policy.</summary>
    [HttpGet("policies/{key:int}/run")]
    [ApiOperation("Show the last run (or dry run) of a policy with its report", "/Admin/RetentionPolicy", Response = typeof(RetentionJobStatus))]
    public async Task<IActionResult> LastRun(int key)
    {
        if (await Retention.GetPolicyAsync(key, Cancellation) == null) return ApiProblems.NotFound($"retention policy {key}");

        return Jobs.Get(key) is { } status ? Respond(status) : ApiProblems.NotFound($"run of retention policy {key} since the server started");
    }

    /// <summary>Lists legal holds.</summary>
    [HttpGet("holds")]
    [ApiOperation("List legal holds, optionally of one repository", "/Admin/RetentionHolds", Response = typeof(RetentionHoldView), PagedList = true)]
    public async Task<IActionResult> Holds(string? repository, int? page, int? pageSize)
        => RespondPage(await Retention.ListHoldsAsync(string.IsNullOrWhiteSpace(repository) ? null : repository, Cancellation), page, pageSize);

    /// <summary>Places a hold.</summary>
    [HttpPost("holds")]
    [ApiOperation("Place a legal hold on an artifact (retention never deletes it)", "/Admin/RetentionHolds", Response = typeof(RetentionHold), Status = 201)]
    public async Task<IActionResult> AddHold([FromBody] HoldRequest request)
    {
        ThrowIfClosed();
        var hold = await Retention.AddHoldAsync(request.Repository ?? string.Empty, request.Name ?? string.Empty, request.Version, request.Reason, Actor, Cancellation);

        return Respond(hold, 201);
    }

    /// <summary>Lifts a hold.</summary>
    [HttpDelete("holds/{key:int}")]
    [ApiOperation("Lift a legal hold", "/Admin/RetentionHolds", Confirm = true, Status = 204)]
    public async Task<IActionResult> RemoveHold(int key)
    {
        ThrowIfClosed();
        if (await RequireConfirmationAsync() is { } refusal) return refusal;

        return await Retention.RemoveHoldAsync(key, Actor, Cancellation) ? Done() : ApiProblems.NotFound($"hold {key}");
    }
}

/// <summary>A scanning policy's thresholds; a severity is 0 (low) to 3 (critical), none disables the action.</summary>
public sealed class ScanPolicyRequest
{
    /// <summary>Warn from this severity.</summary>
    public int? WarnSeverity { get; set; }

    /// <summary>Refuse downloads from this severity.</summary>
    public int? BlockDownloadSeverity { get; set; }

    /// <summary>Refuse deploys from this severity.</summary>
    public int? BlockDeploySeverity { get; set; }

    /// <summary>Refuse downloads of versions that were never scanned.</summary>
    public bool BlockUnscanned { get; set; }

    internal ScanPolicyValues ToValues()
    {
        var values = new ScanPolicyValues(WarnSeverity, BlockDownloadSeverity, BlockDeploySeverity, BlockUnscanned);

        return values.Validate() is { } problem ? throw new AccessModelException(problem) : values;
    }
}

/// <summary>A repository with its scan numbers and the policy in force.</summary>
public sealed record ScanRepositoryDto(string Repository, ScanSummary Summary, EffectiveScanPolicy Policy);

/// <summary>The scanning setup and every repository's numbers.</summary>
public sealed record ScanOverviewDto(bool Enabled, bool TrivyConfigured, IReadOnlyList<ScanRepositoryDto> Repositories);

/// <summary>The dashboard of one repository.</summary>
public sealed record ScanDashboardDto(ScanRepositoryDto Repository, ScanPolicy? OwnPolicy, AdminJobStatus? LastScan);

/// <summary>
/// Vulnerability scanning: the Scanning, ScanRepository and ScanProject pages' operations. Saving a policy asks for the owner's password
/// (it decides who may download and deploy what); starting a scan does not and runs as the <c>vulnerability-scan</c> operation.
/// </summary>
[Route(ApiCatalog.RootTemplate + "/scanning")]
public class ScanningController : AccessApiController
{
    /// <summary>The overview.</summary>
    [HttpGet]
    [ApiOperation("Show whether scanning is on and every repository's numbers and policy", "/Admin/Scanning", Response = typeof(ScanOverviewDto))]
    public async Task<IActionResult> Overview()
    {
        var options = Service<IOptionsSnapshot<ScanningOptions>>().Value;
        var rows = new List<ScanRepositoryDto>();
        foreach (var repository in (await Service<IRepositoryService>().ListAsync(Cancellation)).Where(r => r.Kind != RepositoryKind.Virtual))
        {
            rows.Add(await RowAsync(repository));
        }

        return Respond(new ScanOverviewDto(options.Enabled, Service<ITrivyRunner>().IsConfigured(options), rows));
    }

    /// <summary>One repository's dashboard.</summary>
    [HttpGet("repositories/{name}")]
    [ApiOperation("Show a repository's scan numbers, its own policy and the last scan", "/Admin/ScanRepository", Response = typeof(ScanDashboardDto))]
    public async Task<IActionResult> Repository(string name)
    {
        var repository = await FindAsync(name);
        if (repository == null) return ApiProblems.NotFound($"scannable repository '{name}'");

        var own = await Service<IScanPolicyService>().GetAsync(ScanPolicyScope.Repository, repository.Key, Cancellation);

        return Respond(new ScanDashboardDto(await RowAsync(repository), own, Service<AdminJobService>().Get("vulnerability-scan", repository.Name)));
    }

    /// <summary>The versions with findings.</summary>
    [HttpGet("repositories/{name}/results")]
    [ApiOperation("List the scanned versions with findings of at least a severity, worst first", "/Admin/ScanRepository", Response = typeof(ScanResult), PagedList = true)]
    public async Task<IActionResult> Results(string name, int? min, int? page, int? pageSize)
    {
        var repository = await FindAsync(name);
        if (repository == null) return ApiProblems.NotFound($"scannable repository '{name}'");

        var (number, size) = ApiProblems.Clamp(page, pageSize);
        using var scope = Service<IServiceScopeFactory>().CreateRepositoryScope(repository);
        var scans = scope.ServiceProvider.GetRequiredService<IScanService>();
        var results = await scans.ListAsync(Math.Clamp(min ?? 0, 0, PackageVulnerabilityRules.MaxSeverity), (number - 1) * size, size + 1, Cancellation);
        // The list is read one past the page: "total" is exact on the last page and a lower bound (the count so far) while more follow.
        var total = (number - 1) * size + results.Count;

        return Respond(new Paged<ScanResult>(results.Take(size).ToList(), number, size, total));
    }

    /// <summary>Saves a repository's policy.</summary>
    [HttpPut("repositories/{name}/policy")]
    [ApiOperation("Set a repository's scanning policy (all thresholds empty removes it)", "/Admin/ScanRepository", Confirm = true, Response = typeof(EffectiveScanPolicy))]
    public async Task<IActionResult> SetPolicy(string name, [FromBody] ScanPolicyRequest request)
    {
        ThrowIfClosed();
        var repository = await FindAsync(name);
        if (repository == null) return ApiProblems.NotFound($"scannable repository '{name}'");
        var values = request.ToValues();
        if (await RequireConfirmationAsync() is { } refusal) return refusal;

        var policies = Service<IScanPolicyService>();
        await policies.SaveAsync(ScanPolicyScope.Repository, repository.Key, values, Actor, Cancellation);

        return Respond(await policies.GetEffectiveAsync(repository.Key, Cancellation));
    }

    /// <summary>Starts a scan.</summary>
    [HttpPost("repositories/{name}/scan")]
    [ApiOperation("Scan every artifact of a repository in the background", "/Admin/ScanRepository", Response = typeof(AdminJobStatus), Status = 202)]
    public async Task<IActionResult> Scan(string name)
    {
        ThrowIfClosed();
        var repository = await FindAsync(name);
        if (repository == null) return ApiProblems.NotFound($"scannable repository '{name}'");

        var jobs = Service<AdminJobService>();
        var operation = jobs.Operations.FirstOrDefault(o => o.Name == "vulnerability-scan");
        if (operation == null || !operation.IsAvailable(HttpContext.RequestServices) || !operation.AppliesTo(repository))
        {
            return ApiProblems.Conflict("Scanning is not available: it is turned off (Scanning:Enabled) or the repository cannot be scanned.");
        }

        if (!jobs.TryStart(operation.Name, repository, Actor, out var refusal)) return ApiProblems.Conflict(refusal ?? "The scan was not started.");

        return Respond(jobs.Get(operation.Name, repository.Name), 202, $"{ApiCatalog.Root}/operations/{operation.Name}/repositories/{Uri.EscapeDataString(repository.Name)}");
    }

    /// <summary>A project's policy.</summary>
    [HttpGet("projects/{name}/policy")]
    [ApiOperation("Show a project's scanning policy", "/Admin/ScanProject", Response = typeof(ScanPolicy))]
    public async Task<IActionResult> ProjectPolicy(string name)
    {
        if (Unavailable() is { } unavailable) return unavailable;
        var project = await AccessAdmin.GetProjectAsync(name, Cancellation);
        if (project == null) return ApiProblems.NotFound($"project '{name}'");

        return await Service<IScanPolicyService>().GetAsync(ScanPolicyScope.Project, project.Project.Key, Cancellation) is { } policy
            ? Respond(policy)
            : ApiProblems.NotFound($"scanning policy of project '{name}'");
    }

    /// <summary>Saves a project's policy.</summary>
    [HttpPut("projects/{name}/policy")]
    [ApiOperation("Set a project's scanning policy (all thresholds empty removes it)", "/Admin/ScanProject", Confirm = true, Status = 204)]
    public async Task<IActionResult> SetProjectPolicy(string name, [FromBody] ScanPolicyRequest request)
    {
        if (Unavailable() is { } unavailable) return unavailable;
        ThrowIfClosed();
        var project = await AccessAdmin.GetProjectAsync(name, Cancellation);
        if (project == null) return ApiProblems.NotFound($"project '{name}'");
        var values = request.ToValues();
        if (await RequireConfirmationAsync() is { } refusal) return refusal;

        await Service<IScanPolicyService>().SaveAsync(ScanPolicyScope.Project, project.Project.Key, values, Actor, Cancellation);

        return Done();
    }

    private async Task<RepositoryInfo?> FindAsync(string name)
    {
        var repository = await Service<IRepositoryService>().FindAsync(name, Cancellation);

        return repository == null || repository.Kind == RepositoryKind.Virtual ? null : repository;
    }

    private async Task<ScanRepositoryDto> RowAsync(RepositoryInfo repository)
    {
        using var scope = Service<IServiceScopeFactory>().CreateRepositoryScope(repository);
        var summary = await scope.ServiceProvider.GetRequiredService<IScanService>().SummarizeAsync(Cancellation);

        return new ScanRepositoryDto(repository.Name, summary, await Service<IScanPolicyService>().GetEffectiveAsync(repository.Key, Cancellation));
    }
}
