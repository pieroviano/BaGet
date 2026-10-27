using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.Application;
using BaGet.Identity.Application.Models;
using BaGet.Identity.Application.Screens;
using BaGet.Identity.Application.UseCases;
using BaGet.Identity.Controllers.Base;
using BaGet.Identity.Mvc;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace BaGet.Identity.Controllers.UI;

/// <summary>The form of the Docker management handlers; every one asks for the caller's password.</summary>
public sealed class DockerManageForm
{
    /// <summary>The tag to delete (<c>DeleteTag</c>).</summary>
    public string? Tag { get; set; }

    /// <summary>The manifest digest to delete (<c>DeleteManifest</c>).</summary>
    public string? Digest { get; set; }

    /// <summary>The caller's password.</summary>
    public string? Password { get; set; }
}
