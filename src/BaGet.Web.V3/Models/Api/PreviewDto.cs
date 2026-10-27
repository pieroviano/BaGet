using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Web.V3.Models.Api;

/// <summary>What a preview found.</summary>
public sealed record PreviewDto(string PackageId, string Version, IReadOnlyList<PreviewStepDto> Steps, string? Answers);
