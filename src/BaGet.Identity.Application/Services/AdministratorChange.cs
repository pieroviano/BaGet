using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BaGet.Identity.Application.Services;

/// <summary>
/// The outcome of promoting or demoting a user.
/// </summary>
public enum AdministratorChange
{
    /// <summary>The flag was changed.</summary>
    Changed,

    /// <summary>The user already had the requested state.</summary>
    Unchanged,

    /// <summary>There is no such user.</summary>
    UserNotFound,

    /// <summary>The user is the only administrator, so cannot be demoted.</summary>
    LastAdministrator,
}
