using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BaGet.Web.V3.Models.Api;

/// <summary>Creates an account.</summary>
public sealed class CreateUserRequest
{
    /// <summary>The user name.</summary>
    public string? UserName { get; set; }

    /// <summary>The address (trusted: marked confirmed), or none.</summary>
    public string? Email { get; set; }

    /// <summary>The first password; none = the server generates one and returns it once.</summary>
    public string? Password { get; set; }
}
