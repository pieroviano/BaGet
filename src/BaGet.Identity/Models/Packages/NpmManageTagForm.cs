using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.Application.Screens;
using BaGet.Identity.Controllers.Base;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace BaGet.Identity.Models.Packages;

/// <summary>The form of <c>SetTag</c>.</summary>
public sealed class NpmManageTagForm
{
    /// <summary>The dist-tag.</summary>
    public string? Tag { get; set; }

    /// <summary>The version it points at (not used by <c>RemoveTag</c>).</summary>
    public string? Version { get; set; }
}
