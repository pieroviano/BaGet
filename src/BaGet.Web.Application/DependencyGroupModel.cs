using System;
using System.Collections.Generic;
using System.Linq;
using BaGet.Core;
using NuGet.Frameworks;
using NuGet.Versioning;

namespace BaGet.Web;

public class DependencyGroupModel
{
    public string? Name { get; set; }
    public IReadOnlyList<DependencyModel>? Dependencies { get; set; }
}
