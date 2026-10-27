using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Web.V3.Models.Api;

/// <summary>One member's answer.</summary>
public sealed record PreviewStepDto(string Member, ResolutionOutcome Outcome);
