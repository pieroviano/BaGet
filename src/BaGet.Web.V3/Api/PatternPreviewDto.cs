using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Web.V3.Api;

/// <summary>What a set of patterns selects.</summary>
public sealed record PatternPreviewDto(IReadOnlyList<string> Repositories, string? TestPackage, PatternTest? Test);
