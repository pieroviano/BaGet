using System;
using System.Collections.Generic;
using System.Linq;
using BaGet.Core;

namespace BaGet.Identity.Application.Models;

/// <summary>The form of <c>SetVerified</c>.</summary>
public sealed class PackageManageVerifiedForm
{
    /// <summary>Whether the ID is verified.</summary>
    public bool Verified { get; set; }
}
