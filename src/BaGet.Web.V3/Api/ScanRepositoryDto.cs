using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BaGet.Web.V3.Api;

/// <summary>A repository with its scan numbers and the policy in force.</summary>
public sealed record ScanRepositoryDto(string Repository, ScanSummary Summary, EffectiveScanPolicy Policy);
