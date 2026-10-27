using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BaGet.Identity.Application.Services.Accounts;

/// <summary>The outcome of <see cref="AdminUserCreationService.CreateAsync"/>.</summary>
/// <param name="User">The new user, or null if none was created.</param>
/// <param name="Error">Why nothing was created, or null.</param>
/// <param name="GeneratedPassword">The password for <see cref="NewUserPasswordMode.Generated"/>, to show once.</param>
public record NewUserResult(BaGetUser? User, string? Error, string? GeneratedPassword = null)
{
    /// <summary>Whether the account was created.</summary>
    public bool Succeeded => User != null;
}
