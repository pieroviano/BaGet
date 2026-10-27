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

/// <summary>A user in a list.</summary>
public sealed record UserDto(string UserName, string? Email, DateTime Created, bool IsAdministrator, bool PendingApproval = false);
