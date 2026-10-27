using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Web.V3.Api;

/// <summary>A virtual repository's members, as the API takes them.</summary>
public sealed class VirtualRequest
{
    /// <summary>The member repositories' names in resolution order.</summary>
    public List<string> Members { get; set; } = new();

    /// <summary>The member that receives deployments, or null for none.</summary>
    public string? DeployRepository { get; set; }

    internal VirtualRepositorySettings ToSettings() => new() { Members = Members ?? new List<string>(), DeployRepository = string.IsNullOrWhiteSpace(DeployRepository) ? null : DeployRepository };
}
