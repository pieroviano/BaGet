using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Mvc;

namespace BaGet.Web.V3.Api;

/// <summary>A field a format asks for when creating a local repository.</summary>
public sealed record FormatFieldDto(string Name, string Label, string? Help, bool Required, IReadOnlyList<string>? Options);
