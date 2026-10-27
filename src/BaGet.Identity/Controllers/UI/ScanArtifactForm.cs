using System.Threading;
using System.Threading.Tasks;
using BaGet.Identity.Application;
using BaGet.Identity.Application.Models;
using BaGet.Identity.Application.Screens;
using BaGet.Identity.Application.UseCases;
using BaGet.Identity.Controllers.Base;
using BaGet.Identity.Mvc;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace BaGet.Identity.Controllers.UI;

/// <summary>The body of the scan twin's POST: the artifact and version to scan, and where the link back goes.</summary>
public sealed class ScanArtifactForm
{
    /// <summary>The artifact name (NuGet id, npm name, Maven <c>group:artifact</c>, Docker image).</summary>
    public string? Name { get; set; }

    /// <summary>The version (Docker: the manifest digest).</summary>
    public string? Version { get; set; }

    /// <summary>Where the link back goes (a local address only).</summary>
    public string? ReturnUrl { get; set; }
}
