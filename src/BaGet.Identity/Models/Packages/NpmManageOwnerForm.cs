using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.Application.Screens;
using BaGet.Identity.Controllers.Base;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace BaGet.Identity.Models.Packages;

/// <summary>The form of the npm management handlers that name an owner.</summary>
public sealed class NpmManageOwnerForm
{
    /// <summary>The user or organization.</summary>
    public string? OwnerName { get; set; }
}
