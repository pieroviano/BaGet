using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaGet.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BaGet.Web.V3.Models.Api;

/// <summary>A maintenance operation and its last runs.</summary>
public sealed record OperationDto(string Name, string Title, string Description, bool RequiresConfirmation, IReadOnlyList<string> Repositories, IReadOnlyList<AdminJobStatus> Runs);
