using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace BaGet.Identity.Pages.Admin;

/// <summary>
/// What the replication form posts (<c>Form.*</c>): a <see cref="ReplicationConfig"/> as a person types it. The peer's API key is write
/// only: it is never filled back into a page; an empty field keeps the stored key.
/// </summary>
public sealed class ReplicationForm
{
    public string? Name { get; set; }

    public string? Repository { get; set; }

    /// <summary><c>Push</c> or <c>Pull</c>.</summary>
    public ReplicationDirection Direction { get; set; } = ReplicationDirection.Push;

    public string? PeerUrl { get; set; }

    public string? PeerRepository { get; set; } = BaGet.Core.Repository.DefaultName;

    /// <summary>The peer's API key (write only).</summary>
    public string? ApiKey { get; set; }

    /// <summary>Forget the stored key.</summary>
    public bool ClearApiKey { get; set; }

    public bool Enabled { get; set; } = true;

    public bool OnDeploy { get; set; }

    public int? ScheduleMinutes { get; set; }

    public bool PropagateDeletes { get; set; }

    public ReplicationConflict Conflict { get; set; } = ReplicationConflict.TargetWins;

    /// <summary>The administration's input for configuration <paramref name="key"/> (0 creates).</summary>
    public ReplicationInput ToInput(int key)
        => new(key, Name, Repository, Direction, PeerUrl, PeerRepository, ApiKey, ClearApiKey, Enabled, OnDeploy, ScheduleMinutes, PropagateDeletes, Conflict);

    /// <summary>The form filled in from a stored configuration (without its key).</summary>
    public static ReplicationForm From(ReplicationConfigView view)
    {
        var c = view.Config;

        return new ReplicationForm
        {
            Name = c.Name,
            Repository = view.Repository,
            Direction = c.Direction,
            PeerUrl = c.PeerUrl,
            PeerRepository = c.PeerRepository,
            Enabled = c.Enabled,
            OnDeploy = c.OnDeploy,
            ScheduleMinutes = c.ScheduleMinutes,
            PropagateDeletes = c.PropagateDeletes,
            Conflict = c.Conflict,
        };
    }
}

/// <summary>What the replication fields partial shows.</summary>
/// <param name="Form">The values.</param>
/// <param name="Repositories">The local repositories to choose from.</param>
/// <param name="IdPrefix">Prefix of the element ids, so two forms on one page do not clash.</param>
/// <param name="HasApiKey">Whether a key is stored (the field then says so).</param>
public sealed record ReplicationFieldsView(ReplicationForm Form, IReadOnlyList<RepositoryInfo> Repositories, string IdPrefix, bool HasApiKey);

/// <summary>
/// Base of the replication pages: the access pages' rules (administrators only, 404 for anyone else, redirect where there is no
/// relational database, 503 on a POST while the server is read-only), with the replication services and the repositories a
/// configuration can name. A <see cref="ReplicationException"/> is an <see cref="AccessModelException"/>, so <c>ActAsync</c> turns it into
/// the page's <c>Error</c>.
/// </summary>
[Obsolete("The retention/replication screens are controllers now; use ReplicationControllerBase. This page model base has no page left and will be removed in the next major version.")]
public abstract class ReplicationPageModel : AccessPageModel
{
    /// <summary>Configurations and their log.</summary>
    protected IReplicationAdministration Replication => HttpContext.RequestServices.GetRequiredService<IReplicationAdministration>();

    /// <summary>Runs replications in the background.</summary>
    protected ReplicationJobs Jobs => HttpContext.RequestServices.GetRequiredService<ReplicationJobs>();

    /// <summary>The local repositories a replication can name (remote and virtual ones are not replicated), by name.</summary>
    public IReadOnlyList<RepositoryInfo> Repositories { get; private set; } = Array.Empty<RepositoryInfo>();

    /// <summary>Loads <see cref="Repositories"/>.</summary>
    protected async Task LoadChoicesAsync(CancellationToken cancellationToken)
    {
        Repositories = await AccessSupport.ReplicationRepositoriesAsync(HttpContext.RequestServices, cancellationToken);
    }

    /// <summary>The schedule and triggers of a configuration in a few words.</summary>
    public static string Describe(ReplicationConfig c) => AccessSupport.DescribeReplication(c);
}
