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

/// <summary>What a user may do in one repository, and why.</summary>
public sealed record UserAccessDto(string Repository, AccessAction Actions, string? Override, IReadOnlyList<AccessReason> Reasons);
