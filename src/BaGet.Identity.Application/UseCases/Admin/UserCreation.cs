using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.Application.Screens;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BaGet.Identity.Application.UseCases.Admin;

/// <summary>A new account from the Users screen.</summary>
/// <param name="NewUserName">The new account's name.</param>
/// <param name="NewEmail">Its address, or null.</param>
/// <param name="NewPassword">The password typed; empty means generate one (unless <paramref name="Invite"/>).</param>
/// <param name="Invite">Mail the user a link to choose their own password.</param>
/// <param name="Password">The administrator's password, asked again.</param>
/// <param name="Q">The list's user-name filter.</param>
/// <param name="PageNumber">The list's page.</param>
public sealed record UserCreation(string? NewUserName, string? NewEmail, string? NewPassword, bool Invite, string? Password, string? Q = null, int PageNumber = 1);
