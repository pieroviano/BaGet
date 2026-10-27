using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BaGet.Web.V3.Api;

/// <summary>The scanning setup and every repository's numbers.</summary>
public sealed record ScanOverviewDto(bool Enabled, bool TrivyConfigured, IReadOnlyList<ScanRepositoryDto> Repositories);
