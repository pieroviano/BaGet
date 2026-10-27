using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BaGet.Web.V3.Models.Api;

/// <summary>The dashboard of one repository.</summary>
public sealed record ScanDashboardDto(ScanRepositoryDto Repository, ScanPolicy? OwnPolicy, AdminJobStatus? LastScan);
