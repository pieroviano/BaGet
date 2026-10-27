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

/// <summary>The form of <c>Unpublish</c> (one version) and <c>UnpublishAll</c> (the version is ignored); the password confirms.</summary>
public sealed class NpmManageUnpublishForm
{
    /// <summary>The version (<c>Unpublish</c> only).</summary>
    public string? Version { get; set; }

    /// <summary>The caller's password.</summary>
    public string? Password { get; set; }
}
