using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.Application.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BaGet.Web.V3.Api;

/// <summary>Sets whether a user is an administrator.</summary>
public sealed class AdministratorRequest
{
    /// <summary>The new state.</summary>
    public bool? IsAdministrator { get; set; }
}
