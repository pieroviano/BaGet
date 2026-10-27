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

/// <summary>A user as the UserDetail page shows them.</summary>
public sealed record UserDetailDto(
    UserDto User,
    IReadOnlyList<string> Groups,
    IReadOnlyList<UserProjectRole> ProjectRoles,
    IReadOnlyList<string> Organizations,
    int OwnedPackageIds,
    IReadOnlyList<ApiKeyDto> Keys);
