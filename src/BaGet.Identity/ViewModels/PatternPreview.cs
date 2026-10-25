using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core;
using BaGet.Identity.Mvc;
using BaGet.Identity.Pages.Account;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace BaGet.Identity.Pages.Admin;

/// <summary>The result of previewing repository and package patterns.</summary>
/// <param name="Repositories">The NuGet repositories whose names match the repository patterns.</param>
/// <param name="TestPackage">The package ID typed in the test box, if any.</param>
/// <param name="Test">Whether the target would apply to <paramref name="TestPackage"/>, and why.</param>
public sealed record PatternPreview(IReadOnlyList<string> Repositories, string? TestPackage, PatternTest? Test);
