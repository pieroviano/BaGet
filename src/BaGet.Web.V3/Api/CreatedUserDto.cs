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

/// <summary>The created account; <see cref="GeneratedPassword"/> is shown this once.</summary>
public sealed record CreatedUserDto(UserDto User, string? GeneratedPassword);
