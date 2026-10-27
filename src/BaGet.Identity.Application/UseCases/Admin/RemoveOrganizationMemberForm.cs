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

namespace BaGet.Identity.Application.UseCases.Admin;

/// <summary>What the "remove a member" (or "leave") form posts.</summary>
public sealed class RemoveOrganizationMemberForm
{
    /// <summary>The organization.</summary>
    public string? Organization { get; set; }

    /// <summary>The member to remove.</summary>
    public string? UserName { get; set; }
}
