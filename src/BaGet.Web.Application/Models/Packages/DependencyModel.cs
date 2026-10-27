using System;
using System.Collections.Generic;
using System.Linq;
using BaGet.Core;
using NuGet.Frameworks;
using NuGet.Versioning;

namespace BaGet.Web.Application.Models.Packages;

public record DependencyModel
{
    public string? PackageId { get; init; }
    public string? VersionSpec { get; init; }
}