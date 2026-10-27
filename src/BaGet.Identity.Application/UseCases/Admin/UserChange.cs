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

/// <summary>A change to one account from the Users screen, with the list's filter and page to come back to.</summary>
/// <param name="UserName">The account the change applies to.</param>
/// <param name="Password">The administrator's password, asked again.</param>
/// <param name="Q">The list's user-name filter.</param>
/// <param name="PageNumber">The list's page.</param>
/// <param name="ResetEmail">For a password reset: the address to mail the new password to, if the administrator typed one.</param>
public sealed record UserChange(string? UserName, string? Password, string? Q = null, int PageNumber = 1, string? ResetEmail = null);
