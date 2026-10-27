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

/// <summary>An API key without its secret or hash.</summary>
public sealed record ApiKeyDto(int Key, string? Description, IReadOnlyList<string> Scopes, string? PackageGlobPattern, DateTime Created, DateTime? Expires, string Repositories);
