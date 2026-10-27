using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BaGet.Web.V3.Api;

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
