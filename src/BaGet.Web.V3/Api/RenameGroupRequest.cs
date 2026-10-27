using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Web.V3.Api;

/// <summary>Renames a group.</summary>
public sealed class RenameGroupRequest
{
    /// <summary>The new name (the built-in groups keep theirs).</summary>
    public string? NewName { get; set; }
}
