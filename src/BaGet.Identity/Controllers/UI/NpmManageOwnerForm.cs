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

/// <summary>The form of the npm management handlers that name an owner.</summary>
public sealed class NpmManageOwnerForm
{
    /// <summary>The user or organization.</summary>
    public string? OwnerName { get; set; }
}
