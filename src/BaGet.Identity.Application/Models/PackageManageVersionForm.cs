using System;
using System.Collections.Generic;
using System.Linq;
using BaGet.Core;

namespace BaGet.Identity.Application.Models;

/// <summary>The form of a handler that names a version.</summary>
public sealed class PackageManageVersionForm
{
    /// <summary>The (normalized) version.</summary>
    public string? Version { get; set; }
}
