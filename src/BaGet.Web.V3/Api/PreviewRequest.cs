using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Web.V3.Api;

/// <summary>Asks where a package version would come from in a virtual repository.</summary>
public sealed class PreviewRequest
{
    /// <summary>The package id (<c>group:artifact</c> for Maven, the image name for Docker).</summary>
    public string? PackageId { get; set; }

    /// <summary>The version as text.</summary>
    public string? Version { get; set; }
}
