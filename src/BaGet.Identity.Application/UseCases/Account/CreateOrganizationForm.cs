using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.Application.Screens;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace BaGet.Identity.Application.UseCases.Account;

/// <summary>What the "create an organization" form posts.</summary>
public sealed class CreateOrganizationForm
{
    /// <summary>The organization's name.</summary>
    public string? Name { get; set; }
}
