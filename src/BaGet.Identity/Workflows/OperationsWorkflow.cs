using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace BaGet.Identity.Workflows;

/// <summary>
/// The operational screens of the administrator: maintenance operations (catalog rebuild, downloads import, Docker garbage
/// collection) started as background jobs for a chosen repository (503 while the <see cref="MaintenanceGate"/> is closed, recorded
/// with <see cref="IAdminAudit"/>); the validation queue across every repository (retry, release, reject); the abuse reports of
/// every repository (resolve, reopen); and the read-only formats and cluster status. Shared by <c>AdminOperationsController</c>
///.
/// </summary>
internal sealed class OperationsWorkflow : AdminWorkflow
{
    private const string NotConfirmedToStart = "The confirmation is incorrect, so nothing was started.";
    private const string NotConfirmed = "The confirmation is incorrect, so nothing was changed.";

    public OperationsWorkflow(HttpContext httpContext, IUrlHelper url) : base(httpContext, url)
    {
    }

    /// <summary>The operations screen.</summary>
    public OperationsViewModel Operations { get; } = new();

    /// <summary>The validation screen.</summary>
    public ValidationViewModel Validation { get; } = new();

    /// <summary>The abuse reports screen; the caller sets <c>All</c> first.</summary>
    public AbuseViewModel Abuse { get; } = new();

    /// <summary>The formats screen.</summary>
    public FormatsViewModel Formats { get; } = new();

    /// <summary>The cluster screen.</summary>
    public ClusterViewModel Cluster { get; } = new();

    private IValidationAdministration? ValidationAdministration => Services.GetService<IValidationAdministration>();

    private IAbuseReportAdministration Reports => Services.GetRequiredService<IAbuseReportAdministration>();

    // ------------------------------------------------------------------ operations

    /// <summary>Lists the operations and their status.</summary>
    public async Task GetOperationsAsync(CancellationToken cancellationToken)
    {
        await LoadConfirmationAsync(Operations, Http.Request.Path + Http.Request.QueryString);

        var jobs = Services.GetRequiredService<AdminJobService>();
        var repositories = await Services.GetRequiredService<IRepositoryService>().ListAsync(cancellationToken);

        Operations.Rows = jobs.Operations
            .Where(o => o.IsAvailable(Services))
            .SelectMany(o => repositories.Where(o.AppliesTo).Select(r => new OperationRow(o, r, jobs.Get(o.Name, r.Name))))
            .ToList();
    }

    /// <summary>Starts <paramref name="operation"/> on <paramref name="repository"/>.</summary>
    public async Task<IActionResult?> StartAsync(string? operation, string? repository, string? password, CancellationToken cancellationToken)
    {
        var jobs = Services.GetRequiredService<AdminJobService>();
        var info = await Services.GetRequiredService<IRepositoryService>().FindAsync(repository, cancellationToken);
        var definition = jobs.Operations.FirstOrDefault(o => o.Name == operation);
        if (info == null || definition == null || !definition.IsAvailable(Services) || !definition.AppliesTo(info)) return new NotFoundResult();

        var user = await UserManager.GetUserAsync(User);

        // An operation that deletes data asks for the password again, like the other destructive pages.
        if (definition.RequiresConfirmation && !await IsConfirmedAsync(user, password))
        {
            Operations.Error = NotConfirmedToStart;

            return BackToOperations();
        }

        if (!jobs.TryStart(definition.Name, info, user?.UserName, out var refusal))
        {
            Operations.Error = refusal;
        }
        else
        {
            Operations.Message = $"{definition.Title} started on {info.Name}.";
        }

        return BackToOperations();
    }

    private static IActionResult BackToOperations() => RedirectToScreen("/Admin/Operations");

    // ------------------------------------------------------------------ validation

    /// <summary>Lists the submissions.</summary>
    public async Task GetValidationAsync(CancellationToken cancellationToken)
    {
        var administration = ValidationAdministration;
        Validation.IsSupported = administration?.IsSupported == true;
        if (Validation.IsSupported)
        {
            Validation.Submissions = await administration!.ListAsync(cancellationToken);
        }

        await LoadConfirmationAsync(Validation);
    }

    /// <summary>Validates the submission again.</summary>
    public Task<IActionResult?> RetryAsync(string? submissionId, CancellationToken cancellationToken)
        => ValidationActAsync(submissionId, null, confirm: false, (a, id, user) => a.RetryAsync(id, user, cancellationToken), "queued for validation again");

    /// <summary>Indexes the quarantined package although validation failed.</summary>
    public Task<IActionResult?> ReleaseAsync(string? submissionId, string? password, CancellationToken cancellationToken)
        => ValidationActAsync(submissionId, password, confirm: true, (a, id, user) => a.ReleaseAsync(id, user, cancellationToken), "released and indexed");

    /// <summary>Deletes the quarantined package.</summary>
    public Task<IActionResult?> RejectAsync(string? submissionId, string? password, CancellationToken cancellationToken)
        => ValidationActAsync(submissionId, password, confirm: true, (a, id, user) => a.RejectAsync(id, user, cancellationToken), "rejected and deleted");

    private async Task<IActionResult?> ValidationActAsync(
        string? submissionId,
        string? password,
        bool confirm,
        Func<IValidationAdministration, string, string?, Task<string?>> action,
        string done)
    {
        var administration = ValidationAdministration;
        if (administration == null || !administration.IsSupported || string.IsNullOrEmpty(submissionId)) return new NotFoundResult();

        var user = await UserManager.GetUserAsync(User);
        if (user == null) return new ChallengeResult();

        if (confirm && !await IsConfirmedAsync(user, password))
        {
            Validation.Error = NotConfirmed;

            return RedirectToScreen("/Admin/Validation");
        }

        var refusal = await action(administration, submissionId, user.UserName);
        if (refusal == null)
        {
            Validation.Message = $"Submission {submissionId} was {done}.";
        }
        else
        {
            Validation.Error = refusal;
        }

        return RedirectToScreen("/Admin/Validation");
    }

    // ------------------------------------------------------------------ abuse reports

    /// <summary>Lists the reports.</summary>
    public async Task GetAbuseAsync(CancellationToken cancellationToken)
    {
        Abuse.IsSupported = Reports.IsSupported;
        Abuse.Items = await Reports.ListAsync(unresolvedOnly: !Abuse.All, cancellationToken);
    }

    /// <summary>Marks a report resolved.</summary>
    public Task<IActionResult?> ResolveAsync(int key, CancellationToken cancellationToken) => SetResolvedAsync(key, true, cancellationToken);

    /// <summary>Reopens a resolved report.</summary>
    public Task<IActionResult?> ReopenAsync(int key, CancellationToken cancellationToken) => SetResolvedAsync(key, false, cancellationToken);

    private async Task<IActionResult?> SetResolvedAsync(int key, bool resolved, CancellationToken cancellationToken)
    {
        if (!await Reports.SetResolvedAsync(key, resolved, cancellationToken)) return new NotFoundResult();

        var user = await UserManager.GetUserAsync(User);
        Services.GetRequiredService<IAdminAudit>()
            .Record(user?.UserName, resolved ? "abuse.resolve" : "abuse.reopen", $"report {key}");
        Abuse.Message = resolved ? $"Report {key} was resolved." : $"Report {key} was reopened.";

        return RedirectToScreen("/Admin/Abuse", new { all = Abuse.All ? "true" : null });
    }

    // ------------------------------------------------------------------ formats and cluster

    /// <summary>Lists the formats.</summary>
    public async Task GetFormatsAsync(CancellationToken cancellationToken)
    {
        var repositories = await Services.GetRequiredService<IRepositoryAdministration>().ListAsync(cancellationToken);

        Formats.Rows = Services.GetRequiredService<FormatRegistry>().Modules
            .Select(module => new FormatRow(
                module,
                repositories.Count(r => r.Format == module.Format),
                Capabilities(Services, module.Format)))
            .ToList();
    }

    // What the module's services answer to: a capability a phase has not implemented yet is simply not listed.
    private static IReadOnlyList<string> Capabilities(IServiceProvider services, RepositoryFormat format)
    {
        var list = new List<string>();
        if (services.GetFormatService<IFormatIndexer>(format) != null) list.Add("Deploy (indexing)");
        if (services.GetFormatService<IFormatSearchProvider>(format) != null) list.Add("Search");
        if (services.GetFormatService<IFormatRetentionHooks>(format) != null) list.Add("Retention");
        if (services.GetFormatService<IFormatScanner>(format) != null) list.Add("Vulnerability scanning");

        return list;
    }

    /// <summary>Reads the cluster status; <paramref name="refresh"/> runs the health checks again instead of using the last answer.</summary>
    public async Task GetClusterAsync(bool refresh, CancellationToken cancellationToken)
    {
        if (refresh) await Services.GetRequiredService<ReadinessService>().CheckAsync(force: true, cancellationToken);

        Cluster.Status = await Services.GetRequiredService<IClusterStatusService>().GetAsync(cancellationToken);
    }
}
